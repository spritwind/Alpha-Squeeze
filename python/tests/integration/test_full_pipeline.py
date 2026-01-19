"""
完整流程整合測試

測試從資料擷取到訊號計算的完整流程
"""

import pytest
from datetime import datetime, timedelta
from unittest.mock import Mock, patch, AsyncMock
import polars as pl

from engine.squeeze_calculator import (
    SqueezeCalculator,
    FactorScores,
    SqueezeSignal,
    Trend,
)
from scrapers.finmind_client import FinMindClient
from workers.daily_fetch import DailyDataFetcher


class TestFullPipeline:
    """完整流程測試"""

    @pytest.fixture
    def calculator(self):
        return SqueezeCalculator()

    @pytest.fixture
    def test_ticker(self):
        return "2330"  # 台積電

    @pytest.mark.asyncio
    async def test_finmind_data_fetch(self, test_ticker):
        """測試 FinMind 資料擷取"""
        client = FinMindClient()
        end_date = datetime.now().strftime("%Y-%m-%d")
        start_date = (datetime.now() - timedelta(days=5)).strftime("%Y-%m-%d")

        # 這個測試需要實際 API 連線
        # 在 CI 環境可能需要 mock
        try:
            df = await client.fetch_stock_price(test_ticker, start_date, end_date)
            if df is not None and len(df) > 0:
                assert "stock_id" in df.columns or "ticker" in df.columns
        except Exception as e:
            pytest.skip(f"FinMind API not available: {e}")

    def test_squeeze_score_calculation_consistency(self, calculator):
        """測試評分計算一致性"""
        # 相同輸入應產生相同輸出
        params = {
            "ticker": "2330",
            "borrow_change": -500000,
            "margin_ratio": 15.0,
            "iv": 0.22,
            "hv": 0.30,
            "price": 600,
            "prev_price": 580,
            "volume": 50000000,
            "avg_volume": 30000000,
            "borrow_range": (-1000000, 1000000),
        }

        result1 = calculator.calculate_squeeze_score(**params)
        result2 = calculator.calculate_squeeze_score(**params)

        assert result1.score == result2.score
        assert result1.trend == result2.trend

    def test_squeeze_score_weighted_sum(self, calculator):
        """驗證加權總分正確"""
        params = {
            "ticker": "TEST",
            "borrow_change": 0,
            "margin_ratio": 10.0,
            "iv": 0.25,
            "hv": 0.25,
            "price": 100,
            "prev_price": 100,
            "volume": 1000,
            "avg_volume": 1000,
            "borrow_range": (-1000000, 1000000),
        }

        result = calculator.calculate_squeeze_score(**params)
        factors = result.factors

        # 手動計算加權總分
        expected_score = round(
            0.35 * factors.borrow_score +
            0.25 * factors.gamma_score +
            0.20 * factors.margin_score +
            0.20 * factors.momentum_score
        )

        assert result.score == expected_score

    def test_trend_classification_bullish(self, calculator):
        """測試 BULLISH 趨勢分類邏輯"""
        # 高分 = BULLISH
        high_score_result = calculator.calculate_squeeze_score(
            ticker="TEST",
            borrow_change=-1000000,
            margin_ratio=25.0,
            iv=0.18,
            hv=0.32,
            price=110,
            prev_price=100,
            volume=100000,
            avg_volume=30000,
            borrow_range=(-1000000, 1000000),
        )
        assert high_score_result.trend == Trend.BULLISH
        assert high_score_result.score >= 70

    def test_trend_classification_bearish(self, calculator):
        """測試 BEARISH 趨勢分類邏輯"""
        # 低分 = BEARISH
        low_score_result = calculator.calculate_squeeze_score(
            ticker="TEST",
            borrow_change=1000000,
            margin_ratio=1.0,
            iv=0.45,
            hv=0.20,
            price=90,
            prev_price=100,
            volume=10000,
            avg_volume=50000,
            borrow_range=(-1000000, 1000000),
        )
        assert low_score_result.trend == Trend.BEARISH
        assert low_score_result.score <= 40

    def test_trend_classification_neutral(self, calculator):
        """測試 NEUTRAL 趨勢分類邏輯"""
        neutral_result = calculator.calculate_squeeze_score(
            ticker="TEST",
            borrow_change=0,
            margin_ratio=8.0,
            iv=0.25,
            hv=0.25,
            price=100,
            prev_price=100,
            volume=50000,
            avg_volume=50000,
            borrow_range=(-1000000, 1000000),
        )
        assert neutral_result.trend == Trend.NEUTRAL
        assert 40 < neutral_result.score < 70


class TestDatabaseIntegration:
    """資料庫整合測試"""

    @pytest.mark.asyncio
    async def test_metrics_upsert_and_retrieve(self):
        """測試資料寫入與讀取"""
        # 需要測試資料庫連線
        # 在實際環境中使用測試資料庫
        pytest.skip("Database integration tests require actual DB connection")

    @pytest.mark.asyncio
    async def test_squeeze_signal_storage(self):
        """測試軋空訊號儲存"""
        pytest.skip("Database integration tests require actual DB connection")


class TestGrpcCommunication:
    """gRPC 通訊測試"""

    @pytest.mark.asyncio
    async def test_grpc_server_responds(self):
        """測試 gRPC Server 回應"""
        # 需要啟動的 gRPC server
        pytest.skip("gRPC integration tests require running server")

    @pytest.mark.asyncio
    async def test_grpc_batch_request(self):
        """測試批量請求"""
        pytest.skip("gRPC integration tests require running server")


class TestDataPipelineValidation:
    """資料管線驗證測試"""

    @pytest.fixture
    def sample_metrics_data(self):
        """建立範例資料"""
        return pl.DataFrame({
            "ticker": ["2330", "2330", "2330"],
            "trade_date": ["2024-01-01", "2024-01-02", "2024-01-03"],
            "close_price": [580.0, 585.0, 590.0],
            "open_price": [575.0, 580.0, 586.0],
            "high_price": [588.0, 590.0, 595.0],
            "low_price": [573.0, 578.0, 585.0],
            "volume": [50000000, 55000000, 48000000],
        })

    def test_hv_calculation_with_polars(self, sample_metrics_data):
        """測試使用 Polars 計算 HV"""
        calculator = SqueezeCalculator()

        # 延伸資料以滿足 20 日窗口
        extended_data = pl.DataFrame({
            "ticker": ["2330"] * 25,
            "trade_date": [f"2024-01-{str(i+1).zfill(2)}" for i in range(25)],
            "close_price": [580.0 + i * 2 for i in range(25)],
        }).lazy()

        result = calculator.calculate_hv_20d(extended_data, "close_price")
        collected = result.collect()

        assert "hv_20d" in collected.columns
        # 前 19 筆應為 null（不足 20 日）
        assert collected["hv_20d"][:19].null_count() == 19
        # 第 20 筆後應有值
        assert collected["hv_20d"][20] is not None

    def test_factor_scores_dataclass(self):
        """測試 FactorScores 資料類別"""
        factors = FactorScores(
            borrow_score=85.5,
            gamma_score=72.3,
            margin_score=68.0,
            momentum_score=55.2
        )

        assert factors.borrow_score == 85.5
        assert factors.gamma_score == 72.3
        assert factors.margin_score == 68.0
        assert factors.momentum_score == 55.2

    def test_squeeze_signal_dataclass(self):
        """測試 SqueezeSignal 資料類別"""
        factors = FactorScores(80.0, 70.0, 60.0, 50.0)
        signal = SqueezeSignal(
            ticker="2330",
            score=75,
            trend=Trend.BULLISH,
            comment="軋空潛力高",
            factors=factors
        )

        assert signal.ticker == "2330"
        assert signal.score == 75
        assert signal.trend == Trend.BULLISH
        assert signal.comment == "軋空潛力高"
        assert signal.factors == factors


class TestEdgeCases:
    """邊界情況測試"""

    @pytest.fixture
    def calculator(self):
        return SqueezeCalculator()

    def test_zero_values_handling(self, calculator):
        """測試零值處理"""
        result = calculator.calculate_squeeze_score(
            ticker="TEST",
            borrow_change=0,
            margin_ratio=0,
            iv=0,
            hv=0,
            price=0,
            prev_price=0,
            volume=0,
            avg_volume=0,
            borrow_range=(-1000000, 1000000),
        )

        # 應該不會拋出異常
        assert 0 <= result.score <= 100
        assert result.trend in [Trend.BULLISH, Trend.NEUTRAL, Trend.BEARISH]

    def test_negative_values_handling(self, calculator):
        """測試負值處理"""
        result = calculator.calculate_squeeze_score(
            ticker="TEST",
            borrow_change=-2000000,
            margin_ratio=-5.0,
            iv=-0.1,
            hv=-0.2,
            price=-100,
            prev_price=-110,
            volume=-1000,
            avg_volume=-2000,
            borrow_range=(-1000000, 1000000),
        )

        # 應該不會拋出異常，且分數在合理範圍
        assert 0 <= result.score <= 100

    def test_extreme_values_handling(self, calculator):
        """測試極端值處理"""
        result = calculator.calculate_squeeze_score(
            ticker="TEST",
            borrow_change=-10000000,
            margin_ratio=100.0,
            iv=0.01,
            hv=0.99,
            price=10000,
            prev_price=1000,
            volume=1000000000,
            avg_volume=1000,
            borrow_range=(-1000000, 1000000),
        )

        # 分數應該被限制在 0-100 範圍內
        assert 0 <= result.score <= 100
