# Gudang Kalimat

Aplikasi Windows mungil (±85 KB, tanpa instalasi, jalan offline) untuk menyimpan kalimat yang sering dipakai,
lalu menyalinnya ke clipboard dengan sekali klik: balasan chat, template email, ucapan, kata-kata mutiara, apa saja.

- **Daftar kalimat** langsung tampil saat dibuka, lengkap dengan siapa yang menulis (**Dari**) dan **Kategori**.
- **📋 Salin**: pilih kalimat (atau klik dua kali / tekan Enter), kalimat tampil di kotak bawah dan tersalin.
  Kotak bawah boleh diedit dulu sebelum disalin.
- **🎲 Acak + Salin**: ambil satu kalimat acak (dari hasil pencarian/filter yang sedang tampil) dan langsung tersalin.
- **Cari**: ketik beberapa kata sekaligus; filter per orang dan per kategori.
- **Tambah, Tambah banyak, Ubah, Hapus**. "Tambah banyak" = tempel puluhan kalimat sekaligus.
- **Impor/Ekspor** Excel (.xlsx), CSV, dan TXT (satu kalimat per baris).
- **Ramai-ramai**: lewat folder bersama (Google Drive/OneDrive/Dropbox) atau Google Sheets. Lihat di bawah.
- **🖼 Gambar → Teks** (Ctrl+G): ubah gambar (file, tempel dari clipboard, atau seret ke jendela) atau tulisan menjadi
  gambar dari huruf untuk ditempel di chat. Gaya braille ⣿ (paling detail), blok █▓▒░, atau ASCII; atur lebar, ketebalan,
  kontras, gradasi, balik warna. Hasilnya bisa disalin, disimpan sebagai kalimat (kategori "Gambar"), atau disimpan .txt.
- **Ctrl+Alt+K** dari aplikasi mana saja memunculkan Gudang Kalimat; opsi "kecilkan setelah menyalin" supaya bisa langsung tempel.

## Unduh & jalankan

1. Unduh **[`unduh/GudangKalimat.zip`](unduh/GudangKalimat.zip)** (di halaman file itu, tekan tombol unduh / *Download raw file*),
   lalu ekstrak (klik kanan → Extract All).
2. Klik dua kali **`GudangKalimat.exe`**.
   - Kalau muncul layar biru "Windows protected your PC": klik **More info → Run anyway** (aplikasi ini tidak bertanda tangan digital
     berbayar, jadi Windows belum mengenalnya).
3. Selesai. Kalimat tersimpan di **`kalimat.csv`** di folder yang sama, dan bisa dibuka/diedit langsung di Excel.

Butuh Windows 10/11 (atau Windows 7/8 dengan .NET Framework 4.5+). Tidak perlu internet, kecuali untuk Google Sheets.

## Mengedit kalimat lewat Excel

File data adalah tabel biasa dengan kolom **Kalimat, Dari, Kategori** (kolom Dibuat dan ID diisi otomatis, boleh dibiarkan).
Buka `kalimat.csv` di Excel, tambah/ubah baris, simpan. Aplikasi yang sedang terbuka langsung memuat perubahan.

Punya daftar kalimat di Excel sendiri? Tekan **Impor…** dan pilih file `.xlsx`/`.csv`. Kolom dikenali dari judulnya
(misalnya "Kalimat"/"Teks", "Dari"/"Penulis", "Kategori"/"Tag"); kalau tidak ada judul, kolom pertama dianggap kalimat.
Contoh format: [`contoh-kalimat.xlsx`](contoh-kalimat.xlsx).

## Dipakai ramai-ramai

Pilih salah satu, tidak perlu paham teknis:

**A. Folder bersama (semua orang bisa menambah dari aplikasi)**
1. Satu orang membuat folder di Google Drive (pakai aplikasi *Google Drive for desktop*), OneDrive, atau Dropbox, lalu membagikannya ke teman.
2. Di aplikasi: **Pengaturan → File data → Pilih…**, arahkan ke file `kalimat.csv` di folder itu. Teman memilih file yang sama.
3. Kalimat yang ditambah siapa pun muncul otomatis di aplikasi semua orang. Setiap simpan, aplikasi membaca versi terbaru dulu,
   jadi tambahan orang lain tidak tertimpa.

**B. Google Sheets (semua orang mengedit di browser/HP, aplikasi membaca)**
1. Buat Google Sheet dengan kolom **Kalimat, Dari, Kategori**, isi bersama-sama (bisa dari HP).
2. **Bagikan → Akses umum → "Siapa saja yang memiliki link" (Viewer)**, lalu salin link-nya.
3. Di aplikasi: **Pengaturan → Google Sheets**, tempel link, tekan **Tes link**, lalu Simpan.
4. Kalimat dari Sheet tampil dengan tanda ☁ dan diperbarui setiap aplikasi dibuka atau tombol **Sinkron** ditekan.
   Salinan terakhir disimpan, jadi tetap muncul saat offline. Mengubah/menghapus kalimat ☁ dilakukan di Google Sheets.

**C. Kirim file**: tekan **Ekspor…** → kirim `.xlsx` lewat WhatsApp/email → penerima tekan **Impor…** (kalimat kembar otomatis dilewati).

## Tips Gambar → Teks

- Untuk WhatsApp di HP, pakai lebar ±30–35 karakter. Kalau bentuknya bergeser, apit hasilnya dengan tiga backtick (```) supaya
  WhatsApp memakai huruf berlebar sama. Discord dan aplikasi di PC biasanya rapi tanpa itu.
- Gambar dengan latar polos dan kontras tinggi (logo, stiker, siluet) hasilnya paling bagus. Kalau terlalu gelap/terang,
  geser **Ketebalan** atau **Kontras**; untuk chat bertema gelap centang **Balik terang/gelap**.

## Pintasan keyboard

| Tombol | Fungsi |
|---|---|
| Ctrl+Alt+K | munculkan aplikasi dari mana saja |
| Ctrl+F | ke kotak cari · Enter di kotak cari = salin hasil pertama · Esc = kosongkan |
| ↓ lalu Enter | pilih dari daftar lalu salin |
| Ctrl+R | Acak + Salin |
| Ctrl+N / F2 / Delete | tambah / ubah / hapus |
| Ctrl+G | Gambar → Teks (di jendelanya: Ctrl+V tempel gambar, Ctrl+C salin hasil) |
| F5 | muat ulang (Sinkron) |

## Untuk pengembang

- Kode: C# WinForms (.NET Framework 4, sintaks C# 5) di `src/`. Tanpa library tambahan; xlsx dibaca/ditulis sendiri.
- Build di Windows: klik dua kali `build.bat` (memakai compiler bawaan Windows). Di Linux/Mac: `./build.sh` (Mono).
- Paket unduhan (`unduh/GudangKalimat.zip`) berisi `GudangKalimat.exe`, `contoh-kalimat.xlsx`, dan `BACA-SAYA.txt`.
- Tes logika data (CSV/XLSX/berbagi file/link Google Sheets): `./tests/jalankan-tes.sh` (Mono).
- Pengaturan disimpan di `%APPDATA%\GudangKalimat\pengaturan.txt`.
