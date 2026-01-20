"""
CB Seed Data - 產生測試用 CB 追蹤資料

在無法連接櫃買中心時，用於填充測試資料
"""

import logging
from datetime import datetime, timedelta
from typing import Optional

from engine.config import get_settings
from engine.database import get_database

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(name)s: %(message)s'
)
logger = logging.getLogger(__name__)


def seed_cb_tracking_data(trade_date: Optional[str] = None):
    """
    為現有的 CB 發行資料產生追蹤資料

    Args:
        trade_date: 交易日期 (預設為今天)
    """
    db = get_database()

    if trade_date is None:
        trade_date = datetime.now().strftime('%Y-%m-%d')

    logger.info(f"Seeding CB tracking data for {trade_date}")

    # 取得所有有效的 CB 發行資料
    issuances_sql = """
        SELECT CBTicker, UnderlyingTicker, CurrentConversionPrice, OutstandingAmount,
               RedemptionTriggerPct, RedemptionTriggerDays
        FROM CBIssuance
        WHERE IsActive = 1
    """
    issuances = db.execute_query(issuances_sql)

    if not issuances:
        logger.warning("No active CB issuances found")
        return

    logger.info(f"Found {len(issuances)} active CB issuances")

    # 取得標的股票價格 (模擬資料)
    # 實際應該從 DailyStockMetrics 取得
    for issuance in issuances:
        cb_ticker = issuance['CBTicker']
        underlying_ticker = issuance['UnderlyingTicker']
        conversion_price = float(issuance['CurrentConversionPrice'] or 0)
        outstanding_amount = float(issuance['OutstandingAmount'] or 0)
        trigger_pct = float(issuance['RedemptionTriggerPct'] or 130)
        trigger_days = int(issuance['RedemptionTriggerDays'] or 30)

        if conversion_price <= 0:
            logger.warning(f"Invalid conversion price for {cb_ticker}, skipping")
            continue

        # 取得標的股票收盤價
        stock_sql = f"""
            SELECT TOP 1 ClosePrice
            FROM DailyStockMetrics
            WHERE Ticker = '{underlying_ticker}'
            ORDER BY TradeDate DESC
        """
        stock_result = db.execute_query(stock_sql)

        if stock_result and stock_result[0]['ClosePrice']:
            underlying_close = float(stock_result[0]['ClosePrice'])
        else:
            # 模擬一個接近轉換價格的股價 (用於測試不同警示等級)
            import random
            # 隨機生成一個介於 100% ~ 145% 轉換價的股價
            ratio = random.uniform(1.0, 1.45)
            underlying_close = conversion_price * ratio

        # 計算股價/轉換價比率
        price_ratio = (underlying_close / conversion_price) * 100

        # 判斷是否超過觸發價
        trigger_price = conversion_price * (trigger_pct / 100)
        is_above_trigger = underlying_close >= trigger_price

        # 計算連續天數 (檢查前一天的資料)
        prev_day_sql = f"""
            SELECT ConsecutiveDaysAbove
            FROM CBDailyTracking
            WHERE CBTicker = '{cb_ticker}'
              AND TradeDate < '{trade_date}'
            ORDER BY TradeDate DESC
        """
        prev_result = db.execute_query(prev_day_sql)

        if prev_result and is_above_trigger:
            consecutive_days = int(prev_result[0]['ConsecutiveDaysAbove'] or 0) + 1
        elif is_above_trigger:
            consecutive_days = 1
        else:
            consecutive_days = 0

        # 計算警示等級
        if consecutive_days >= trigger_days:
            warning_level = 'CRITICAL'
        elif consecutive_days >= trigger_days * 0.7:
            warning_level = 'WARNING'
        elif consecutive_days >= trigger_days * 0.3:
            warning_level = 'CAUTION'
        else:
            warning_level = 'SAFE'

        # 計算餘額變動 (模擬)
        balance_change = 0.0

        # Upsert CBDailyTracking
        upsert_sql = f"""
            MERGE INTO CBDailyTracking AS target
            USING (SELECT '{cb_ticker}' AS CBTicker, '{trade_date}' AS TradeDate) AS source
            ON target.CBTicker = source.CBTicker AND target.TradeDate = source.TradeDate
            WHEN MATCHED THEN
                UPDATE SET
                    UnderlyingClosePrice = {underlying_close:.2f},
                    ConversionPrice = {conversion_price:.2f},
                    PriceToConversionRatio = {price_ratio:.2f},
                    IsAboveTrigger = {1 if is_above_trigger else 0},
                    ConsecutiveDaysAbove = {consecutive_days},
                    OutstandingBalance = {outstanding_amount:.4f},
                    BalanceChangePercent = {balance_change:.2f},
                    WarningLevel = '{warning_level}'
            WHEN NOT MATCHED THEN
                INSERT (CBTicker, TradeDate, UnderlyingClosePrice, ConversionPrice,
                        PriceToConversionRatio, IsAboveTrigger, ConsecutiveDaysAbove,
                        OutstandingBalance, BalanceChangePercent, WarningLevel, CreatedAt)
                VALUES ('{cb_ticker}', '{trade_date}', {underlying_close:.2f}, {conversion_price:.2f},
                        {price_ratio:.2f}, {1 if is_above_trigger else 0}, {consecutive_days},
                        {outstanding_amount:.4f}, {balance_change:.2f}, '{warning_level}', GETDATE());
        """

        try:
            db.execute_non_query(upsert_sql)
            logger.info(f"  {cb_ticker}: 股價 {underlying_close:.2f}, 轉換價 {conversion_price:.2f}, "
                       f"比率 {price_ratio:.1f}%, 連續 {consecutive_days} 天, 等級 {warning_level}")
        except Exception as e:
            logger.error(f"  Failed to upsert {cb_ticker}: {e}")

    logger.info(f"\nCB tracking data seeded for {trade_date}")


def seed_historical_cb_data(days: int = 30):
    """
    產生歷史 CB 追蹤資料 (用於測試連續天數計算)

    Args:
        days: 要回填的天數
    """
    logger.info(f"Seeding {days} days of historical CB tracking data")

    today = datetime.now()

    for i in range(days, -1, -1):
        date = (today - timedelta(days=i)).strftime('%Y-%m-%d')
        seed_cb_tracking_data(date)

    logger.info(f"\nHistorical CB tracking data seeded for {days} days")


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description='Seed CB tracking data')
    parser.add_argument('--date', '-d', help='Trade date (YYYY-MM-DD)')
    parser.add_argument('--historical', '-H', type=int, default=0,
                       help='Seed historical data for N days')

    args = parser.parse_args()

    if args.historical > 0:
        seed_historical_cb_data(args.historical)
    else:
        seed_cb_tracking_data(args.date)
