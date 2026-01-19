"""
Alpha Squeeze - Base Scraper

Abstract base class for all web scrapers with common functionality:
- Playwright browser management
- Retry logic with exponential backoff
- Rate limiting
- Error handling and logging
"""

import asyncio
import logging
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Generic, Optional, TypeVar

# 嘗試匯入 playwright，如果未安裝則提供佔位符
try:
    from playwright.async_api import Browser, Page, Playwright, async_playwright
    PLAYWRIGHT_AVAILABLE = True
except ImportError:
    Browser = None
    Page = None
    Playwright = None
    async_playwright = None
    PLAYWRIGHT_AVAILABLE = False

logger = logging.getLogger(__name__)

T = TypeVar("T")


@dataclass
class ScrapeResult(Generic[T]):
    """Result container for scrape operations."""

    success: bool
    data: Optional[T] = None
    error: Optional[str] = None
    timestamp: datetime = field(default_factory=datetime.now)
    duration_ms: float = 0.0


@dataclass
class ScraperConfig:
    """Configuration for scrapers."""

    headless: bool = True
    max_retries: int = 3
    retry_delay: float = 5.0
    request_delay: float = 2.0
    timeout: int = 30000
    user_agent: str = (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/120.0.0.0 Safari/537.36"
    )


class BaseScraper(ABC, Generic[T]):
    """
    Abstract base class for web scrapers.

    Provides:
    - Playwright browser lifecycle management
    - Retry logic with exponential backoff
    - Rate limiting between requests
    - Structured error handling

    Usage:
        class MyScraper(BaseScraper[MyDataType]):
            async def scrape(self, page: Page, **kwargs) -> MyDataType:
                # Implementation
                pass

        async with MyScraper() as scraper:
            result = await scraper.execute(ticker="2330")
    """

    def __init__(self, config: Optional[ScraperConfig] = None):
        """
        Initialize scraper with configuration.

        Args:
            config: Scraper configuration (uses defaults if None)
        """
        self.config = config or ScraperConfig()
        self._playwright: Optional[Playwright] = None
        self._browser: Optional[Browser] = None
        self._failed_items: list[str] = []

    async def __aenter__(self) -> "BaseScraper[T]":
        """Start browser on context entry."""
        if not PLAYWRIGHT_AVAILABLE:
            raise RuntimeError(
                "Playwright not installed. Run: pip install playwright && playwright install chromium"
            )
        await self._start_browser()
        return self

    async def __aexit__(
        self,
        exc_type: Optional[type],
        exc_val: Optional[BaseException],
        exc_tb: Any,
    ) -> None:
        """Close browser on context exit."""
        await self._close_browser()

    async def _start_browser(self) -> None:
        """Initialize Playwright and launch browser."""
        logger.info(f"Starting browser (headless={self.config.headless})")
        self._playwright = await async_playwright().start()
        self._browser = await self._playwright.chromium.launch(
            headless=self.config.headless,
        )

    async def _close_browser(self) -> None:
        """Close browser and Playwright."""
        if self._browser:
            await self._browser.close()
            self._browser = None
        if self._playwright:
            await self._playwright.stop()
            self._playwright = None
        logger.info("Browser closed")

    async def _new_page(self) -> Page:
        """Create a new page with configured settings."""
        if not self._browser:
            raise RuntimeError("Browser not initialized. Use async context manager.")

        page = await self._browser.new_page(
            user_agent=self.config.user_agent,
        )
        page.set_default_timeout(self.config.timeout)
        return page

    @abstractmethod
    async def scrape(self, page: Page, **kwargs: Any) -> T:
        """
        Implement the actual scraping logic.

        Args:
            page: Playwright page instance
            **kwargs: Scrape-specific parameters

        Returns:
            Scraped data of type T
        """
        raise NotImplementedError

    async def execute(self, **kwargs: Any) -> ScrapeResult[T]:
        """
        Execute scrape with retry logic.

        Args:
            **kwargs: Parameters passed to scrape()

        Returns:
            ScrapeResult containing data or error
        """
        start_time = datetime.now()
        last_error: Optional[str] = None

        for attempt in range(1, self.config.max_retries + 1):
            page: Optional[Page] = None
            try:
                page = await self._new_page()
                data = await self.scrape(page, **kwargs)

                duration = (datetime.now() - start_time).total_seconds() * 1000
                return ScrapeResult(
                    success=True,
                    data=data,
                    duration_ms=duration,
                )

            except Exception as e:
                last_error = str(e)
                logger.warning(
                    f"Scrape attempt {attempt}/{self.config.max_retries} failed: {e}"
                )

                if attempt < self.config.max_retries:
                    delay = self.config.retry_delay * attempt
                    logger.info(f"Retrying in {delay:.1f} seconds...")
                    await asyncio.sleep(delay)

            finally:
                if page:
                    await page.close()

        # All retries failed
        duration = (datetime.now() - start_time).total_seconds() * 1000
        logger.error(f"All {self.config.max_retries} attempts failed: {last_error}")

        return ScrapeResult(
            success=False,
            error=last_error,
            duration_ms=duration,
        )

    async def execute_batch(
        self,
        items: list[dict[str, Any]],
    ) -> list[ScrapeResult[T]]:
        """
        Execute scrape for multiple items with rate limiting.

        Args:
            items: List of kwargs dicts for each scrape

        Returns:
            List of ScrapeResult for each item
        """
        results: list[ScrapeResult[T]] = []

        for i, item_kwargs in enumerate(items):
            result = await self.execute(**item_kwargs)
            results.append(result)

            if not result.success:
                item_id = item_kwargs.get("ticker", item_kwargs.get("id", str(i)))
                self._failed_items.append(str(item_id))

            # Rate limiting (skip for last item)
            if i < len(items) - 1:
                await asyncio.sleep(self.config.request_delay)

        return results

    @property
    def failed_items(self) -> list[str]:
        """Return list of failed item identifiers."""
        return self._failed_items.copy()

    def clear_failed_items(self) -> None:
        """Clear the failed items list."""
        self._failed_items.clear()
