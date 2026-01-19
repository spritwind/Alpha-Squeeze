"""
Alpha Squeeze - 量化引擎模組

核心功能：
- SqueezeCalculator: Squeeze Score 計算器
- gRPC Server: 提供 .NET 客戶端存取
- Config: 集中式配置管理

主要匯出：
    from engine import SqueezeCalculator, Trend, FactorScores, SqueezeSignal
    from engine.config import get_settings
"""

from engine.squeeze_calculator import (
    SqueezeCalculator,
    FactorScores,
    SqueezeSignal,
    Trend,
    batch_calculate_squeeze_scores,
)

from engine.config import get_settings, Settings

__all__ = [
    "SqueezeCalculator",
    "FactorScores",
    "SqueezeSignal",
    "Trend",
    "batch_calculate_squeeze_scores",
    "get_settings",
    "Settings",
]

__version__ = "0.1.0"
