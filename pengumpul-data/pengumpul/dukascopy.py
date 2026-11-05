"""Tick bid/ask dari arsip publik Dukascopy (forex, logam, komoditas CFD).

Setiap jam satu file ``.bi5`` (LZMA). Isinya rekaman 20 byte big-endian:
milidetik sejak awal jam (uint32), ask (uint32), bid (uint32), volume ask (float32), volume bid (float32).
Harga disimpan sebagai bilangan bulat, jadi dibagi ``pembagi`` (umumnya 100000, pasangan JPY & logam 1000).
Catatan: di URL, bulan dihitung mulai 0 (Januari = 00).
"""

from __future__ import annotations

import logging
import lzma
from concurrent.futures import ThreadPoolExecutor
from datetime import date, datetime, timedelta, timezone
from pathlib import Path
from typing import Callable

import numpy as np
import pandas as pd

from . import storage
from .net import Http

log = logging.getLogger("pengumpul")
URL = "https://datafeed.dukascopy.com/datafeed/{sym}/{y:04d}/{m:02d}/{d:02d}/{h:02d}h_ticks.bi5"
REC = np.dtype([("ms", ">u4"), ("ask", ">u4"), ("bid", ">u4"), ("ask_vol", ">f4"), ("bid_vol", ">f4")])
KOLOM = ["waktu", "bid", "ask", "bid_vol", "ask_vol"]

Getter = Callable[[str], bytes | None]


def url_for(sym: str, day: date, hour: int) -> str:
    return URL.format(sym=sym, y=day.year, m=day.month - 1, d=day.day, h=hour)


def pembagi(sym: str, override: dict[str, float]) -> float:
    if sym in override:
        return override[sym]
    if "JPY" in sym or sym.startswith(("XAU", "XAG")):
        return 1000.0
    return 100000.0


def decode_hour(blob: bytes | None, hour_start: datetime, div: float) -> pd.DataFrame:
    """Satu file jam → DataFrame tick. File kosong / tidak ada = jam tanpa transaksi."""
    if not blob:
        return pd.DataFrame(columns=KOLOM)
    raw = lzma.decompress(blob)
    if len(raw) % REC.itemsize:
        raise ValueError(f"ukuran data tick tidak kelipatan {REC.itemsize} byte ({len(raw)})")
    arr = np.frombuffer(raw, dtype=REC)
    base = pd.Timestamp(hour_start).tz_convert("UTC") if pd.Timestamp(hour_start).tzinfo else pd.Timestamp(hour_start, tz="UTC")
    return pd.DataFrame({
        "waktu": base + pd.to_timedelta(arr["ms"].astype("int64"), unit="ms"),
        "bid": arr["bid"].astype("float64") / div,
        "ask": arr["ask"].astype("float64") / div,
        "bid_vol": arr["bid_vol"].astype("float64"),
        "ask_vol": arr["ask_vol"].astype("float64"),
    })


def fetch_day(sym: str, day: date, div: float, get: Getter, paralel: int = 4) -> pd.DataFrame:
    """Unduh 24 jam satu hari (UTC). Kalau satu jam gagal, seluruh hari dianggap gagal (dicoba lagi nanti)."""
    def one(h: int) -> pd.DataFrame:
        start = datetime(day.year, day.month, day.day, h, tzinfo=timezone.utc)
        return decode_hour(get(url_for(sym, day, h)), start, div)

    with ThreadPoolExecutor(max_workers=paralel) as ex:
        parts = list(ex.map(one, range(24)))
    parts = [p for p in parts if not p.empty]
    if not parts:
        return pd.DataFrame(columns=KOLOM).astype({"waktu": "datetime64[ns, UTC]", "bid": "float64", "ask": "float64",
                                                    "bid_vol": "float64", "ask_vol": "float64"})
    df = pd.concat(parts, ignore_index=True)
    df["waktu"] = df["waktu"].astype("datetime64[ns, UTC]")
    return df.sort_values("waktu", kind="stable").reset_index(drop=True)


def day_path(root: Path, sym: str, day: date) -> Path:
    return root / "tick" / "dukascopy" / storage.safe_name(sym) / f"{day.isoformat()}.parquet"


def days_to_fetch(dari: date, sampai: date) -> list[date]:
    out, d = [], dari
    while d <= sampai:
        out.append(d)
        d += timedelta(days=1)
    return out


def run(cfg, dari: date | None = None, sampai: date | None = None, simbol_filter: list[str] | None = None,
        get: Getter | None = None, ulang: bool = False) -> dict:
    """Unduh tick hari-hari penuh (UTC) yang belum ada. Default: ``hari_mundur`` hari terakhir sebelum hari ini."""
    root = cfg.folder_data
    kemarin = datetime.now(timezone.utc).date() - timedelta(days=1)
    sampai = min(sampai or kemarin, kemarin)          # hari ini belum lengkap, jangan disimpan
    dari = dari or (sampai - timedelta(days=max(cfg.duka_hari, 1) - 1))
    if get is None:
        http = Http()
        get = lambda url: http.get_bytes(url, ok404=True)  # noqa: E731
    hasil = {"hari_baru": 0, "tick_baru": 0, "dilewati": 0, "gagal": {}}
    for sym in cfg.duka_simbol:
        if simbol_filter and sym not in simbol_filter:
            continue
        div = pembagi(sym, cfg.duka_pembagi)
        for day in days_to_fetch(dari, sampai):
            path = day_path(root, sym, day)
            if path.exists() and not ulang:
                hasil["dilewati"] += 1
                continue
            try:
                df = fetch_day(sym, day, div, get, cfg.duka_paralel)
            except Exception as e:  # noqa: BLE001
                hasil["gagal"][f"{sym} {day}"] = str(e)[:200]
                log.warning("dukascopy %s %s gagal: %s", sym, day, e)
                continue
            storage.write_parquet(df, path)        # hari tanpa tick (akhir pekan) tetap ditandai dengan file kosong
            hasil["hari_baru"] += 1
            hasil["tick_baru"] += len(df)
            log.info("dukascopy %s %s: %d tick", sym, day, len(df))
    storage.update_status(root, "dukascopy", hasil)
    return hasil
