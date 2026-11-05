"""Bar OHLCV dari Yahoo Finance (saham IDX/US, indeks termasuk VIX, forex, futures komoditas)."""

from __future__ import annotations

import logging
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Callable

import pandas as pd

from . import storage

log = logging.getLogger("pengumpul")

# berapa hari ke belakang Yahoo masih melayani tiap interval, dan ukuran potongan per permintaan
BATAS_HARI = {"1m": 29, "2m": 59, "5m": 59, "15m": 59, "30m": 59, "60m": 729, "90m": 59, "1h": 729}
POTONGAN_HARI = {"1m": 7}

Fetcher = Callable[..., pd.DataFrame]


def _yf_fetch(simbol: str, interval: str, start: datetime | None, end: datetime | None,
              period: str | None, prepost: bool) -> pd.DataFrame:
    import yfinance as yf  # impor di sini supaya tes tidak butuh jaringan
    t = yf.Ticker(simbol)
    if period:
        return t.history(period=period, interval=interval, auto_adjust=False, actions=False, prepost=prepost)
    return t.history(start=start, end=end, interval=interval, auto_adjust=False, actions=False, prepost=prepost)


def normalize(raw: pd.DataFrame) -> pd.DataFrame:
    """Index waktu → kolom ``waktu`` (UTC); kolom huruf kecil; baris kosong dibuang."""
    cols = ["waktu", "open", "high", "low", "close", "adj_close", "volume"]
    if raw is None or raw.empty:
        return pd.DataFrame(columns=cols)
    df = raw.copy()
    idx = pd.to_datetime(df.index)
    idx = idx.tz_localize("UTC") if idx.tz is None else idx.tz_convert("UTC")
    df.index = idx
    df = df.rename(columns={"Open": "open", "High": "high", "Low": "low", "Close": "close",
                            "Adj Close": "adj_close", "Volume": "volume"})
    if "adj_close" not in df:
        df["adj_close"] = df["close"]
    df = df[["open", "high", "low", "close", "adj_close", "volume"]].dropna(subset=["open", "high", "low", "close"], how="all")
    df["volume"] = df["volume"].fillna(0).astype("int64")
    for c in ("open", "high", "low", "close", "adj_close"):
        df[c] = df[c].astype("float64")
    df = df.reset_index(names="waktu")
    df["waktu"] = df["waktu"].astype("datetime64[ns, UTC]")
    return df[cols]


def plan(interval: str, last: pd.Timestamp | None, now: datetime) -> list[tuple[datetime | None, datetime | None, str | None]]:
    """Daftar (start, end, period) yang perlu diminta supaya arsip tersambung dengan data terbaru."""
    end = now + timedelta(days=1)
    if interval in ("1d", "1wk", "1mo"):
        if last is None:
            return [(None, None, "max")]
        return [(last.to_pydatetime() - timedelta(days=10), end, None)]
    batas = now - timedelta(days=BATAS_HARI.get(interval, 59))
    start = batas if last is None else max(batas, last.to_pydatetime() - timedelta(days=1))
    step = timedelta(days=POTONGAN_HARI.get(interval, 10_000))
    out = []
    while start < end:
        stop = min(start + step, end)
        out.append((start, stop, None))
        start = stop
    return out


def update_symbol(root: Path, simbol: str, interval: str, prepost: bool = False, jeda: float = 0.4,
                  fetch: Fetcher = _yf_fetch, now: datetime | None = None) -> int:
    now = now or datetime.now(timezone.utc)
    last = storage.last_time(root, interval, simbol)
    parts = []
    for start, end, period in plan(interval, last, now):
        raw = fetch(simbol, interval, start, end, period, prepost)
        parts.append(normalize(raw))
        if jeda:
            time.sleep(jeda)
    parts = [p for p in parts if not p.empty]
    if not parts:
        return 0
    df = pd.concat(parts, ignore_index=True)
    return storage.save_yahoo(root, interval, simbol, df)


def run(cfg, simbol_filter: list[str] | None = None, interval_filter: list[str] | None = None,
        fetch: Fetcher = _yf_fetch) -> dict:
    """Perbarui semua simbol × interval. Satu simbol gagal tidak menghentikan yang lain."""
    root = cfg.folder_data
    intervals = [i for i in cfg.yahoo_interval if not interval_filter or i in interval_filter]
    hasil = {"baris_baru": 0, "berhasil": 0, "kosong": [], "gagal": {}}
    for grup, simbol in cfg.yahoo_semua():
        if simbol_filter and simbol not in simbol_filter:
            continue
        for interval in intervals:
            prepost = cfg.prepost_us and grup == "us" and interval in storage.MENIT
            try:
                n = update_symbol(root, simbol, interval, prepost, cfg.jeda, fetch)
            except Exception as e:  # noqa: BLE001 - catat, lanjut simbol berikutnya
                hasil["gagal"][f"{simbol} {interval}"] = str(e)[:200]
                log.warning("yahoo %s %s gagal: %s", simbol, interval, e)
                continue
            if n == 0 and storage.last_time(root, interval, simbol) is None:
                hasil["kosong"].append(f"{simbol} {interval}")
                log.info("yahoo %s %s: tidak ada data", simbol, interval)
            else:
                hasil["berhasil"] += 1
                hasil["baris_baru"] += n
                log.info("yahoo %s %s: +%d bar", simbol, interval, n)
    storage.update_status(root, "yahoo", {k: (v if not isinstance(v, list) else v[:50]) for k, v in hasil.items()})
    return hasil
