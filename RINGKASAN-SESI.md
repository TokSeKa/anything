# Ringkasan Sesi Kerja (serah terima)

> **Untuk AI/sesi berikutnya:** file ini satu-satunya konteks yang dibawa. Baca semuanya dulu, lalu hubungkan (attach/clone)
> kedua repo di bawah sebelum bekerja. Pemilik proyek berbahasa **Indonesia**; jawab dan tulis UI/dokumen dalam bahasa Indonesia.
> Terakhir diperbarui: 2026-11-05.

## 1. Repo

| Repo | Branch kerja | Commit terakhir | Isi |
|---|---|---|---|
| https://github.com/TokSeKa/game-mabar-BTK | `suit-bot-arena` | `de1e9aa` | Suit Bot Arena, Kilas Pasar, Arena Mabar (11 game), dokumentasi, deploy |
| https://github.com/TokSeKa/anything | `gudang-kalimat` (berisi juga `pengumpul-data`) | `c6b5dc6` | pengumpul-data, gudang-kalimat, file ini |

- **Belum ada yang di-merge ke `main`** di kedua repo. Belum ada pull request (buat hanya kalau diminta).
- `anything`: branch `pengumpul-data` (`1bfed2f`) adalah bagian dari `gudang-kalimat`; cukup merge `gudang-kalimat`.
- `game-mabar-BTK` juga punya branch lama `prototype-visual-arena` (prototipe awal, sudah tidak dipakai).

Ambil repo di sesi baru:

```bash
git clone -b suit-bot-arena https://github.com/TokSeKa/game-mabar-BTK.git
```

```bash
git clone -b gudang-kalimat https://github.com/TokSeKa/anything.git
```

## 2. Tentang pemilik & aturan kerja

- Lulusan informatika, laptop **Windows**, memakai **GitHub Desktop** dan **Docker Desktop**; belum punya VPS (rencana nanti).
  Sering mengakses dari HP. Tujuan utama: dipakai sendiri/bareng teman ("untuk main aja"), gratis.
- Commit diakhiri baris:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` dan `Claude-Session: <link sesi>`.
  Jangan menaruh nama model di commit/PR/kode. Push ke branch kerja; jangan langsung ke `main`.
- Keputusan desain yang **wajib dipertahankan**:
  - Bot di semua game: **tanpa keacakan & tanpa memori antar langkah** ("syarat mutlak gak ada code untuk random").
    Keacakan hanya milik game, diturunkan dari seed.
  - Waktu manusia habis/offline → aksi bawaan.
  - Akun = username + password (SQLite `akun.db`), satu akun untuk semua game & liga.
  - Aturan Suit: sirkular; yang kalah −X HP (default 100, bisa diatur) per individu per ronde; HP hanya ke golongan yang mengalahkan;
    sisa pembagian +1 ke pemenang ber-HP terendah dulu, tidak dibuang; ingatan bot default 10 ronde (bisa diatur).
  - Kilas Pasar: kripto & IDX dulu lalu US, tanpa forex, spot saja, mode buta default.
- Yang **sengaja tidak dibuat**: aplikasi catatan/produktivitas (pemilik pakai Obsidian/Notion), tick saham IDX/US dan broker
  per emiten (tidak gratis; scraping Stockbit/RTI melanggar ketentuan mereka).

## 3. Produk yang sudah jadi

### 3.1 Suit Bot Arena (`game-mabar-BTK`, `#suit`)
Batu-gunting-kertas sirkular untuk 2–8 pemain; pemain memprogram bot (Blockly atau Python subset aman), replay animasi,
latihan melawan 11 bot contoh, panduan untuk AI (`docs/PANDUAN_BOT_LLM.md`).
Upgrade: **mode event** (Taruhan Ganda, Pikun, Kilat, Darah Tipis, Kabut, Kejutan), **turnamen 1 lawan 1** + klasemen/matriks,
**debugger langkah demi langkah** (`POST /api/debug`), **penonton + reaksi emoji**, **liga ELO** (`#liga`, game id `suit_arena`).

### 3.2 Kilas Pasar (`#pasar`)
Simulator trading candle asli (Binance untuk kripto, Yahoo untuk IDX/US), 13 timeframe, mode buta, order spot (market/limit/stop/OCO)
+ fee/slippage, indikator & alat gambar, mode main sendiri/tantangan harian/balapan, akun & papan peringkat.
Data diunduh dengan `scripts/unduh-data.*` (preset cepat/penuh/update/contoh).

### 3.3 Arena Mabar – 11 game (`#main`)
Suit Live, Poker Hold'em, Tenggelamkan Kapal, Arena Robot Grid, Petualangan Kartu Nusantara, Pasar Malam Tycoon, Lelang Rahasia,
Rebutan Lahan, Dilema Kas Desa, Angka Dua Pertiga, Pasar Malam Sepi.
Setiap kursi manusia **atau** bot (boleh campur), timer & aksi bawaan, penonton, reaksi, replay, latihan bot, panduan bot per game,
liga ELO per game, mode instan kalau semua bot. Seed dek poker bisa diverifikasi (hash diumumkan, seed dibuka di akhir).

### 3.4 Main lokal & deploy
- `scripts\main-lokal.bat` / `.sh`: Docker + Cloudflare Quick Tunnel → link publik dari laptop.
- `DEPLOY.md`: satu container (`suit-bot-arena`), Nginx/Caddy (subdomain/sub-path), volume data `pasar-data` (candle + `akun.db`).
- Dokumen: `README.md`, `docs/GAMES.md` (semua game & cara main), `docs/bot/*.md` (dibuat otomatis: `python -m app.games.gen_docs`).

### 3.5 pengumpul-data (`anything/pengumpul-data`)
Script Python harian: Yahoo 1m/5m/1h/1d (±50 saham IDX, saham/ETF US, VIX & indeks, forex, futures komoditas, kripto),
tick Dukascopy (forex & logam), IDX ringkasan saham (asing/domestik) & ringkasan broker. Parquet rapi + ekspor CSV, log & status.
Windows: `scripts\coba-sekarang.bat`, `scripts\pasang-jadwal-windows.bat` (Task Scheduler 18:30). Docker: `docker compose up -d`.
Daftar simbol di `simbol.toml`.

### 3.6 Gudang Kalimat (`anything/gudang-kalimat`)
Aplikasi Windows WinForms (.NET Framework 4, C# 5, ±85 KB, offline, tanpa instalasi): daftar kalimat (Kalimat/Dari/Kategori), cari,
📋 Salin, 🎲 Acak + Salin, tambah/tambah banyak/ubah/hapus, impor/ekspor xlsx/csv/txt, data di `kalimat.csv` (edit di Excel),
berbagi lewat folder Google Drive/OneDrive (muat otomatis, simpan dengan baca-ulang) atau link Google Sheets (baca + cache), Ctrl+Alt+K.
v1.1: **Gambar → Teks** (Ctrl+G): gambar/tulisan → teks braille/blok/ASCII untuk chat; salin, simpan jadi kalimat, atau .txt.
Unduhan siap pakai: `gudang-kalimat/unduh/GudangKalimat.zip`. Build: `build.bat` (Windows) / `build.sh` (Mono).

## 4. Status pengujian

| Produk | Sudah diuji | Belum diuji |
|---|---|---|
| Suit, Kilas, Arena Mabar | 190 tes backend lolos (`cd backend && pytest -q`), E2E Playwright semua game, image Docker dibangun & dijalankan | dimainkan teman sungguhan; deploy VPS nyata |
| pengumpul-data | 15 tes dengan data contoh; penanganan gagal saat jaringan ditolak; image Docker | **belum pernah mengambil data sungguhan** (sandbox menolak Yahoo, Dukascopy, IDX). Paling berisiko: endpoint & nama kolom JSON IDX (Cloudflare), kode simbol yang berganti |
| Gudang Kalimat | 34 tes (data + gambar→teks) dengan Mono; dijalankan di Xvfb: cari, acak+salin, tambah, muat ulang otomatis, gambar→teks dari file & tulisan, salin, simpan | **belum dijalankan di Windows asli**; Ctrl+Alt+K; Google Sheets sungguhan; tempel gambar dari clipboard (Mono tidak mendukung, Windows seharusnya bisa) |

## 5. Yang belum / langkah berikutnya

1. Pemilik mencoba di laptop: pengumpul-data (`coba-sekarang.bat`) dan Gudang Kalimat (unduh zip). Perbaiki dari isi `data\log` / pesan error.
2. Merge branch ke `main` (atau buat PR) setelah pemilik puas.
3. Gudang Kalimat: GitHub Release tidak bisa dibuat dari sesi cloud (403); kalau perlu, pemilik membuat Release manual dengan zip di `unduh/`.
4. Pemain sungguhan menguji keseimbangan game (kesulitan deck-builder, angka uang tycoon) yang baru disetel lewat simulasi bot.
5. Saat VPS siap: ikuti `DEPLOY.md`; opsional pindahkan pengumpul-data ke VPS (Docker).
6. Ide yang pernah ditawarkan tapi belum diminta: asisten Telegram (catat cepat, pengingat, rekap), brief pagi, alert pasar,
   "vault starter" Obsidian.

## 6. Catatan teknis untuk sesi cloud berikutnya

- Sandbox cloud **menolak** host `*.yahoo.com`, `datafeed.dukascopy.com`, `www.idx.co.id` (dan Google) kecuali diizinkan di
  pengaturan Network access environment. Package manager (pip/npm/apt) diizinkan.
- Docker di sandbox: jalankan `dockerd` manual; build butuh CA proxy (`/root/.ccr/ca-bundle.crt`) lewat Dockerfile tes sementara
  dan base image `mirror.gcr.io/library/...`. Jangan commit Dockerfile tes itu.
- `game-mabar-BTK`: backend FastAPI (Python 3.12), satu worker uvicorn (state room di memori), process pool untuk match;
  frontend React+Vite (`cd frontend && npm run build`). Tes: `cd backend && pytest -q`.
- `gudang-kalimat`: kompilasi dengan `mcs -langversion:5` (Mono, `apt-get install mono-devel`); tes `./tests/jalankan-tes.sh`.
  Jaga kode tetap C# 5 supaya `build.bat` (compiler bawaan Windows) tetap bisa dipakai.
- Saat mematikan server tes, jangan `pkill -f` dengan pola yang juga cocok dengan perintah shell sendiri; pakai
  `pgrep -f "^/path/python3 /path/uvicorn"` lalu `kill`.
