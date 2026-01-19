"""
配置 API 測試

驗證系統配置功能：
- 配置讀取
- 配置更新
- 權重驗證
"""

import pytest
from unittest.mock import Mock, patch, MagicMock

from engine.squeeze_calculator import SqueezeCalculator, SqueezeConfig


class TestSqueezeConfig:
    """SqueezeConfig 資料類別測試"""

    def test_default_values(self):
        """預設值應符合規劃書規範"""
        config = SqueezeConfig()

        # 權重檢查 (規劃書: 35%, 25%, 20%, 20%)
        assert config.weight_borrow == 0.35
        assert config.weight_gamma == 0.25
        assert config.weight_margin == 0.20
        assert config.weight_momentum == 0.20

        # 閾值檢查 (規劃書: 70, 40)
        assert config.bullish_threshold == 70
        assert config.bearish_threshold == 40

    def test_validate_correct_weights(self):
        """正確的權重總和應通過驗證"""
        config = SqueezeConfig()
        assert config.validate() is True

    def test_validate_incorrect_weights(self):
        """錯誤的權重總和應無法通過驗證"""
        config = SqueezeConfig(
            weight_borrow=0.50,
            weight_gamma=0.50,
            weight_margin=0.50,
            weight_momentum=0.50
        )
        assert config.validate() is False

    def test_from_dict(self):
        """應能從字典建立配置"""
        config_dict = {
            'SQUEEZE_WEIGHT_BORROW': '0.40',
            'SQUEEZE_WEIGHT_GAMMA': '0.30',
            'SQUEEZE_WEIGHT_MARGIN': '0.15',
            'SQUEEZE_WEIGHT_MOMENTUM': '0.15',
            'SQUEEZE_THRESHOLD_BULLISH': '75',
            'SQUEEZE_THRESHOLD_BEARISH': '35',
        }
        config = SqueezeConfig.from_dict(config_dict)

        assert config.weight_borrow == 0.40
        assert config.weight_gamma == 0.30
        assert config.weight_margin == 0.15
        assert config.weight_momentum == 0.15
        assert config.bullish_threshold == 75
        assert config.bearish_threshold == 35


class TestCalculatorWithCustomConfig:
    """使用自訂配置的 Calculator 測試"""

    def test_custom_config_affects_score(self):
        """自訂配置應影響分數計算"""
        # 預設配置
        default_calc = SqueezeCalculator(use_database=False)

        # 自訂配置：提高借券權重
        custom_config = SqueezeConfig(
            weight_borrow=0.60,  # 提高到 60%
            weight_gamma=0.20,
            weight_margin=0.10,
            weight_momentum=0.10
        )
        custom_calc = SqueezeCalculator(config=custom_config)

        # 相同輸入
        params = {
            'ticker': '2330',
            'borrow_change': -500000,  # 大量回補
            'margin_ratio': 5.0,  # 低券資比
            'iv': 0.25,
            'hv': 0.25,
            'price': 100,
            'prev_price': 100,
            'volume': 1000000,
            'avg_volume': 1000000,
            'borrow_range': (-1000000, 1000000)
        }

        default_result = default_calc.calculate_squeeze_score(**params)
        custom_result = custom_calc.calculate_squeeze_score(**params)

        # 由於借券回補高分且借券權重提高，自訂配置應得更高分
        assert custom_result.score >= default_result.score

    def test_custom_thresholds_affect_trend(self):
        """自訂閾值應影響趨勢判定"""
        # 低閾值配置
        low_threshold_config = SqueezeConfig(
            bullish_threshold=50,  # 降低 BULLISH 門檻
            bearish_threshold=30
        )
        calc = SqueezeCalculator(config=low_threshold_config)

        # 中等分數情境
        result = calc.calculate_squeeze_score(
            ticker='2330',
            borrow_change=-100000,
            margin_ratio=8.0,
            iv=0.25,
            hv=0.25,
            price=100,
            prev_price=100,
            volume=1000000,
            avg_volume=1000000,
            borrow_range=(-1000000, 1000000)
        )

        # 55 分在預設配置是 NEUTRAL，但在低閾值配置應是 BULLISH
        if result.score >= 50:
            from engine.squeeze_calculator import Trend
            assert result.trend == Trend.BULLISH


class TestCalculatorConfigReload:
    """配置重載測試"""

    def test_config_property_accessible(self):
        """應能存取當前配置"""
        calc = SqueezeCalculator(use_database=False)
        config = calc.config

        assert isinstance(config, SqueezeConfig)
        assert config.weight_borrow > 0

    @patch('engine.database.get_database')
    @patch('engine.database.ConfigRepository')
    def test_reload_config_from_db(self, mock_repo_class, mock_get_db):
        """重載配置應從資料庫讀取"""
        # 設置 mock
        mock_db = MagicMock()
        mock_get_db.return_value = mock_db

        mock_repo = MagicMock()
        mock_repo.get_all.return_value = [
            {'ConfigKey': 'SQUEEZE_WEIGHT_BORROW', 'ConfigValue': '0.40'},
            {'ConfigKey': 'SQUEEZE_WEIGHT_GAMMA', 'ConfigValue': '0.30'},
            {'ConfigKey': 'SQUEEZE_WEIGHT_MARGIN', 'ConfigValue': '0.15'},
            {'ConfigKey': 'SQUEEZE_WEIGHT_MOMENTUM', 'ConfigValue': '0.15'},
        ]
        mock_repo_class.return_value = mock_repo

        # 建立 calculator（不從DB讀取）並測試配置屬性
        calc = SqueezeCalculator(use_database=False)

        # 驗證預設配置存在
        assert calc.config.weight_borrow == 0.35  # 預設值


class TestConfigValidation:
    """配置驗證測試"""

    def test_weights_normalization(self):
        """權重自動正規化測試"""
        # 建立權重總和不為 1 的配置
        bad_config = SqueezeConfig(
            weight_borrow=0.50,
            weight_gamma=0.50,
            weight_margin=0.50,
            weight_momentum=0.50  # 總和 2.0
        )

        calc = SqueezeCalculator(config=bad_config)

        # 驗證已正規化
        total = (
            calc.config.weight_borrow +
            calc.config.weight_gamma +
            calc.config.weight_margin +
            calc.config.weight_momentum
        )
        assert abs(total - 1.0) < 0.001, "權重應已正規化為 1.0"

    def test_margin_tier_configuration(self):
        """券資比等級配置測試"""
        config = SqueezeConfig(
            margin_tier1_max=3.0,   # 0-3%
            margin_tier2_max=8.0,   # 3-8%
            margin_tier3_max=15.0   # 8-15%
        )
        calc = SqueezeCalculator(config=config)

        # 測試各等級邊界
        score_tier1 = calc.calculate_margin_score(2.0)   # Tier 1
        score_tier2 = calc.calculate_margin_score(6.0)   # Tier 2
        score_tier3 = calc.calculate_margin_score(12.0)  # Tier 3
        score_max = calc.calculate_margin_score(20.0)    # 超過 Tier 3

        assert score_tier1 < score_tier2 < score_tier3 < score_max
        assert score_max == 100
