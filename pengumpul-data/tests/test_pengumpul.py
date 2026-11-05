"""Tes tanpa jaringan: sumber data diganti fungsi palsu dengan format yang sama seperti aslinya."""

import lzma
import struct
from datetime import date, datetime, timedelta, timezone

import numpy as np
import pandas as pd
import pytest

from pengumpul import cli, config, dukascopy, idx, storage, yahoo


@pytest.fixture()
def cfg(tmp_path):
    return config.from_dict({
        "umum": {"folder_data": str(tmp_path / "data"), "jeda_permintaan": 0},
        "yahoo": {"interval": ["1m", "1d"], "idx": ["BBCA.JK"], "indeks": ["^VIX"], "us": ["BBCA.JK"]},
        "dukascopy": {"simbol": ["EURUSD", "USDJPY"], "hari_mundur": 2, "paralel": 2},
        "idx": {"hari_mundur": 3},
    })


def test_config_bawaan_valid():
    c = config.load(config.ROOT / "simbol.toml")
    assert "^VIX" in c.yahoo["indeks"] and "BBCA.JK" in c.yahoo["idx"]
    assert c.duka_pembagi["XAUUSD"] == 1000 and c.idx_saham
    assert len(c.yahoo_semua()) == len({s for _, s in c.yahoo_semua()})


def test_config_salah():
    with pytest.raises(ValueError):
        config.from_dict({"yahoo": {"interval": ["3m"]}})
    with pytest.raises(ValueError):
        config.from_dict({"umum": {"jam_harian": "25:00"}})


# ---------------------------------------------------------------- yahoo
def fake_bars(start: datetime, n: int, freq: str = "1min", tz: str = "Asia/Jakarta") -> pd.DataFrame:
    idx_ = pd.date_range(start, periods=n, freq=freq, tz="UTC").tz_convert(tz)
    base = np.arange(n, dtype=float) + 100
    return pd.DataFrame({"Open": base, "High": base + 1, "Low": base - 1, "Close": base + 0.5,
                         "Adj Close": base + 0.5, "Volume": np.arange(n) * 10}, index=idx_.rename("Datetime"))


def test_normalize_utc_dan_kolom():
    df = yahoo.normalize(fake_bars(datetime(2026, 11, 3, 2, 0, tzinfo=timezone.utc), 3))
    assert list(df.columns) == ["waktu", "open", "high", "low", "close", "adj_close", "volume"]
    assert str(df["waktu"].dt.tz) == "UTC" and df["waktu"].iloc[0] == pd.Timestamp("2026-11-03 02:00", tz="UTC")
    assert yahoo.normalize(pd.DataFrame()).empty


def test_rencana_permintaan_sesuai_batas_yahoo():
    now = datetime(2026, 11, 5, 12, tzinfo=timezone.utc)
    p = yahoo.plan("1m", None, now)
    assert len(p) == 5 and all((e - s) <= timedelta(days=7) for s, e, _ in p)       # 30 hari dipotong per 7 hari
    assert p[0][0] == now - timedelta(days=29)
    p = yahoo.plan("1m", pd.Timestamp("2026-11-04 08:00", tz="UTC"), now)
    assert len(p) == 1 and p[0][0] == datetime(2026, 11, 3, 8, tzinfo=timezone.utc)   # lanjut dari data terakhir
    assert yahoo.plan("1d", None, now) == [(None, None, "max")]


def test_yahoo_menyimpan_per_bulan_dan_tidak_dobel(cfg):
    calls = []

    def fetch(simbol, interval, start, end, period, prepost):
        calls.append((simbol, interval, period))
        if simbol == "^VIX" and interval == "1m":
            return pd.DataFrame()
        if interval == "1d":
            return fake_bars(datetime(2026, 10, 30, tzinfo=timezone.utc), 5, "1D")
        return fake_bars(datetime(2026, 10, 31, 23, 58, tzinfo=timezone.utc), 4)   # menyeberang ke November

    h = yahoo.run(cfg, fetch=fetch)
    root = cfg.folder_data
    files = sorted(p.name for p in (root / "yahoo" / "1m" / "BBCA.JK").glob("*.parquet"))
    assert files == ["2026-10.parquet", "2026-11.parquet"]
    assert (root / "yahoo" / "1d" / "_VIX.parquet").exists()
    assert h["kosong"] == ["^VIX 1m"] and not h["gagal"]
    assert len([c for c in calls if c[0] == "BBCA.JK"]) > 0
    before = h["baris_baru"]
    h2 = yahoo.run(cfg, fetch=fetch)                       # jalan lagi: tidak ada baris baru
    assert h2["baris_baru"] == 0 and before > 0
    df = cli.load_range(root, "yahoo/1m", "BBCA.JK", None, None)
    assert len(df) == 4 and df["waktu"].is_monotonic_increasing


def test_yahoo_satu_simbol_error_tidak_menghentikan(cfg):
    def fetch(simbol, *a):
        if simbol == "BBCA.JK":
            raise RuntimeError("diblokir")
        return fake_bars(datetime(2026, 11, 3, tzinfo=timezone.utc), 2, "1D")
    h = yahoo.run(cfg, interval_filter=["1d"], fetch=fetch)
    assert "BBCA.JK 1d" in h["gagal"] and h["berhasil"] == 1


# ---------------------------------------------------------------- dukascopy
def bi5(records):
    raw = b"".join(struct.pack(">3I2f", *r) for r in records)
    return lzma.compress(raw, format=lzma.FORMAT_ALONE)


def test_url_bulan_dimulai_dari_nol():
    assert dukascopy.url_for("EURUSD", date(2026, 1, 5), 7).endswith("/EURUSD/2026/00/05/07h_ticks.bi5")


def test_decode_tick():
    blob = bi5([(1500, 108512, 108505, 1.5, 2.25), (3600_000 - 1, 108520, 108510, 0.75, 1.0)])
    df = dukascopy.decode_hour(blob, datetime(2026, 11, 3, 10, tzinfo=timezone.utc), 100000)
    assert df["waktu"].iloc[0] == pd.Timestamp("2026-11-03 10:00:01.500", tz="UTC")
    assert df["ask"].iloc[0] == pytest.approx(1.08512) and df["bid"].iloc[0] == pytest.approx(1.08505)
    assert df["bid_vol"].iloc[0] == 2.25
    assert dukascopy.decode_hour(b"", datetime(2026, 11, 3, tzinfo=timezone.utc), 1e5).empty
    assert dukascopy.pembagi("USDJPY", {}) == 1000 and dukascopy.pembagi("EURUSD", {}) == 100000


def test_tick_harian_lewati_yang_ada_dan_hari_gagal(cfg):
    kemarin = datetime.now(timezone.utc).date() - timedelta(days=1)
    seen = []

    def get(url):
        seen.append(url)
        if "USDJPY" in url and "/05h_" in url:
            raise ConnectionError("putus")
        if "/10h_" in url:
            return bi5([(0, 151234, 151220, 1, 1)] if "JPY" in url else [(0, 108512, 108505, 1, 1)])
        return b""
    h = dukascopy.run(cfg, get=get)
    assert h["hari_baru"] == 2 and len(h["gagal"]) == 2                 # EURUSD 2 hari ok, USDJPY 2 hari gagal
    df = pd.read_parquet(dukascopy.day_path(cfg.folder_data, "EURUSD", kemarin))
    assert len(df) == 1 and df["ask"].iloc[0] == pytest.approx(1.08512)
    n = len(seen)
    h2 = dukascopy.run(cfg, simbol_filter=["EURUSD"], get=get)
    assert h2["dilewati"] == 2 and len(seen) == n                        # tidak diunduh ulang
    assert not any(str(datetime.now(timezone.utc).date()) in str(p) for p in (cfg.folder_data / "tick").rglob("*.parquet"))


# ---------------------------------------------------------------- idx
SAHAM = {"draw": 0, "recordsTotal": 2, "data": [
    {"No": 1, "Date": "2026-11-04T00:00:00", "StockCode": "BBCA ", "StockName": "Bank Central Asia Tbk.", "Remarks": "",
     "Previous": 9800, "OpenPrice": 9825, "High": 9900, "Low": 9775, "Close": 9850, "Change": 50, "Volume": 100_000_000,
     "Value": 985_000_000_000, "Frequency": 25_000, "ForeignSell": 30_000_000, "ForeignBuy": 45_000_000,
     "ListedShares": 123_275_050_000, "NonRegularVolume": 500_000},
    {"No": 2, "Date": "2026-11-04T00:00:00", "StockCode": "AALI", "StockName": "Astra Agro Lestari Tbk.", "Remarks": "",
     "Previous": 6000, "OpenPrice": 0, "High": 0, "Low": 0, "Close": 6000, "Change": 0, "Volume": 0, "Value": 0,
     "Frequency": 0, "ForeignSell": 0, "ForeignBuy": 0},
]}
BROKER = {"data": [
    {"Date": "2026-11-04T00:00:00", "IDFirm": "YP", "FirmName": "Mirae Asset Sekuritas Indonesia", "Volume": 9_000_000, "Value": 5e11, "Frequency": 90000},
    {"Date": "2026-11-04T00:00:00", "IDFirm": "CC", "FirmName": "Mandiri Sekuritas", "Volume": 8_000_000, "Value": 7e11, "Frequency": 70000},
]}


def test_parse_ringkasan_saham_asing_domestik():
    df = idx.parse_saham(SAHAM, date(2026, 11, 4))
    b = df[df["kode"] == "BBCA"].iloc[0]
    assert b["asing_bersih"] == 15_000_000
    assert b["domestik_beli"] == 55_000_000 and b["domestik_jual"] == 70_000_000 and b["domestik_bersih"] == -15_000_000
    assert b["porsi_asing_pct"] == 37.5
    assert pd.isna(df[df["kode"] == "AALI"].iloc[0]["porsi_asing_pct"])
    assert df["tanggal"].iloc[0] == pd.Timestamp("2026-11-04")


def test_parse_broker_urut_nilai():
    df = idx.parse_broker(BROKER, date(2026, 11, 4))
    assert list(df["kode_broker"]) == ["CC", "YP"]


def test_idx_run_libur_dan_hari_ini(cfg):
    now = datetime(2026, 11, 5, 18, 30)            # Kamis
    def get_json(url):
        if "date=20261103" in url:
            return {"data": []}                    # libur
        if "date=20261105" in url:
            return {"data": []}                    # hari ini belum terbit
        return SAHAM if "Stock" in url else BROKER
    h = idx.run(cfg, get_json=get_json, now=now)
    root = cfg.folder_data
    assert idx.path_for(root, "ringkasan_saham", date(2026, 11, 4)).exists()
    assert idx.path_for(root, "ringkasan_saham", date(2026, 11, 3)).exists()       # ditandai libur
    assert not idx.path_for(root, "ringkasan_saham", date(2026, 11, 5)).exists()   # dicoba lagi nanti
    assert h["hari_baru"] == 2 and len(h["libur_atau_belum_terbit"]) == 4
    df = cli.load_range(root, "idx/ringkasan_saham", "BBCA", None, None)
    assert len(df) == 1 and df["asing_bersih"].iloc[0] == 15_000_000


def test_idx_cloudflare_dicatat_sebagai_gagal(cfg):
    def get_json(url):
        raise ValueError("balasan bukan JSON (mungkin diblokir Cloudflare)")
    h = idx.run(cfg, get_json=get_json, now=datetime(2026, 11, 5, 18))
    assert h["hari_baru"] == 0 and len(h["gagal"]) == 6


# ---------------------------------------------------------------- lainnya
def test_jadwal_berikutnya():
    tz = timezone(timedelta(hours=7))
    assert cli.next_run(datetime(2026, 11, 5, 10, 0, tzinfo=tz), "18:30") == datetime(2026, 11, 5, 18, 30, tzinfo=tz)
    assert cli.next_run(datetime(2026, 11, 5, 19, 0, tzinfo=tz), "18:30") == datetime(2026, 11, 6, 18, 30, tzinfo=tz)


def test_status_dan_ekspor(cfg, tmp_path, capsys):
    storage.save_yahoo(cfg.folder_data, "1d", "^VIX", yahoo.normalize(fake_bars(datetime(2026, 11, 2, tzinfo=timezone.utc), 3, "1D")))
    s = storage.summarize(cfg.folder_data)
    assert s.iloc[0]["simbol"] == "_VIX" and s.iloc[0]["file"] == 1
    cfg_file = tmp_path / "c.toml"
    cfg_file.write_text(f'[umum]\nfolder_data = "{cfg.folder_data.as_posix()}"\n', encoding="utf-8")
    out = tmp_path / "vix.csv"
    assert cli.main(["--config", str(cfg_file), "ekspor", "yahoo/1d", "^VIX", "--zona", "Asia/Jakarta", "--ke", str(out)]) == 0
    csv = pd.read_csv(out)
    assert len(csv) == 3 and "+07:00" in csv["waktu"].iloc[0]
