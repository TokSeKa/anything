# Pengumpul Data Pasar

Script yang setiap hari mengunduh dan merapikan data pasar ke folder `data/` (format Parquet, bisa diekspor ke CSV):

| Data | Sumber | Detail terkecil | Catatan |
|---|---|---|---|
| Saham IDX (±50 saham besar) | Yahoo Finance | **1 menit** | Yahoo hanya menyimpan 1 menit ±30 hari ke belakang → jalankan rutin supaya arsipmu terus bertambah |
| Saham & ETF US | Yahoo Finance | 1 menit | pre/after market opsional |
| Indeks: **VIX**, IHSG, S&P 500, Nasdaq 100, DXY, yield 10Y, Nikkei, Hang Seng | Yahoo Finance | 1 menit | |
| Forex (termasuk USD/IDR) | Yahoo Finance | 1 menit | |
| Futures komoditas: emas, perak, minyak WTI & Brent, gas, tembaga, platina, jagung, kedelai, kopi | Yahoo Finance | 1 menit | |
| **Tick** forex & logam (EURUSD, USDJPY, XAUUSD, …) | Dukascopy | **per tick** (bid/ask/volume, milidetik) | histori bertahun-tahun, bisa diisi mundur |
| **Asing vs domestik** semua emiten IDX | idx.co.id | harian | beli/jual/bersih asing & domestik, porsi asing, OHLC, volume, nilai, frekuensi |
| Ringkasan **broker** IDX | idx.co.id | harian | total volume/nilai/frekuensi tiap broker di seluruh pasar |

Interval yang disimpan untuk Yahoo: `1m`, `5m`, `1h`, `1d` (bisa diubah). Timeframe lain (15m, 4h, mingguan) tinggal dihitung dari data 1 menit.

**Yang tidak tersedia gratis:** tick (setiap transaksi) saham IDX & US, dan rincian broker apa membeli saham apa
(broker summary per emiten, seperti di Stockbit/RTI). Keduanya hanya ada di layanan berbayar atau di balik login,
dan mengambilnya diam-diam melanggar ketentuan layanan itu, jadi tidak dibuat di sini.

---

## Cara pakai di laptop Windows

1. Pasang **Python 3.11 atau lebih baru** dari https://www.python.org/downloads/ (centang **Add python.exe to PATH**).
2. Ambil repo ini (GitHub Desktop → Clone).
3. Klik dua kali **`scripts\coba-sekarang.bat`**. Pertama kali akan memasang library (beberapa menit), lalu mengunduh data dan
   menampilkan ringkasan isi folder `data`.
4. Supaya otomatis tiap hari: klik dua kali **`scripts\pasang-jadwal-windows.bat`** (default jam 18:30, setelah ringkasan IDX terbit).
   Kalau laptop sedang mati pada jamnya, tugas langsung dijalankan begitu laptop menyala.
   Jam lain: jalankan dari Command Prompt `scripts\pasang-jadwal-windows.bat 20:00`. Hapus jadwal: `scripts\hapus-jadwal-windows.bat`.

Pengambilan pertama paling lama (1 menit × 30 hari × semua simbol). Setelah itu tiap hari hanya mengambil yang baru.

## Cara pakai dengan Docker (laptop dengan Docker Desktop, atau VPS)

```bash
docker compose up -d --build
```

Container jalan terus: langsung mengumpulkan sekali, lalu setiap hari pada `jam_harian` (WIB). Data ada di folder `./data`.
Lihat lognya:

```bash
docker logs -f pengumpul-data
```

## Linux / Mac tanpa Docker

```bash
./scripts/jalankan-harian.sh
```

Otomatis tiap hari jam 18:30 lewat cron (`crontab -e`, ganti path-nya):

```
30 18 * * * /path/ke/pengumpul-data/scripts/jalankan-harian.sh >> /path/ke/pengumpul-data/data/cron.log 2>&1
```

---

## Mengatur apa yang dikumpulkan

Semua daftar ada di **`simbol.toml`**: saham IDX (pakai akhiran `.JK`), saham US, indeks, forex, komoditas, kripto, simbol tick
Dukascopy, interval, jam jadwal. Kalau mau versi pribadi tanpa mengubah file asli, salin jadi `simbol.lokal.toml` (otomatis dipakai).

Kode simbol Yahoo bisa dicari di https://finance.yahoo.com (misalnya `^VIX`, `GC=F` emas, `USDIDR=X`).

## Perintah lain

| Perintah | Kegunaan |
|---|---|
| `python -m pengumpul harian` | semua sumber yang aktif |
| `python -m pengumpul yahoo --simbol BBCA.JK TLKM.JK --interval 1m` | sebagian simbol/interval saja |
| `python -m pengumpul tick --dari 2025-01-01 --sampai 2025-12-31 --simbol XAUUSD` | isi tick mundur (setahun ±8.700 file per simbol, butuh waktu) |
| `python -m pengumpul idx --dari 2026-10-01` | isi ringkasan IDX yang terlewat |
| `python -m pengumpul status` | apa saja yang sudah tersimpan |
| `python -m pengumpul ekspor yahoo/1m BBCA.JK --zona Asia/Jakarta --ke bbca_1m.csv` | ekspor ke CSV |
| `python -m pengumpul ekspor idx/ringkasan_saham BBCA --ke bbca_asing.csv` | riwayat asing/domestik satu emiten |
| `python -m pengumpul ekspor idx/ringkasan_saham semua --dari 2026-11-01` | semua emiten |

Di Windows, jalankan dari folder repo dengan `.venv\Scripts\python -m pengumpul ...`.

## Struktur data

```
data/
  yahoo/1m/BBCA.JK/2026-11.parquet      bar 1 menit, satu file per bulan
  yahoo/1d/_VIX.parquet                 bar harian (karakter ^ diganti _)
  tick/dukascopy/XAUUSD/2026-11-04.parquet
  idx/ringkasan_saham/2026-11-04.parquet
  idx/ringkasan_broker/2026-11-04.parquet
  log/2026-11-05.log                    log tiap hari
  status.json                           hasil jalan terakhir tiap sumber
```

- Semua kolom `waktu` dalam **UTC** (ubah saat ekspor dengan `--zona Asia/Jakarta`).
- Bar Yahoo: `open, high, low, close, adj_close, volume`. Volume saham IDX dalam lembar.
- Tick: `bid, ask, bid_vol, ask_vol` (volume dalam satuan juta dari Dukascopy).
- Ringkasan saham IDX: `asing_beli`, `asing_jual`, `asing_bersih`, `domestik_beli`, `domestik_jual`, `domestik_bersih` (lembar),
  `porsi_asing_pct`, `asing_bersih_nilai_kira` (asing bersih × harga tutup), dan kolom asli IDX lainnya.
- Hari libur bursa ditandai file kosong supaya tidak diminta ulang.

Membaca di Python:

```python
import pandas as pd
df = pd.read_parquet("data/yahoo/1m/BBCA.JK")                     # semua bulan sekaligus
bar_15m = df.set_index("waktu").resample("15min").agg({"open": "first", "high": "max", "low": "min", "close": "last", "volume": "sum"}).dropna()
asing = pd.read_parquet("data/idx/ringkasan_saham")                 # semua hari, semua emiten
```

## Kalau ada masalah

| Gejala di log | Penyebab & solusi |
|---|---|
| `idx ... gagal: 403` atau `bukan JSON (mungkin diblokir Cloudflare)` | Situs IDX memblokir permintaan otomatis. Coba lagi nanti; library `curl_cffi` sudah dipakai untuk meniru browser. Kalau terus gagal, kabari untuk disesuaikan. |
| `yahoo ... gagal: Too Many Requests` | Kebanyakan permintaan. Naikkan `jeda_permintaan` di `simbol.toml` (misalnya 1.0). |
| `yahoo ... tidak ada data` | Kode simbol salah atau sudah delisting. |
| Harga tick aneh (terlalu besar/kecil 10×, 100×) | Tambahkan pembagi yang benar di `[dukascopy.pembagi]`. |

Script tidak berhenti kalau satu sumber/simbol gagal; semuanya dicoba lagi pada jalan berikutnya. Kode keluar 0 = semua berhasil,
1 = ada yang gagal (detail di `data/log`).

## Tes

```bash
pip install pytest && python -m pytest -q
```

Tes memakai data contoh yang dibuat sesuai format asli tiap sumber (tanpa internet).

Data ini untuk keperluan pribadi. Patuhi ketentuan penggunaan masing-masing sumber data.
