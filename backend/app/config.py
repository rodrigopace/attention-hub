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
    google_calendar_id: str | None = None
    google_calendar_access_token: str | None = None
    google_calendar_timeout_seconds: float = 10.0
    google_calendar_busy_title_template: str = "[{source_display_name}] Busy"

    @property
    def google_calendar_enabled(self) -> bool:
        return bool(self.google_calendar_id and self.google_calendar_access_token)

    @property
    def sqlite_path(self) -> Path:
        if not self.database_url.startswith("sqlite:///"):
            raise ValueError("Only sqlite:/// database URLs are implemented in the MVP backend")
        return Path(self.database_url.removeprefix("sqlite:///"))


@lru_cache
def get_settings() -> Settings:
    return Settings()
