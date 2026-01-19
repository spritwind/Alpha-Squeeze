"""
Alpha Squeeze - gRPC Server

提供 Squeeze Score 計算服務給 .NET 客戶端。

服務端點：
- GetSqueezeSignal: 分析單一標的
- GetBatchSignals: 批量分析多標的
- GetTopCandidates: 取得當日熱門軋空標的

啟動方式：
    python -m engine.server
    # 或
    squeeze-server
"""

import asyncio
import logging
from concurrent import futures
from datetime import datetime
from typing import Optional

import grpc

from engine.config import get_settings
from engine.squeeze_calculator import SqueezeCalculator, Trend

# 嘗試匯入 gRPC 生成的程式碼
try:
    from engine.protos import squeeze_pb2, squeeze_pb2_grpc

    GRPC_AVAILABLE = True
except ImportError:
    GRPC_AVAILABLE = False
    squeeze_pb2 = None
    squeeze_pb2_grpc = None

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger(__name__)


class SqueezeEngineServicer:
    """
    gRPC 服務實作

    實作 SqueezeEngine 服務定義的所有 RPC 方法。
    """

    def __init__(self):
        """初始化計算器"""
        self.calculator = SqueezeCalculator()
        self._cache: dict[str, dict] = {}  # 簡易快取
        logger.info("SqueezeEngineServicer 初始化完成")

    async def GetSqueezeSignal(
        self,
        request,
        context: grpc.aio.ServicerContext,
    ):
        """
        分析單一標的的軋空潛力

        Args:
            request: SqueezeRequest 包含股票資料
            context: gRPC context

        Returns:
            SqueezeResponse 包含分析結果
        """
        try:
            logger.info(f"分析軋空訊號: {request.ticker}")
            start_time = datetime.now()

            # 執行計算
            signal = self.calculator.calculate_squeeze_score(
                ticker=request.ticker,
                borrow_change=request.borrow_change,
                margin_ratio=request.margin_ratio,
                iv=request.current_iv,
                hv=request.hv_20d,
                price=request.close_price,
                prev_price=request.close_price,  # 需要從歷史資料取得
                volume=request.volume,
                avg_volume=request.volume,  # 需要從歷史資料計算
            )

            # 建構回應
            response = squeeze_pb2.SqueezeResponse(
                ticker=signal.ticker,
                score=signal.score,
                trend=signal.trend.value,
                comment=signal.comment,
                factors=squeeze_pb2.FactorScores(
                    borrow_score=signal.factors.borrow_score,
                    gamma_score=signal.factors.gamma_score,
                    margin_score=signal.factors.margin_score,
                    momentum_score=signal.factors.momentum_score,
                ),
            )

            duration = (datetime.now() - start_time).total_seconds() * 1000
            logger.info(
                f"完成分析 {request.ticker}: Score={signal.score}, "
                f"Trend={signal.trend.value}, 耗時={duration:.2f}ms"
            )

            return response

        except Exception as e:
            logger.error(f"分析 {request.ticker} 時發生錯誤: {e}", exc_info=True)
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details(f"分析失敗: {str(e)}")
            raise

    async def GetBatchSignals(
        self,
        request,
        context: grpc.aio.ServicerContext,
    ):
        """
        批量分析多個標的

        Args:
            request: BatchSqueezeRequest 包含多個股票請求
            context: gRPC context

        Returns:
            BatchSqueezeResponse 包含所有分析結果
        """
        logger.info(f"批量分析 {len(request.requests)} 個標的")
        start_time = datetime.now()

        responses = []
        for req in request.requests:
            try:
                resp = await self.GetSqueezeSignal(req, context)
                responses.append(resp)
            except Exception as e:
                logger.warning(f"分析 {req.ticker} 失敗: {e}")
                # 繼續處理其他標的
                continue

        duration = (datetime.now() - start_time).total_seconds() * 1000
        logger.info(f"批量分析完成: {len(responses)}/{len(request.requests)} 成功, 耗時={duration:.2f}ms")

        return squeeze_pb2.BatchSqueezeResponse(responses=responses)

    async def GetTopCandidates(
        self,
        request,
        context: grpc.aio.ServicerContext,
    ):
        """
        取得當日熱門軋空候選標的

        Args:
            request: TopCandidatesRequest 包含篩選條件
            context: gRPC context

        Returns:
            TopCandidatesResponse 包含排序後的候選清單
        """
        logger.info(
            f"查詢熱門候選: date={request.date}, "
            f"limit={request.limit}, min_score={request.min_score}"
        )

        try:
            from engine.database import get_database, StockMetricsRepository

            db = get_database()
            repo = StockMetricsRepository(db)

            # 取得指定日期的所有股票指標
            target_date = request.date or datetime.now().strftime("%Y-%m-%d")
            metrics = repo.get_by_date(target_date)

            candidates = []
            for m in metrics:
                # 計算每個標的的軋空分數
                signal = self.calculator.calculate_squeeze_score(
                    ticker=m.get("Ticker", ""),
                    borrow_change=m.get("BorrowingBalanceChange", 0) or 0,
                    margin_ratio=float(m.get("MarginRatio", 0) or 0),
                    iv=0,  # IV 需要從權證資料取得
                    hv=float(m.get("HistoricalVolatility20D", 0) or 0),
                    price=float(m.get("ClosePrice", 0) or 0),
                    prev_price=float(m.get("ClosePrice", 0) or 0),
                    volume=m.get("Volume", 0) or 0,
                    avg_volume=m.get("Volume", 0) or 0,
                )

                # 只保留符合最低分數要求的候選
                if signal.score >= request.min_score:
                    candidates.append(squeeze_pb2.SqueezeResponse(
                        ticker=signal.ticker,
                        score=signal.score,
                        trend=signal.trend.value,
                        comment=signal.comment,
                        factors=squeeze_pb2.FactorScores(
                            borrow_score=signal.factors.borrow_score,
                            gamma_score=signal.factors.gamma_score,
                            margin_score=signal.factors.margin_score,
                            momentum_score=signal.factors.momentum_score,
                        ),
                    ))

            # 依分數排序並限制數量
            candidates.sort(key=lambda x: x.score, reverse=True)
            candidates = candidates[:request.limit]

            logger.info(f"找到 {len(candidates)} 個候選標的")

            return squeeze_pb2.TopCandidatesResponse(
                candidates=candidates,
                analysis_date=target_date,
                generated_at=datetime.now().isoformat(),
            )

        except Exception as e:
            logger.error(f"查詢熱門候選失敗: {e}", exc_info=True)
            # 返回空結果而非拋出錯誤
            return squeeze_pb2.TopCandidatesResponse(
                candidates=[],
                analysis_date=request.date or datetime.now().strftime("%Y-%m-%d"),
                generated_at=datetime.now().isoformat(),
            )


async def serve(port: Optional[int] = None) -> None:
    """
    啟動 gRPC 伺服器

    Args:
        port: 伺服器埠號（預設從設定讀取）
    """
    if not GRPC_AVAILABLE:
        logger.error(
            "gRPC 程式碼尚未生成！請先執行: python scripts/generate_grpc.py"
        )
        return

    settings = get_settings()
    port = port or settings.grpc.port

    # 建立伺服器
    server = grpc.aio.server(
        futures.ThreadPoolExecutor(max_workers=settings.grpc.max_workers)
    )

    # 註冊服務
    squeeze_pb2_grpc.add_SqueezeEngineServicer_to_server(
        SqueezeEngineServicer(),
        server,
    )

    # 綁定埠號
    listen_addr = f"[::]:{port}"
    server.add_insecure_port(listen_addr)

    logger.info(f"gRPC 伺服器啟動於 {listen_addr}")
    logger.info(f"執行緒數: {settings.grpc.max_workers}")

    await server.start()

    try:
        await server.wait_for_termination()
    except KeyboardInterrupt:
        logger.info("收到中斷訊號，正在關閉伺服器...")
        await server.stop(5)
        logger.info("伺服器已關閉")


def main() -> None:
    """gRPC 伺服器入口點"""
    asyncio.run(serve())


if __name__ == "__main__":
    main()
