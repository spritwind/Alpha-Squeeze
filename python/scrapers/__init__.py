"""
Alpha Squeeze - 爬蟲模組

資料來源：
- FinMind API: 股價、借券、融資融券資料
- 元大權證網: 權證 IV、Greeks
- 統一權證網: 權證 IV (備援)

主要匯出：
    from scrapers import FinMindClient, WarrantScraper, WarrantData
"""

from scrapers.finmind_client import FinMindClient
from scrapers.base_scraper import ScrapeResult, ScraperConfig

# 嘗試匯入需要 playwright 的模組
try:
    from scrapers.warrant_scraper import WarrantScraper, WarrantData
    from scrapers.base_scraper import BaseScraper
    PLAYWRIGHT_AVAILABLE = True
except ImportError:
    WarrantScraper = None
    WarrantData = None
    BaseScraper = None
    PLAYWRIGHT_AVAILABLE = False

__all__ = [
    "FinMindClient",
    "ScrapeResult",
    "ScraperConfig",
]

if PLAYWRIGHT_AVAILABLE:
    __all__.extend(["WarrantScraper", "WarrantData", "BaseScraper"])

__version__ = "0.1.0"
