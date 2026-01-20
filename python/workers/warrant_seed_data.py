"""
Alpha Squeeze - Warrant IV Seed Data Generator

Generates sample warrant IV data for testing when real scraper is unavailable.
This creates realistic warrant data based on tracked stocks.
"""

import logging
import random
from datetime import datetime, timedelta
import pyodbc

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    force=True
)
logger = logging.getLogger(__name__)

# Connection string
CONNECTION_STRING = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=localhost\\SQLEXPRESS;"
    "DATABASE=AlphaSqueeze;"
    "UID=leo;"
    "PWD=1qaz@WSX;"
    "TrustServerCertificate=yes;"
)

# Popular warrant issuers in Taiwan
ISSUERS = ["元大", "統一", "凱基", "富邦", "群益", "永豐", "國泰"]


def generate_warrant_ticker(underlying: str, issuer_idx: int, warrant_idx: int, warrant_type: str) -> str:
    """Generate a realistic warrant ticker"""
    type_code = "C" if warrant_type == "Call" else "P"
    return f"{underlying}{type_code}{issuer_idx:01d}{warrant_idx:02d}"


def generate_warrant_data(underlying: str, trade_date: datetime) -> list[dict]:
    """Generate warrant data for a single underlying stock"""
    warrants = []

    # Generate 3-8 warrants per underlying
    num_warrants = random.randint(3, 8)

    for i in range(num_warrants):
        issuer_idx = random.randint(0, len(ISSUERS) - 1)
        issuer = ISSUERS[issuer_idx]
        warrant_type = random.choice(["Call", "Put"])

        # Calculate expiry (30-180 days from trade date)
        days_to_expiry = random.randint(30, 180)
        expiry_date = trade_date + timedelta(days=days_to_expiry)

        # Base price around 100 for typical stocks
        base_price = random.uniform(50, 500)
        strike_distance = random.uniform(-0.1, 0.2)  # -10% to +20% from spot
        strike_price = base_price * (1 + strike_distance)

        # IV typically ranges from 20% to 80%
        implied_volatility = random.uniform(0.20, 0.80)

        # Effective leverage typically 3-15x
        effective_leverage = random.uniform(3.0, 15.0)

        # Spread ratio (bid-ask spread percentage)
        spread_ratio = random.uniform(0.005, 0.05)

        # Greeks
        moneyness = base_price / strike_price
        delta = 0.5 + (moneyness - 1) * 2  # Simplified approximation
        delta = max(0.05, min(0.95, delta))
        if warrant_type == "Put":
            delta = -delta

        gamma = random.uniform(0.01, 0.10)
        theta = -random.uniform(0.01, 0.05)  # Theta is negative
        vega = random.uniform(0.05, 0.20)

        warrant_ticker = generate_warrant_ticker(underlying, issuer_idx, i, warrant_type)
        warrant_name = f"{underlying}{issuer}{warrant_type[:1]}{str(expiry_date.month).zfill(2)}"

        warrants.append({
            "underlying_ticker": underlying,
            "warrant_ticker": warrant_ticker,
            "warrant_name": warrant_name,
            "issuer": issuer,
            "warrant_type": warrant_type,
            "implied_volatility": implied_volatility,
            "effective_leverage": effective_leverage,
            "spread_ratio": spread_ratio,
            "strike_price": round(strike_price, 2),
            "expiry_date": expiry_date.strftime("%Y-%m-%d"),
            "days_to_expiry": days_to_expiry,
            "delta": round(delta, 6),
            "gamma": round(gamma, 6),
            "theta": round(theta, 6),
            "vega": round(vega, 6),
            "trade_date": trade_date.strftime("%Y-%m-%d"),
        })

    return warrants


def seed_warrant_data():
    """Main function to seed warrant data"""
    logger.info("Starting warrant IV data seeding...")

    try:
        conn = pyodbc.connect(CONNECTION_STRING)
        cursor = conn.cursor()

        # Get tracked tickers
        cursor.execute("""
            SELECT DISTINCT Ticker FROM TrackedTickers WHERE IsActive = 1
            UNION
            SELECT DISTINCT Ticker FROM DailyStockMetrics
        """)
        tickers = [row[0] for row in cursor.fetchall()]

        if not tickers:
            # Default tickers if none found
            tickers = ["2330", "2454", "2317", "2881", "2891", "2882", "3008", "2603", "2609", "1301"]
            logger.info(f"No tracked tickers found, using defaults: {tickers}")
        else:
            logger.info(f"Found {len(tickers)} tickers to seed warrant data")

        # Generate data for the past 30 days
        today = datetime.now()
        total_inserted = 0

        for days_ago in range(30, -1, -1):
            trade_date = today - timedelta(days=days_ago)

            # Skip weekends
            if trade_date.weekday() >= 5:
                continue

            for ticker in tickers:
                warrants = generate_warrant_data(ticker, trade_date)

                for w in warrants:
                    try:
                        cursor.execute("""
                            MERGE INTO WarrantMarketData AS target
                            USING (SELECT ? AS WarrantTicker, ? AS TradeDate) AS source
                            ON target.WarrantTicker = source.WarrantTicker
                               AND target.TradeDate = source.TradeDate
                            WHEN NOT MATCHED THEN
                                INSERT (UnderlyingTicker, WarrantTicker, WarrantName, Issuer,
                                        WarrantType, ImpliedVolatility, EffectiveLeverage,
                                        SpreadRatio, StrikePrice, ExpiryDate, DaysToExpiry,
                                        Delta, Gamma, Theta, Vega, TradeDate, LastUpdate)
                                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, GETDATE());
                        """, (
                            w["warrant_ticker"], w["trade_date"],
                            w["underlying_ticker"], w["warrant_ticker"], w["warrant_name"],
                            w["issuer"], w["warrant_type"], w["implied_volatility"],
                            w["effective_leverage"], w["spread_ratio"], w["strike_price"],
                            w["expiry_date"], w["days_to_expiry"], w["delta"],
                            w["gamma"], w["theta"], w["vega"], w["trade_date"]
                        ))
                        total_inserted += cursor.rowcount
                    except Exception as e:
                        # Skip duplicates
                        pass

        conn.commit()

        # Verify results
        cursor.execute("""
            SELECT
                COUNT(*) AS TotalRecords,
                COUNT(DISTINCT UnderlyingTicker) AS UniqueUnderlyings,
                COUNT(DISTINCT WarrantTicker) AS UniqueWarrants,
                MIN(TradeDate) AS FirstDate,
                MAX(TradeDate) AS LastDate,
                AVG(ImpliedVolatility) AS AvgIV
            FROM WarrantMarketData
        """)
        result = cursor.fetchone()

        logger.info(f"Warrant data seeding completed!")
        logger.info(f"  Total records: {result[0]}")
        logger.info(f"  Unique underlyings: {result[1]}")
        logger.info(f"  Unique warrants: {result[2]}")
        logger.info(f"  Date range: {result[3]} to {result[4]}")
        logger.info(f"  Average IV: {result[5]:.2%}" if result[5] else "  Average IV: N/A")

        cursor.close()
        conn.close()

        return True

    except Exception as e:
        logger.error(f"Failed to seed warrant data: {e}")
        return False


if __name__ == "__main__":
    success = seed_warrant_data()
    exit(0 if success else 1)
