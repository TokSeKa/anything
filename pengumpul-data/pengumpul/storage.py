"""Penyimpanan Parquet: tulis atomik, gabung dengan data lama tanpa duplikat.

Struktur folder data:

    yahoo/<interval>/<SIMBOL>/<YYYY-MM>.parquet     interval menit (1m..90m), dipecah per bulan
    yahoo/<interval>/<SIMBOL>.parquet               1h, 1d, 1wk, 1mo (satu file per simbol)
    tick/dukascopy/<SIMBOL>/<YYYY-MM-DD>.parquet    tick bid/ask per hari (UTC)
    idx/ringkasan_saham/<YYYY-MM-DD>.parquet        semua emiten satu hari bursa
    idx/ringkasan_broker/<YYYY-MM-DD>.parquet       semua broker satu hari bursa
    log/<YYYY-MM-DD>.log, status.json
"""

from __future__ import annotations

import json
import os
import re
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import pandas as pd

MENIT = {"1m", "2m", "5m", "15m", "30m", "60m", "90m"}


def safe_name(simbol: str) -> str:
    """Nama file aman untuk semua OS: ^VIX → _VIX, EURUSD=X → EURUSD=X, BRK-B tetap."""
    return re.sub(r'[^A-Za-z0-9.=_\-]', "_", simbol)


def write_parquet(df: pd.DataFrame, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(suffix=".tmp", dir=path.parent)
    os.close(fd)
    try:
        df.to_parquet(tmp, index=False, compression="zstd")
        os.replace(tmp, path)
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)


def merge_save(new: pd.DataFrame, path: Path, key: str = "waktu") -> int:
    """Gabungkan ``new`` dengan isi file (kalau ada). Baris ber-``key`` sama: yang baru menang.
    Mengembalikan jumlah baris baru yang sebelumnya belum ada."""
    if new.empty:
        return 0
    if path.exists():
        old = pd.read_parquet(path)
        before = set(old[key])
        both = pd.concat([old, new], ignore_index=True)
        both = both.drop_duplicates(subset=[key], keep="last").sort_values(key).reset_index(drop=True)
        added = len(set(new[key]) - before)
    else:
        both = new.drop_duplicates(subset=[key], keep="last").sort_values(key).reset_index(drop=True)
        added = len(both)
    write_parquet(both, path)
    return added


def save_yahoo(root: Path, interval: str, simbol: str, df: pd.DataFrame) -> int:
    """Simpan bar Yahoo; interval menit dipecah per bulan (UTC) supaya file tidak membengkak."""
    base = root / "yahoo" / interval
    if df.empty:
        return 0
    if interval in MENIT:
        total = 0
        bulan = df["waktu"].dt.strftime("%Y-%m")
        for b, part in df.groupby(bulan):
            total += merge_save(part.reset_index(drop=True), base / safe_name(simbol) / f"{b}.parquet")
        return total
    return merge_save(df, base / f"{safe_name(simbol)}.parquet")


def last_time(root: Path, interval: str, simbol: str) -> pd.Timestamp | None:
    """Waktu bar terakhir yang sudah tersimpan (untuk menentukan perlu ambil mundur berapa jauh)."""
    base = root / "yahoo" / interval
    if interval in MENIT:
        files = sorted((base / safe_name(simbol)).glob("*.parquet"))
        if not files:
            return None
        df = pd.read_parquet(files[-1], columns=["waktu"])
    else:
        f = base / f"{safe_name(simbol)}.parquet"
        if not f.exists():
            return None
        df = pd.read_parquet(f, columns=["waktu"])
    return None if df.empty else df["waktu"].max()


# ---------------------------------------------------------------- status
def status_path(root: Path) -> Path:
    return root / "status.json"


def read_status(root: Path) -> dict[str, Any]:
    p = status_path(root)
    if not p.exists():
        return {}
    try:
        return json.loads(p.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def update_status(root: Path, sumber: str, info: dict[str, Any]) -> None:
    st = read_status(root)
    st[sumber] = {**info, "waktu": datetime.now(timezone.utc).isoformat(timespec="seconds")}
    root.mkdir(parents=True, exist_ok=True)
    p = status_path(root)
    tmp = p.with_suffix(".tmp")
    tmp.write_text(json.dumps(st, indent=2, ensure_ascii=False), encoding="utf-8")
    os.replace(tmp, p)


def summarize(root: Path) -> pd.DataFrame:
    """Ringkasan isi folder data: per sumber/simbol berapa file, ukuran, dan rentang waktunya."""
    rows = []
    if not root.exists():
        return pd.DataFrame(columns=["sumber", "simbol", "file", "ukuran_mb", "dari", "sampai"])
    for sub in sorted((root / "yahoo").glob("*")) if (root / "yahoo").exists() else []:
        for item in sorted(sub.iterdir()):
            files = sorted(item.glob("*.parquet")) if item.is_dir() else [item]
            files = [f for f in files if f.suffix == ".parquet"]
            if not files:
                continue
            first = pd.read_parquet(files[0], columns=["waktu"])["waktu"].min()
            last = pd.read_parquet(files[-1], columns=["waktu"])["waktu"].max()
            rows.append({"sumber": f"yahoo/{sub.name}", "simbol": item.stem if item.is_file() else item.name,
                         "file": len(files), "ukuran_mb": round(sum(f.stat().st_size for f in files) / 1e6, 2),
                         "dari": first, "sampai": last})
    tick = root / "tick" / "dukascopy"
    if tick.exists():
        for d in sorted(p for p in tick.iterdir() if p.is_dir()):
            files = sorted(d.glob("*.parquet"))
            if files:
                rows.append({"sumber": "tick/dukascopy", "simbol": d.name, "file": len(files),
                             "ukuran_mb": round(sum(f.stat().st_size for f in files) / 1e6, 2),
                             "dari": files[0].stem, "sampai": files[-1].stem})
    for jenis in ("ringkasan_saham", "ringkasan_broker"):
        d = root / "idx" / jenis
        files = sorted(d.glob("*.parquet")) if d.exists() else []
        if files:
            rows.append({"sumber": f"idx/{jenis}", "simbol": "(semua)", "file": len(files),
                         "ukuran_mb": round(sum(f.stat().st_size for f in files) / 1e6, 2),
                         "dari": files[0].stem, "sampai": files[-1].stem})
    return pd.DataFrame(rows)
