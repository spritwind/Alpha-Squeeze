"""
Alpha Squeeze - Historical Data Backfill Worker

Fetches historical stock data from FinMind API and stores in database.
Supports:
- Date range specification
- Resumable jobs with progress tracking
- Rate limiting to respect API constraints
- Automatic gap detection and filling
"""

import argparse
import asyncio
import logging
from datetime import datetime, timedelta
from typing import List, Optional, Tuple

import polars as pl

from engine.config import get_settings
from engine.database import (
    DatabaseConnection,
    BackfillJobRepository,
    StockMetricsRepository,
    TrackedTickerRepository,
    ConfigRepository,
    get_database,
)
from workers.daily_fetch import DailyDataFetcher

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(name)s: %(message)s'
)
logger = logging.getLogger(__name__)


class BackfillService:
    """
    Service for backfilling historical stock data.

    Features:
    - Fetches data from FinMind API
    - Stores to MSSQL database
    - Tracks progress in BackfillJobs table
    - Supports resumable operations
    """

    def __init__(
        self,
        db: Optional[DatabaseConnection] = None,
        finmind_token: Optional[str] = None
    ):
        """
        Initialize backfill service.

        Args:
            db: Database connection (uses default if None)
            finmind_token: FinMind API token (uses config if None)
        """
        self._db = db or get_database()
        self._settings = get_settings()

        # Initialize repositories
        self._job_repo = BackfillJobRepository(self._db)
        self._metrics_repo = StockMetricsRepository(self._db)
        self._ticker_repo = TrackedTickerRepository(self._db)
        self._config_repo = ConfigRepository(self._db)

        # Initialize FinMind fetcher
        token = finmind_token or self._settings.finmind.token
        self._fetcher = DailyDataFetcher(finmind_token=token)

        # Get rate limit delay from config
        delay_str = self._config_repo.get_value('FINMIND_RATE_LIMIT_DELAY')
        self._rate_limit_delay = float(delay_str) if delay_str else 0.5

    def _get_default_date_range(self) -> Tuple[str, str]:
        """Get default backfill date range (last N days from config)."""
        days_str = self._config_repo.get_value('BACKFILL_DEFAULT_DAYS')
        days = int(days_str) if days_str else 30

        end_date = datetime.now()
        start_date = end_date - timedelta(days=days)

        return start_date.strftime('%Y-%m-%d'), end_date.strftime('%Y-%m-%d')

    async def run_backfill(
        self,
        start_date: Optional[str] = None,
        end_date: Optional[str] = None,
        tickers: Optional[List[str]] = None,
        job_type: str = 'STOCK_METRICS',
        created_by: str = 'SYSTEM'
    ) -> int:
        """
        Execute backfill operation.

        Args:
            start_date: Start date (YYYY-MM-DD), defaults to config value
            end_date: End date (YYYY-MM-DD), defaults to today
            tickers: List of tickers to backfill, defaults to all active tickers
            job_type: Job type identifier
            created_by: Job creator identifier

        Returns:
            Job ID
        """
        # Determine date range
        if not start_date or not end_date:
            default_start, default_end = self._get_default_date_range()
            start_date = start_date or default_start
            end_date = end_date or default_end

        # Determine tickers
        if not tickers:
            tickers = self._ticker_repo.get_active_tickers()

        if not tickers:
            logger.warning("No tickers to backfill")
            return 0

        logger.info(f"Starting backfill: {start_date} to {end_date} for {len(tickers)} tickers")

        # Create job record
        job_id = self._job_repo.create_job(
            job_type=job_type,
            start_date=start_date,
            end_date=end_date,
            total_tickers=len(tickers),
            created_by=created_by
        )

        logger.info(f"Created backfill job #{job_id}")

        # Start job
        self._job_repo.start_job(job_id)

        processed = 0
        failed = 0
        failed_tickers = []

        try:
            for i, ticker in enumerate(tickers, 1):
                logger.info(f"[{i}/{len(tickers)}] Processing {ticker}...")

                try:
                    # Fetch data from FinMind
                    df = self._fetcher.fetch_complete_metrics(
                        ticker=ticker,
                        start_date=start_date,
                        end_date=end_date
                    )

                    if df is not None and len(df) > 0:
                        # Normalize column names for database
                        df = self._normalize_dataframe(df)

                        # Save to database
                        rows_affected = self._metrics_repo.upsert_metrics(df)
                        logger.info(f"  Saved {rows_affected} records for {ticker}")
                        processed += 1
                    else:
                        logger.warning(f"  No data returned for {ticker}")
                        processed += 1  # Still count as processed (no data available)

                except Exception as e:
                    logger.error(f"  Error processing {ticker}: {e}")
                    failed += 1
                    failed_tickers.append(ticker)

                # Update progress
                self._job_repo.update_progress(job_id, processed, failed)

                # Rate limiting
                if i < len(tickers):
                    await asyncio.sleep(self._rate_limit_delay)

            # Complete job
            error_msg = f"Failed tickers: {failed_tickers}" if failed_tickers else None
            self._job_repo.complete_job(job_id, error_msg)

            logger.info(f"Backfill completed: {processed} processed, {failed} failed")
            return job_id

        except Exception as e:
            logger.error(f"Backfill job failed: {e}")
            self._job_repo.complete_job(job_id, str(e))
            raise

    def _normalize_dataframe(self, df: pl.DataFrame) -> pl.DataFrame:
        """
        Normalize DataFrame column names to match database schema.
        """
        # Define column mapping
        column_mapping = {
            'stock_id': 'ticker',
            'date': 'trade_date',
            'open': 'open_price',
            'max': 'high_price',
            'min': 'low_price',
            'close': 'close_price',
            'Trading_Volume': 'volume',
            'Trading_money': 'turnover',
            'securities_lending_balance': 'borrowing_balance',
            'MarginPurchaseTodayBalance': 'margin_balance',
            'ShortSaleTodayBalance': 'short_balance',
        }

        # Rename columns that exist
        for old_name, new_name in column_mapping.items():
            if old_name in df.columns:
                df = df.rename({old_name: new_name})

        # Ensure required columns exist with proper types
        required_columns = [
            'ticker', 'trade_date', 'close_price', 'open_price',
            'high_price', 'low_price', 'volume', 'turnover',
            'borrowing_balance', 'borrowing_balance_change',
            'margin_balance', 'short_balance', 'margin_ratio',
            'historical_volatility_20d'
        ]

        for col in required_columns:
            if col not in df.columns:
                df = df.with_columns(pl.lit(None).alias(col))

        return df

    def get_job_status(self, job_id: int) -> Optional[dict]:
        """Get status of a backfill job."""
        return self._job_repo.get_job(job_id)

    def get_recent_jobs(self, limit: int = 10) -> List[dict]:
        """Get recent backfill jobs."""
        return self._job_repo.get_recent_jobs(limit)

    def find_data_gaps(
        self,
        ticker: str,
        start_date: str,
        end_date: str
    ) -> List[str]:
        """
        Find dates without data for a ticker.

        Returns:
            List of missing dates
        """
        return self._metrics_repo.get_missing_dates(ticker, start_date, end_date)

    async def fill_gaps(
        self,
        tickers: Optional[List[str]] = None,
        lookback_days: int = 30
    ) -> int:
        """
        Automatically detect and fill data gaps.

        Args:
            tickers: Tickers to check (defaults to all active)
            lookback_days: Number of days to check for gaps

        Returns:
            Job ID
        """
        end_date = datetime.now()
        start_date = end_date - timedelta(days=lookback_days)

        tickers = tickers or self._ticker_repo.get_active_tickers()
        tickers_with_gaps = []

        for ticker in tickers:
            gaps = self.find_data_gaps(
                ticker,
                start_date.strftime('%Y-%m-%d'),
                end_date.strftime('%Y-%m-%d')
            )
            if gaps:
                logger.info(f"Found {len(gaps)} gaps for {ticker}")
                tickers_with_gaps.append(ticker)

        if not tickers_with_gaps:
            logger.info("No data gaps found")
            return 0

        return await self.run_backfill(
            start_date=start_date.strftime('%Y-%m-%d'),
            end_date=end_date.strftime('%Y-%m-%d'),
            tickers=tickers_with_gaps,
            job_type='GAP_FILL',
            created_by='SYSTEM_AUTO'
        )


async def main():
    """CLI entry point for backfill worker."""
    parser = argparse.ArgumentParser(
        description='Alpha Squeeze Historical Data Backfill'
    )

    parser.add_argument(
        '--start-date', '-s',
        type=str,
        help='Start date (YYYY-MM-DD), defaults to 30 days ago'
    )

    parser.add_argument(
        '--end-date', '-e',
        type=str,
        help='End date (YYYY-MM-DD), defaults to today'
    )

    parser.add_argument(
        '--tickers', '-t',
        type=str,
        nargs='+',
        help='Specific tickers to backfill (space-separated)'
    )

    parser.add_argument(
        '--fill-gaps',
        action='store_true',
        help='Automatically detect and fill data gaps'
    )

    parser.add_argument(
        '--lookback-days',
        type=int,
        default=30,
        help='Days to look back for gap detection (default: 30)'
    )

    parser.add_argument(
        '--finmind-token',
        type=str,
        help='FinMind API token (or set FINMIND_TOKEN env var)'
    )

    parser.add_argument(
        '--dry-run',
        action='store_true',
        help='Show what would be done without executing'
    )

    args = parser.parse_args()

    # Initialize service
    service = BackfillService(finmind_token=args.finmind_token)

    if args.dry_run:
        logger.info("DRY RUN MODE - No changes will be made")

        if args.fill_gaps:
            tickers = args.tickers or service._ticker_repo.get_active_tickers()
            end_date = datetime.now()
            start_date = end_date - timedelta(days=args.lookback_days)

            for ticker in tickers[:5]:  # Sample check
                gaps = service.find_data_gaps(
                    ticker,
                    start_date.strftime('%Y-%m-%d'),
                    end_date.strftime('%Y-%m-%d')
                )
                if gaps:
                    logger.info(f"{ticker}: {len(gaps)} missing dates")
                    logger.info(f"  Sample: {gaps[:5]}")
        else:
            start_date = args.start_date or (datetime.now() - timedelta(days=30)).strftime('%Y-%m-%d')
            end_date = args.end_date or datetime.now().strftime('%Y-%m-%d')
            tickers = args.tickers or service._ticker_repo.get_active_tickers()

            logger.info(f"Would backfill: {start_date} to {end_date}")
            logger.info(f"Tickers ({len(tickers)}): {tickers[:10]}...")

        return

    # Execute backfill
    if args.fill_gaps:
        job_id = await service.fill_gaps(
            tickers=args.tickers,
            lookback_days=args.lookback_days
        )
    else:
        job_id = await service.run_backfill(
            start_date=args.start_date,
            end_date=args.end_date,
            tickers=args.tickers
        )

    if job_id:
        logger.info(f"Backfill job completed: #{job_id}")
        status = service.get_job_status(job_id)
        logger.info(f"Status: {status}")


if __name__ == '__main__':
    asyncio.run(main())
