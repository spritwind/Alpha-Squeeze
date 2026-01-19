"""
Alpha Squeeze - Core Squeeze Score Calculator

Implements the weighted scoring algorithm:
S = (W_B × F_B) + (W_G × F_G) + (W_M × F_M) + (W_V × F_V)

Where:
- F_B: Borrowing balance change score (法人空頭) - Weight: 35%
- F_G: IV-HV divergence score (Gamma效應) - Weight: 25%
- F_M: Margin ratio score (散戶燃料) - Weight: 20%
- F_V: Price-volume momentum score (價量動能) - Weight: 20%

Configuration is loaded from database (SystemConfig table) with fallback to defaults.
"""

import logging
from dataclasses import dataclass
from enum import Enum
from typing import Optional, Dict, Any
import polars as pl

logger = logging.getLogger(__name__)


class Trend(Enum):
    BULLISH = "BULLISH"
    NEUTRAL = "NEUTRAL"
    BEARISH = "BEARISH"


@dataclass
class FactorScores:
    """Individual factor scores (0-100)"""
    borrow_score: float
    gamma_score: float
    margin_score: float
    momentum_score: float


@dataclass
class SqueezeSignal:
    """Complete squeeze analysis result"""
    ticker: str
    score: int  # 0-100
    trend: Trend
    comment: str
    factors: FactorScores


@dataclass
class SqueezeConfig:
    """Configuration for squeeze calculation algorithm."""
    # Weights (must sum to 1.0)
    weight_borrow: float = 0.35
    weight_gamma: float = 0.25
    weight_margin: float = 0.20
    weight_momentum: float = 0.20

    # Thresholds
    bullish_threshold: int = 70
    bearish_threshold: int = 40

    # Margin scoring tiers
    margin_tier1_max: float = 5.0   # 0-5%
    margin_tier2_max: float = 10.0  # 5-10%
    margin_tier3_max: float = 20.0  # 10-20%

    def validate(self) -> bool:
        """Validate configuration."""
        total_weight = (
            self.weight_borrow + self.weight_gamma +
            self.weight_margin + self.weight_momentum
        )
        if abs(total_weight - 1.0) > 0.001:
            logger.warning(f"Weights sum to {total_weight}, expected 1.0")
            return False
        return True

    @classmethod
    def from_dict(cls, config: Dict[str, Any]) -> 'SqueezeConfig':
        """Create config from dictionary."""
        return cls(
            weight_borrow=float(config.get('SQUEEZE_WEIGHT_BORROW', 0.35)),
            weight_gamma=float(config.get('SQUEEZE_WEIGHT_GAMMA', 0.25)),
            weight_margin=float(config.get('SQUEEZE_WEIGHT_MARGIN', 0.20)),
            weight_momentum=float(config.get('SQUEEZE_WEIGHT_MOMENTUM', 0.20)),
            bullish_threshold=int(config.get('SQUEEZE_THRESHOLD_BULLISH', 70)),
            bearish_threshold=int(config.get('SQUEEZE_THRESHOLD_BEARISH', 40)),
            margin_tier1_max=float(config.get('MARGIN_SCORE_TIER1_MAX', 5.0)),
            margin_tier2_max=float(config.get('MARGIN_SCORE_TIER2_MAX', 10.0)),
            margin_tier3_max=float(config.get('MARGIN_SCORE_TIER3_MAX', 20.0)),
        )


class SqueezeCalculator:
    """
    Calculates squeeze potential scores for Taiwan stocks.

    Configuration is loaded from database on initialization.
    Falls back to default values if database is unavailable.
    """

    # Default values (used when DB is unavailable)
    _DEFAULT_CONFIG = SqueezeConfig()

    def __init__(self, config: Optional[SqueezeConfig] = None, use_database: bool = True):
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

        # Validate configuration
        if not self._config.validate():
            logger.warning("Invalid config detected, normalizing weights...")
            self._normalize_weights()

        logger.info(f"SqueezeCalculator initialized with config: "
                   f"weights=[{self._config.weight_borrow}, {self._config.weight_gamma}, "
                   f"{self._config.weight_margin}, {self._config.weight_momentum}], "
                   f"thresholds=[{self._config.bullish_threshold}, {self._config.bearish_threshold}]")

    def _load_config_from_db(self) -> SqueezeConfig:
        """Load configuration from database."""
        try:
            from engine.database import get_database, ConfigRepository

            db = get_database()
            repo = ConfigRepository(db)

            # Get all config values
            all_configs = repo.get_all()
            config_dict = {c['ConfigKey']: c['ConfigValue'] for c in all_configs}

            logger.info(f"Loaded {len(config_dict)} config values from database")
            return SqueezeConfig.from_dict(config_dict)

        except Exception as e:
            logger.warning(f"Failed to load config from database: {e}")
            logger.info("Using default configuration")
            return self._DEFAULT_CONFIG

    def _normalize_weights(self) -> None:
        """Normalize weights to sum to 1.0."""
        total = (
            self._config.weight_borrow + self._config.weight_gamma +
            self._config.weight_margin + self._config.weight_momentum
        )
        if total > 0:
            self._config.weight_borrow /= total
            self._config.weight_gamma /= total
            self._config.weight_margin /= total
            self._config.weight_momentum /= total

    @property
    def config(self) -> SqueezeConfig:
        """Get current configuration."""
        return self._config

    def reload_config(self) -> None:
        """Reload configuration from database."""
        self._config = self._load_config_from_db()
        logger.info("Configuration reloaded")

    def calculate_hv_20d(self, df: pl.LazyFrame, price_col: str = "close_price") -> pl.LazyFrame:
        """
        Calculate 20-day rolling Historical Volatility (annualized).

        Args:
            df: LazyFrame with price data
            price_col: Column name for close price

        Returns:
            LazyFrame with HV_20D column added
        """
        return df.with_columns(
            pl.col(price_col)
            .log()
            .diff()
            .rolling_std(window_size=20)
            .mul(252 ** 0.5)  # Annualize (252 trading days)
            .alias("hv_20d")
        )

    def calculate_borrow_score(self, borrow_change: float, historical_range: tuple[float, float]) -> float:
        """
        Calculate borrowing balance change score (F_B).

        Higher negative values (回補) = Higher scores
        """
        min_val, max_val = historical_range

        if borrow_change >= 0:
            # Positive change (增加空單) = Low score
            return max(0, 30 - (borrow_change / max_val) * 30)

        # Negative change (回補) = High score
        normalized = abs(borrow_change) / abs(min_val)
        return min(100, 50 + normalized * 50)

    def calculate_gamma_score(self, iv: float, hv: float) -> float:
        """
        Calculate IV-HV divergence score (F_G).

        When IV < HV (warrant undervalued) = Higher score
        """
        if iv <= 0 or hv <= 0:
            return 50  # Neutral if data missing

        divergence = (hv - iv) / hv  # Positive when IV < HV

        if divergence > 0:
            # IV < HV: Warrant undervalued, high squeeze potential
            return min(100, 50 + divergence * 100)
        else:
            # IV > HV: Warrant overvalued
            return max(0, 50 + divergence * 50)

    def calculate_margin_score(self, margin_ratio: float) -> float:
        """
        Calculate margin ratio score (F_M).

        Higher 券資比 (more crowded shorts) = Higher score
        Scoring tiers are configurable via database.
        """
        if margin_ratio <= 0:
            return 0

        tier1 = self._config.margin_tier1_max  # Default: 5%
        tier2 = self._config.margin_tier2_max  # Default: 10%
        tier3 = self._config.margin_tier3_max  # Default: 20%

        if margin_ratio >= tier3:
            return 100
        elif margin_ratio >= tier2:
            return 70 + (margin_ratio - tier2) / (tier3 - tier2) * 30
        elif margin_ratio >= tier1:
            return 40 + (margin_ratio - tier1) / (tier2 - tier1) * 30
        else:
            return margin_ratio / tier1 * 40

    def calculate_momentum_score(
        self,
        price: float,
        prev_price: float,
        volume: int,
        avg_volume: float,
        resistance_level: Optional[float] = None
    ) -> float:
        """
        Calculate price-volume momentum score (F_V).

        帶量突破 = High score
        """
        if prev_price <= 0 or avg_volume <= 0:
            return 50

        price_change = (price - prev_price) / prev_price
        volume_ratio = volume / avg_volume

        # Base score from price direction
        if price_change > 0:
            base_score = 50 + min(25, price_change * 500)  # Max +25 from price
        else:
            base_score = 50 + max(-25, price_change * 500)

        # Volume amplifier
        if volume_ratio > 2.0:
            volume_bonus = 25
        elif volume_ratio > 1.5:
            volume_bonus = 15
        elif volume_ratio > 1.0:
            volume_bonus = 5
        else:
            volume_bonus = -10

        # Resistance breakout bonus
        breakout_bonus = 0
        if resistance_level and price > resistance_level:
            breakout_bonus = 10

        return max(0, min(100, base_score + volume_bonus + breakout_bonus))

    def calculate_squeeze_score(
        self,
        ticker: str,
        borrow_change: float,
        margin_ratio: float,
        iv: float,
        hv: float,
        price: float,
        prev_price: float,
        volume: int,
        avg_volume: float,
        borrow_range: tuple[float, float] = (-1000000, 1000000)
    ) -> SqueezeSignal:
        """
        Calculate the complete Squeeze Score.

        Returns:
            SqueezeSignal with score, trend, and tactical comment
        """
        # Calculate individual factor scores
        borrow_score = self.calculate_borrow_score(borrow_change, borrow_range)
        gamma_score = self.calculate_gamma_score(iv, hv)
        margin_score = self.calculate_margin_score(margin_ratio)
        momentum_score = self.calculate_momentum_score(
            price, prev_price, volume, avg_volume
        )

        factors = FactorScores(
            borrow_score=round(borrow_score, 2),
            gamma_score=round(gamma_score, 2),
            margin_score=round(margin_score, 2),
            momentum_score=round(momentum_score, 2)
        )

        # Weighted total score (using configured weights)
        total_score = (
            self._config.weight_borrow * borrow_score +
            self._config.weight_gamma * gamma_score +
            self._config.weight_margin * margin_score +
            self._config.weight_momentum * momentum_score
        )
        score = round(total_score)

        # Determine trend (using configured thresholds)
        if score >= self._config.bullish_threshold:
            trend = Trend.BULLISH
        elif score <= self._config.bearish_threshold:
            trend = Trend.BEARISH
        else:
            trend = Trend.NEUTRAL

        # Generate tactical comment
        comment = self._generate_comment(factors, trend, score)

        return SqueezeSignal(
            ticker=ticker,
            score=score,
            trend=trend,
            comment=comment,
            factors=factors
        )

    def _generate_comment(self, factors: FactorScores, trend: Trend, score: int) -> str:
        """Generate tactical recommendation based on factor analysis."""
        comments = []

        # Analyze strongest factors
        factor_items = [
            ("法人回補", factors.borrow_score),
            ("Gamma壓縮", factors.gamma_score),
            ("空單擁擠", factors.margin_score),
            ("價量動能", factors.momentum_score),
        ]

        # Sort by score
        sorted_factors = sorted(factor_items, key=lambda x: x[1], reverse=True)

        if trend == Trend.BULLISH:
            top_factor = sorted_factors[0][0]
            comments.append(f"軋空潛力高，{top_factor}訊號強勁")
            if factors.gamma_score > 70:
                comments.append("權證低估，留意Gamma Squeeze機會")
        elif trend == Trend.BEARISH:
            comments.append("軋空機率低，建議觀望")
        else:
            comments.append("中性訊號，等待更明確方向")

        return "；".join(comments)


# Polars-based batch processing
def batch_calculate_squeeze_scores(
    metrics_df: pl.LazyFrame,
    warrant_df: pl.LazyFrame
) -> pl.LazyFrame:
    """
    Batch calculate squeeze scores for all tickers using Polars.

    Args:
        metrics_df: Daily stock metrics
        warrant_df: Warrant market data with IV

    Returns:
        LazyFrame with squeeze scores
    """
    calculator = SqueezeCalculator()

    # Join and calculate (implementation for batch processing)
    # This would be expanded based on actual data structure
    pass
