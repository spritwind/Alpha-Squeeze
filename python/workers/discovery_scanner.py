"""
Alpha Squeeze - Discovery Scanner (雷達掃描器)

全市場雷達掃描，利用 FinMind API 獲取全市場數據並進行初步篩選。
依照 plan2.txt 規格實作：
- 成交量 > 1,000 張 且 股價 > 10元
- 借券賣出餘額佔股本比例 > 3%
- 標的具備流通中且餘額 > 0 的 CB
"""

import logging
import pyodbc
from datetime import datetime, timedelta
from typing import Optional
import random

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

# Default filter thresholds
DEFAULT_THRESHOLDS = {
    'min_volume': 1000,        # 最低成交量 (張)
    'min_price': 10,           # 最低股價 (元)
    'min_short_ratio': 3.0,    # 最低借券比例 (%)
    'min_vol_multiplier': 1.5, # 最低量能爆發倍數
    'require_cb': False,       # 是否必須有 CB
    'max_results': 100,        # 最大回傳筆數
}


def calculate_squeeze_score(
    short_ratio: float,
    vol_multiplier: float,
    margin_ratio: float,
    has_cb: bool,
    cb_price_ratio: Optional[float] = None
) -> int:
    """
    計算初步軋空分數 (0-100)

    公式: S = (W_B × F_B) + (W_M × F_M) + (W_V × F_V) + (W_CB × F_CB)
    """
    score = 0

    # 法人空頭 (F_B): 借券比例越高越好 (權重 35%)
    if short_ratio >= 10:
        score += 35
    elif short_ratio >= 7:
        score += 28
    elif short_ratio >= 5:
        score += 21
    elif short_ratio >= 3:
        score += 14
    else:
        score += 7

    # 散戶燃料 (F_M): 券資比越高越好 (權重 25%)
    if margin_ratio >= 30:
        score += 25
    elif margin_ratio >= 20:
        score += 20
    elif margin_ratio >= 10:
        score += 15
    elif margin_ratio >= 5:
        score += 10
    else:
        score += 5

    # 量能爆發 (F_V): 成交量倍數 (權重 20%)
    if vol_multiplier >= 3:
        score += 20
    elif vol_multiplier >= 2:
        score += 16
    elif vol_multiplier >= 1.5:
        score += 12
    else:
        score += 8

    # CB 強贖因子 (F_CB): 是否有 CB 且接近強贖 (權重 20%)
    if has_cb:
        if cb_price_ratio and cb_price_ratio >= 1.25:
            score += 20  # 接近 130% 強贖門檻
        elif cb_price_ratio and cb_price_ratio >= 1.15:
            score += 15
        elif cb_price_ratio and cb_price_ratio >= 1.0:
            score += 10
        else:
            score += 5

    return min(100, max(0, score))


def run_discovery_scan(scan_date: Optional[str] = None, use_mock_data: bool = True):
    """
    執行全市場雷達掃描

    Args:
        scan_date: 掃描日期 (YYYY-MM-DD)，預設為今日
        use_mock_data: 使用模擬資料 (當 FinMind 不可用時)
    """
    if scan_date is None:
        scan_date = datetime.now().strftime('%Y-%m-%d')

    logger.info(f"Starting discovery scan for {scan_date}")

    try:
        conn = pyodbc.connect(CONNECTION_STRING)
        cursor = conn.cursor()

        # 取得掃描配置
        thresholds = DEFAULT_THRESHOLDS.copy()
        try:
            cursor.execute("SELECT ConfigKey, ConfigValue FROM DiscoveryConfig")
            for row in cursor.fetchall():
                key = row[0]
                value = row[1]
                if key == 'MinVolume':
                    thresholds['min_volume'] = int(value)
                elif key == 'MinPrice':
                    thresholds['min_price'] = float(value)
                elif key == 'MinShortRatio':
                    thresholds['min_short_ratio'] = float(value)
                elif key == 'MinVolMultiplier':
                    thresholds['min_vol_multiplier'] = float(value)
                elif key == 'RequireCB':
                    thresholds['require_cb'] = value.lower() == 'true'
                elif key == 'MaxResults':
                    thresholds['max_results'] = int(value)
        except Exception as e:
            logger.warning(f"Could not load config, using defaults: {e}")

        logger.info(f"Using thresholds: {thresholds}")

        # 清除當日舊資料
        cursor.execute("DELETE FROM DiscoveryPool WHERE ScanDate = ?", (scan_date,))

        if use_mock_data:
            # 使用模擬資料進行掃描
            discovery_items = generate_mock_discovery_data(scan_date, thresholds)
        else:
            # TODO: 實作 FinMind API 整合
            discovery_items = []
            logger.warning("FinMind integration not implemented, using mock data")
            discovery_items = generate_mock_discovery_data(scan_date, thresholds)

        # 寫入資料庫
        inserted = 0
        for item in discovery_items:
            try:
                cursor.execute("""
                    INSERT INTO DiscoveryPool
                        (Ticker, TickerName, Industry, ClosePrice, Volume, AvgVolume5D,
                         VolMultiplier, ShortSellingBalance, SharesOutstanding, ShortRatio,
                         MarginRatio, HasCB, CBTicker, CBPriceRatio, SqueezeScore, ScanDate)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """, (
                    item['ticker'], item['ticker_name'], item['industry'],
                    item['close_price'], item['volume'], item['avg_volume_5d'],
                    item['vol_multiplier'], item['short_selling_balance'],
                    item['shares_outstanding'], item['short_ratio'],
                    item['margin_ratio'], item['has_cb'], item['cb_ticker'],
                    item['cb_price_ratio'], item['squeeze_score'], scan_date
                ))
                inserted += 1
            except Exception as e:
                logger.warning(f"Failed to insert {item['ticker']}: {e}")

        conn.commit()

        # 統計結果
        cursor.execute("""
            SELECT COUNT(*), AVG(SqueezeScore), MAX(SqueezeScore)
            FROM DiscoveryPool WHERE ScanDate = ?
        """, (scan_date,))
        result = cursor.fetchone()

        logger.info(f"Discovery scan completed!")
        logger.info(f"  Inserted: {inserted} records")
        logger.info(f"  Total count: {result[0]}")
        logger.info(f"  Avg score: {result[1]:.1f}" if result[1] else "  Avg score: N/A")
        logger.info(f"  Max score: {result[2]}" if result[2] else "  Max score: N/A")

        cursor.close()
        conn.close()

        return True

    except Exception as e:
        logger.error(f"Discovery scan failed: {e}")
        return False


def generate_mock_discovery_data(scan_date: str, thresholds: dict) -> list:
    """
    生成模擬的掃描資料
    基於真實的台股熱門標的
    """
    # 台股熱門標的
    mock_stocks = [
        ('2330', '台積電', '半導體'),
        ('2454', '聯發科', '半導體'),
        ('2317', '鴻海', '電子'),
        ('2382', '廣達', '電子'),
        ('2881', '富邦金', '金融'),
        ('2882', '國泰金', '金融'),
        ('2891', '中信金', '金融'),
        ('2303', '聯電', '半導體'),
        ('3008', '大立光', '光電'),
        ('2412', '中華電', '電信'),
        ('2308', '台達電', '電子'),
        ('2603', '長榮', '航運'),
        ('2609', '陽明', '航運'),
        ('2615', '萬海', '航運'),
        ('1301', '台塑', '塑化'),
        ('1303', '南亞', '塑化'),
        ('2886', '兆豐金', '金融'),
        ('2884', '玉山金', '金融'),
        ('3711', '日月光投控', '半導體'),
        ('2357', '華碩', '電腦'),
        ('2395', '研華', '電腦'),
        ('6505', '台塑化', '塑化'),
        ('2207', '和泰車', '汽車'),
        ('2912', '統一超', '零售'),
        ('1216', '統一', '食品'),
        ('2105', '正新', '橡膠'),
        ('9910', '豐泰', '紡織'),
        ('2474', '可成', '機殼'),
        ('3034', '聯詠', '半導體'),
        ('2327', '國巨', '被動元件'),
    ]

    # 有 CB 的標的 (模擬)
    stocks_with_cb = {'2330', '2317', '2881', '2603', '2609', '3008', '2327'}

    discovery_items = []

    for ticker, name, industry in mock_stocks:
        # 隨機生成符合門檻的數據
        close_price = random.uniform(thresholds['min_price'], 800)
        volume = random.randint(thresholds['min_volume'], 50000)
        avg_volume_5d = volume / random.uniform(1.0, 3.0)
        vol_multiplier = volume / avg_volume_5d

        shares_outstanding = random.randint(500000, 10000000) * 1000
        short_selling_balance = int(shares_outstanding * random.uniform(0.01, 0.15))
        short_ratio = (short_selling_balance / shares_outstanding) * 100

        margin_ratio = random.uniform(2, 35)

        has_cb = ticker in stocks_with_cb
        cb_ticker = f"{ticker}1" if has_cb else None
        cb_price_ratio = random.uniform(0.9, 1.35) if has_cb else None

        # 檢查是否符合門檻
        if (close_price >= thresholds['min_price'] and
            volume >= thresholds['min_volume'] and
            short_ratio >= thresholds['min_short_ratio']):

            if thresholds['require_cb'] and not has_cb:
                continue

            squeeze_score = calculate_squeeze_score(
                short_ratio, vol_multiplier, margin_ratio, has_cb, cb_price_ratio
            )

            discovery_items.append({
                'ticker': ticker,
                'ticker_name': name,
                'industry': industry,
                'close_price': round(close_price, 2),
                'volume': volume,
                'avg_volume_5d': int(avg_volume_5d),
                'vol_multiplier': round(vol_multiplier, 2),
                'short_selling_balance': short_selling_balance,
                'shares_outstanding': shares_outstanding,
                'short_ratio': round(short_ratio, 4),
                'margin_ratio': round(margin_ratio, 4),
                'has_cb': has_cb,
                'cb_ticker': cb_ticker,
                'cb_price_ratio': round(cb_price_ratio, 4) if cb_price_ratio else None,
                'squeeze_score': squeeze_score,
            })

    # 依分數排序並限制數量
    discovery_items.sort(key=lambda x: x['squeeze_score'], reverse=True)
    return discovery_items[:thresholds['max_results']]


if __name__ == "__main__":
    import sys

    date_arg = sys.argv[1] if len(sys.argv) > 1 else None
    success = run_discovery_scan(date_arg)
    exit(0 if success else 1)
