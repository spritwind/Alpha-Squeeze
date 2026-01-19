"""
爬蟲模組單元測試

測試項目：
- BaseScraper 基類功能
- WarrantScraper 權證爬蟲
- FinMindClient API 客戶端
"""

import asyncio
from datetime import datetime
from typing import Any, Optional
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

# 嘗試匯入 playwright，如果未安裝則跳過相關測試
try:
    from playwright.async_api import Page
    PLAYWRIGHT_AVAILABLE = True
except ImportError:
    Page = None
    PLAYWRIGHT_AVAILABLE = False

from scrapers.base_scraper import (
    ScrapeResult,
    ScraperConfig,
)

# 只有在 playwright 可用時才匯入 WarrantData
if PLAYWRIGHT_AVAILABLE:
    from scrapers.warrant_scraper import WarrantData, WarrantScraper
else:
    WarrantData = None
    WarrantScraper = None


# ===== BaseScraper 測試 =====
# 注意：BaseScraper 測試需要 playwright，在未安裝時跳過


class TestScraperConfig:
    """ScraperConfig 測試"""

    def test_default_values(self):
        """預設值應正確"""
        config = ScraperConfig()
        assert config.headless is True
        assert config.max_retries == 3
        assert config.retry_delay == 5.0
        assert config.request_delay == 2.0
        assert config.timeout == 30000

    def test_custom_values(self):
        """應能設定自訂值"""
        config = ScraperConfig(
            headless=False,
            max_retries=5,
            retry_delay=10.0,
            request_delay=3.0,
            timeout=60000
        )
        assert config.headless is False
        assert config.max_retries == 5
        assert config.retry_delay == 10.0
        assert config.request_delay == 3.0
        assert config.timeout == 60000


class TestScrapeResult:
    """ScrapeResult 測試"""

    def test_success_result(self):
        """成功結果應正確初始化"""
        result = ScrapeResult(
            success=True,
            data={"key": "value"},
            duration_ms=100.0
        )
        assert result.success is True
        assert result.data == {"key": "value"}
        assert result.error is None
        assert result.duration_ms == 100.0
        assert isinstance(result.timestamp, datetime)

    def test_failure_result(self):
        """失敗結果應正確初始化"""
        result = ScrapeResult(
            success=False,
            error="Connection timeout",
            duration_ms=5000.0
        )
        assert result.success is False
        assert result.data is None
        assert result.error == "Connection timeout"


@pytest.mark.skipif(not PLAYWRIGHT_AVAILABLE, reason="需要 playwright")
class TestBaseScraper:
    """BaseScraper 測試（需要 playwright）"""
    pass  # 需要 playwright 才能執行完整測試


# ===== WarrantData 測試 =====

@pytest.mark.skipif(not PLAYWRIGHT_AVAILABLE, reason="需要 playwright")
class TestWarrantData:
    """WarrantData 資料類別測試（需要 playwright）"""

    def test_dataclass_creation(self):
        """應能正確建立 WarrantData"""
        warrant = WarrantData(
            underlying_ticker="2330",
            warrant_ticker="030001",
            warrant_name="台積電認購",
            issuer="元大",
            warrant_type="Call",
            implied_volatility=0.35,
            effective_leverage=5.0,
            spread_ratio=0.02,
            strike_price=650.0,
            expiry_date=datetime(2024, 6, 30),
            days_to_expiry=90
        )
        assert warrant.underlying_ticker == "2330"
        assert warrant.warrant_type == "Call"
        assert warrant.implied_volatility == 0.35

    def test_optional_greeks(self):
        """Greeks 應為可選欄位"""
        warrant = WarrantData(
            underlying_ticker="2330",
            warrant_ticker="030001",
            warrant_name="Test",
            issuer="Test",
            warrant_type="Put",
            implied_volatility=0.30,
            effective_leverage=4.0,
            spread_ratio=0.01,
            strike_price=600.0,
            expiry_date=datetime(2024, 6, 30),
            days_to_expiry=60,
            delta=0.5,
            gamma=0.01,
            theta=-0.02,
            vega=0.15
        )
        assert warrant.delta == 0.5
        assert warrant.gamma == 0.01
        assert warrant.theta == -0.02
        assert warrant.vega == 0.15


@pytest.mark.skipif(not PLAYWRIGHT_AVAILABLE, reason="需要 playwright")
class TestWarrantScraper:
    """WarrantScraper 測試（需要 playwright）"""

    def test_initialization(self):
        """應能正確初始化"""
        scraper = WarrantScraper()
        assert scraper.MAX_RETRIES == 3
        assert scraper.failed_tickers == []

    def test_target_urls(self):
        """目標網址應正確設定"""
        scraper = WarrantScraper()
        assert "yuanta" in scraper.YUANTA_URL.lower()
        assert "pscnet" in scraper.UNI_URL.lower()


# ===== FinMindClient 測試 =====

class TestFinMindClient:
    """FinMindClient 測試"""

    @pytest.fixture
    def mock_loader(self):
        """Mock FinMind DataLoader"""
        with patch("scrapers.finmind_client.DataLoader") as mock:
            loader_instance = MagicMock()
            mock.return_value = loader_instance
            yield loader_instance

    def test_initialization_without_token(self, mock_loader):
        """無 token 應能初始化"""
        from scrapers.finmind_client import FinMindClient

        client = FinMindClient()
        assert client._token is None

    def test_initialization_with_token(self, mock_loader):
        """有 token 應呼叫 login"""
        from scrapers.finmind_client import FinMindClient

        client = FinMindClient(token="test_token")
        assert client._token == "test_token"
        mock_loader.login_by_token.assert_called_once_with(api_token="test_token")


# ===== 整合測試標記 =====

@pytest.mark.integration
@pytest.mark.skipif(not PLAYWRIGHT_AVAILABLE, reason="需要 playwright")
class TestWarrantScraperIntegration:
    """WarrantScraper 整合測試（需要網路和 playwright）"""

    @pytest.mark.skip(reason="需要實際網路連線")
    async def test_scrape_yuanta(self):
        """測試元大權證網爬取"""
        async with WarrantScraper() as scraper:
            warrants = await scraper.scrape_yuanta_warrants("2330")
            assert isinstance(warrants, list)


@pytest.mark.integration
class TestFinMindClientIntegration:
    """FinMindClient 整合測試（需要 API）"""

    @pytest.mark.skip(reason="需要 FinMind API")
    def test_fetch_stock_prices(self):
        """測試股價資料抓取"""
        from scrapers.finmind_client import FinMindClient

        client = FinMindClient()
        df = client.get_stock_prices("2330", "2024-01-01", "2024-01-31")
        assert len(df) > 0
