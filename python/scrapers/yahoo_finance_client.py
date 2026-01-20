"""
Alpha Squeeze - Yahoo Finance Client

Fetches real-time and historical stock prices from Yahoo Finance
for Taiwan Stock Exchange (TWSE/TPEx) securities.

Used for:
1. Price validation against other data sources
2. Real-time price updates
3. Historical data backfill
"""

import logging
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass

import yfinance as yf
import pandas as pd

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


@dataclass
class StockPrice:
    """Stock price data from Yahoo Finance."""
    ticker: str
    date: str
    open_price: float
    high_price: float
    low_price: float
    close_price: float
    volume: int
    source: str = "Yahoo Finance"


class YahooFinanceClient:
    """
    Client for fetching Taiwan stock data from Yahoo Finance.

    Taiwan stocks use .TW suffix (TWSE) or .TWO suffix (TPEx/OTC).
    """

    # Common Taiwan stock suffixes
    TWSE_SUFFIX = ".TW"    # 上市
    TPEX_SUFFIX = ".TWO"   # 上櫃

    def __init__(self):
        """Initialize Yahoo Finance client."""
        self._cache: Dict[str, StockPrice] = {}
        self._cache_time: Optional[datetime] = None
        self._cache_ttl = timedelta(minutes=5)

    def _get_yahoo_ticker(self, ticker: str) -> str:
        """
        Convert Taiwan stock ticker to Yahoo Finance format.

        Args:
            ticker: Taiwan stock ticker (e.g., "2330")

        Returns:
            Yahoo Finance ticker (e.g., "2330.TW")
        """
        # Remove any existing suffix
        clean_ticker = ticker.replace(self.TWSE_SUFFIX, "").replace(self.TPEX_SUFFIX, "")

        # Default to TWSE (.TW), most common
        return f"{clean_ticker}{self.TWSE_SUFFIX}"

    def get_current_price(self, ticker: str) -> Optional[StockPrice]:
        """
        Get current/latest stock price.

        Args:
            ticker: Taiwan stock ticker (e.g., "2330")

        Returns:
            StockPrice object or None if failed
        """
        yahoo_ticker = self._get_yahoo_ticker(ticker)

        try:
            stock = yf.Ticker(yahoo_ticker)
            hist = stock.history(period="1d")

            if hist.empty:
                # Try OTC suffix if TWSE fails
                yahoo_ticker = f"{ticker}{self.TPEX_SUFFIX}"
                stock = yf.Ticker(yahoo_ticker)
                hist = stock.history(period="1d")

                if hist.empty:
                    logger.warning(f"No data found for {ticker}")
                    return None

            latest = hist.iloc[-1]

            return StockPrice(
                ticker=ticker,
                date=hist.index[-1].strftime("%Y-%m-%d"),
                open_price=float(latest["Open"]),
                high_price=float(latest["High"]),
                low_price=float(latest["Low"]),
                close_price=float(latest["Close"]),
                volume=int(latest["Volume"])
            )

        except Exception as e:
            logger.error(f"Error fetching {ticker}: {e}")
            return None

    def get_batch_prices(self, tickers: List[str]) -> Dict[str, StockPrice]:
        """
        Get current prices for multiple tickers.

        Args:
            tickers: List of Taiwan stock tickers

        Returns:
            Dictionary mapping ticker to StockPrice
        """
        results = {}

        # Use yfinance batch download for efficiency
        yahoo_tickers = [self._get_yahoo_ticker(t) for t in tickers]

        try:
            # Download all at once
            data = yf.download(
                yahoo_tickers,
                period="1d",
                progress=False,
                threads=True
            )

            if data.empty:
                logger.warning("No data returned from batch download")
                return results

            # Handle single ticker case (different DataFrame structure)
            if len(tickers) == 1:
                if not data.empty:
                    ticker = tickers[0]
                    latest = data.iloc[-1]
                    results[ticker] = StockPrice(
                        ticker=ticker,
                        date=data.index[-1].strftime("%Y-%m-%d"),
                        open_price=float(latest["Open"]),
                        high_price=float(latest["High"]),
                        low_price=float(latest["Low"]),
                        close_price=float(latest["Close"]),
                        volume=int(latest["Volume"])
                    )
            else:
                # Multiple tickers - MultiIndex columns
                for i, ticker in enumerate(tickers):
                    yahoo_ticker = yahoo_tickers[i]
                    try:
                        if yahoo_ticker in data["Close"].columns:
                            close = data["Close"][yahoo_ticker].dropna()
                            if not close.empty:
                                latest_date = close.index[-1]
                                results[ticker] = StockPrice(
                                    ticker=ticker,
                                    date=latest_date.strftime("%Y-%m-%d"),
                                    open_price=float(data["Open"][yahoo_ticker].loc[latest_date]),
                                    high_price=float(data["High"][yahoo_ticker].loc[latest_date]),
                                    low_price=float(data["Low"][yahoo_ticker].loc[latest_date]),
                                    close_price=float(close.iloc[-1]),
                                    volume=int(data["Volume"][yahoo_ticker].loc[latest_date])
                                )
                    except Exception as e:
                        logger.warning(f"Error processing {ticker}: {e}")

        except Exception as e:
            logger.error(f"Batch download error: {e}")
            # Fall back to individual requests
            for ticker in tickers:
                price = self.get_current_price(ticker)
                if price:
                    results[ticker] = price

        return results

    def get_historical_prices(
        self,
        ticker: str,
        start_date: str,
        end_date: str
    ) -> List[StockPrice]:
        """
        Get historical prices for a ticker.

        Args:
            ticker: Taiwan stock ticker
            start_date: Start date (YYYY-MM-DD)
            end_date: End date (YYYY-MM-DD)

        Returns:
            List of StockPrice objects
        """
        yahoo_ticker = self._get_yahoo_ticker(ticker)
        results = []

        try:
            stock = yf.Ticker(yahoo_ticker)
            hist = stock.history(start=start_date, end=end_date)

            if hist.empty:
                # Try OTC
                yahoo_ticker = f"{ticker}{self.TPEX_SUFFIX}"
                stock = yf.Ticker(yahoo_ticker)
                hist = stock.history(start=start_date, end=end_date)

            for date, row in hist.iterrows():
                results.append(StockPrice(
                    ticker=ticker,
                    date=date.strftime("%Y-%m-%d"),
                    open_price=float(row["Open"]),
                    high_price=float(row["High"]),
                    low_price=float(row["Low"]),
                    close_price=float(row["Close"]),
                    volume=int(row["Volume"])
                ))

        except Exception as e:
            logger.error(f"Error fetching history for {ticker}: {e}")

        return results

    def validate_price(
        self,
        ticker: str,
        expected_price: float,
        tolerance: float = 0.05
    ) -> Tuple[bool, Optional[float], str]:
        """
        Validate a price against Yahoo Finance data.

        Args:
            ticker: Stock ticker
            expected_price: Price to validate
            tolerance: Acceptable difference ratio (default 5%)

        Returns:
            Tuple of (is_valid, actual_price, message)
        """
        actual = self.get_current_price(ticker)

        if actual is None:
            return False, None, f"無法從 Yahoo Finance 取得 {ticker} 價格"

        diff_ratio = abs(actual.close_price - expected_price) / actual.close_price

        if diff_ratio <= tolerance:
            return True, actual.close_price, f"價格驗證通過 (差異 {diff_ratio*100:.1f}%)"
        else:
            return False, actual.close_price, (
                f"價格差異過大: 預期 {expected_price:.2f}, "
                f"實際 {actual.close_price:.2f} (差異 {diff_ratio*100:.1f}%)"
            )

    def validate_batch_prices(
        self,
        prices: Dict[str, float],
        tolerance: float = 0.05
    ) -> Dict[str, dict]:
        """
        Validate multiple prices against Yahoo Finance.

        Args:
            prices: Dict mapping ticker to expected price
            tolerance: Acceptable difference ratio

        Returns:
            Dict with validation results for each ticker
        """
        tickers = list(prices.keys())
        actual_prices = self.get_batch_prices(tickers)

        results = {}
        for ticker, expected in prices.items():
            if ticker in actual_prices:
                actual = actual_prices[ticker].close_price
                diff_ratio = abs(actual - expected) / actual

                results[ticker] = {
                    "expected": expected,
                    "actual": actual,
                    "diff_ratio": diff_ratio,
                    "is_valid": diff_ratio <= tolerance,
                    "message": "通過" if diff_ratio <= tolerance else f"差異 {diff_ratio*100:.1f}%"
                }
            else:
                results[ticker] = {
                    "expected": expected,
                    "actual": None,
                    "diff_ratio": None,
                    "is_valid": False,
                    "message": "無法取得真實價格"
                }

        return results


def fetch_and_update_prices(tickers: List[str] = None) -> Dict[str, StockPrice]:
    """
    Convenience function to fetch current prices.

    Args:
        tickers: List of tickers, or None for default list

    Returns:
        Dictionary of ticker to StockPrice
    """
    if tickers is None:
        # Default watchlist
        tickers = ["2330", "2454", "2317", "2881", "2882", "2303", "2308"]

    client = YahooFinanceClient()
    return client.get_batch_prices(tickers)


if __name__ == "__main__":
    # Test the client
    client = YahooFinanceClient()

    test_tickers = ["2330", "2454", "2317", "2881"]

    print("=== Yahoo Finance 即時股價測試 ===\n")

    prices = client.get_batch_prices(test_tickers)

    for ticker, price in prices.items():
        print(f"{ticker}: {price.close_price:.2f} (日期: {price.date})")

    print("\n=== 價格驗證測試 ===\n")

    # Test with wrong prices (from database)
    db_prices = {
        "2330": 1050.00,
        "2454": 1580.00,
        "2317": 180.00,
        "2881": 85.50
    }

    validation = client.validate_batch_prices(db_prices)

    for ticker, result in validation.items():
        status = "[PASS]" if result["is_valid"] else "[FAIL]"
        actual_str = f"{result['actual']:.2f}" if result['actual'] else 'N/A'
        print(f"{status} {ticker}: Expected {result['expected']:.2f}, Actual {actual_str} - {result['message']}")
