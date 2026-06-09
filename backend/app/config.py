from functools import lru_cache
from pathlib import Path
from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="ATTENTION_HUB_", env_file=".env")

    database_url: str = Field(
        default="sqlite:///./data/attention-hub.sqlite3",
        description="Storage URL. Only sqlite:// is implemented in the MVP.",
    )
    default_poll_interval_seconds: int = 120

    @property
    def sqlite_path(self) -> Path:
        if not self.database_url.startswith("sqlite:///"):
            raise ValueError("Only sqlite:/// database URLs are implemented in the MVP backend")
        return Path(self.database_url.removeprefix("sqlite:///"))


@lru_cache
def get_settings() -> Settings:
    return Settings()

