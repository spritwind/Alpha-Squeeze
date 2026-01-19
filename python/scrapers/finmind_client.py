"""
Alpha Squeeze - FinMind API Client

Provides a unified interface for accessing Taiwan stock data via FinMind API.

Supported data types:
- taiwan_stock_daily: Stock price OHLCV data
- taiwan_stock_securities_lending: Securities borrowing (借券) data
- taiwan_stock_margin_purchase_short_sale: Margin trading data
- taiwan_stock_tick: Intraday tick data

Usage:
    client = FinMindClient(token="your_token")
    metrics = client.get_daily_metrics("2330", "2024-01-01", "2024-01-31")
"""

import logging
from datetime import datetime, timedelta
from typing import Optional

import polars as pl

try:
    from FinMind.data import DataLoader
except ImportError:
    DataLoader = None
    logging.warning("FinMind not installed. Run: pip install FinMind")

logger = logging.getLogger(__name__)


class FinMindClient:
    """
    FinMind API client for Taiwan stock data.

    Provides methods to fetch various types of stock market data
    and combines them into unified metrics format.
    """

    def __init__(self, token: Optional[str] = None):
        """
        Initialize client with optional API token.

        Args:
            token: FinMind API token (optional, enables higher rate limits)
        """
        if DataLoader is None:
            raise RuntimeError(
                "FinMind package not installed. Run: pip install FinMind"
            )

        self._loader = DataLoader()
        self._token = token

        if token:
            self._loader.login_by_token(api_token=token)
            logger.info("FinMind: Authenticated with token")
        else:
            logger.info("FinMind: Using anonymous access (limited rate)")

    def get_stock_prices(
        self,
        ticker: str,
        start_date: str,
        end_date: str,
    ) -> pl.DataFrame:
        """
        Fetch daily stock price data (OHLCV).

        Args:
            ticker: Stock ticker (e.g., "2330")
            start_date: Start date (YYYY-MM-DD)
            end_date: End date (YYYY-MM-DD)

        Returns:
            DataFrame with columns:
            - ticker, trade_date, open_price, high_price, low_price,
              close_price, volume, turnover
        """
        logger.debug(f"Fetching prices: {ticker} ({start_date} to {end_date})")

        df = self._loader.taiwan_stock_daily(
            stock_id=ticker,
            start_date=start_date,
            end_date=end_date,
        )

        if df.empty:
            logger.warning(f"No price data found for {ticker}")
            return pl.DataFrame()

        return pl.from_pandas(df).rename({
            "stock_id": "ticker",
            "date": "trade_date",
            "open": "open_price",
            "max": "high_price",
            "min": "low_price",
            "close": "close_price",
            "Trading_Volume": "volume",
            "Trading_money": "turnover",
        }).select([
            "ticker",
            pl.col("trade_date").str.to_date("%Y-%m-%d"),
            "open_price",
            "high_price",
            "low_price",
            "close_price",
            "volume",
            "turnover",
        ])

    def get_borrowing_data(
        self,
        ticker: str,
        start_date: str,
        end_date: str,
    ) -> pl.DataFrame:
        """
        Fetch securities borrowing (借券) data.

        Args:
            ticker: Stock ticker
            start_date: Start date (YYYY-MM-DD)
            end_date: End date (YYYY-MM-DD)

        Returns:
            DataFrame with columns:
            - ticker, trade_date, borrowing_balance, borrowing_balance_change
        """
        logger.debug(f"Fetching borrowing data: {ticker}")

        df = self._loader.taiwan_stock_securities_lending(
            stock_id=ticker,
            start_date=start_date,
            end_date=end_date,
        )

        if df.empty:
            logger.warning(f"No borrowing data found for {ticker}")
            return pl.DataFrame()

        return (
            pl.from_pandas(df)
            .rename({
                "stock_id": "ticker",
                "date": "trade_date",
                "securities_lending_balance": "borrowing_balance",
            })
            .select([
                "ticker",
                pl.col("trade_date").str.to_date("%Y-%m-%d"),
                "borrowing_balance",
            ])
            .sort("trade_date")
            .with_columns(
                pl.col("borrowing_balance")
                .diff()
                .fill_null(0)
                .alias("borrowing_balance_change")
            )
        )

    def get_margin_data(
        self,
        ticker: str,
        start_date: str,
        end_date: str,
    ) -> pl.DataFrame:
        """
        Fetch margin trading (融資融券) data.

        Args:
            ticker: Stock ticker
            start_date: Start date (YYYY-MM-DD)
            end_date: End date (YYYY-MM-DD)

        Returns:
            DataFrame with columns:
            - ticker, trade_date, margin_balance, short_balance, margin_ratio
        """
        logger.debug(f"Fetching margin data: {ticker}")

        df = self._loader.taiwan_stock_margin_purchase_short_sale(
            stock_id=ticker,
            start_date=start_date,
            end_date=end_date,
        )

        if df.empty:
            logger.warning(f"No margin data found for {ticker}")
            return pl.DataFrame()

        return (
            pl.from_pandas(df)
            .rename({
                "stock_id": "ticker",
                "date": "trade_date",
                "MarginPurchaseTodayBalance": "margin_balance",
                "ShortSaleTodayBalance": "short_balance",
            })
            .select([
                "ticker",
                pl.col("trade_date").str.to_date("%Y-%m-%d"),
                "margin_balance",
                "short_balance",
            ])
            .with_columns(
                # Margin ratio = Short / Margin * 100
                pl.when(pl.col("margin_balance") > 0)
                .then(pl.col("short_balance") / pl.col("margin_balance") * 100)
                .otherwise(0.0)
                .alias("margin_ratio")
            )
        )

    def get_daily_metrics(
        self,
        ticker: str,
        start_date: str,
        end_date: str,
        include_hv: bool = True,
    ) -> pl.DataFrame:
        """
        Fetch complete daily metrics combining all data sources.

        Args:
            ticker: Stock ticker
            start_date: Start date (YYYY-MM-DD)
            end_date: End date (YYYY-MM-DD)
            include_hv: Calculate 20-day historical volatility

        Returns:
            DataFrame with all metrics joined
        """
        logger.info(f"Fetching complete metrics: {ticker} ({start_date} to {end_date})")

        # Fetch all data sources
        prices = self.get_stock_prices(ticker, start_date, end_date)
        if prices.is_empty():
            return pl.DataFrame()

        borrowing = self.get_borrowing_data(ticker, start_date, end_date)
        margin = self.get_margin_data(ticker, start_date, end_date)

        # Join data
        result = prices

        if not borrowing.is_empty():
            result = result.join(
                borrowing.select([
                    "ticker",
                    "trade_date",
                    "borrowing_balance",
                    "borrowing_balance_change",
                ]),
                on=["ticker", "trade_date"],
                how="left",
            )

        if not margin.is_empty():
            result = result.join(
                margin.select([
                    "ticker",
                    "trade_date",
                    "margin_balance",
                    "short_balance",
                    "margin_ratio",
                ]),
                on=["ticker", "trade_date"],
                how="left",
            )

        # Calculate HV if requested
        if include_hv:
            result = result.sort("trade_date").with_columns(
                pl.col("close_price")
                .log()
                .diff()
                .rolling_std(window_size=20)
                .mul(252**0.5)  # Annualize
                .alias("historical_volatility_20d")
            )

        # Calculate 20-day high for momentum analysis
        result = result.with_columns(
            pl.col("high_price")
            .rolling_max(window_size=20)
            .alias("high_20d")
        )

        # Calculate 20-day average volume
        result = result.with_columns(
            pl.col("volume")
            .rolling_mean(window_size=20)
            .alias("avg_volume_20d")
        )

        logger.info(f"Fetched {len(result)} records for {ticker}")
        return result

    def get_batch_metrics(
        self,
        tickers: list[str],
        start_date: str,
        end_date: str,
        delay: float = 0.5,
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
        import asyncio

        all_data: list[pl.DataFrame] = []
        failed: list[str] = []

        for ticker in tickers:
            try:
                df = self.get_daily_metrics(ticker, start_date, end_date)
                if not df.is_empty():
                    all_data.append(df)

                # Simple synchronous delay for rate limiting
                import time
                time.sleep(delay)

            except Exception as e:
                logger.warning(f"Failed to fetch {ticker}: {e}")
                failed.append(ticker)

        if failed:
            logger.warning(f"Failed tickers: {failed}")

        if not all_data:
            return pl.DataFrame()

        return pl.concat(all_data)

    def get_stock_list(
        self,
        market: str = "twse",
    ) -> pl.DataFrame:
        """
        Get list of stocks in market.

        Args:
            market: "twse" (listed) or "tpex" (OTC)

        Returns:
            DataFrame with ticker and company info
        """
        if market == "twse":
            df = self._loader.taiwan_stock_info()
        else:
            df = self._loader.taiwan_stock_info()

        return pl.from_pandas(df)
