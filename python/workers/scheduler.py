"""
Alpha Squeeze - Scheduler

Daily pipeline orchestration for data fetching, scraping, and analysis.

Schedule:
- 18:30: Fetch stock metrics from FinMind
- 19:00: Scrape warrant IV data
- 19:30: Calculate squeeze scores
- 20:00: Generate daily report

Usage:
    python -m workers.scheduler

Or programmatically:
    from workers.scheduler import Scheduler
    scheduler = Scheduler()
    asyncio.run(scheduler.run())
"""

import asyncio
import logging
from datetime import datetime, timedelta
from typing import Any, Callable, Coroutine, Optional

import polars as pl
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from apscheduler.triggers.cron import CronTrigger
from pytz import timezone

from engine.config import get_settings
from engine.squeeze_calculator import SqueezeCalculator
from scrapers.finmind_client import FinMindClient
from scrapers.warrant_scraper import WarrantScraper

logger = logging.getLogger(__name__)

# Default target tickers (top traded stocks)
DEFAULT_TICKERS = [
    "2330",  # TSMC
    "2454",  # MediaTek
    "2317",  # Hon Hai
    "2308",  # Delta
    "2412",  # Chunghwa Telecom
    "2881",  # Fubon FHC
    "2882",  # Cathay FHC
    "2883",  # China Dev FHC
    "2884",  # E.SUN FHC
    "2885",  # Yuanta FHC
    "2303",  # UMC
    "2357",  # ASUS
    "3711",  # ASE
    "2382",  # Quanta
    "2886",  # Mega FHC
]


class DailyPipeline:
    """
    Executes the daily data pipeline.

    Steps:
    1. Fetch stock metrics from FinMind API
    2. Scrape warrant IV data from issuer websites
    3. Calculate squeeze scores for all tickers
    4. Save results to database
    """

    def __init__(
        self,
        tickers: Optional[list[str]] = None,
        finmind_token: Optional[str] = None,
    ):
        """
        Initialize pipeline.

        Args:
            tickers: List of tickers to analyze (uses defaults if None)
            finmind_token: FinMind API token
        """
        self.tickers = tickers or DEFAULT_TICKERS
        self.finmind_token = finmind_token
        self._metrics_data: Optional[pl.DataFrame] = None
        self._warrant_data: Optional[dict[str, Any]] = None
        self._results: Optional[pl.DataFrame] = None

    async def fetch_metrics(self) -> pl.DataFrame:
        """
        Step 1: Fetch stock metrics from FinMind.

        Returns:
            DataFrame with all ticker metrics
        """
        logger.info("Step 1: Fetching stock metrics from FinMind")

        settings = get_settings()
        token = self.finmind_token or settings.finmind.token

        # Date range: last 30 days for HV calculation
        end_date = datetime.now().strftime("%Y-%m-%d")
        start_date = (datetime.now() - timedelta(days=30)).strftime("%Y-%m-%d")

        client = FinMindClient(token=token)
        self._metrics_data = client.get_batch_metrics(
            tickers=self.tickers,
            start_date=start_date,
            end_date=end_date,
            delay=settings.finmind.rate_limit_delay,
        )

        logger.info(f"Fetched {len(self._metrics_data)} metric records")
        return self._metrics_data

    async def scrape_warrants(self) -> dict[str, Any]:
        """
        Step 2: Scrape warrant IV data.

        Returns:
            Dict mapping ticker to warrant data
        """
        logger.info("Step 2: Scraping warrant IV data")

        async with WarrantScraper() as scraper:
            self._warrant_data = await scraper.scrape_all_warrants(
                tickers=self.tickers,
                primary_source="yuanta",
            )

        total_warrants = sum(len(w) for w in self._warrant_data.values())
        logger.info(f"Scraped {total_warrants} warrants for {len(self._warrant_data)} tickers")

        if scraper.failed_items:
            logger.warning(f"Failed to scrape: {scraper.failed_items}")

        return self._warrant_data

    async def calculate_scores(self) -> pl.DataFrame:
        """
        Step 3: Calculate squeeze scores.

        Returns:
            DataFrame with scores for all tickers
        """
        logger.info("Step 3: Calculating squeeze scores")

        if self._metrics_data is None or self._metrics_data.is_empty():
            raise ValueError("No metrics data available. Run fetch_metrics first.")

        calculator = SqueezeCalculator()
        results: list[dict[str, Any]] = []

        # Get latest date metrics for each ticker
        latest_date = self._metrics_data["trade_date"].max()
        latest_metrics = self._metrics_data.filter(
            pl.col("trade_date") == latest_date
        )

        for row in latest_metrics.iter_rows(named=True):
            ticker = row["ticker"]

            # Get warrant IV if available
            iv = 0.0
            if self._warrant_data and ticker in self._warrant_data:
                warrants = self._warrant_data[ticker]
                if warrants:
                    # Use average IV from call warrants
                    call_ivs = [w.implied_volatility for w in warrants if w.warrant_type == "Call"]
                    if call_ivs:
                        iv = sum(call_ivs) / len(call_ivs)

            # Get previous day's close for momentum
            ticker_data = self._metrics_data.filter(pl.col("ticker") == ticker).sort("trade_date")
            prev_close = row["close_price"]  # Default to same if no previous
            if len(ticker_data) >= 2:
                prev_close = ticker_data[-2]["close_price"]

            # Calculate score
            signal = calculator.calculate_squeeze_score(
                ticker=ticker,
                borrow_change=row.get("borrowing_balance_change", 0) or 0,
                margin_ratio=row.get("margin_ratio", 0) or 0,
                iv=iv,
                hv=row.get("historical_volatility_20d", 0) or 0,
                price=row.get("close_price", 0) or 0,
                prev_price=prev_close or row.get("close_price", 0) or 0,
                volume=int(row.get("volume", 0) or 0),
                avg_volume=row.get("avg_volume_20d", 0) or 1,
            )

            results.append({
                "ticker": signal.ticker,
                "signal_date": latest_date,
                "squeeze_score": signal.score,
                "trend": signal.trend.value,
                "comment": signal.comment,
                "borrow_score": signal.factors.borrow_score,
                "gamma_score": signal.factors.gamma_score,
                "margin_score": signal.factors.margin_score,
                "momentum_score": signal.factors.momentum_score,
            })

        self._results = pl.DataFrame(results).sort("squeeze_score", descending=True)
        logger.info(f"Calculated scores for {len(self._results)} tickers")

        # Log top candidates
        top = self._results.head(5)
        logger.info("Top 5 squeeze candidates:")
        for row in top.iter_rows(named=True):
            logger.info(
                f"  {row['ticker']}: Score={row['squeeze_score']}, "
                f"Trend={row['trend']}"
            )

        return self._results

    async def save_results(self) -> None:
        """
        Step 4: Save results to database.

        Note: Actual implementation would use gRPC or direct database connection.
        """
        logger.info("Step 4: Saving results to database")

        if self._results is None or self._results.is_empty():
            logger.warning("No results to save")
            return

        # TODO: Implement database save via gRPC or pyodbc
        # For now, save to CSV as backup
        output_path = f"output/squeeze_signals_{datetime.now().strftime('%Y%m%d')}.csv"
        self._results.write_csv(output_path)
        logger.info(f"Results saved to {output_path}")

    async def run(self) -> pl.DataFrame:
        """
        Execute the complete daily pipeline.

        Returns:
            DataFrame with squeeze scores
        """
        start_time = datetime.now()
        logger.info(f"Starting daily pipeline at {start_time}")

        try:
            await self.fetch_metrics()
            await self.scrape_warrants()
            await self.calculate_scores()
            await self.save_results()

            duration = (datetime.now() - start_time).total_seconds()
            logger.info(f"Daily pipeline completed in {duration:.1f} seconds")

            return self._results or pl.DataFrame()

        except Exception as e:
            logger.error(f"Pipeline failed: {e}", exc_info=True)
            raise


class Scheduler:
    """
    APScheduler-based task scheduler for daily pipeline.

    Manages scheduled execution of data fetching and analysis tasks.
    """

    def __init__(
        self,
        tickers: Optional[list[str]] = None,
        tz: str = "Asia/Taipei",
    ):
        """
        Initialize scheduler.

        Args:
            tickers: List of tickers to track
            tz: Timezone for scheduling
        """
        self.tickers = tickers or DEFAULT_TICKERS
        self.tz = timezone(tz)
        self._scheduler = AsyncIOScheduler(timezone=self.tz)
        self._setup_jobs()

    def _setup_jobs(self) -> None:
        """Configure scheduled jobs."""
        settings = get_settings()

        # Parse schedule times
        fetch_hour, fetch_min = map(int, settings.scheduler.fetch_time.split(":"))
        scrape_hour, scrape_min = map(int, settings.scheduler.scrape_time.split(":"))
        calc_hour, calc_min = map(int, settings.scheduler.calculate_time.split(":"))

        # 18:30 - Fetch metrics
        self._scheduler.add_job(
            self._run_fetch,
            CronTrigger(hour=fetch_hour, minute=fetch_min, day_of_week="mon-fri"),
            id="fetch_metrics",
            name="Fetch Stock Metrics",
        )

        # 19:00 - Scrape warrants
        self._scheduler.add_job(
            self._run_scrape,
            CronTrigger(hour=scrape_hour, minute=scrape_min, day_of_week="mon-fri"),
            id="scrape_warrants",
            name="Scrape Warrant IV",
        )

        # 19:30 - Calculate scores
        self._scheduler.add_job(
            self._run_calculate,
            CronTrigger(hour=calc_hour, minute=calc_min, day_of_week="mon-fri"),
            id="calculate_scores",
            name="Calculate Squeeze Scores",
        )

        logger.info("Scheduled jobs configured")

    async def _run_fetch(self) -> None:
        """Execute fetch job."""
        logger.info("Running scheduled fetch job")
        pipeline = DailyPipeline(tickers=self.tickers)
        await pipeline.fetch_metrics()

    async def _run_scrape(self) -> None:
        """Execute scrape job."""
        logger.info("Running scheduled scrape job")
        pipeline = DailyPipeline(tickers=self.tickers)
        await pipeline.scrape_warrants()

    async def _run_calculate(self) -> None:
        """Execute calculation job."""
        logger.info("Running scheduled calculation job")
        pipeline = DailyPipeline(tickers=self.tickers)
        await pipeline.run()  # Full pipeline

    def start(self) -> None:
        """Start the scheduler."""
        self._scheduler.start()
        logger.info("Scheduler started")

    def stop(self) -> None:
        """Stop the scheduler."""
        self._scheduler.shutdown()
        logger.info("Scheduler stopped")

    async def run(self) -> None:
        """Run scheduler in async context."""
        self.start()
        try:
            while True:
                await asyncio.sleep(1)
        except KeyboardInterrupt:
            self.stop()


async def run_pipeline_now() -> pl.DataFrame:
    """
    Execute pipeline immediately (for testing/manual runs).

    Returns:
        DataFrame with results
    """
    pipeline = DailyPipeline()
    return await pipeline.run()


def main() -> None:
    """Entry point for scheduler."""
    import sys

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    )

    if "--now" in sys.argv:
        # Run pipeline immediately
        logger.info("Running pipeline immediately")
        asyncio.run(run_pipeline_now())
    else:
        # Start scheduler
        logger.info("Starting scheduler")
        scheduler = Scheduler()
        asyncio.run(scheduler.run())


if __name__ == "__main__":
    main()
