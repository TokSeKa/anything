"""Membaca simbol.toml (atau simbol.lokal.toml) menjadi konfigurasi."""

from __future__ import annotations

import tomllib
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parent.parent

YAHOO_GRUP = ("idx", "us", "indeks", "forex", "komoditas", "kripto")
INTERVAL_OK = ("1m", "2m", "5m", "15m", "30m", "60m", "90m", "1h", "1d", "1wk", "1mo")


@dataclass
class Config:
    folder_data: Path
    jam_harian: str = "18:30"
    zona_waktu: str = "Asia/Jakarta"
    jeda: float = 0.4
    yahoo_aktif: bool = True
    yahoo_interval: list[str] = field(default_factory=lambda: ["1m", "5m", "1h", "1d"])
    prepost_us: bool = False
    yahoo: dict[str, list[str]] = field(default_factory=dict)      # grup → simbol
    duka_aktif: bool = True
    duka_hari: int = 3
    duka_paralel: int = 4
    duka_simbol: list[str] = field(default_factory=list)
    duka_pembagi: dict[str, float] = field(default_factory=dict)
    idx_aktif: bool = True
    idx_saham: bool = True
    idx_broker: bool = True
    idx_hari: int = 7

    def yahoo_semua(self) -> list[tuple[str, str]]:
        """[(grup, simbol)] tanpa duplikat, urut sesuai file."""
        out, seen = [], set()
        for g in YAHOO_GRUP:
            for s in self.yahoo.get(g, []):
                if s not in seen:
                    seen.add(s)
                    out.append((g, s))
        return out


def config_path(explicit: str | Path | None = None) -> Path:
    if explicit:
        return Path(explicit)
    lokal = ROOT / "simbol.lokal.toml"
    return lokal if lokal.exists() else ROOT / "simbol.toml"


def load(path: str | Path | None = None) -> Config:
    p = config_path(path)
    with open(p, "rb") as f:
        raw: dict[str, Any] = tomllib.load(f)
    return from_dict(raw, base=p.parent)


def from_dict(raw: dict[str, Any], base: Path = ROOT) -> Config:
    u = raw.get("umum", {})
    folder = Path(u.get("folder_data", "data"))
    if not folder.is_absolute():
        folder = base / folder
    y = raw.get("yahoo", {})
    d = raw.get("dukascopy", {})
    i = raw.get("idx", {})
    interval = [str(x) for x in y.get("interval", ["1m", "5m", "1h", "1d"])]
    salah = [x for x in interval if x not in INTERVAL_OK]
    if salah:
        raise ValueError(f"interval Yahoo tidak dikenal: {salah}. Pilihan: {', '.join(INTERVAL_OK)}")
    jam = str(u.get("jam_harian", "18:30"))
    hh, _, mm = jam.partition(":")
    if not (hh.isdigit() and mm.isdigit() and 0 <= int(hh) < 24 and 0 <= int(mm) < 60):
        raise ValueError(f"jam_harian harus format JJ:MM, bukan {jam!r}")
    return Config(
        folder_data=folder,
        jam_harian=jam,
        zona_waktu=str(u.get("zona_waktu", "Asia/Jakarta")),
        jeda=float(u.get("jeda_permintaan", 0.4)),
        yahoo_aktif=bool(y.get("aktif", True)),
        yahoo_interval=interval,
        prepost_us=bool(y.get("prepost_us", False)),
        yahoo={g: [str(s).strip() for s in y.get(g, []) if str(s).strip()] for g in YAHOO_GRUP},
        duka_aktif=bool(d.get("aktif", True)),
        duka_hari=int(d.get("hari_mundur", 3)),
        duka_paralel=max(1, int(d.get("paralel", 4))),
        duka_simbol=[str(s).strip().upper() for s in d.get("simbol", [])],
        duka_pembagi={str(k).upper(): float(v) for k, v in d.get("pembagi", {}).items()},
        idx_aktif=bool(i.get("aktif", True)),
        idx_saham=bool(i.get("ringkasan_saham", True)),
        idx_broker=bool(i.get("ringkasan_broker", True)),
        idx_hari=int(i.get("hari_mundur", 7)),
    )
