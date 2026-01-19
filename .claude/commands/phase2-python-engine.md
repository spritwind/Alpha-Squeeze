# Phase 2: Python 量化引擎開發指引

## 目標
建立完整的 Python 量化計算引擎，包含 Squeeze Score 演算法、gRPC 服務、資料擷取與爬蟲功能。

## 前置條件
- Python 3.11+ 已安裝
- 已完成 Phase 1 資料層

## 開發任務

### Task 2.1: 建立 Python 專案結構

```bash
cd python

# 建立虛擬環境
python -m venv .venv
.venv\Scripts\activate  # Windows
# source .venv/bin/activate  # Linux/Mac

# 安裝依賴
pip install -r requirements.txt

# 安裝 Playwright browsers
playwright install chromium
```

建立以下目錄結構：
```
python/
├── engine/
│   ├── __init__.py
│   ├── squeeze_calculator.py   # ✅ 已存在，需完善
│   ├── server.py               # ✅ 已存在，需完善
│   ├── config.py               # 配置管理
│   └── protos/                 # gRPC 生成檔案
├── scrapers/
│   ├── __init__.py
│   ├── warrant_scraper.py      # ✅ 已存在，需完善
│   ├── base_scraper.py         # 爬蟲基類
│   └── finmind_client.py       # FinMind API 封裝
├── workers/
│   ├── __init__.py
│   ├── daily_fetch.py          # ✅ 已存在，需完善
│   └── scheduler.py            # 排程管理
├── tests/
│   ├── __init__.py
│   ├── test_squeeze_calculator.py
│   ├── test_scrapers.py
│   └── test_workers.py
├── requirements.txt            # ✅ 已存在
└── pyproject.toml
```

### Task 2.2: 生成 gRPC 程式碼

```bash
# 從 proto 目錄生成 Python gRPC 程式碼
python -m grpc_tools.protoc \
    -I../proto \
    --python_out=engine/protos \
    --grpc_python_out=engine/protos \
    ../proto/squeeze.proto
```

### Task 2.3: 完善 Squeeze Calculator

完善 `python/engine/squeeze_calculator.py`：

```python
"""
Squeeze Score Calculator - 核心演算法實作

公式: S = (0.35 × F_B) + (0.25 × F_G) + (0.20 × F_M) + (0.20 × F_V)
"""

import polars as pl
from dataclasses import dataclass
from enum import Enum
from typing import Optional, Tuple
import numpy as np


class Trend(Enum):
    BULLISH = "BULLISH"
    NEUTRAL = "NEUTRAL"
    BEARISH = "BEARISH"


@dataclass
class SqueezeConfig:
    """可調整的權重與閾值配置"""
    weight_borrow: float = 0.35
    weight_gamma: float = 0.25
    weight_margin: float = 0.20
    weight_momentum: float = 0.20
    bullish_threshold: int = 70
    bearish_threshold: int = 40


class SqueezeCalculator:
    """
    軋空評分計算器

    使用說明：
    1. 初始化時可傳入自訂配置
    2. calculate_squeeze_score() 計算單一標的
    3. batch_calculate() 批量計算多標的
    """

    def __init__(self, config: Optional[SqueezeConfig] = None):
        self.config = config or SqueezeConfig()

    def calculate_borrow_score(
        self,
        borrow_change: float,
        percentile_rank: float  # 該標的在所有標的中的百分位
    ) -> float:
        """
        計算法人空頭分數 (F_B)

        邏輯：
        - 借券餘額減少（負值）= 法人回補 = 高分
        - 借券餘額增加（正值）= 法人加碼放空 = 低分

        Args:
            borrow_change: 借券餘額變化量
            percentile_rank: 百分位排名 (0-1)

        Returns:
            分數 (0-100)
        """
        if borrow_change >= 0:
            # 加碼放空，基礎分 0-40
            return max(0, 40 - percentile_rank * 40)
        else:
            # 回補，基礎分 50-100
            return min(100, 50 + percentile_rank * 50)

    def calculate_gamma_score(self, iv: float, hv: float) -> float:
        """
        計算 Gamma 效應分數 (F_G)

        邏輯：
        - IV < HV: 權證被低估，Gamma Squeeze 潛力高
        - IV > HV: 權證被高估，潛力低

        Args:
            iv: 權證隱含波動率
            hv: 標的歷史波動率 (20日)

        Returns:
            分數 (0-100)
        """
        if iv <= 0 or hv <= 0:
            return 50  # 資料不足時返回中性分數

        # 計算乖離率
        divergence = (hv - iv) / hv

        if divergence > 0:
            # IV < HV: 被低估
            # 乖離 10% = 70分, 20% = 90分, 30%+ = 100分
            return min(100, 50 + divergence * 166.7)
        else:
            # IV > HV: 被高估
            # 乖離 -10% = 40分, -20% = 30分, -30%+ = 0分
            return max(0, 50 + divergence * 166.7)

    def calculate_margin_score(self, margin_ratio: float) -> float:
        """
        計算散戶燃料分數 (F_M)

        邏輯：
        - 券資比越高，空單越擁擠，軋空潛力越大

        券資比參考：
        - 0-5%: 正常
        - 5-10%: 偏高
        - 10-20%: 很高
        - 20%+: 極度擁擠

        Args:
            margin_ratio: 券資比 (%)

        Returns:
            分數 (0-100)
        """
        if margin_ratio <= 0:
            return 0

        if margin_ratio >= 30:
            return 100
        elif margin_ratio >= 20:
            return 85 + (margin_ratio - 20) * 1.5
        elif margin_ratio >= 10:
            return 60 + (margin_ratio - 10) * 2.5
        elif margin_ratio >= 5:
            return 35 + (margin_ratio - 5) * 5
        else:
            return margin_ratio * 7

    def calculate_momentum_score(
        self,
        close_price: float,
        prev_close: float,
        volume: int,
        avg_volume_20d: float,
        high_20d: Optional[float] = None
    ) -> float:
        """
        計算價量動能分數 (F_V)

        邏輯：
        - 價格上漲 + 量能放大 = 高分
        - 突破近期高點 = 額外加分

        Args:
            close_price: 收盤價
            prev_close: 前日收盤價
            volume: 成交量
            avg_volume_20d: 20日平均成交量
            high_20d: 20日最高價 (可選)

        Returns:
            分數 (0-100)
        """
        if prev_close <= 0 or avg_volume_20d <= 0:
            return 50

        # 價格變化
        price_change_pct = (close_price - prev_close) / prev_close

        # 量能比率
        volume_ratio = volume / avg_volume_20d

        # 價格分數 (±5% = ±25分)
        price_score = 50 + min(25, max(-25, price_change_pct * 500))

        # 量能乘數
        if volume_ratio >= 3.0:
            volume_multiplier = 1.3
        elif volume_ratio >= 2.0:
            volume_multiplier = 1.2
        elif volume_ratio >= 1.5:
            volume_multiplier = 1.1
        elif volume_ratio >= 1.0:
            volume_multiplier = 1.0
        else:
            volume_multiplier = 0.9

        base_score = price_score * volume_multiplier

        # 突破加分
        breakout_bonus = 0
        if high_20d and close_price > high_20d:
            breakout_bonus = 10

        return max(0, min(100, base_score + breakout_bonus))

    def calculate_squeeze_score(
        self,
        ticker: str,
        borrow_change: float,
        borrow_percentile: float,
        margin_ratio: float,
        iv: float,
        hv: float,
        close_price: float,
        prev_close: float,
        volume: int,
        avg_volume_20d: float,
        high_20d: Optional[float] = None
    ) -> dict:
        """
        計算完整的 Squeeze Score

        Returns:
            {
                "ticker": str,
                "score": int (0-100),
                "trend": "BULLISH" | "NEUTRAL" | "BEARISH",
                "comment": str,
                "factors": {
                    "borrow_score": float,
                    "gamma_score": float,
                    "margin_score": float,
                    "momentum_score": float
                }
            }
        """
        # 計算各維度分數
        borrow_score = self.calculate_borrow_score(borrow_change, borrow_percentile)
        gamma_score = self.calculate_gamma_score(iv, hv)
        margin_score = self.calculate_margin_score(margin_ratio)
        momentum_score = self.calculate_momentum_score(
            close_price, prev_close, volume, avg_volume_20d, high_20d
        )

        # 加權總分
        total_score = (
            self.config.weight_borrow * borrow_score +
            self.config.weight_gamma * gamma_score +
            self.config.weight_margin * margin_score +
            self.config.weight_momentum * momentum_score
        )
        score = round(total_score)

        # 判定趨勢
        if score >= self.config.bullish_threshold:
            trend = Trend.BULLISH
        elif score <= self.config.bearish_threshold:
            trend = Trend.BEARISH
        else:
            trend = Trend.NEUTRAL

        # 生成評語
        comment = self._generate_comment(
            borrow_score, gamma_score, margin_score, momentum_score, trend
        )

        return {
            "ticker": ticker,
            "score": score,
            "trend": trend.value,
            "comment": comment,
            "factors": {
                "borrow_score": round(borrow_score, 2),
                "gamma_score": round(gamma_score, 2),
                "margin_score": round(margin_score, 2),
                "momentum_score": round(momentum_score, 2)
            }
        }

    def _generate_comment(
        self,
        borrow: float,
        gamma: float,
        margin: float,
        momentum: float,
        trend: Trend
    ) -> str:
        """生成戰術建議"""
        factors = [
            ("法人回補", borrow),
            ("Gamma壓縮", gamma),
            ("空單擁擠", margin),
            ("價量突破", momentum)
        ]
        sorted_factors = sorted(factors, key=lambda x: x[1], reverse=True)
        top_factor = sorted_factors[0][0]

        if trend == Trend.BULLISH:
            base = f"軋空潛力高，{top_factor}訊號強勁"
            if gamma >= 70:
                base += "；權證低估，留意 Gamma Squeeze"
            if margin >= 70:
                base += "；空單極度擁擠"
            return base
        elif trend == Trend.BEARISH:
            return "軋空機率低，建議觀望或等待更佳進場點"
        else:
            return f"中性訊號，{top_factor}稍具優勢，等待更明確方向"


def batch_calculate_with_polars(
    metrics_df: pl.LazyFrame,
    warrant_df: pl.LazyFrame,
    config: Optional[SqueezeConfig] = None
) -> pl.DataFrame:
    """
    使用 Polars 批量計算所有標的的 Squeeze Score

    高效能實作，適用於每日收盤後批量分析
    """
    calculator = SqueezeCalculator(config)

    # Join metrics with warrant IV data
    combined = (
        metrics_df
        .join(
            warrant_df.group_by("underlying_ticker", "trade_date")
            .agg(pl.col("implied_volatility").mean().alias("avg_iv")),
            left_on=["ticker", "trade_date"],
            right_on=["underlying_ticker", "trade_date"],
            how="left"
        )
        .with_columns([
            # Calculate percentile rank for borrow change
            pl.col("borrowing_balance_change")
            .rank("ordinal")
            .over("trade_date")
            .alias("borrow_rank"),
            pl.col("borrowing_balance_change").count().over("trade_date").alias("total_count")
        ])
        .with_columns(
            (pl.col("borrow_rank") / pl.col("total_count")).alias("borrow_percentile")
        )
    )

    # Collect and calculate scores
    df = combined.collect()

    results = []
    for row in df.iter_rows(named=True):
        result = calculator.calculate_squeeze_score(
            ticker=row["ticker"],
            borrow_change=row["borrowing_balance_change"] or 0,
            borrow_percentile=row["borrow_percentile"] or 0.5,
            margin_ratio=row["margin_ratio"] or 0,
            iv=row.get("avg_iv", 0) or 0,
            hv=row["historical_volatility_20d"] or 0,
            close_price=row["close_price"] or 0,
            prev_close=row["close_price"] or 0,  # Would use lag in production
            volume=row["volume"] or 0,
            avg_volume_20d=row["volume"] or 1,  # Would calculate properly
        )
        results.append(result)

    return pl.DataFrame(results)
```

### Task 2.4: 完善 gRPC Server

完善 `python/engine/server.py` 以正確實作 gRPC 服務。

### Task 2.5: 完善 FinMind Client

建立 `python/scrapers/finmind_client.py`：

```python
"""
FinMind API 封裝

提供統一的介面存取 FinMind 台股資料
"""

import polars as pl
from datetime import datetime, timedelta
from typing import Optional, List
from FinMind.data import DataLoader
import logging

logger = logging.getLogger(__name__)


class FinMindClient:
    """
    FinMind API 封裝類別

    支援資料：
    - taiwan_stock_daily: 股價資料
    - taiwan_stock_securities_lending: 借券資料
    - taiwan_stock_margin_purchase_short_sale: 融資融券資料
    """

    def __init__(self, token: Optional[str] = None):
        self.loader = DataLoader()
        if token:
            self.loader.login_by_token(api_token=token)
            logger.info("FinMind logged in with token")

    def get_daily_metrics(
        self,
        ticker: str,
        start_date: str,
        end_date: str
    ) -> pl.DataFrame:
        """取得完整的每日指標"""
        # 實作細節...
        pass
```

### Task 2.6: 完善 Playwright 權證爬蟲

完善 `python/scrapers/warrant_scraper.py` 以實際爬取元大、統一權證網。

### Task 2.7: 建立排程器

建立 `python/workers/scheduler.py`：

```python
"""
排程管理器

每日任務：
- 18:30: 執行 daily_fetch 抓取籌碼資料
- 19:00: 執行 warrant_scraper 抓取權證 IV
- 19:30: 執行 Squeeze Score 計算
"""

import asyncio
import schedule
from datetime import datetime
import logging

logger = logging.getLogger(__name__)


async def run_daily_pipeline():
    """執行每日完整資料處理流程"""
    logger.info(f"Starting daily pipeline at {datetime.now()}")

    # Step 1: Fetch stock metrics from FinMind
    # Step 2: Scrape warrant IV data
    # Step 3: Calculate squeeze scores
    # Step 4: Save to database

    logger.info("Daily pipeline completed")
```

### Task 2.8: 建立單元測試

建立 `python/tests/test_squeeze_calculator.py`：

```python
"""
Squeeze Calculator 單元測試
"""

import pytest
from engine.squeeze_calculator import SqueezeCalculator, SqueezeConfig, Trend


class TestSqueezeCalculator:

    @pytest.fixture
    def calculator(self):
        return SqueezeCalculator()

    # ===== Borrow Score Tests =====

    def test_borrow_score_heavy_covering_high_percentile(self, calculator):
        """大量回補 + 高百分位 = 高分"""
        score = calculator.calculate_borrow_score(-1000000, 0.95)
        assert score >= 90

    def test_borrow_score_heavy_shorting_high_percentile(self, calculator):
        """大量放空 + 高百分位 = 低分"""
        score = calculator.calculate_borrow_score(1000000, 0.95)
        assert score <= 10

    def test_borrow_score_neutral(self, calculator):
        """無變化 = 中性分數"""
        score = calculator.calculate_borrow_score(0, 0.5)
        assert 30 <= score <= 50

    # ===== Gamma Score Tests =====

    def test_gamma_score_iv_less_than_hv(self, calculator):
        """IV < HV = 高分 (權證低估)"""
        score = calculator.calculate_gamma_score(iv=0.20, hv=0.30)
        assert score >= 70

    def test_gamma_score_iv_greater_than_hv(self, calculator):
        """IV > HV = 低分 (權證高估)"""
        score = calculator.calculate_gamma_score(iv=0.40, hv=0.25)
        assert score <= 30

    def test_gamma_score_iv_equals_hv(self, calculator):
        """IV = HV = 中性"""
        score = calculator.calculate_gamma_score(iv=0.25, hv=0.25)
        assert 45 <= score <= 55

    def test_gamma_score_missing_data(self, calculator):
        """缺失資料 = 中性 50 分"""
        assert calculator.calculate_gamma_score(0, 0.25) == 50
        assert calculator.calculate_gamma_score(0.25, 0) == 50

    # ===== Margin Score Tests =====

    def test_margin_score_extreme_crowding(self, calculator):
        """極度擁擠 (30%+) = 滿分"""
        score = calculator.calculate_margin_score(35.0)
        assert score == 100

    def test_margin_score_high(self, calculator):
        """高券資比 (10-20%) = 高分"""
        score = calculator.calculate_margin_score(15.0)
        assert 70 <= score <= 85

    def test_margin_score_normal(self, calculator):
        """正常 (0-5%) = 低分"""
        score = calculator.calculate_margin_score(3.0)
        assert 15 <= score <= 30

    # ===== Momentum Score Tests =====

    def test_momentum_score_breakout_with_volume(self, calculator):
        """帶量突破 = 高分"""
        score = calculator.calculate_momentum_score(
            close_price=110,
            prev_close=100,
            volume=5000000,
            avg_volume_20d=2000000,
            high_20d=105
        )
        assert score >= 80

    def test_momentum_score_decline_low_volume(self, calculator):
        """縮量下跌 = 低分"""
        score = calculator.calculate_momentum_score(
            close_price=95,
            prev_close=100,
            volume=500000,
            avg_volume_20d=2000000
        )
        assert score <= 40

    # ===== Total Score Tests =====

    def test_squeeze_score_bullish_case(self, calculator):
        """全部高分 = BULLISH"""
        result = calculator.calculate_squeeze_score(
            ticker="2330",
            borrow_change=-500000,
            borrow_percentile=0.9,
            margin_ratio=18.0,
            iv=0.22,
            hv=0.32,
            close_price=600,
            prev_close=580,
            volume=50000000,
            avg_volume_20d=20000000,
            high_20d=595
        )
        assert result["score"] >= 70
        assert result["trend"] == "BULLISH"

    def test_squeeze_score_bearish_case(self, calculator):
        """全部低分 = BEARISH"""
        result = calculator.calculate_squeeze_score(
            ticker="2330",
            borrow_change=500000,
            borrow_percentile=0.1,
            margin_ratio=2.0,
            iv=0.40,
            hv=0.25,
            close_price=550,
            prev_close=580,
            volume=5000000,
            avg_volume_20d=20000000
        )
        assert result["score"] <= 40
        assert result["trend"] == "BEARISH"

    def test_squeeze_score_includes_all_factors(self, calculator):
        """確認返回所有維度分數"""
        result = calculator.calculate_squeeze_score(
            ticker="2330",
            borrow_change=0,
            borrow_percentile=0.5,
            margin_ratio=10.0,
            iv=0.25,
            hv=0.25,
            close_price=100,
            prev_close=100,
            volume=1000,
            avg_volume_20d=1000
        )
        assert "factors" in result
        assert "borrow_score" in result["factors"]
        assert "gamma_score" in result["factors"]
        assert "margin_score" in result["factors"]
        assert "momentum_score" in result["factors"]


class TestSqueezeConfig:

    def test_custom_weights(self):
        """測試自訂權重"""
        config = SqueezeConfig(
            weight_borrow=0.50,
            weight_gamma=0.20,
            weight_margin=0.15,
            weight_momentum=0.15
        )
        calculator = SqueezeCalculator(config)

        # 權重總和應為 1.0
        total = (config.weight_borrow + config.weight_gamma +
                 config.weight_margin + config.weight_momentum)
        assert total == 1.0

    def test_custom_thresholds(self):
        """測試自訂閾值"""
        config = SqueezeConfig(
            bullish_threshold=80,
            bearish_threshold=30
        )
        calculator = SqueezeCalculator(config)
        assert calculator.config.bullish_threshold == 80
        assert calculator.config.bearish_threshold == 30
```

## 驗收標準

### 功能驗收
- [ ] gRPC Server 可啟動並回應請求
- [ ] Squeeze Score 計算結果與手動計算一致
- [ ] FinMind API 可成功抓取資料
- [ ] Playwright 爬蟲可抓取權證 IV

### 測試驗收
- [ ] 所有單元測試通過
- [ ] 測試覆蓋率 > 80%
- [ ] 邊界條件測試完整

### 效能驗收
- [ ] 單一標的計算 < 10ms
- [ ] 批量 100 標的計算 < 1s
- [ ] gRPC 回應時間 < 100ms

## 執行測試
```bash
cd python
pytest tests/ -v --cov=engine --cov=scrapers --cov-report=html
```

## 完成後輸出
1. 完整的 Python 量化引擎
2. 通過的測試報告與覆蓋率報告
3. gRPC Server 啟動日誌
4. 範例計算結果輸出
