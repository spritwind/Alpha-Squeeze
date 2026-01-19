"""
Workers 模組單元測試

測試項目：
- DailyDataFetcher 資料擷取
- DailyPipeline 完整流程
- Scheduler 排程器
"""

import asyncio
from datetime import datetime, timedelta
from unittest.mock import AsyncMock, MagicMock, patch

import polars as pl
import pytest

from engine.config import Settings, get_settings


# ===== DailyDataFetcher 測試 =====

class TestDailyDataFetcher:
    """DailyDataFetcher 測試"""

    @pytest.fixture
    def mock_loader(self):
        """Mock FinMind DataLoader"""
        with patch("workers.daily_fetch.DataLoader") as mock:
            loader_instance = MagicMock()
            mock.return_value = loader_instance

            # Mock 股價資料
            import pandas as pd
            loader_instance.taiwan_stock_daily.return_value = pd.DataFrame({
                "stock_id": ["2330"],
                "date": ["2024-01-15"],
                "open": [600.0],
                "max": [610.0],
                "min": [595.0],
                "close": [605.0],
                "Trading_Volume": [50000000],
                "Trading_money": [30000000000]
            })

            # Mock 借券資料 (使用 TaiwanDailyShortSaleBalances API)
            loader_instance.taiwan_daily_short_sale_balances.return_value = pd.DataFrame({
                "stock_id": ["2330"],
                "date": ["2024-01-15"],
                "SBLShortSalesCurrentDayBalance": [100000]
            })

            # Mock 融資融券資料
            loader_instance.taiwan_stock_margin_purchase_short_sale.return_value = pd.DataFrame({
                "stock_id": ["2330"],
                "date": ["2024-01-15"],
                "MarginPurchaseTodayBalance": [500000],
                "ShortSaleTodayBalance": [80000]
            })

            yield loader_instance

    def test_initialization(self, mock_loader):
        """應能正確初始化"""
        from workers.daily_fetch import DailyDataFetcher

        fetcher = DailyDataFetcher()
        assert fetcher.loader is not None

    def test_initialization_with_token(self, mock_loader):
        """有 token 應呼叫 login"""
        from workers.daily_fetch import DailyDataFetcher

        fetcher = DailyDataFetcher(finmind_token="test_token")
        mock_loader.login_by_token.assert_called_once()

    def test_fetch_stock_prices(self, mock_loader):
        """應能抓取股價資料"""
        from workers.daily_fetch import DailyDataFetcher

        fetcher = DailyDataFetcher()
        df = fetcher.fetch_stock_prices("2330", "2024-01-01", "2024-01-31")

        assert isinstance(df, pl.DataFrame)
        mock_loader.taiwan_stock_daily.assert_called_once()

    def test_fetch_borrowing_data(self, mock_loader):
        """應能抓取借券資料 (使用 TaiwanDailyShortSaleBalances API)"""
        from workers.daily_fetch import DailyDataFetcher

        fetcher = DailyDataFetcher()
        df = fetcher.fetch_borrowing_data("2330", "2024-01-01", "2024-01-31")

        assert isinstance(df, pl.DataFrame)
        mock_loader.taiwan_daily_short_sale_balances.assert_called_once()

    def test_fetch_margin_data(self, mock_loader):
        """應能抓取融資融券資料"""
        from workers.daily_fetch import DailyDataFetcher

        fetcher = DailyDataFetcher()
        df = fetcher.fetch_margin_data("2330", "2024-01-01", "2024-01-31")

        assert isinstance(df, pl.DataFrame)
        mock_loader.taiwan_stock_margin_purchase_short_sale.assert_called_once()


# ===== DailyPipeline 測試 =====

class TestDailyPipeline:
    """DailyPipeline 測試"""

    def test_initialization_default_tickers(self):
        """應使用預設股票清單"""
        from workers.scheduler import DailyPipeline, DEFAULT_TICKERS

        pipeline = DailyPipeline()
        assert pipeline.tickers == DEFAULT_TICKERS

    def test_initialization_custom_tickers(self):
        """應能使用自訂股票清單"""
        from workers.scheduler import DailyPipeline

        custom_tickers = ["2330", "2454"]
        pipeline = DailyPipeline(tickers=custom_tickers)
        assert pipeline.tickers == custom_tickers


# ===== Scheduler 測試 =====

class TestScheduler:
    """Scheduler 測試"""

    def test_initialization(self):
        """應能正確初始化"""
        from workers.scheduler import Scheduler

        scheduler = Scheduler()
        assert scheduler.tickers is not None
        assert len(scheduler.tickers) > 0

    def test_custom_tickers(self):
        """應能使用自訂股票清單"""
        from workers.scheduler import Scheduler

        custom = ["2330"]
        scheduler = Scheduler(tickers=custom)
        assert scheduler.tickers == custom

    def test_timezone_setting(self):
        """應能設定時區"""
        from workers.scheduler import Scheduler
        from pytz import timezone

        scheduler = Scheduler(tz="Asia/Taipei")
        assert scheduler.tz == timezone("Asia/Taipei")


# ===== Config 測試 =====

class TestConfig:
    """Configuration 測試"""

    def test_squeeze_config_weights(self):
        """權重總和應為 1.0"""
        from engine.config import SqueezeConfig

        config = SqueezeConfig()
        assert config.validate_weights() is True

    def test_squeeze_config_default_values(self):
        """預設值應正確"""
        from engine.config import SqueezeConfig

        config = SqueezeConfig()
        assert config.weight_borrow == 0.35
        assert config.weight_gamma == 0.25
        assert config.weight_margin == 0.20
        assert config.weight_momentum == 0.20
        assert config.bullish_threshold == 70
        assert config.bearish_threshold == 40

    def test_grpc_settings(self):
        """gRPC 設定應正確"""
        from engine.config import GrpcSettings

        settings = GrpcSettings()
        assert settings.port == 50051
        assert settings.max_workers == 10
        assert "50051" in settings.address

    def test_database_settings_connection_string(self):
        """資料庫連線字串應正確生成"""
        from engine.config import DatabaseSettings

        settings = DatabaseSettings(
            server="localhost",
            database="TestDB",
            trusted_connection=True
        )
        conn_str = settings.connection_string
        assert "localhost" in conn_str
        assert "TestDB" in conn_str
        assert "Trusted_Connection=yes" in conn_str

    def test_settings_singleton(self):
        """get_settings 應返回相同實例"""
        from engine.config import get_settings

        settings1 = get_settings()
        settings2 = get_settings()
        assert settings1 is settings2


# ===== 整合測試標記 =====

@pytest.mark.integration
class TestDailyFetchIntegration:
    """DailyDataFetcher 整合測試"""

    @pytest.mark.skip(reason="需要 FinMind API")
    async def test_full_fetch_workflow(self):
        """測試完整抓取流程"""
        from workers.daily_fetch import DailyDataFetcher

        fetcher = DailyDataFetcher()
        df = fetcher.fetch_complete_metrics(
            "2330",
            "2024-01-01",
            "2024-01-31"
        )
        assert len(df) > 0


@pytest.mark.integration
class TestPipelineIntegration:
    """DailyPipeline 整合測試"""

    @pytest.mark.skip(reason="需要完整環境")
    async def test_full_pipeline(self):
        """測試完整 Pipeline"""
        from workers.scheduler import DailyPipeline

        pipeline = DailyPipeline(tickers=["2330"])
        results = await pipeline.run()
        assert len(results) > 0
