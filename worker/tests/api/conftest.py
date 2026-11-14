"""A running worker app (lifespan included) over fake adapters."""

from collections.abc import Callable, Iterator
from contextlib import ExitStack
from dataclasses import dataclass
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.main import create_app
from stemstudio_worker.ports import Separator

from .contract import Response
from .fakes import YOUTUBE, ApiFakes


@dataclass
class Api:
    """Test client plus the fakes and settings behind it."""

    client: TestClient
    fakes: ApiFakes
    settings: WorkerSettings

    def wait(self) -> None:
        """Block until every queued job is processed."""
        self.client.app.state.runtime.queue.join()  # type: ignore[attr-defined]

    def post_url(self, url: str = YOUTUBE, source_type: str = "youtube") -> Response:
        return self.client.post("/v1/jobs", json={"sourceType": source_type, "url": url})

    def upload(self, name: str = "My Song.mp3", data: bytes = b"ID3 audio") -> Response:
        return self.client.post("/v1/jobs", files={"file": (name, data, "audio/mpeg")})

    def completed_job(self) -> str:
        """Create a YouTube job, wait for it and return its id."""
        job_id: str = self.post_url().json()["jobId"]
        self.wait()
        return job_id


ApiFactory = Callable[..., Api]


@pytest.fixture
def make_api(tmp_path: Path) -> Iterator[ApiFactory]:
    """Factory: ``make_api(separator=None, max_upload_bytes=...)`` -> started :class:`Api`."""
    with ExitStack() as stack:

        def factory(separator: Separator | None = None, max_upload_bytes: int = 1_000) -> Api:
            fakes = ApiFakes()
            settings = WorkerSettings(
                data_dir=tmp_path, separator="fake", max_upload_bytes=max_upload_bytes
            )
            app = create_app(settings, adapters=fakes.adapters(separator))
            client = stack.enter_context(TestClient(app))
            return Api(client, fakes, settings)

        yield factory


@pytest.fixture
def api(make_api: ApiFactory) -> Api:
    """The default app: six-stem fake separator, 1000-byte upload limit."""
    return make_api()
