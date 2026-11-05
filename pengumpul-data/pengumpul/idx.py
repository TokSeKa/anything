"""Ringkasan perdagangan harian Bursa Efek Indonesia (idx.co.id).

- Ringkasan saham: setiap emiten satu baris (OHLC, volume, nilai, frekuensi, beli/jual ASING),
  ditambah kolom turunan: asing bersih dan sisi domestik (volume total dikurangi asing).
- Ringkasan broker: total volume/nilai/frekuensi tiap anggota bursa di seluruh pasar.

Catatan: rincian broker apa membeli saham apa (broker summary per emiten) tidak disediakan gratis oleh IDX.
"""

from __future__ import annotations

import logging
import time
from datetime import date, datetime, timedelta
from pathlib import Path
from typing import Any, Callable
from zoneinfo import ZoneInfo

import pandas as pd

from . import storage
from .net import Http

log = logging.getLogger("pengumpul")
BASE = "https://www.idx.co.id/primary/TradingSummary"
HEADERS = {
    "Accept": "application/json, text/plain, */*",
    "Referer": "https://www.idx.co.id/id/data-pasar/ringkasan-perdagangan/ringkasan-saham/",
}

KOLOM_SAHAM = {
    "Date": "tanggal", "StockCode": "kode", "StockName": "nama", "Remarks": "keterangan",
    "Previous": "sebelumnya", "OpenPrice": "open", "FirstTrade": "transaksi_pertama", "High": "high", "Low": "low",
    "Close": "close", "Change": "perubahan", "Volume": "volume", "Value": "nilai", "Frequency": "frekuensi",
    "IndexIndividual": "indeks_individual", "Offer": "offer", "OfferVolume": "offer_volume", "Bid": "bid",
    "BidVolume": "bid_volume", "ListedShares": "saham_tercatat", "TradebleShares": "saham_beredar",
    "WeightForIndex": "bobot_indeks", "ForeignSell": "asing_jual", "ForeignBuy": "asing_beli",
    "NonRegularVolume": "nonreguler_volume", "NonRegularValue": "nonreguler_nilai",
    "NonRegularFrequency": "nonreguler_frekuensi",
}
KOLOM_BROKER = {"Date": "tanggal", "IDFirm": "kode_broker", "FirmName": "nama_broker", "Volume": "volume",
                "Value": "nilai", "Frequency": "frekuensi"}

GetJson = Callable[[str], Any]


def _rows(payload: Any) -> list[dict[str, Any]]:
    if isinstance(payload, dict):
        data = payload.get("data", payload.get("Data"))
        if isinstance(data, list):
            return data
    if isinstance(payload, list):
        return payload
    raise ValueError("format balasan IDX tidak dikenal (tidak ada daftar 'data')")


def _clean(rows: list[dict[str, Any]], mapping: dict[str, str], hari: date) -> pd.DataFrame:
    df = pd.DataFrame(rows)
    if df.empty:
        return df
    df = df.rename(columns=mapping)
    keep = [c for c in mapping.values() if c in df.columns]
    df = df[keep].copy()
    df["tanggal"] = pd.Timestamp(hari)
    for c in df.columns:
        if c not in ("tanggal", "kode", "nama", "keterangan", "kode_broker", "nama_broker"):
            df[c] = pd.to_numeric(df[c], errors="coerce")
    return df


def parse_saham(payload: Any, hari: date) -> pd.DataFrame:
    df = _clean(_rows(payload), KOLOM_SAHAM, hari)
    if df.empty:
        return df
    df["kode"] = df["kode"].astype(str).str.strip()
    if {"asing_beli", "asing_jual", "volume"} <= set(df.columns):
        df["asing_bersih"] = df["asing_beli"] - df["asing_jual"]
        df["domestik_beli"] = df["volume"] - df["asing_beli"]
        df["domestik_jual"] = df["volume"] - df["asing_jual"]
        df["domestik_bersih"] = df["domestik_beli"] - df["domestik_jual"]
        vol = df["volume"].where(df["volume"] > 0)
        df["porsi_asing_pct"] = ((df["asing_beli"] + df["asing_jual"]) / (2 * vol) * 100).round(2)
        if "close" in df:
            df["asing_bersih_nilai_kira"] = df["asing_bersih"] * df["close"]   # perkiraan pakai harga tutup
    return df.sort_values("kode").reset_index(drop=True)


def parse_broker(payload: Any, hari: date) -> pd.DataFrame:
    df = _clean(_rows(payload), KOLOM_BROKER, hari)
    if df.empty:
        return df
    df["kode_broker"] = df["kode_broker"].astype(str).str.strip()
    return df.sort_values("nilai", ascending=False).reset_index(drop=True)


def path_for(root: Path, jenis: str, hari: date) -> Path:
    return root / "idx" / jenis / f"{hari.isoformat()}.parquet"


def hari_bursa(dari: date, sampai: date) -> list[date]:
    out, d = [], dari
    while d <= sampai:
        if d.weekday() < 5:            # libur nasional terdeteksi dari balasan kosong
            out.append(d)
        d += timedelta(days=1)
    return out


def run(cfg, dari: date | None = None, sampai: date | None = None, get_json: GetJson | None = None,
        ulang: bool = False, now: datetime | None = None) -> dict:
    root = cfg.folder_data
    hari_ini = (now or datetime.now(ZoneInfo("Asia/Jakarta"))).date()
    sampai = min(sampai or hari_ini, hari_ini)
    dari = dari or (sampai - timedelta(days=max(cfg.idx_hari, 1) - 1))
    if get_json is None:
        http = Http(browser=True)
        get_json = lambda url: http.get_json(url, headers=HEADERS)  # noqa: E731
    jenis_list = [(j, f, p) for j, f, p, on in (
        ("ringkasan_saham", "GetStockSummary", parse_saham, cfg.idx_saham),
        ("ringkasan_broker", "GetBrokerSummary", parse_broker, cfg.idx_broker),
    ) if on]
    hasil: dict[str, Any] = {"hari_baru": 0, "libur_atau_belum_terbit": [], "gagal": {}}
    for hari in hari_bursa(dari, sampai):
        for jenis, endpoint, parse in jenis_list:
            path = path_for(root, jenis, hari)
            if path.exists() and not ulang:
                continue
            url = f"{BASE}/{endpoint}?length=9999&start=0&date={hari:%Y%m%d}"
            try:
                df = parse(get_json(url), hari)
            except Exception as e:  # noqa: BLE001
                hasil["gagal"][f"{jenis} {hari}"] = str(e)[:200]
                log.warning("idx %s %s gagal: %s", jenis, hari, e)
                continue
            finally:
                time.sleep(cfg.jeda)
            if df.empty:
                hasil["libur_atau_belum_terbit"].append(f"{jenis} {hari}")
                if hari < hari_ini:
                    storage.write_parquet(df, path)    # hari lampau kosong = libur; tandai supaya tidak diminta lagi
                continue
            storage.write_parquet(df, path)
            hasil["hari_baru"] += 1
            log.info("idx %s %s: %d baris", jenis, hari, len(df))
    storage.update_status(root, "idx", hasil)
    return hasil
