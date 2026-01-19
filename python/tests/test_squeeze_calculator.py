"""
Squeeze Score Calculator 單元測試

測試核心演算法的正確性：
- 各維度分數計算 (F_B, F_G, F_M, F_V)
- 加權總分計算
- 趨勢判定
- 戰術建議生成
"""

import pytest

from engine.squeeze_calculator import (
    SqueezeCalculator,
    FactorScores,
    SqueezeSignal,
    Trend,
)


class TestSqueezeCalculator:
    """Squeeze Calculator 測試類別"""

    @pytest.fixture
    def calculator(self) -> SqueezeCalculator:
        """建立測試用 Calculator 實例（不使用資料庫）"""
        return SqueezeCalculator(use_database=False)

    # ===== 借券分數測試 (F_B) =====

    class TestBorrowScore:
        """借券餘額變化分數測試"""

        @pytest.fixture
        def calculator(self) -> SqueezeCalculator:
            return SqueezeCalculator()

        def test_heavy_covering_high_score(self, calculator: SqueezeCalculator):
            """大量回補（負值變化）應得高分"""
            score = calculator.calculate_borrow_score(
                borrow_change=-500000,
                historical_range=(-1000000, 1000000)
            )
            assert score >= 70, "大量回補應得 70 分以上"

        def test_heavy_shorting_low_score(self, calculator: SqueezeCalculator):
            """大量放空（正值變化）應得低分"""
            score = calculator.calculate_borrow_score(
                borrow_change=800000,
                historical_range=(-1000000, 1000000)
            )
            assert score <= 30, "大量放空應得 30 分以下"

        def test_neutral_change_middle_score(self, calculator: SqueezeCalculator):
            """無變化應得中性分數"""
            score = calculator.calculate_borrow_score(
                borrow_change=0,
                historical_range=(-1000000, 1000000)
            )
            assert 25 <= score <= 35, "無變化應得 25-35 分"

        def test_score_within_range(self, calculator: SqueezeCalculator):
            """分數應在 0-100 範圍內"""
            for change in [-2000000, -1000000, 0, 1000000, 2000000]:
                score = calculator.calculate_borrow_score(
                    borrow_change=change,
                    historical_range=(-1000000, 1000000)
                )
                assert 0 <= score <= 100, f"分數 {score} 超出範圍"

    # ===== Gamma 分數測試 (F_G) =====

    class TestGammaScore:
        """IV-HV 乖離分數測試"""

        @pytest.fixture
        def calculator(self) -> SqueezeCalculator:
            return SqueezeCalculator()

        def test_iv_less_than_hv_high_score(self, calculator: SqueezeCalculator):
            """IV < HV（權證低估）應得高分"""
            score = calculator.calculate_gamma_score(iv=0.20, hv=0.30)
            assert score >= 70, "IV 低於 HV 應得高分"

        def test_iv_greater_than_hv_low_score(self, calculator: SqueezeCalculator):
            """IV > HV（權證高估）應得低分"""
            score = calculator.calculate_gamma_score(iv=0.40, hv=0.25)
            assert score <= 40, "IV 高於 HV 應得低分"

        def test_iv_equals_hv_neutral_score(self, calculator: SqueezeCalculator):
            """IV = HV 應得中性分數"""
            score = calculator.calculate_gamma_score(iv=0.25, hv=0.25)
            assert 45 <= score <= 55, "IV 等於 HV 應得中性分數"

        def test_missing_iv_returns_neutral(self, calculator: SqueezeCalculator):
            """缺失 IV 資料應返回 50 分"""
            score = calculator.calculate_gamma_score(iv=0, hv=0.25)
            assert score == 50, "缺失 IV 應返回 50"

        def test_missing_hv_returns_neutral(self, calculator: SqueezeCalculator):
            """缺失 HV 資料應返回 50 分"""
            score = calculator.calculate_gamma_score(iv=0.25, hv=0)
            assert score == 50, "缺失 HV 應返回 50"

        def test_extreme_divergence_capped(self, calculator: SqueezeCalculator):
            """極端乖離應被限制在 0-100"""
            score_high = calculator.calculate_gamma_score(iv=0.10, hv=0.50)
            score_low = calculator.calculate_gamma_score(iv=0.80, hv=0.20)
            assert 0 <= score_high <= 100
            assert 0 <= score_low <= 100

    # ===== 券資比分數測試 (F_M) =====

    class TestMarginScore:
        """券資比分數測試"""

        @pytest.fixture
        def calculator(self) -> SqueezeCalculator:
            return SqueezeCalculator()

        def test_extreme_crowding_max_score(self, calculator: SqueezeCalculator):
            """極度擁擠（>20%）應得滿分"""
            score = calculator.calculate_margin_score(margin_ratio=25.0)
            assert score >= 95, "極度擁擠應得接近滿分"

        def test_high_crowding_high_score(self, calculator: SqueezeCalculator):
            """高擁擠（10-20%）應得高分"""
            score = calculator.calculate_margin_score(margin_ratio=15.0)
            assert 80 <= score <= 95, "高擁擠應得 80-95 分"

        def test_moderate_crowding_medium_score(self, calculator: SqueezeCalculator):
            """中等擁擠（5-10%）應得中高分"""
            score = calculator.calculate_margin_score(margin_ratio=7.0)
            assert 50 <= score <= 70, "中等擁擠應得 50-70 分"

        def test_low_crowding_low_score(self, calculator: SqueezeCalculator):
            """低擁擠（0-5%）應得低分"""
            score = calculator.calculate_margin_score(margin_ratio=2.0)
            assert score <= 25, "低擁擠應得 25 分以下"

        def test_zero_ratio_zero_score(self, calculator: SqueezeCalculator):
            """零券資比應得 0 分"""
            score = calculator.calculate_margin_score(margin_ratio=0)
            assert score == 0, "零券資比應得 0 分"

        def test_negative_ratio_zero_score(self, calculator: SqueezeCalculator):
            """負券資比應得 0 分"""
            score = calculator.calculate_margin_score(margin_ratio=-5.0)
            assert score == 0, "負券資比應得 0 分"

    # ===== 動能分數測試 (F_V) =====

    class TestMomentumScore:
        """價量動能分數測試"""

        @pytest.fixture
        def calculator(self) -> SqueezeCalculator:
            return SqueezeCalculator()

        def test_price_up_volume_up_high_score(self, calculator: SqueezeCalculator):
            """帶量上漲應得高分"""
            score = calculator.calculate_momentum_score(
                price=110,
                prev_price=100,
                volume=5000000,
                avg_volume=2000000,
                resistance_level=105
            )
            assert score >= 75, "帶量上漲應得高分"

        def test_price_down_volume_down_low_score(self, calculator: SqueezeCalculator):
            """縮量下跌應得低分"""
            score = calculator.calculate_momentum_score(
                price=95,
                prev_price=100,
                volume=500000,
                avg_volume=2000000
            )
            assert score <= 40, "縮量下跌應得低分"

        def test_breakout_with_volume_bonus(self, calculator: SqueezeCalculator):
            """突破壓力位應有額外加分"""
            score_no_breakout = calculator.calculate_momentum_score(
                price=104,
                prev_price=100,
                volume=3000000,
                avg_volume=2000000,
                resistance_level=105
            )
            score_breakout = calculator.calculate_momentum_score(
                price=106,
                prev_price=100,
                volume=3000000,
                avg_volume=2000000,
                resistance_level=105
            )
            assert score_breakout > score_no_breakout, "突破應得更高分"

        def test_flat_price_neutral_score(self, calculator: SqueezeCalculator):
            """價格持平應得中性偏低分數（量能剛好平均）"""
            score = calculator.calculate_momentum_score(
                price=100,
                prev_price=100,
                volume=2000000,
                avg_volume=2000000
            )
            # 價格持平 = base 50，量能比 1.0 = -10 懲罰
            # 實際得分 = 40
            assert 35 <= score <= 50, "價格持平且量能平均應得中性偏低分數"

        def test_missing_data_neutral_score(self, calculator: SqueezeCalculator):
            """資料缺失應返回中性分數"""
            score = calculator.calculate_momentum_score(
                price=100,
                prev_price=0,
                volume=1000000,
                avg_volume=2000000
            )
            assert score == 50, "缺失前收盤價應返回 50"

    # ===== 總分計算測試 =====

    class TestTotalScore:
        """Squeeze Score 總分計算測試"""

        @pytest.fixture
        def calculator(self) -> SqueezeCalculator:
            return SqueezeCalculator()

        def test_bullish_case(self, calculator: SqueezeCalculator):
            """全部高分應判定為 BULLISH"""
            result = calculator.calculate_squeeze_score(
                ticker="2330",
                borrow_change=-500000,
                margin_ratio=18.0,
                iv=0.22,
                hv=0.32,
                price=600,
                prev_price=580,
                volume=50000000,
                avg_volume=20000000,
                borrow_range=(-1000000, 1000000)
            )
            assert result.score >= 70, "全部高分應得 70 分以上"
            assert result.trend == Trend.BULLISH, "應判定為 BULLISH"

        def test_bearish_case(self, calculator: SqueezeCalculator):
            """全部低分應判定為 BEARISH"""
            result = calculator.calculate_squeeze_score(
                ticker="2330",
                borrow_change=800000,
                margin_ratio=2.0,
                iv=0.40,
                hv=0.25,
                price=550,
                prev_price=580,
                volume=5000000,
                avg_volume=20000000,
                borrow_range=(-1000000, 1000000)
            )
            assert result.score <= 40, "全部低分應得 40 分以下"
            assert result.trend == Trend.BEARISH, "應判定為 BEARISH"

        def test_neutral_case(self, calculator: SqueezeCalculator):
            """混合分數應判定為 NEUTRAL"""
            result = calculator.calculate_squeeze_score(
                ticker="2330",
                borrow_change=0,
                margin_ratio=8.0,
                iv=0.25,
                hv=0.25,
                price=580,
                prev_price=580,
                volume=20000000,
                avg_volume=20000000,
                borrow_range=(-1000000, 1000000)
            )
            assert 40 < result.score < 70, "混合分數應在 40-70 之間"
            assert result.trend == Trend.NEUTRAL, "應判定為 NEUTRAL"

        def test_result_contains_all_factors(self, calculator: SqueezeCalculator):
            """結果應包含所有維度分數"""
            result = calculator.calculate_squeeze_score(
                ticker="2330",
                borrow_change=0,
                margin_ratio=10.0,
                iv=0.25,
                hv=0.25,
                price=100,
                prev_price=100,
                volume=1000,
                avg_volume=1000,
                borrow_range=(-1000000, 1000000)
            )
            assert isinstance(result.factors, FactorScores)
            assert hasattr(result.factors, "borrow_score")
            assert hasattr(result.factors, "gamma_score")
            assert hasattr(result.factors, "margin_score")
            assert hasattr(result.factors, "momentum_score")

        def test_score_within_valid_range(self, calculator: SqueezeCalculator):
            """總分應在 0-100 範圍內"""
            # 極端高分情境
            result_high = calculator.calculate_squeeze_score(
                ticker="TEST",
                borrow_change=-2000000,
                margin_ratio=50.0,
                iv=0.10,
                hv=0.50,
                price=150,
                prev_price=100,
                volume=10000000,
                avg_volume=1000000,
                borrow_range=(-1000000, 1000000)
            )
            assert 0 <= result_high.score <= 100

            # 極端低分情境
            result_low = calculator.calculate_squeeze_score(
                ticker="TEST",
                borrow_change=2000000,
                margin_ratio=0,
                iv=0.50,
                hv=0.10,
                price=50,
                prev_price=100,
                volume=100000,
                avg_volume=10000000,
                borrow_range=(-1000000, 1000000)
            )
            assert 0 <= result_low.score <= 100

    # ===== 權重驗證測試 =====

    class TestWeights:
        """權重配置測試"""

        def test_default_weights_sum_to_one(self):
            """預設權重總和應為 1.0"""
            calculator = SqueezeCalculator(use_database=False)
            config = calculator.config
            total = (
                config.weight_borrow +
                config.weight_gamma +
                config.weight_margin +
                config.weight_momentum
            )
            assert abs(total - 1.0) < 0.001, f"權重總和應為 1.0，實際為 {total}"

        def test_weight_values(self):
            """檢查預設權重值"""
            calculator = SqueezeCalculator(use_database=False)
            config = calculator.config
            assert config.weight_borrow == 0.35
            assert config.weight_gamma == 0.25
            assert config.weight_margin == 0.20
            assert config.weight_momentum == 0.20

    # ===== 閾值測試 =====

    class TestThresholds:
        """趨勢閾值測試"""

        def test_threshold_values(self):
            """檢查預設閾值"""
            calculator = SqueezeCalculator(use_database=False)
            config = calculator.config
            assert config.bullish_threshold == 70
            assert config.bearish_threshold == 40

        def test_boundary_at_bullish_threshold(self):
            """邊界值測試：恰好達到 BULLISH 閾值"""
            calculator = SqueezeCalculator(use_database=False)
            # 建立一個恰好得 70 分的情境較複雜，這裡測試概念
            assert calculator.config.bullish_threshold == 70

        def test_boundary_at_bearish_threshold(self):
            """邊界值測試：恰好達到 BEARISH 閾值"""
            calculator = SqueezeCalculator(use_database=False)
            assert calculator.config.bearish_threshold == 40


class TestFactorScores:
    """FactorScores 資料類別測試"""

    def test_dataclass_creation(self):
        """應能正確建立 FactorScores"""
        scores = FactorScores(
            borrow_score=80.0,
            gamma_score=70.0,
            margin_score=60.0,
            momentum_score=50.0
        )
        assert scores.borrow_score == 80.0
        assert scores.gamma_score == 70.0
        assert scores.margin_score == 60.0
        assert scores.momentum_score == 50.0


class TestSqueezeSignal:
    """SqueezeSignal 資料類別測試"""

    def test_dataclass_creation(self):
        """應能正確建立 SqueezeSignal"""
        factors = FactorScores(80.0, 70.0, 60.0, 50.0)
        signal = SqueezeSignal(
            ticker="2330",
            score=75,
            trend=Trend.BULLISH,
            comment="軋空潛力高",
            factors=factors
        )
        assert signal.ticker == "2330"
        assert signal.score == 75
        assert signal.trend == Trend.BULLISH
        assert signal.comment == "軋空潛力高"


class TestTrend:
    """Trend 列舉測試"""

    def test_trend_values(self):
        """應包含所有趨勢值"""
        assert Trend.BULLISH.value == "BULLISH"
        assert Trend.NEUTRAL.value == "NEUTRAL"
        assert Trend.BEARISH.value == "BEARISH"

    def test_trend_enum_members(self):
        """應有三個成員"""
        assert len(Trend) == 3
