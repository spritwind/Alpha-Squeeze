"""
CB Warning Calculator 單元測試

測試核心演算法的正確性：
- 預警等級判定 (SAFE/CAUTION/WARNING/CRITICAL)
- 連續天數計算
- 觸發進度計算
- CB Score 計算
"""

import pytest
import polars as pl

from engine.cb_calculator import (
    CBWarningCalculator,
    CBWarningConfig,
    CBWarningResult,
    WarningLevel,
    batch_calculate_cb_warnings,
    get_critical_cbs,
    warnings_summary,
)


class TestCBWarningCalculator:
    """CB Warning Calculator 測試類別"""

    @pytest.fixture
    def calculator(self) -> CBWarningCalculator:
        """建立測試用 Calculator 實例（不使用資料庫）"""
        return CBWarningCalculator(use_database=False)

    @pytest.fixture
    def custom_config_calculator(self) -> CBWarningCalculator:
        """建立自訂配置的 Calculator"""
        config = CBWarningConfig(
            trigger_threshold_pct=130.0,
            trigger_days_required=30,
            reset_on_below=True
        )
        return CBWarningCalculator(config=config, use_database=False)

    # ===== 預警等級測試 =====

    class TestWarningLevel:
        """預警等級判定測試"""

        @pytest.fixture
        def calculator(self) -> CBWarningCalculator:
            return CBWarningCalculator(use_database=False)

        def test_safe_level(self, calculator: CBWarningCalculator):
            """連續天數 < 10 應為 SAFE"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1000,
                conversion_price=850,
                previous_consecutive_days=5,
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.warning_level == WarningLevel.SAFE

        def test_caution_level(self, calculator: CBWarningCalculator):
            """連續天數 10-19 應為 CAUTION"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1200,  # > 850 * 1.3 = 1105
                conversion_price=850,
                previous_consecutive_days=14,  # 15 天後會是 CAUTION
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.warning_level == WarningLevel.CAUTION

        def test_warning_level(self, calculator: CBWarningCalculator):
            """連續天數 20-29 應為 WARNING"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1200,
                conversion_price=850,
                previous_consecutive_days=24,
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.warning_level == WarningLevel.WARNING

        def test_critical_level(self, calculator: CBWarningCalculator):
            """連續天數 >= 30 應為 CRITICAL"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1200,
                conversion_price=850,
                previous_consecutive_days=29,
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.warning_level == WarningLevel.CRITICAL

    # ===== 連續天數計算測試 =====

    class TestConsecutiveDays:
        """連續天數計算測試"""

        @pytest.fixture
        def calculator(self) -> CBWarningCalculator:
            return CBWarningCalculator(use_database=False)

        def test_above_trigger_increments(self, calculator: CBWarningCalculator):
            """超過觸發門檻時連續天數應增加"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1200,  # > 850 * 1.3 = 1105
                conversion_price=850,
                previous_consecutive_days=10,
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.consecutive_days == 11
            assert result.is_above_trigger is True

        def test_below_trigger_resets(self, calculator: CBWarningCalculator):
            """低於觸發門檻時連續天數應重置"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1000,  # < 850 * 1.3 = 1105
                conversion_price=850,
                previous_consecutive_days=15,
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.consecutive_days == 0
            assert result.is_above_trigger is False

        def test_exactly_at_trigger(self, calculator: CBWarningCalculator):
            """恰好等於觸發門檻時應計入"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1105,  # = 850 * 1.3
                conversion_price=850,
                previous_consecutive_days=5,
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.consecutive_days == 6
            assert result.is_above_trigger is True

        def test_no_reset_config(self):
            """配置不重置時應保持天數"""
            config = CBWarningConfig(
                trigger_threshold_pct=130.0,
                trigger_days_required=30,
                reset_on_below=False
            )
            calculator = CBWarningCalculator(config=config, use_database=False)

            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1000,  # Below trigger
                conversion_price=850,
                previous_consecutive_days=15,
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.consecutive_days == 15  # 不重置

    # ===== 觸發進度計算測試 =====

    class TestTriggerProgress:
        """觸發進度計算測試"""

        @pytest.fixture
        def calculator(self) -> CBWarningCalculator:
            return CBWarningCalculator(use_database=False)

        def test_progress_calculation(self, calculator: CBWarningCalculator):
            """觸發進度應正確計算"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1200,
                conversion_price=850,
                previous_consecutive_days=14,  # 結果為 15 天
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            # 15/30 * 100 = 50%
            assert result.trigger_progress == 50.0

        def test_progress_capped_at_100(self, calculator: CBWarningCalculator):
            """觸發進度不應超過 100%"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1200,
                conversion_price=850,
                previous_consecutive_days=35,  # 超過 30 天
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.trigger_progress == 100.0

        def test_days_remaining(self, calculator: CBWarningCalculator):
            """剩餘天數應正確計算"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1200,
                conversion_price=850,
                previous_consecutive_days=19,  # 結果為 20 天
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.days_remaining == 10  # 30 - 20

    # ===== 價格比率計算測試 =====

    class TestPriceRatio:
        """價格比率計算測試"""

        @pytest.fixture
        def calculator(self) -> CBWarningCalculator:
            return CBWarningCalculator(use_database=False)

        def test_price_ratio_calculation(self, calculator: CBWarningCalculator):
            """價格比率應正確計算"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1100,
                conversion_price=850,
                previous_consecutive_days=0,
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            # 1100 / 850 * 100 = 129.41%
            assert 129.0 <= result.price_ratio <= 130.0

        def test_zero_conversion_price(self, calculator: CBWarningCalculator):
            """轉換價為 0 時應處理"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1100,
                conversion_price=0,
                previous_consecutive_days=0,
                outstanding_balance=35.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            assert result.price_ratio == 0.0

    # ===== 餘額變化計算測試 =====

    class TestBalanceChange:
        """餘額變化計算測試"""

        @pytest.fixture
        def calculator(self) -> CBWarningCalculator:
            return CBWarningCalculator(use_database=False)

        def test_balance_decrease(self, calculator: CBWarningCalculator):
            """餘額減少時應為負值"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1100,
                conversion_price=850,
                previous_consecutive_days=0,
                outstanding_balance=30.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            # (30 - 35) / 35 * 100 = -14.29%
            assert result.balance_change_pct < 0

        def test_balance_increase(self, calculator: CBWarningCalculator):
            """餘額增加時應為正值"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1100,
                conversion_price=850,
                previous_consecutive_days=0,
                outstanding_balance=40.0,
                previous_balance=35.0,
                trade_date="2026-01-19"
            )
            # (40 - 35) / 35 * 100 = 14.29%
            assert result.balance_change_pct > 0

        def test_no_previous_balance(self, calculator: CBWarningCalculator):
            """無前一日餘額時應為 0"""
            result = calculator.calculate_warning(
                cb_ticker="23301",
                underlying_ticker="2330",
                current_price=1100,
                conversion_price=850,
                previous_consecutive_days=0,
                outstanding_balance=35.0,
                previous_balance=None,
                trade_date="2026-01-19"
            )
            assert result.balance_change_pct == 0.0


class TestCBScore:
    """CB Score (整合至 Squeeze Score) 測試"""

    @pytest.fixture
    def calculator(self) -> CBWarningCalculator:
        return CBWarningCalculator(use_database=False)

    def test_redemption_called_max_score(self, calculator: CBWarningCalculator):
        """已公告強贖應得滿分"""
        score = calculator.calculate_cb_score(
            premium_rate=5.0,
            remaining_ratio=0.5,
            days_above_trigger=20,
            redemption_called=True
        )
        assert score == 100

    def test_high_days_high_score(self, calculator: CBWarningCalculator):
        """高達標天數應得高分"""
        score = calculator.calculate_cb_score(
            premium_rate=5.0,
            remaining_ratio=0.6,
            days_above_trigger=28,
            redemption_called=False
        )
        assert score >= 70

    def test_discount_premium_high_score(self, calculator: CBWarningCalculator):
        """折價（負溢價率）應得高分"""
        score = calculator.calculate_cb_score(
            premium_rate=-5.0,
            remaining_ratio=0.5,
            days_above_trigger=15,
            redemption_called=False
        )
        assert score >= 50

    def test_low_remaining_low_bonus(self, calculator: CBWarningCalculator):
        """低剩餘餘額佔比應得較低加分"""
        score_low = calculator.calculate_cb_score(
            premium_rate=5.0,
            remaining_ratio=0.2,
            days_above_trigger=10,
            redemption_called=False
        )
        score_high = calculator.calculate_cb_score(
            premium_rate=5.0,
            remaining_ratio=0.8,
            days_above_trigger=10,
            redemption_called=False
        )
        assert score_high > score_low

    def test_score_within_range(self, calculator: CBWarningCalculator):
        """分數應在 0-100 範圍內"""
        for premium in [-20, -5, 0, 5, 20, 50]:
            for ratio in [0.1, 0.3, 0.5, 0.7, 0.9]:
                for days in [0, 5, 10, 15, 25, 30]:
                    score = calculator.calculate_cb_score(
                        premium_rate=premium,
                        remaining_ratio=ratio,
                        days_above_trigger=days,
                        redemption_called=False
                    )
                    assert 0 <= score <= 100, f"分數 {score} 超出範圍"


class TestBatchCalculation:
    """批量計算測試"""

    def test_batch_calculate_cb_warnings(self):
        """批量計算應正確處理"""
        # 建立測試資料
        cb_data = pl.DataFrame([
            {
                'cb_ticker': '23301',
                'underlying_ticker': '2330',
                'current_conversion_price': 850.0,
                'outstanding_amount': 35.0,
            },
            {
                'cb_ticker': '24541',
                'underlying_ticker': '2454',
                'current_conversion_price': 1200.0,
                'outstanding_amount': 25.0,
            },
        ])

        price_data = pl.DataFrame([
            {'ticker': '2330', 'trade_date': '2026-01-19', 'close_price': 1150.0},
            {'ticker': '2454', 'trade_date': '2026-01-19', 'close_price': 1400.0},
        ])

        results = batch_calculate_cb_warnings(
            cb_issuance_df=cb_data,
            stock_prices_df=price_data,
            previous_tracking_df=None,
            trade_date='2026-01-19'
        )

        assert len(results) == 2
        assert '23301' in results['cb_ticker'].to_list()
        assert '24541' in results['cb_ticker'].to_list()

    def test_batch_with_missing_prices(self):
        """缺少股價時應跳過"""
        cb_data = pl.DataFrame([
            {
                'cb_ticker': '23301',
                'underlying_ticker': '2330',
                'current_conversion_price': 850.0,
                'outstanding_amount': 35.0,
            },
        ])

        # 沒有對應股價
        price_data = pl.DataFrame([
            {'ticker': '9999', 'trade_date': '2026-01-19', 'close_price': 100.0},
        ])

        results = batch_calculate_cb_warnings(
            cb_issuance_df=cb_data,
            stock_prices_df=price_data,
            previous_tracking_df=None,
            trade_date='2026-01-19'
        )

        assert len(results) == 0


class TestWarningsSummary:
    """預警摘要測試"""

    def test_summary_calculation(self):
        """摘要統計應正確計算"""
        warnings_df = pl.DataFrame([
            {'cb_ticker': '1', 'warning_level': 'CRITICAL'},
            {'cb_ticker': '2', 'warning_level': 'CRITICAL'},
            {'cb_ticker': '3', 'warning_level': 'WARNING'},
            {'cb_ticker': '4', 'warning_level': 'CAUTION'},
            {'cb_ticker': '5', 'warning_level': 'SAFE'},
            {'cb_ticker': '6', 'warning_level': 'SAFE'},
        ])

        summary = warnings_summary(warnings_df, '2026-01-19')

        assert summary['total_count'] == 6
        assert summary['critical_count'] == 2
        assert summary['warning_count'] == 1
        assert summary['caution_count'] == 1
        assert summary['safe_count'] == 2

    def test_empty_summary(self):
        """空資料時應返回零值"""
        warnings_df = pl.DataFrame()

        summary = warnings_summary(warnings_df, '2026-01-19')

        assert summary['total_count'] == 0
        assert summary['critical_count'] == 0


class TestGetCriticalCBs:
    """取得高風險 CB 測試"""

    def test_filter_by_level(self):
        """應正確篩選等級"""
        warnings_df = pl.DataFrame([
            {'cb_ticker': '1', 'warning_level': 'CRITICAL', 'consecutive_days': 35},
            {'cb_ticker': '2', 'warning_level': 'WARNING', 'consecutive_days': 25},
            {'cb_ticker': '3', 'warning_level': 'CAUTION', 'consecutive_days': 15},
            {'cb_ticker': '4', 'warning_level': 'SAFE', 'consecutive_days': 5},
        ])

        # 篩選 WARNING 以上
        critical = get_critical_cbs(warnings_df, WarningLevel.WARNING, limit=10)
        assert len(critical) == 2

    def test_sort_by_days(self):
        """應按連續天數降序排列"""
        warnings_df = pl.DataFrame([
            {'cb_ticker': '1', 'warning_level': 'CRITICAL', 'consecutive_days': 30},
            {'cb_ticker': '2', 'warning_level': 'CRITICAL', 'consecutive_days': 35},
            {'cb_ticker': '3', 'warning_level': 'CRITICAL', 'consecutive_days': 32},
        ])

        critical = get_critical_cbs(warnings_df, WarningLevel.CRITICAL, limit=10)

        # 應按天數降序
        days_list = critical['consecutive_days'].to_list()
        assert days_list == sorted(days_list, reverse=True)


class TestWarningLevelEnum:
    """WarningLevel 列舉測試"""

    def test_level_values(self):
        """應包含所有等級值"""
        assert WarningLevel.SAFE.value == "SAFE"
        assert WarningLevel.CAUTION.value == "CAUTION"
        assert WarningLevel.WARNING.value == "WARNING"
        assert WarningLevel.CRITICAL.value == "CRITICAL"

    def test_level_enum_members(self):
        """應有四個成員"""
        assert len(WarningLevel) == 4


class TestCBWarningConfig:
    """CBWarningConfig 測試"""

    def test_default_config(self):
        """預設配置應正確"""
        config = CBWarningConfig()
        assert config.trigger_threshold_pct == 130.0
        assert config.trigger_days_required == 30
        assert config.reset_on_below is True

    def test_from_dict(self):
        """從字典建立應正確"""
        config_dict = {
            'CB_TRIGGER_THRESHOLD_PCT': '125.0',
            'CB_TRIGGER_DAYS_REQUIRED': '20',
        }
        config = CBWarningConfig.from_dict(config_dict)
        assert config.trigger_threshold_pct == 125.0
        assert config.trigger_days_required == 20


class TestCBWarningResult:
    """CBWarningResult 測試"""

    def test_to_dict(self):
        """轉換為字典應正確"""
        result = CBWarningResult(
            cb_ticker='23301',
            underlying_ticker='2330',
            trade_date='2026-01-19',
            current_price=1150.0,
            conversion_price=850.0,
            price_ratio=135.29,
            is_above_trigger=True,
            consecutive_days=25,
            days_remaining=5,
            trigger_progress=83.33,
            outstanding_balance=35.0,
            balance_change_pct=-2.5,
            warning_level=WarningLevel.WARNING,
            comment='高度警戒'
        )

        d = result.to_dict()

        assert d['cb_ticker'] == '23301'
        assert d['warning_level'] == 'WARNING'  # 應轉為字串
        assert d['is_above_trigger'] is True
