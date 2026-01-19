"""
Alpha Squeeze - Database Connection Module

Provides MSSQL database connectivity using pyodbc.
Supports both Windows Authentication and SQL Server Authentication.
"""

import logging
from contextlib import contextmanager
from typing import Any, Dict, List, Optional, Generator

import pyodbc
import polars as pl

from engine.config import get_settings

logger = logging.getLogger(__name__)


class DatabaseConnection:
    """
    MSSQL database connection manager.

    Supports connection pooling and context management.
    """

    def __init__(self, connection_string: Optional[str] = None):
        """
        Initialize database connection.

        Args:
            connection_string: ODBC connection string.
                              If None, uses settings from config.
        """
        if connection_string:
            self._connection_string = connection_string
        else:
            settings = get_settings()
            self._connection_string = settings.database.connection_string

        self._connection: Optional[pyodbc.Connection] = None

    @property
    def connection_string(self) -> str:
        return self._connection_string

    def connect(self) -> pyodbc.Connection:
        """Establish database connection."""
        if self._connection is None or self._connection.closed:
            logger.debug("Establishing database connection...")
            self._connection = pyodbc.connect(self._connection_string)
            logger.info("Database connection established")
        return self._connection

    def close(self) -> None:
        """Close database connection."""
        if self._connection and not self._connection.closed:
            self._connection.close()
            logger.info("Database connection closed")
        self._connection = None

    @contextmanager
    def get_cursor(self) -> Generator[pyodbc.Cursor, None, None]:
        """Get a database cursor with automatic cleanup."""
        conn = self.connect()
        cursor = conn.cursor()
        try:
            yield cursor
            conn.commit()
        except Exception:
            conn.rollback()
            raise
        finally:
            cursor.close()

    def execute_query(self, query: str, params: tuple = ()) -> List[Dict[str, Any]]:
        """
        Execute a SELECT query and return results as list of dicts.

        Args:
            query: SQL query string
            params: Query parameters

        Returns:
            List of dictionaries with column names as keys
        """
        with self.get_cursor() as cursor:
            cursor.execute(query, params)
            columns = [desc[0] for desc in cursor.description]
            rows = cursor.fetchall()
            return [dict(zip(columns, row)) for row in rows]

    def execute_non_query(self, query: str, params: tuple = ()) -> int:
        """
        Execute an INSERT/UPDATE/DELETE query.

        Returns:
            Number of affected rows
        """
        with self.get_cursor() as cursor:
            cursor.execute(query, params)
            return cursor.rowcount

    def execute_many(self, query: str, params_list: List[tuple]) -> int:
        """
        Execute a query with multiple parameter sets (batch insert).

        Returns:
            Total number of affected rows
        """
        with self.get_cursor() as cursor:
            cursor.fast_executemany = True
            cursor.executemany(query, params_list)
            return cursor.rowcount


class ConfigRepository:
    """
    Repository for SystemConfig table operations.
    """

    def __init__(self, db: DatabaseConnection):
        self._db = db

    def get_all(self) -> List[Dict[str, Any]]:
        """Get all configuration values."""
        query = """
            SELECT ConfigKey, ConfigValue, ValueType, Category,
                   Description, MinValue, MaxValue, IsReadOnly, UpdatedAt
            FROM SystemConfig
            ORDER BY Category, ConfigKey
        """
        return self._db.execute_query(query)

    def get_by_category(self, category: str) -> List[Dict[str, Any]]:
        """Get configuration values by category."""
        query = """
            SELECT ConfigKey, ConfigValue, ValueType, Category,
                   Description, MinValue, MaxValue, IsReadOnly, UpdatedAt
            FROM SystemConfig
            WHERE Category = ?
            ORDER BY ConfigKey
        """
        return self._db.execute_query(query, (category,))

    def get_value(self, key: str) -> Optional[str]:
        """Get a single configuration value."""
        query = "SELECT ConfigValue FROM SystemConfig WHERE ConfigKey = ?"
        results = self._db.execute_query(query, (key,))
        return results[0]['ConfigValue'] if results else None

    def get_squeeze_weights(self) -> Dict[str, float]:
        """Get squeeze algorithm weights."""
        configs = self.get_by_category('SQUEEZE_WEIGHT')
        return {
            'borrow': float(next((c['ConfigValue'] for c in configs if c['ConfigKey'] == 'SQUEEZE_WEIGHT_BORROW'), 0.35)),
            'gamma': float(next((c['ConfigValue'] for c in configs if c['ConfigKey'] == 'SQUEEZE_WEIGHT_GAMMA'), 0.25)),
            'margin': float(next((c['ConfigValue'] for c in configs if c['ConfigKey'] == 'SQUEEZE_WEIGHT_MARGIN'), 0.20)),
            'momentum': float(next((c['ConfigValue'] for c in configs if c['ConfigKey'] == 'SQUEEZE_WEIGHT_MOMENTUM'), 0.20)),
        }

    def get_squeeze_thresholds(self) -> Dict[str, int]:
        """Get squeeze algorithm thresholds."""
        configs = self.get_by_category('SQUEEZE_THRESHOLD')
        return {
            'bullish': int(next((c['ConfigValue'] for c in configs if c['ConfigKey'] == 'SQUEEZE_THRESHOLD_BULLISH'), 70)),
            'bearish': int(next((c['ConfigValue'] for c in configs if c['ConfigKey'] == 'SQUEEZE_THRESHOLD_BEARISH'), 40)),
        }

    def update_value(self, key: str, value: str, updated_by: str = None) -> bool:
        """Update a configuration value."""
        query = """
            UPDATE SystemConfig
            SET ConfigValue = ?, UpdatedAt = GETDATE(), UpdatedBy = ?
            WHERE ConfigKey = ? AND IsReadOnly = 0
        """
        rows = self._db.execute_non_query(query, (value, updated_by, key))
        return rows > 0


class TrackedTickerRepository:
    """
    Repository for TrackedTickers table operations.
    """

    def __init__(self, db: DatabaseConnection):
        self._db = db

    def get_active_tickers(self) -> List[str]:
        """Get list of active tickers to track."""
        query = """
            SELECT Ticker FROM TrackedTickers
            WHERE IsActive = 1
            ORDER BY Priority, Ticker
        """
        results = self._db.execute_query(query)
        return [r['Ticker'] for r in results]

    def get_all(self) -> List[Dict[str, Any]]:
        """Get all tracked tickers with details."""
        query = """
            SELECT Ticker, TickerName, Category, IsActive, Priority, AddedAt, Notes
            FROM TrackedTickers
            ORDER BY Priority, Ticker
        """
        return self._db.execute_query(query)

    def add_ticker(self, ticker: str, name: str = None, category: str = None) -> bool:
        """Add a new ticker to track."""
        query = """
            INSERT INTO TrackedTickers (Ticker, TickerName, Category)
            VALUES (?, ?, ?)
        """
        try:
            self._db.execute_non_query(query, (ticker, name, category))
            return True
        except pyodbc.IntegrityError:
            return False  # Already exists

    def set_active(self, ticker: str, is_active: bool) -> bool:
        """Enable or disable tracking for a ticker."""
        query = "UPDATE TrackedTickers SET IsActive = ? WHERE Ticker = ?"
        rows = self._db.execute_non_query(query, (is_active, ticker))
        return rows > 0


class StockMetricsRepository:
    """
    Repository for DailyStockMetrics table operations.
    """

    def __init__(self, db: DatabaseConnection):
        self._db = db

    def get_by_date(self, trade_date: str) -> List[Dict[str, Any]]:
        """Get all metrics for a specific date."""
        query = """
            SELECT Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                   BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                   MarginRatio, HistoricalVolatility20D, Volume, Turnover
            FROM DailyStockMetrics
            WHERE TradeDate = ?
            ORDER BY Ticker
        """
        return self._db.execute_query(query, (trade_date,))

    def get_date_range(self, ticker: str, start_date: str, end_date: str) -> List[Dict[str, Any]]:
        """Get metrics for a ticker within date range."""
        query = """
            SELECT Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                   BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                   MarginRatio, HistoricalVolatility20D, Volume, Turnover
            FROM DailyStockMetrics
            WHERE Ticker = ? AND TradeDate BETWEEN ? AND ?
            ORDER BY TradeDate
        """
        return self._db.execute_query(query, (ticker, start_date, end_date))

    def get_missing_dates(self, ticker: str, start_date: str, end_date: str) -> List[str]:
        """Find dates without data for a ticker."""
        query = """
            WITH DateRange AS (
                SELECT CAST(? AS DATE) AS dt
                UNION ALL
                SELECT DATEADD(DAY, 1, dt)
                FROM DateRange
                WHERE dt < ?
            )
            SELECT FORMAT(dr.dt, 'yyyy-MM-dd') AS MissingDate
            FROM DateRange dr
            LEFT JOIN DailyStockMetrics dsm
                ON dr.dt = dsm.TradeDate AND dsm.Ticker = ?
            WHERE dsm.ID IS NULL
                AND DATEPART(WEEKDAY, dr.dt) NOT IN (1, 7)  -- Exclude weekends
            OPTION (MAXRECURSION 400)
        """
        results = self._db.execute_query(query, (start_date, end_date, ticker))
        return [r['MissingDate'] for r in results]

    def upsert_metrics(self, df: pl.DataFrame) -> int:
        """
        Insert or update stock metrics from a Polars DataFrame.

        Expected columns: ticker, trade_date, close_price, open_price, high_price,
                         low_price, borrowing_balance, borrowing_balance_change,
                         margin_balance, short_balance, margin_ratio,
                         historical_volatility_20d, volume, turnover

        Returns:
            Number of rows affected
        """
        query = """
            MERGE INTO DailyStockMetrics AS target
            USING (SELECT ? AS Ticker, ? AS TradeDate, ? AS ClosePrice, ? AS OpenPrice,
                          ? AS HighPrice, ? AS LowPrice, ? AS BorrowingBalance,
                          ? AS BorrowingBalanceChange, ? AS MarginBalance, ? AS ShortBalance,
                          ? AS MarginRatio, ? AS HistoricalVolatility20D, ? AS Volume, ? AS Turnover) AS source
            ON target.Ticker = source.Ticker AND target.TradeDate = source.TradeDate
            WHEN MATCHED THEN
                UPDATE SET
                    ClosePrice = source.ClosePrice,
                    OpenPrice = source.OpenPrice,
                    HighPrice = source.HighPrice,
                    LowPrice = source.LowPrice,
                    BorrowingBalance = source.BorrowingBalance,
                    BorrowingBalanceChange = source.BorrowingBalanceChange,
                    MarginBalance = source.MarginBalance,
                    ShortBalance = source.ShortBalance,
                    MarginRatio = source.MarginRatio,
                    HistoricalVolatility20D = source.HistoricalVolatility20D,
                    Volume = source.Volume,
                    Turnover = source.Turnover,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                        BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                        MarginRatio, HistoricalVolatility20D, Volume, Turnover)
                VALUES (source.Ticker, source.TradeDate, source.ClosePrice, source.OpenPrice,
                        source.HighPrice, source.LowPrice, source.BorrowingBalance,
                        source.BorrowingBalanceChange, source.MarginBalance, source.ShortBalance,
                        source.MarginRatio, source.HistoricalVolatility20D, source.Volume, source.Turnover);
        """

        # Prepare data rows
        rows = []
        for row in df.iter_rows(named=True):
            rows.append((
                row.get('ticker'),
                row.get('trade_date'),
                row.get('close_price'),
                row.get('open_price'),
                row.get('high_price'),
                row.get('low_price'),
                row.get('borrowing_balance'),
                row.get('borrowing_balance_change'),
                row.get('margin_balance'),
                row.get('short_balance'),
                row.get('margin_ratio'),
                row.get('historical_volatility_20d'),
                row.get('volume'),
                row.get('turnover'),
            ))

        total_affected = 0
        with self._db.get_cursor() as cursor:
            for params in rows:
                cursor.execute(query, params)
                total_affected += 1

        logger.info(f"Upserted {total_affected} stock metrics records")
        return total_affected


class BackfillJobRepository:
    """
    Repository for BackfillJobs table operations.
    """

    def __init__(self, db: DatabaseConnection):
        self._db = db

    def create_job(
        self,
        job_type: str,
        start_date: str,
        end_date: str,
        total_tickers: int,
        created_by: str = None
    ) -> int:
        """Create a new backfill job and return its ID."""
        query = """
            INSERT INTO BackfillJobs (JobType, StartDate, EndDate, Status, TotalTickers, CreatedBy)
            OUTPUT INSERTED.ID
            VALUES (?, ?, ?, 'PENDING', ?, ?)
        """
        with self._db.get_cursor() as cursor:
            cursor.execute(query, (job_type, start_date, end_date, total_tickers, created_by))
            row = cursor.fetchone()
            return row[0] if row else 0

    def start_job(self, job_id: int) -> None:
        """Mark job as started."""
        query = """
            UPDATE BackfillJobs
            SET Status = 'RUNNING', StartedAt = GETDATE()
            WHERE ID = ?
        """
        self._db.execute_non_query(query, (job_id,))

    def update_progress(self, job_id: int, processed: int, failed: int = 0) -> None:
        """Update job progress."""
        query = """
            UPDATE BackfillJobs
            SET ProcessedTickers = ?, FailedTickers = ?
            WHERE ID = ?
        """
        self._db.execute_non_query(query, (processed, failed, job_id))

    def complete_job(self, job_id: int, error_message: str = None) -> None:
        """Mark job as completed or failed."""
        status = 'FAILED' if error_message else 'COMPLETED'
        query = """
            UPDATE BackfillJobs
            SET Status = ?, CompletedAt = GETDATE(), ErrorMessage = ?
            WHERE ID = ?
        """
        self._db.execute_non_query(query, (status, error_message, job_id))

    def get_job(self, job_id: int) -> Optional[Dict[str, Any]]:
        """Get job details by ID."""
        query = """
            SELECT ID, JobType, StartDate, EndDate, Status, TotalTickers,
                   ProcessedTickers, FailedTickers, ErrorMessage,
                   StartedAt, CompletedAt, CreatedAt, CreatedBy
            FROM BackfillJobs
            WHERE ID = ?
        """
        results = self._db.execute_query(query, (job_id,))
        return results[0] if results else None

    def get_recent_jobs(self, limit: int = 10) -> List[Dict[str, Any]]:
        """Get recent backfill jobs."""
        query = f"""
            SELECT TOP {limit}
                   ID, JobType, StartDate, EndDate, Status, TotalTickers,
                   ProcessedTickers, FailedTickers, ErrorMessage,
                   StartedAt, CompletedAt, CreatedAt
            FROM BackfillJobs
            ORDER BY CreatedAt DESC
        """
        return self._db.execute_query(query)


# Factory function for getting database instance
_db_instance: Optional[DatabaseConnection] = None


def get_database() -> DatabaseConnection:
    """Get singleton database connection instance."""
    global _db_instance
    if _db_instance is None:
        _db_instance = DatabaseConnection()
    return _db_instance
