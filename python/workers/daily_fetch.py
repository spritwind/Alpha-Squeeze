"""
Alpha Squeeze - Daily Data Fetch Worker

Scheduled job for fetching daily stock metrics from FinMind API.
Runs daily at 18:30 after market close.
"""

import asyncio
import logging
from datetime import datetime, timedelta
from typing import Optional

import polars as pl

# FinMind API wrapper
# pip install FinMind
try:
    from FinMind.data import DataLoader
except ImportError:
    DataLoader = None
    logging.warning("FinMind not installed. Run: pip install FinMind")

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class DailyDataFetcher:
    """
    Fetches daily stock data from FinMind API.

    Data includes:
    - Stock prices (OHLCV)
    - Borrowing balance (借券餘額)
    - Margin trading data (融資融券)
    """

    def __init__(self, finmind_token: Optional[str] = None):
        """
        Initialize fetcher with FinMind credentials.

        Args:
            finmind_token: FinMind API token (optional for limited access)
        """
        if DataLoader is None:
            raise RuntimeError("FinMind package not installed")

        self.loader = DataLoader()
        if finmind_token:
            self.loader.login_by_token(api_token=finmind_token)

    def fetch_stock_prices(
        self,
        ticker: str,
        start_date: str,
        end_date: str
    ) -> pl.DataFrame:
        """
        Fetch stock price data.

        Args:
            ticker: Stock ticker (e.g., "2330")
            start_date: Start date (YYYY-MM-DD)
            end_date: End date (YYYY-MM-DD)

        Returns:
            DataFrame with OHLCV data
        """
        logger.info(f"Fetching prices for {ticker}: {start_date} to {end_date}")

        df = self.loader.taiwan_stock_daily(
            stock_id=ticker,
            start_date=start_date,
            end_date=end_date
        )

        return pl.from_pandas(df).rename({
            "stock_id": "ticker",
            "date": "trade_date",
            "open": "open_price",
            "max": "high_price",
            "min": "low_price",
            "close": "close_price",
            "Trading_Volume": "volume",
            "Trading_money": "turnover"
        })

    def fetch_borrowing_data(
        self,
        ticker: str,
        start_date: str,
        end_date: str
    ) -> pl.DataFrame:
        """
        Fetch securities borrowing data (借券賣出餘額).

        Uses taiwan_daily_short_sale_balances API for SBL (Securities Borrowing and Lending) data.
        Returns borrowing balance and daily changes.
        """
        logger.info(f"Fetching borrowing data for {ticker}")

        df = self.loader.taiwan_daily_short_sale_balances(
            stock_id=ticker,
            start_date=start_date,
            end_date=end_date
        )

        if df.empty:
            logger.warning(f"No borrowing data for {ticker}")
            return pl.DataFrame(schema={
                "ticker": pl.Utf8,
                "trade_date": pl.Utf8,
                "borrowing_balance": pl.Float64,
                "borrowing_balance_change": pl.Float64
            })

        result = pl.from_pandas(df).rename({
            "stock_id": "ticker",
            "date": "trade_date",
            "SBLShortSalesCurrentDayBalance": "borrowing_balance"
        }).select([
            "ticker", "trade_date", "borrowing_balance"
        ]).with_columns(
            pl.col("borrowing_balance").cast(pl.Float64).diff().alias("borrowing_balance_change")
        )

        return result

    def fetch_margin_data(
        self,
        ticker: str,
        start_date: str,
        end_date: str
    ) -> pl.DataFrame:
        """
        Fetch margin trading data (融資融券).

        Returns margin balance, short balance, and margin ratio (券資比).
        券資比 = 融券餘額 / 融資餘額 * 100
        """
        logger.info(f"Fetching margin data for {ticker}")

        df = self.loader.taiwan_stock_margin_purchase_short_sale(
            stock_id=ticker,
            start_date=start_date,
            end_date=end_date
        )

        if df.empty:
            logger.warning(f"No margin data for {ticker}")
            return pl.DataFrame(schema={
                "ticker": pl.Utf8,
                "trade_date": pl.Utf8,
                "margin_balance": pl.Float64,
                "short_balance": pl.Float64,
                "margin_ratio": pl.Float64
            })

        result = pl.from_pandas(df).select([
            pl.col("stock_id").alias("ticker"),
            pl.col("date").alias("trade_date"),
            pl.col("MarginPurchaseTodayBalance").cast(pl.Float64).alias("margin_balance"),
            pl.col("ShortSaleTodayBalance").cast(pl.Float64).alias("short_balance")
        ]).with_columns(
            # Calculate margin ratio: 融券 / 融資 * 100 (券資比)
            pl.when(pl.col("margin_balance") > 0)
            .then(pl.col("short_balance") / pl.col("margin_balance") * 100)
            .otherwise(0.0)
            .alias("margin_ratio")
        )

        return result

    def fetch_complete_metrics(
        self,
        ticker: str,
        start_date: str,
        end_date: str
    ) -> pl.DataFrame:
        """
        Fetch and combine all metrics for a ticker.

        Returns complete DailyStockMetrics data.
        """
        try:
            # Fetch all data sources
            prices = self.fetch_stock_prices(ticker, start_date, end_date)
            borrowing = self.fetch_borrowing_data(ticker, start_date, end_date)
            margin = self.fetch_margin_data(ticker, start_date, end_date)

            # Join on ticker and date
            result = (
                prices
                .join(borrowing, on=["ticker", "trade_date"], how="left")
                .join(margin, on=["ticker", "trade_date"], how="left")
            )

            # Calculate HV (20-day rolling volatility)
            result = result.with_columns(
                pl.col("close_price")
                .log()
                .diff()
                .rolling_std(window_size=20)
                .mul(252 ** 0.5)
                .alias("historical_volatility_20d")
            )

            logger.info(f"Fetched {len(result)} records for {ticker}")
            return result

        except Exception as e:
            logger.error(f"Error fetching metrics for {ticker}: {e}")
            raise

    async def fetch_all_tickers(
        self,
        tickers: list[str],
        start_date: str,
        end_date: str,
        delay: float = 0.5
    ) -> pl.DataFrame:
        """
        Fetch metrics for multiple tickers.

        Args:
            tickers: List of stock tickers
            start_date: Start date
            end_date: End date
            delay: Delay between API calls (seconds)

        Returns:
            Combined DataFrame for all tickers
        """
        all_data = []
        failed = []

        for ticker in tickers:
            try:
                df = self.fetch_complete_metrics(ticker, start_date, end_date)
                all_data.append(df)
                await asyncio.sleep(delay)  # Rate limit
            except Exception as e:
                logger.warning(f"Failed to fetch {ticker}: {e}")
                failed.append(ticker)

        if failed:
            logger.warning(f"Failed tickers: {failed}")

        if not all_data:
            return pl.DataFrame()

        return pl.concat(all_data)


async def run_daily_fetch():
    """
    Main daily fetch job.

    Fetches data for all tracked tickers and saves to database.
    """
    logger.info("Starting daily data fetch job")

    # Configuration
    FINMIND_TOKEN = None  # Set from environment variable in production

    # Get date range (last 30 days for HV calculation)
    end_date = datetime.now().strftime("%Y-%m-%d")
    start_date = (datetime.now() - timedelta(days=30)).strftime("%Y-%m-%d")

    # Target tickers (would come from database in production)
    tickers = [
        "2330", "2454", "2317", "2308", "2412",  # 台積電, 聯發科, 鴻海, 台達電, 中華電
        "2881", "2882", "2883", "2884", "2885",  # 富邦金, 國泰金, 開發金, 玉山金, 元大金
        "2303", "2357", "3711", "2382", "2886",  # 聯電, 華碩, 日月光, 廣達, 兆豐金
    ]

    try:
        fetcher = DailyDataFetcher(finmind_token=FINMIND_TOKEN)
        data = await fetcher.fetch_all_tickers(tickers, start_date, end_date)

        logger.info(f"Fetched {len(data)} total records")

        # Save to database (implementation would use Dapper via gRPC or direct pyodbc)
        # save_to_database(data)

        logger.info("Daily fetch completed successfully")

    except Exception as e:
        logger.error(f"Daily fetch failed: {e}")
        raise


def main():
    """Entry point for the daily fetch worker."""
    asyncio.run(run_daily_fetch())


if __name__ == "__main__":
    main()
