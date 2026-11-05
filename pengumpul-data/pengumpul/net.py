"""HTTP dengan retry. IDX dilindungi Cloudflare, jadi kalau ada ``curl_cffi`` dipakai mode meniru Chrome."""

from __future__ import annotations

import logging
import time
from typing import Any

import requests

log = logging.getLogger("pengumpul")
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
      "Chrome/124.0 Safari/537.36")


class HttpError(Exception):
    def __init__(self, status: int, url: str):
        super().__init__(f"HTTP {status} untuk {url}")
        self.status = status


def _browser_session():
    try:
        from curl_cffi import requests as creq  # type: ignore
        return creq.Session(impersonate="chrome")
    except Exception:  # noqa: BLE001 - curl_cffi opsional
        s = requests.Session()
        s.headers.update({"User-Agent": UA})
        return s


class Http:
    """``get_bytes`` / ``get_json`` dengan retry & backoff (2, 4, 8 detik)."""

    def __init__(self, browser: bool = False, timeout: float = 30, retries: int = 3):
        self.s: Any = _browser_session() if browser else requests.Session()
        if not browser:
            self.s.headers.update({"User-Agent": UA})
        self.timeout = timeout
        self.retries = retries

    def get(self, url: str, headers: dict[str, str] | None = None, ok404: bool = False):
        last: Exception | None = None
        for k in range(self.retries + 1):
            try:
                r = self.s.get(url, headers=headers or {}, timeout=self.timeout)
                if r.status_code == 404 and ok404:
                    return None
                if r.status_code in (429, 500, 502, 503, 504):
                    raise HttpError(r.status_code, url)
                if r.status_code >= 400:
                    raise HttpError(r.status_code, url)   # 403 dsb.: jangan diulang terus
                return r
            except HttpError as e:
                last = e
                if e.status not in (429, 500, 502, 503, 504):
                    raise
            except Exception as e:  # noqa: BLE001 - koneksi putus, timeout
                last = e
            if k < self.retries:
                time.sleep(2 ** (k + 1))
        raise last  # type: ignore[misc]

    def get_bytes(self, url: str, ok404: bool = False) -> bytes | None:
        r = self.get(url, ok404=ok404)
        return None if r is None else r.content

    def get_json(self, url: str, headers: dict[str, str] | None = None) -> Any:
        r = self.get(url, headers=headers)
        try:
            return r.json()
        except ValueError:
            snippet = r.text[:120].replace("\n", " ")
            raise ValueError(f"balasan bukan JSON dari {url}: {snippet!r} (mungkin diblokir Cloudflare)") from None
