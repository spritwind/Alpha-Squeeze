"""
Alpha Squeeze - Configuration Management

Centralized configuration using pydantic-settings for type-safe environment variables.
"""

from functools import lru_cache
from typing import Optional

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class DatabaseSettings(BaseSettings):
    """Database connection settings."""

    model_config = SettingsConfigDict(env_prefix="DB_")

    server: str = Field(default="localhost", description="MSSQL server host")
    database: str = Field(default="AlphaSqueeze", description="Database name")
    username: Optional[str] = Field(default=None, description="Database username")
    password: Optional[str] = Field(default=None, description="Database password")
    trusted_connection: bool = Field(default=True, description="Use Windows authentication")
    driver: str = Field(default="ODBC Driver 18 for SQL Server", description="ODBC driver")

    @property
    def connection_string(self) -> str:
        """Build ODBC connection string."""
        if self.trusted_connection:
            return (
                f"DRIVER={{{self.driver}}};"
                f"SERVER={self.server};"
                f"DATABASE={self.database};"
                f"Trusted_Connection=yes;"
                f"TrustServerCertificate=yes;"
            )
        return (
            f"DRIVER={{{self.driver}}};"
            f"SERVER={self.server};"
            f"DATABASE={self.database};"
            f"UID={self.username};"
            f"PWD={self.password};"
            f"TrustServerCertificate=yes;"
        )


class GrpcSettings(BaseSettings):
    """gRPC server settings."""

    model_config = SettingsConfigDict(env_prefix="GRPC_")

    host: str = Field(default="[::]", description="gRPC server host")
    port: int = Field(default=50051, description="gRPC server port")
    max_workers: int = Field(default=10, description="Maximum thread pool workers")

    @property
    def address(self) -> str:
        """Full server address."""
        return f"{self.host}:{self.port}"


class FinMindSettings(BaseSettings):
    """FinMind API settings."""

    model_config = SettingsConfigDict(env_prefix="FINMIND_")

    token: Optional[str] = Field(default=None, description="FinMind API token")
    rate_limit_delay: float = Field(default=0.5, description="Delay between API calls (seconds)")


class ScraperSettings(BaseSettings):
    """Web scraper settings."""

    model_config = SettingsConfigDict(env_prefix="SCRAPER_")

    headless: bool = Field(default=True, description="Run browser in headless mode")
    max_retries: int = Field(default=3, description="Maximum retry attempts")
    retry_delay: float = Field(default=5.0, description="Delay between retries (seconds)")
    request_delay: float = Field(default=2.0, description="Delay between requests (seconds)")
    timeout: int = Field(default=30000, description="Page timeout in milliseconds")


class SchedulerSettings(BaseSettings):
    """Scheduler settings."""

    model_config = SettingsConfigDict(env_prefix="SCHEDULER_")

    fetch_time: str = Field(default="18:30", description="Daily fetch time (HH:MM)")
    scrape_time: str = Field(default="19:00", description="Daily scrape time (HH:MM)")
    calculate_time: str = Field(default="19:30", description="Daily calculation time (HH:MM)")
    timezone: str = Field(default="Asia/Taipei", description="Scheduler timezone")


class SqueezeConfig(BaseSettings):
    """Squeeze algorithm configuration."""

    model_config = SettingsConfigDict(env_prefix="SQUEEZE_")

    # Factor weights (must sum to 1.0)
    weight_borrow: float = Field(default=0.35, description="Borrow factor weight")
    weight_gamma: float = Field(default=0.25, description="Gamma factor weight")
    weight_margin: float = Field(default=0.20, description="Margin factor weight")
    weight_momentum: float = Field(default=0.20, description="Momentum factor weight")

    # Thresholds
    bullish_threshold: int = Field(default=70, description="Bullish trend threshold")
    bearish_threshold: int = Field(default=40, description="Bearish trend threshold")

    def validate_weights(self) -> bool:
        """Validate that weights sum to 1.0."""
        total = (
            self.weight_borrow + self.weight_gamma + self.weight_margin + self.weight_momentum
        )
        return abs(total - 1.0) < 0.001


class Settings(BaseSettings):
    """Main application settings."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # Sub-settings
    database: DatabaseSettings = Field(default_factory=DatabaseSettings)
    grpc: GrpcSettings = Field(default_factory=GrpcSettings)
    finmind: FinMindSettings = Field(default_factory=FinMindSettings)
    scraper: ScraperSettings = Field(default_factory=ScraperSettings)
    scheduler: SchedulerSettings = Field(default_factory=SchedulerSettings)
    squeeze: SqueezeConfig = Field(default_factory=SqueezeConfig)

    # Application settings
    debug: bool = Field(default=False, description="Debug mode")
    log_level: str = Field(default="INFO", description="Logging level")


@lru_cache
def get_settings() -> Settings:
    """Get cached settings instance."""
    return Settings()
