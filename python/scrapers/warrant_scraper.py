"""
Alpha Squeeze - Warrant IV Scraper

Scrapes IV and Greeks from Taiwan warrant issuers using Playwright.
Targets: 元大權證網, 統一權證網
"""

import asyncio
import logging
from dataclasses import dataclass
from datetime import datetime
from typing import Optional

# 嘗試匯入 playwright，如果未安裝則提供佔位符
try:
    from playwright.async_api import async_playwright, Page, Browser
    PLAYWRIGHT_AVAILABLE = True
except ImportError:
    async_playwright = None
    Page = None
    Browser = None
    PLAYWRIGHT_AVAILABLE = False

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


@dataclass
class WarrantData:
    """Scraped warrant data"""
    underlying_ticker: str
    warrant_ticker: str
    warrant_name: str
    issuer: str
    warrant_type: str  # Call/Put
    implied_volatility: float
    effective_leverage: float
    spread_ratio: float
    strike_price: float
    expiry_date: datetime
    days_to_expiry: int
    delta: Optional[float] = None
    gamma: Optional[float] = None
    theta: Optional[float] = None
    vega: Optional[float] = None


class WarrantScraper:
    """
    Playwright-based scraper for Taiwan warrant data.
    """

    MAX_RETRIES = 3
    RETRY_DELAY = 5  # seconds
    REQUEST_DELAY = 2  # seconds between requests

    # Target URLs
    YUANTA_URL = "https://warrant.yuanta.com.tw/"
    UNI_URL = "https://warrant.pscnet.com.tw/"

    def __init__(self):
        self.browser: Optional[Browser] = None
        self.failed_tickers: list[str] = []

    async def __aenter__(self):
        if not PLAYWRIGHT_AVAILABLE:
            raise RuntimeError(
                "Playwright not installed. Run: pip install playwright && playwright install chromium"
            )
        playwright = await async_playwright().start()
        self.browser = await playwright.chromium.launch(headless=True)
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        if self.browser:
            await self.browser.close()

    async def scrape_with_retry(
        self,
        scrape_func,
        *args,
        **kwargs
    ) -> Optional[list[WarrantData]]:
        """
        Execute scrape function with retry logic.

        Args:
            scrape_func: Async function to execute
            *args, **kwargs: Arguments for scrape_func

        Returns:
            Scraped data or None on failure
        """
        for attempt in range(self.MAX_RETRIES):
            try:
                return await scrape_func(*args, **kwargs)
            except Exception as e:
                logger.warning(
                    f"Scrape attempt {attempt + 1}/{self.MAX_RETRIES} failed: {e}"
                )
                if attempt < self.MAX_RETRIES - 1:
                    delay = self.RETRY_DELAY * (attempt + 1)
                    logger.info(f"Retrying in {delay} seconds...")
                    await asyncio.sleep(delay)
                else:
                    logger.error(f"All retry attempts failed: {e}")
                    return None

    async def scrape_yuanta_warrants(
        self,
        underlying_ticker: str
    ) -> list[WarrantData]:
        """
        Scrape warrant data from 元大權證網.

        Args:
            underlying_ticker: The underlying stock ticker

        Returns:
            List of WarrantData for the underlying
        """
        if not self.browser:
            raise RuntimeError("Browser not initialized. Use async context manager.")

        page = await self.browser.new_page()
        warrants = []

        try:
            logger.info(f"Scraping Yuanta warrants for {underlying_ticker}")

            # Navigate to warrant search page
            # Note: Actual URL and selectors would be determined by inspecting the site
            await page.goto(f"{self.YUANTA_URL}warrant/search")
            await page.wait_for_load_state("networkidle")

            # Search for underlying ticker
            await page.fill("input[name='stock_id']", underlying_ticker)
            await page.click("button[type='submit']")
            await page.wait_for_load_state("networkidle")

            # Parse warrant table
            # Note: Selectors would be based on actual page structure
            rows = await page.query_selector_all("table.warrant-list tbody tr")

            for row in rows:
                try:
                    warrant = await self._parse_yuanta_row(row, underlying_ticker)
                    if warrant:
                        warrants.append(warrant)
                except Exception as e:
                    logger.warning(f"Error parsing row: {e}")

            logger.info(f"Found {len(warrants)} warrants for {underlying_ticker}")

        except Exception as e:
            logger.error(f"Error scraping Yuanta for {underlying_ticker}: {e}")
            self.failed_tickers.append(underlying_ticker)
            raise

        finally:
            await page.close()

        return warrants

    async def _parse_yuanta_row(
        self,
        row,
        underlying_ticker: str
    ) -> Optional[WarrantData]:
        """Parse a single warrant row from Yuanta table."""
        # Implementation would extract data from row elements
        # This is a placeholder structure
        pass

    async def scrape_uni_warrants(
        self,
        underlying_ticker: str
    ) -> list[WarrantData]:
        """
        Scrape warrant data from 統一權證網 (backup source).
        """
        # Similar implementation to Yuanta
        pass

    async def scrape_all_warrants(
        self,
        tickers: list[str],
        primary_source: str = "yuanta"
    ) -> dict[str, list[WarrantData]]:
        """
        Scrape warrants for multiple underlying tickers.

        Args:
            tickers: List of underlying stock tickers
            primary_source: Primary data source ("yuanta" or "uni")

        Returns:
            Dict mapping ticker to list of warrants
        """
        results = {}
        scrape_func = (
            self.scrape_yuanta_warrants
            if primary_source == "yuanta"
            else self.scrape_uni_warrants
        )

        for ticker in tickers:
            data = await self.scrape_with_retry(scrape_func, ticker)
            if data:
                results[ticker] = data
            else:
                logger.warning(f"Failed to scrape {ticker}, marking for review")

            # Respect rate limits
            await asyncio.sleep(self.REQUEST_DELAY)

        return results

    def get_failed_tickers(self) -> list[str]:
        """Return list of tickers that failed to scrape."""
        return self.failed_tickers.copy()


async def main():
    """Test scraper functionality."""
    test_tickers = ["2330", "2454", "2317"]

    async with WarrantScraper() as scraper:
        results = await scraper.scrape_all_warrants(test_tickers)

        for ticker, warrants in results.items():
            print(f"\n{ticker}: {len(warrants)} warrants found")
            for w in warrants[:3]:  # Show first 3
                print(f"  - {w.warrant_ticker}: IV={w.implied_volatility:.2%}")

        if scraper.failed_tickers:
            print(f"\nFailed tickers: {scraper.failed_tickers}")


if __name__ == "__main__":
    asyncio.run(main())
