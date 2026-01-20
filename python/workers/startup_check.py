"""
Alpha Squeeze - Startup Data Check & Backfill

Automatic data integrity check on system startup:
1. Check if database tables exist
2. Verify data freshness (last update date)
3. Trigger backfill if data is stale
4. Check CB data availability
"""

import argparse
import asyncio
import logging
import sys
from datetime import datetime, timedelta
from typing import Optional, Tuple

from engine.config import get_settings
from engine.database import get_database, DatabaseConnection

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(name)s: %(message)s'
)
logger = logging.getLogger(__name__)


class StartupChecker:
    """
    System startup data integrity checker.

    Checks:
    - Database connectivity
    - Table existence
    - Data freshness
    - CB data availability
    """

    def __init__(self, db: Optional[DatabaseConnection] = None):
        self._db = db or get_database()
        self._settings = get_settings()

    def check_database_connection(self) -> bool:
        """Test database connectivity."""
        try:
            result = self._db.execute_query("SELECT 1 AS test")
            return len(result) > 0
        except Exception as e:
            logger.error(f"Database connection failed: {e}")
            return False

    def check_tables_exist(self) -> dict:
        """Check if required tables exist."""
        required_tables = [
            'DailyStockMetrics',
            'TrackedTickers',
            'BackfillJobs',
            'SystemConfig',
            'CBIssuance',
            'CBDailyTracking',
        ]

        results = {}
        for table in required_tables:
            try:
                sql = f"""
                    SELECT COUNT(*) AS cnt
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_NAME = '{table}'
                """
                result = self._db.execute_query(sql)
                results[table] = result[0]['cnt'] > 0 if result else False
            except Exception as e:
                logger.warning(f"Error checking table {table}: {e}")
                results[table] = False

        return results

    def get_stock_data_status(self) -> Tuple[Optional[datetime], int]:
        """
        Get stock data status.

        Returns:
            Tuple of (last_trade_date, record_count)
        """
        try:
            sql = """
                SELECT
                    MAX(TradeDate) AS LastDate,
                    COUNT(*) AS RecordCount
                FROM DailyStockMetrics
            """
            result = self._db.execute_query(sql)
            if result and result[0]['LastDate']:
                return result[0]['LastDate'], result[0]['RecordCount']
            return None, 0
        except Exception as e:
            logger.warning(f"Error getting stock data status: {e}")
            return None, 0

    def get_cb_data_status(self) -> Tuple[Optional[datetime], int, int]:
        """
        Get CB data status.

        Returns:
            Tuple of (last_trade_date, issuance_count, tracking_count)
        """
        try:
            # Check CBIssuance
            sql_issuance = "SELECT COUNT(*) AS cnt FROM CBIssuance WHERE IsActive = 1"
            issuance_result = self._db.execute_query(sql_issuance)
            issuance_count = issuance_result[0]['cnt'] if issuance_result else 0

            # Check CBDailyTracking
            sql_tracking = """
                SELECT
                    MAX(TradeDate) AS LastDate,
                    COUNT(*) AS RecordCount
                FROM CBDailyTracking
            """
            tracking_result = self._db.execute_query(sql_tracking)

            if tracking_result and tracking_result[0]['LastDate']:
                return tracking_result[0]['LastDate'], issuance_count, tracking_result[0]['RecordCount']
            return None, issuance_count, 0

        except Exception as e:
            logger.warning(f"Error getting CB data status: {e}")
            return None, 0, 0

    def is_data_stale(self, last_date, max_days: int = 3) -> bool:
        """Check if data is stale (older than max_days trading days)."""
        if last_date is None:
            return True

        today = datetime.now().date()

        # Handle different date types
        if isinstance(last_date, str):
            try:
                last = datetime.strptime(last_date[:10], '%Y-%m-%d').date()
            except ValueError:
                return True
        elif isinstance(last_date, datetime):
            last = last_date.date()
        else:
            last = last_date

        # Consider weekends - if today is Monday, data from Friday is OK
        days_diff = (today - last).days

        # Rough estimate: weekends don't count
        trading_days = days_diff - (days_diff // 7) * 2

        return trading_days > max_days

    def run_full_check(self) -> dict:
        """
        Run complete startup check.

        Returns:
            Dict with check results and recommendations
        """
        results = {
            'timestamp': datetime.now().isoformat(),
            'status': 'OK',
            'checks': {},
            'recommendations': [],
            'needs_backfill': False,
            'needs_cb_backfill': False,
        }

        # 1. Database connection
        logger.info("Checking database connection...")
        db_ok = self.check_database_connection()
        results['checks']['database'] = db_ok
        if not db_ok:
            results['status'] = 'CRITICAL'
            results['recommendations'].append('Fix database connection')
            return results
        logger.info("  Database connection: OK")

        # 2. Table existence
        logger.info("Checking required tables...")
        tables = self.check_tables_exist()
        results['checks']['tables'] = tables

        missing_tables = [t for t, exists in tables.items() if not exists]
        if missing_tables:
            results['status'] = 'WARNING'
            results['recommendations'].append(f"Create missing tables: {missing_tables}")
            logger.warning(f"  Missing tables: {missing_tables}")
        else:
            logger.info("  All required tables exist")

        # 3. Stock data freshness
        logger.info("Checking stock data freshness...")
        stock_last_date, stock_count = self.get_stock_data_status()
        results['checks']['stock_data'] = {
            'last_date': str(stock_last_date) if stock_last_date else None,
            'record_count': stock_count,
        }

        if stock_count == 0:
            results['status'] = 'WARNING'
            results['needs_backfill'] = True
            results['recommendations'].append('Run initial stock data backfill')
            logger.warning("  No stock data found - backfill required")
        elif self.is_data_stale(stock_last_date):
            results['status'] = 'WARNING'
            results['needs_backfill'] = True
            results['recommendations'].append(f'Stock data is stale (last: {stock_last_date})')
            logger.warning(f"  Stock data is stale, last date: {stock_last_date}")
        else:
            logger.info(f"  Stock data OK: {stock_count} records, last date: {stock_last_date}")

        # 4. CB data freshness
        logger.info("Checking CB data status...")
        cb_last_date, cb_issuance_count, cb_tracking_count = self.get_cb_data_status()
        results['checks']['cb_data'] = {
            'last_date': str(cb_last_date) if cb_last_date else None,
            'issuance_count': cb_issuance_count,
            'tracking_count': cb_tracking_count,
        }

        if cb_issuance_count == 0:
            results['needs_cb_backfill'] = True
            results['recommendations'].append('Run CB issuance data import')
            logger.warning("  No CB issuance data - import required")
        elif cb_tracking_count == 0 or self.is_data_stale(cb_last_date):
            results['needs_cb_backfill'] = True
            results['recommendations'].append('Run CB tracking data backfill')
            logger.warning(f"  CB tracking data needs update, last: {cb_last_date}")
        else:
            logger.info(f"  CB data OK: {cb_issuance_count} issuances, {cb_tracking_count} tracking records")

        # Summary
        if results['status'] == 'OK' and not results['recommendations']:
            logger.info("\n All checks passed - system ready")
        else:
            logger.info(f"\n Status: {results['status']}")
            if results['recommendations']:
                logger.info("Recommendations:")
                for rec in results['recommendations']:
                    logger.info(f"  - {rec}")

        return results


async def run_auto_backfill(checker: StartupChecker, results: dict):
    """Automatically run backfill if needed."""
    from workers.backfill import BackfillService

    if results.get('needs_backfill'):
        logger.info("\n Starting automatic stock data backfill...")
        try:
            service = BackfillService()
            job_id = await service.run_backfill()
            logger.info(f"Stock backfill job completed: #{job_id}")
        except Exception as e:
            logger.error(f"Stock backfill failed: {e}")

    if results.get('needs_cb_backfill'):
        logger.info("\n Starting automatic CB data backfill...")
        try:
            # CB backfill would go here
            # For now, just log a reminder
            logger.info("CB data backfill: Run 'python -m scrapers.cb_tpex_scraper' to fetch CB data")
        except Exception as e:
            logger.error(f"CB backfill failed: {e}")


async def main():
    """CLI entry point for startup check."""
    parser = argparse.ArgumentParser(
        description='Alpha Squeeze Startup Data Check'
    )

    parser.add_argument(
        '--auto-fix',
        action='store_true',
        help='Automatically run backfill if data is stale'
    )

    parser.add_argument(
        '--json',
        action='store_true',
        help='Output results as JSON'
    )

    parser.add_argument(
        '--quiet', '-q',
        action='store_true',
        help='Only show warnings and errors'
    )

    args = parser.parse_args()

    if args.quiet:
        logging.getLogger().setLevel(logging.WARNING)

    logger.info("=" * 50)
    logger.info(" Alpha Squeeze - Startup Data Check")
    logger.info("=" * 50)

    checker = StartupChecker()
    results = checker.run_full_check()

    if args.json:
        import json
        print(json.dumps(results, indent=2, default=str))

    if args.auto_fix and (results.get('needs_backfill') or results.get('needs_cb_backfill')):
        await run_auto_backfill(checker, results)

    # Exit code based on status
    if results['status'] == 'CRITICAL':
        sys.exit(2)
    elif results['status'] == 'WARNING':
        sys.exit(1)
    else:
        sys.exit(0)


if __name__ == '__main__':
    asyncio.run(main())
