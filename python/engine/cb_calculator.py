"""
CB 預警計算引擎 (CB Warning Calculator)

核心計算:
1. Days Above Trigger (DAT): 股價連續超過轉換價 * 觸發門檻的天數
2. Warning Level: 根據 DAT 進度判定預警等級
3. CB Score: 整合至 Squeeze Score 系統的額外因子

演算法說明:
- 130% 觸發線：股價 >= 轉換價 * 130%
- 30 日規則：連續 30 個營業日超過觸發線可觸發強贖
- 預警等級：
  - SAFE: DAT < 10 天 (0-33%)
  - CAUTION: DAT 10-19 天 (33-66%)
  - WARNING: DAT 20-29 天 (66-99%)
  - CRITICAL: DAT >= 30 天 (已觸發)
"""

import logging
from dataclasses import dataclass
from enum import Enum
from typing import Optional, List, Dict, Any

import polars as pl

logger = logging.getLogger(__name__)


class WarningLevel(Enum):
    """CB 預警等級"""
    SAFE = "SAFE"           # DAT < 10 天 (0-33%)
    CAUTION = "CAUTION"     # DAT 10-19 天 (33-66%)
    WARNING = "WARNING"     # DAT 20-29 天 (66-99%)
    CRITICAL = "CRITICAL"   # DAT >= 30 天 (已觸發)


@dataclass
class CBWarningConfig:
    """CB 預警配置"""
    trigger_threshold_pct: float = 130.0    # 觸發門檻 (%)
    trigger_days_required: int = 30         # 需要連續天數
    reset_on_below: bool = True             # 跌破門檻是否重置計數

    # 預警等級門檻比例
    caution_threshold_pct: float = 0.33     # ~10 天
    warning_threshold_pct: float = 0.66     # ~20 天

    @classmethod
    def from_dict(cls, config: Dict[str, Any]) -> 'CBWarningConfig':
        """從字典建立配置"""
        return cls(
            trigger_threshold_pct=float(config.get('CB_TRIGGER_THRESHOLD_PCT', 130.0)),
            trigger_days_required=int(config.get('CB_TRIGGER_DAYS_REQUIRED', 30)),
            reset_on_below=bool(config.get('CB_RESET_ON_BELOW', True)),
            caution_threshold_pct=float(config.get('CB_CAUTION_THRESHOLD_PCT', 0.33)),
            warning_threshold_pct=float(config.get('CB_WARNING_THRESHOLD_PCT', 0.66)),
        )


@dataclass
class CBWarningResult:
    """CB 預警計算結果"""
    cb_ticker: str                          # CB 代號
    underlying_ticker: str                  # 標的股票代號
    trade_date: str                         # 交易日期
    current_price: float                    # 標的收盤價
    conversion_price: float                 # 轉換價格
    price_ratio: float                      # 股價 / 轉換價 * 100
    is_above_trigger: bool                  # 是否超過觸發門檻
    consecutive_days: int                   # 連續超過天數
    days_remaining: int                     # 距離觸發剩餘天數
    trigger_progress: float                 # 觸發進度 (0-100%)
    outstanding_balance: float              # 剩餘餘額 (億)
    balance_change_pct: float               # 餘額變化率 (%)
    warning_level: WarningLevel             # 預警等級
    comment: str                            # 風險提示

    def to_dict(self) -> Dict[str, Any]:
        """轉換為字典"""
        return {
            'cb_ticker': self.cb_ticker,
            'underlying_ticker': self.underlying_ticker,
            'trade_date': self.trade_date,
            'current_price': self.current_price,
            'conversion_price': self.conversion_price,
            'price_ratio': self.price_ratio,
            'is_above_trigger': self.is_above_trigger,
            'consecutive_days': self.consecutive_days,
            'days_remaining': self.days_remaining,
            'trigger_progress': self.trigger_progress,
            'outstanding_balance': self.outstanding_balance,
            'balance_change_pct': self.balance_change_pct,
            'warning_level': self.warning_level.value,
            'comment': self.comment
        }


class CBWarningCalculator:
    """
    CB 預警計算器

    計算邏輯:
    1. 每日檢查標的股價是否超過 轉換價 * 130%
    2. 累計連續超過的天數 (DAT)
    3. 根據 DAT / 30 計算觸發進度
    4. 判定預警等級

    Configuration is loaded from database on initialization.
    Falls back to default values if database is unavailable.
    """

    _DEFAULT_CONFIG = CBWarningConfig()

    def __init__(self, config: Optional[CBWarningConfig] = None, use_database: bool = True):
        """
        Initialize calculator with configuration.

        Args:
            config: Pre-loaded configuration (optional)
            use_database: Whether to load config from database (default: True)
        """
        if config:
            self._config = config
        elif use_database:
            self._config = self._load_config_from_db()
        else:
            self._config = self._DEFAULT_CONFIG

        logger.info(f"CBWarningCalculator initialized with config: "
                   f"trigger={self._config.trigger_threshold_pct}%, "
                   f"days={self._config.trigger_days_required}")

    def _load_config_from_db(self) -> CBWarningConfig:
        """Load configuration from database."""
        try:
            from engine.database import get_database, ConfigRepository

            db = get_database()
            repo = ConfigRepository(db)

            # Get all config values
            all_configs = repo.get_all()
            config_dict = {c['ConfigKey']: c['ConfigValue'] for c in all_configs}

            logger.info(f"Loaded CB config from database")
            return CBWarningConfig.from_dict(config_dict)

        except Exception as e:
            logger.warning(f"Failed to load CB config from database: {e}")
            logger.info("Using default CB configuration")
            return self._DEFAULT_CONFIG

    @property
    def config(self) -> CBWarningConfig:
        """Get current configuration."""
        return self._config

    def reload_config(self) -> None:
        """Reload configuration from database."""
        self._config = self._load_config_from_db()
        logger.info("CB Configuration reloaded")

    def calculate_warning(
        self,
        cb_ticker: str,
        underlying_ticker: str,
        current_price: float,
        conversion_price: float,
        previous_consecutive_days: int,
        outstanding_balance: float,
        previous_balance: Optional[float],
        trade_date: str
    ) -> CBWarningResult:
        """
        計算單一 CB 的預警狀態

        Args:
            cb_ticker: CB 代號
            underlying_ticker: 標的股票代號
            current_price: 標的當日收盤價
            conversion_price: 轉換價格
            previous_consecutive_days: 前一日累計連續天數
            outstanding_balance: 剩餘餘額 (億)
            previous_balance: 前一日餘額 (億)
            trade_date: 交易日期

        Returns:
            CBWarningResult
        """
        # 計算價格比率
        if conversion_price <= 0:
            price_ratio = 0.0
        else:
            price_ratio = (current_price / conversion_price) * 100

        # 判斷是否超過觸發門檻
        trigger_price = conversion_price * (self._config.trigger_threshold_pct / 100)
        is_above_trigger = current_price >= trigger_price

        # 計算連續天數
        if is_above_trigger:
            consecutive_days = previous_consecutive_days + 1
        else:
            consecutive_days = 0 if self._config.reset_on_below else previous_consecutive_days

        # 計算觸發進度與剩餘天數
        trigger_progress = min(100.0, (consecutive_days / self._config.trigger_days_required) * 100)
        days_remaining = max(0, self._config.trigger_days_required - consecutive_days)

        # 計算餘額變化率
        if previous_balance and previous_balance > 0:
            balance_change_pct = ((outstanding_balance - previous_balance) / previous_balance) * 100
        else:
            balance_change_pct = 0.0

        # 判定預警等級
        warning_level = self._determine_warning_level(consecutive_days)

        # 生成預警說明
        comment = self._generate_comment(
            consecutive_days=consecutive_days,
            days_remaining=days_remaining,
            price_ratio=price_ratio,
            outstanding_balance=outstanding_balance,
            warning_level=warning_level
        )

        return CBWarningResult(
            cb_ticker=cb_ticker,
            underlying_ticker=underlying_ticker,
            trade_date=trade_date,
            current_price=current_price,
            conversion_price=conversion_price,
            price_ratio=round(price_ratio, 2),
            is_above_trigger=is_above_trigger,
            consecutive_days=consecutive_days,
            days_remaining=days_remaining,
            trigger_progress=round(trigger_progress, 2),
            outstanding_balance=outstanding_balance,
            balance_change_pct=round(balance_change_pct, 2),
            warning_level=warning_level,
            comment=comment
        )

    def _determine_warning_level(self, consecutive_days: int) -> WarningLevel:
        """根據連續天數判定預警等級"""
        threshold_caution = self._config.trigger_days_required * self._config.caution_threshold_pct
        threshold_warning = self._config.trigger_days_required * self._config.warning_threshold_pct

        if consecutive_days >= self._config.trigger_days_required:
            return WarningLevel.CRITICAL
        elif consecutive_days >= threshold_warning:
            return WarningLevel.WARNING
        elif consecutive_days >= threshold_caution:
            return WarningLevel.CAUTION
        else:
            return WarningLevel.SAFE

    def _generate_comment(
        self,
        consecutive_days: int,
        days_remaining: int,
        price_ratio: float,
        outstanding_balance: float,
        warning_level: WarningLevel
    ) -> str:
        """生成預警說明文字"""
        if warning_level == WarningLevel.CRITICAL:
            return (f"已達強贖門檻！連續 {consecutive_days} 日超過轉換價 130%，"
                   f"剩餘 {outstanding_balance:.2f} 億可能面臨轉換壓力")
        elif warning_level == WarningLevel.WARNING:
            return (f"高度警戒：已連續 {consecutive_days} 日，"
                   f"僅剩 {days_remaining} 日即觸發強贖，餘額 {outstanding_balance:.2f} 億")
        elif warning_level == WarningLevel.CAUTION:
            return f"注意追蹤：連續 {consecutive_days} 日超標，股價/轉換價 = {price_ratio:.1f}%"
        else:
            return f"安全範圍：股價/轉換價 = {price_ratio:.1f}%，無近期強贖風險"

    def calculate_cb_score(
        self,
        premium_rate: float,
        remaining_ratio: float,
        days_above_trigger: int,
        redemption_called: bool = False
    ) -> float:
        """
        CB 軋空因子計算 (用於整合至 Squeeze Score)

        評分邏輯:
        1. 溢價率 < 0 (折價) = 轉換誘因極大 = 高分
        2. 剩餘餘額佔比 > 50% + 達標天數 > 15 = 極高軋空潛力
        3. 已公告強贖 = 滿分

        Args:
            premium_rate: 轉換溢價率 (%)
            remaining_ratio: 剩餘餘額佔總發行比例 (0-1)
            days_above_trigger: 連續達標天數
            redemption_called: 是否已公告強贖

        Returns:
            CB Score (0-100)
        """
        if redemption_called:
            return 100  # 已觸發強贖，最高分

        score = 0

        # 溢價率評分 (0-40分)
        if premium_rate < 0:
            # 折價情況，轉換誘因高
            score += min(40, abs(premium_rate) * 2)
        elif premium_rate < 10:
            score += 30 - premium_rate * 2
        else:
            score += max(0, 20 - premium_rate)

        # 剩餘餘額評分 (0-30分)
        if remaining_ratio > 0.7:
            score += 30
        elif remaining_ratio > 0.5:
            score += 25
        elif remaining_ratio > 0.3:
            score += 15
        else:
            score += 5

        # 達標天數評分 (0-30分)
        if days_above_trigger >= 25:
            score += 30  # 即將觸發
        elif days_above_trigger >= 15:
            score += 25  # 高度警戒
        elif days_above_trigger >= 10:
            score += 15  # 注意追蹤
        elif days_above_trigger >= 5:
            score += 8
        else:
            score += 0

        return min(100, score)


def batch_calculate_cb_warnings(
    cb_issuance_df: pl.DataFrame,
    stock_prices_df: pl.DataFrame,
    previous_tracking_df: Optional[pl.DataFrame],
    trade_date: str,
    config: Optional[CBWarningConfig] = None
) -> pl.DataFrame:
    """
    批量計算所有 CB 的預警狀態

    使用 Polars 進行高效能批量計算

    Args:
        cb_issuance_df: CB 發行資訊 DataFrame
            Required columns: cb_ticker, underlying_ticker, current_conversion_price, outstanding_amount
        stock_prices_df: 股價資料 DataFrame
            Required columns: ticker, trade_date, close_price
        previous_tracking_df: 前一日追蹤資料 DataFrame (可選)
            Required columns: cb_ticker, consecutive_days_above, outstanding_balance
        trade_date: 計算日期
        config: 計算配置

    Returns:
        Polars DataFrame 包含所有 CB 預警結果
    """
    calculator = CBWarningCalculator(config, use_database=False if config else True)

    # 過濾指定日期的股價 (使用字串比較)
    prices_for_date = stock_prices_df.filter(pl.col('trade_date').cast(pl.Utf8) == trade_date)

    # Join CB 資訊與股價
    combined = cb_issuance_df.join(
        prices_for_date.select(['ticker', 'close_price']),
        left_on='underlying_ticker',
        right_on='ticker',
        how='left'
    )

    # Join 前一日追蹤資料
    if previous_tracking_df is not None and len(previous_tracking_df) > 0:
        combined = combined.join(
            previous_tracking_df.select([
                pl.col('cb_ticker').alias('prev_cb_ticker'),
                'consecutive_days_above',
                pl.col('outstanding_balance').alias('prev_balance')
            ]),
            left_on='cb_ticker',
            right_on='prev_cb_ticker',
            how='left'
        )
    else:
        combined = combined.with_columns([
            pl.lit(0).alias('consecutive_days_above'),
            pl.lit(None).alias('prev_balance')
        ])

    # Fill null values
    combined = combined.with_columns([
        pl.col('consecutive_days_above').fill_null(0),
        pl.col('close_price').fill_null(0),
    ])

    # 計算各項指標
    results = []
    for row in combined.iter_rows(named=True):
        current_price = row.get('close_price', 0) or 0
        conversion_price = row.get('current_conversion_price', 0) or 0

        if current_price <= 0 or conversion_price <= 0:
            logger.warning(f"Skipping CB {row['cb_ticker']}: missing price data")
            continue

        result = calculator.calculate_warning(
            cb_ticker=row['cb_ticker'],
            underlying_ticker=row['underlying_ticker'],
            current_price=current_price,
            conversion_price=conversion_price,
            previous_consecutive_days=row.get('consecutive_days_above', 0) or 0,
            outstanding_balance=row.get('outstanding_amount', 0) or 0,
            previous_balance=row.get('prev_balance'),
            trade_date=trade_date
        )
        results.append(result.to_dict())

    if not results:
        logger.warning("No CB warnings calculated - empty result set")
        return pl.DataFrame()

    return pl.DataFrame(results)


def get_critical_cbs(
    warnings_df: pl.DataFrame,
    min_level: WarningLevel = WarningLevel.CAUTION,
    limit: int = 10
) -> pl.DataFrame:
    """
    取得高風險 CB 清單

    Args:
        warnings_df: CB 預警結果 DataFrame
        min_level: 最低預警等級篩選
        limit: 返回數量上限

    Returns:
        篩選後的 DataFrame，按 consecutive_days 降序排列
    """
    level_order = {
        WarningLevel.SAFE.value: 0,
        WarningLevel.CAUTION.value: 1,
        WarningLevel.WARNING.value: 2,
        WarningLevel.CRITICAL.value: 3
    }

    min_level_value = level_order.get(min_level.value, 0)

    # Filter by warning level
    filtered = warnings_df.filter(
        pl.col('warning_level').map_elements(
            lambda x: level_order.get(x, 0) >= min_level_value,
            return_dtype=pl.Boolean
        )
    )

    # Sort by consecutive days and limit
    return (
        filtered
        .sort('consecutive_days', descending=True)
        .head(limit)
    )


def warnings_summary(warnings_df: pl.DataFrame, trade_date: str) -> Dict[str, Any]:
    """
    產生 CB 預警摘要統計

    Args:
        warnings_df: CB 預警結果 DataFrame
        trade_date: 分析日期

    Returns:
        摘要字典
    """
    if len(warnings_df) == 0:
        return {
            'analysis_date': trade_date,
            'total_count': 0,
            'critical_count': 0,
            'warning_count': 0,
            'caution_count': 0,
            'safe_count': 0,
        }

    level_counts = warnings_df.group_by('warning_level').len()

    def get_count(level: str) -> int:
        filtered = level_counts.filter(pl.col('warning_level') == level)
        if len(filtered) > 0:
            return filtered['len'][0]
        return 0

    return {
        'analysis_date': trade_date,
        'total_count': len(warnings_df),
        'critical_count': get_count(WarningLevel.CRITICAL.value),
        'warning_count': get_count(WarningLevel.WARNING.value),
        'caution_count': get_count(WarningLevel.CAUTION.value),
        'safe_count': get_count(WarningLevel.SAFE.value),
    }


# Unit test helpers
def create_test_cb_data() -> pl.DataFrame:
    """建立測試用 CB 資料"""
    return pl.DataFrame([
        {
            'cb_ticker': '23301',
            'underlying_ticker': '2330',
            'cb_name': '台積電一',
            'current_conversion_price': 850.0,
            'outstanding_amount': 35.0,
            'total_issue_amount': 50.0,
        },
        {
            'cb_ticker': '24541',
            'underlying_ticker': '2454',
            'cb_name': '聯發科一',
            'current_conversion_price': 1200.0,
            'outstanding_amount': 25.0,
            'total_issue_amount': 30.0,
        },
    ])


def create_test_price_data(trade_date: str) -> pl.DataFrame:
    """建立測試用股價資料"""
    return pl.DataFrame([
        {'ticker': '2330', 'trade_date': trade_date, 'close_price': 1100.0},  # Above 130%
        {'ticker': '2454', 'trade_date': trade_date, 'close_price': 1400.0},  # Below 130%
    ])


if __name__ == "__main__":
    import sys

    logging.basicConfig(level=logging.INFO)

    # 執行簡單測試
    print("Testing CB Warning Calculator...")

    trade_date = "2026-01-19"
    cb_data = create_test_cb_data()
    price_data = create_test_price_data(trade_date)

    results = batch_calculate_cb_warnings(
        cb_issuance_df=cb_data,
        stock_prices_df=price_data,
        previous_tracking_df=None,
        trade_date=trade_date
    )

    print(f"\nCalculated {len(results)} CB warnings:")
    for row in results.iter_rows(named=True):
        print(f"  {row['cb_ticker']}: {row['warning_level']} - "
              f"Days={row['consecutive_days']}, Ratio={row['price_ratio']:.1f}%")
        print(f"    Comment: {row['comment']}")

    summary = warnings_summary(results, trade_date)
    print(f"\nSummary: {summary}")
