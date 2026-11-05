"""Perintah:

    python -m pengumpul harian                 semua sumber yang aktif (dipakai jadwal harian)
    python -m pengumpul yahoo [--simbol BBCA.JK ...] [--interval 1m ...]
    python -m pengumpul tick  [--dari 2026-10-01] [--sampai 2026-10-31] [--simbol EURUSD ...]
    python -m pengumpul idx   [--dari 2026-10-01] [--sampai 2026-10-31]
    python -m pengumpul status                 isi folder data
    python -m pengumpul ekspor yahoo/1m BBCA.JK [--dari 2026-10-01] [--ke hasil.csv]
    python -m pengumpul jadwal                 jalan terus, menjalankan `harian` tiap hari (Docker/VPS)
"""

from __future__ import annotations

import argparse
import logging
import sys
import time
from datetime import date, datetime, timedelta
from pathlib import Path
from zoneinfo import ZoneInfo

import pandas as pd

from . import config, dukascopy, idx, storage, yahoo

log = logging.getLogger("pengumpul")


def setup_logging(root: Path, verbose: bool = False) -> Path:
    (root / "log").mkdir(parents=True, exist_ok=True)
    path = root / "log" / f"{date.today().isoformat()}.log"
    fmt = logging.Formatter("%(asctime)s %(levelname)s %(message)s", "%H:%M:%S")
    log.handlers.clear()
    log.setLevel(logging.DEBUG if verbose else logging.INFO)
    fh = logging.FileHandler(path, encoding="utf-8")
    fh.setFormatter(fmt)
    sh = logging.StreamHandler(sys.stdout)
    sh.setFormatter(fmt)
    log.addHandler(fh)
    log.addHandler(sh)
    for noisy in ("yfinance", "urllib3", "peewee"):
        logging.getLogger(noisy).setLevel(logging.CRITICAL)
    return path


def _date(s: str | None) -> date | None:
    return date.fromisoformat(s) if s else None


def ringkas(nama: str, h: dict) -> str:
    gagal = h.get("gagal", {})
    bagian = [f"{k}={v}" for k, v in h.items() if isinstance(v, (int, float)) and k != "gagal"]
    if h.get("kosong"):
        bagian.append(f"kosong={len(h['kosong'])}")
    if h.get("libur_atau_belum_terbit"):
        bagian.append(f"libur/belum_terbit={len(h['libur_atau_belum_terbit'])}")
    bagian.append(f"gagal={len(gagal)}")
    return f"{nama}: " + ", ".join(bagian)


def cmd_harian(cfg) -> int:
    mulai = time.time()
    laporan, ada_gagal = [], False
    for nama, aktif, fn in (("idx", cfg.idx_aktif, lambda: idx.run(cfg)),
                            ("tick dukascopy", cfg.duka_aktif, lambda: dukascopy.run(cfg)),
                            ("yahoo", cfg.yahoo_aktif, lambda: yahoo.run(cfg))):
        if not aktif:
            continue
        log.info("=== %s ===", nama)
        try:
            h = fn()
        except Exception as e:  # noqa: BLE001 - satu sumber rusak tidak menghentikan yang lain
            log.exception("%s berhenti: %s", nama, e)
            laporan.append(f"{nama}: ERROR {e}")
            ada_gagal = True
            continue
        laporan.append(ringkas(nama, h))
        if h.get("gagal"):
            ada_gagal = True
            for k, v in list(h["gagal"].items())[:10]:
                log.warning("  gagal %s: %s", k, v)
    log.info("=== selesai dalam %.0f detik ===", time.time() - mulai)
    for line in laporan:
        log.info(line)
    return 1 if ada_gagal else 0


def next_run(now: datetime, jam: str) -> datetime:
    hh, mm = (int(x) for x in jam.split(":"))
    target = now.replace(hour=hh, minute=mm, second=0, microsecond=0)
    return target if target > now else target + timedelta(days=1)


def cmd_jadwal(cfg, args) -> int:
    tz = ZoneInfo(cfg.zona_waktu)
    if args.sekarang:
        cmd_harian(cfg)
    while True:
        now = datetime.now(tz)
        nxt = next_run(now, cfg.jam_harian)
        log.info("jalan berikutnya: %s", nxt.strftime("%Y-%m-%d %H:%M %Z"))
        while datetime.now(tz) < nxt:
            time.sleep(min(60, max(1, (nxt - datetime.now(tz)).total_seconds())))
        setup_logging(cfg.folder_data, args.verbose)   # file log baru tiap hari
        cmd_harian(cfg)


def load_range(root: Path, sumber: str, simbol: str, dari: date | None, sampai: date | None) -> pd.DataFrame:
    """Baca data satu simbol dari folder mana pun (yahoo/<interval>, tick, idx)."""
    files: list[Path]
    if sumber.startswith("yahoo/"):
        base = root / sumber
        d = base / storage.safe_name(simbol)
        files = sorted(d.glob("*.parquet")) if d.is_dir() else [base / f"{storage.safe_name(simbol)}.parquet"]
    elif sumber in ("tick", "tick/dukascopy"):
        files = sorted((root / "tick" / "dukascopy" / storage.safe_name(simbol)).glob("*.parquet"))
    elif sumber in ("idx/ringkasan_saham", "idx/ringkasan_broker"):
        files = sorted((root / sumber).glob("*.parquet"))
    else:
        raise SystemExit(f"sumber tidak dikenal: {sumber}. Contoh: yahoo/1m, yahoo/1d, tick, idx/ringkasan_saham")
    files = [f for f in files if f.exists()]
    if sumber.startswith(("tick", "idx")) and (dari or sampai):
        files = [f for f in files if (not dari or f.stem >= dari.isoformat()) and (not sampai or f.stem <= sampai.isoformat())]
    if not files:
        raise SystemExit("data tidak ditemukan. Cek dengan: python -m pengumpul status")
    df = pd.concat([pd.read_parquet(f) for f in files], ignore_index=True)
    if sumber.startswith("idx/") and simbol not in ("", "*", "semua"):
        kol = "kode" if "kode" in df else "kode_broker"
        df = df[df[kol] == simbol]
    if "waktu" in df and (dari or sampai):
        if dari:
            df = df[df["waktu"] >= pd.Timestamp(dari, tz="UTC")]
        if sampai:
            df = df[df["waktu"] < pd.Timestamp(sampai + timedelta(days=1), tz="UTC")]
    return df.reset_index(drop=True)


def cmd_ekspor(cfg, args) -> int:
    df = load_range(cfg.folder_data, args.sumber, args.simbol, _date(args.dari), _date(args.sampai))
    if args.zona and "waktu" in df:
        df["waktu"] = df["waktu"].dt.tz_convert(args.zona)
    out = Path(args.ke) if args.ke else Path(f"{args.sumber.replace('/', '_')}_{storage.safe_name(args.simbol)}.csv")
    if out.suffix.lower() == ".parquet":
        df.to_parquet(out, index=False)
    else:
        df.to_csv(out, index=False)
    print(f"{len(df)} baris → {out}")
    return 0


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="pengumpul", description="Pengumpul data pasar harian")
    p.add_argument("--config", help="file konfigurasi (default simbol.lokal.toml atau simbol.toml)")
    p.add_argument("-v", "--verbose", action="store_true")
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("harian", help="jalankan semua sumber yang aktif")
    y = sub.add_parser("yahoo", help="bar menit/jam/harian dari Yahoo")
    y.add_argument("--simbol", nargs="*")
    y.add_argument("--interval", nargs="*")
    t = sub.add_parser("tick", help="tick Dukascopy (forex & komoditas)")
    t.add_argument("--dari")
    t.add_argument("--sampai")
    t.add_argument("--simbol", nargs="*")
    t.add_argument("--ulang", action="store_true", help="unduh ulang walaupun sudah ada")
    i = sub.add_parser("idx", help="ringkasan saham (asing/domestik) & broker IDX")
    i.add_argument("--dari")
    i.add_argument("--sampai")
    i.add_argument("--ulang", action="store_true")
    sub.add_parser("status", help="ringkasan isi folder data")
    e = sub.add_parser("ekspor", help="ekspor ke CSV/Parquet")
    e.add_argument("sumber", help="yahoo/1m, yahoo/5m, yahoo/1d, tick, idx/ringkasan_saham, idx/ringkasan_broker")
    e.add_argument("simbol", help="misalnya BBCA.JK, ^VIX, EURUSD; untuk idx: kode emiten/broker atau 'semua'")
    e.add_argument("--dari")
    e.add_argument("--sampai")
    e.add_argument("--zona", help="ubah zona waktu kolom waktu, misalnya Asia/Jakarta")
    e.add_argument("--ke", help="nama file hasil (.csv atau .parquet)")
    j = sub.add_parser("jadwal", help="jalan terus dan menjalankan `harian` tiap hari pada jam_harian")
    j.add_argument("--sekarang", action="store_true", help="langsung jalan sekali saat mulai")
    args = p.parse_args(argv)

    cfg = config.load(args.config)
    if args.cmd in ("status", "ekspor"):
        if args.cmd == "status":
            df = storage.summarize(cfg.folder_data)
            if df.empty:
                print(f"Belum ada data di {cfg.folder_data}. Jalankan: python -m pengumpul harian")
            else:
                with pd.option_context("display.max_rows", 500, "display.width", 200):
                    print(df.to_string(index=False))
            return 0
        return cmd_ekspor(cfg, args)

    path = setup_logging(cfg.folder_data, args.verbose)
    log.info("data: %s · log: %s", cfg.folder_data, path)
    if args.cmd == "harian":
        return cmd_harian(cfg)
    if args.cmd == "yahoo":
        h = yahoo.run(cfg, args.simbol, args.interval)
    elif args.cmd == "tick":
        h = dukascopy.run(cfg, _date(args.dari), _date(args.sampai), [s.upper() for s in args.simbol] if args.simbol else None,
                          ulang=args.ulang)
    elif args.cmd == "idx":
        h = idx.run(cfg, _date(args.dari), _date(args.sampai), ulang=args.ulang)
    else:
        return cmd_jadwal(cfg, args)
    log.info(ringkas(args.cmd, h))
    return 1 if h.get("gagal") else 0
