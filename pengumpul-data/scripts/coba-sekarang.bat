@echo off
rem Klik dua kali untuk menjalankan pengumpulan data sekarang dan melihat hasilnya.
call "%~dp0jalankan-harian.bat"
echo.
echo Selesai (kode %errorlevel%: 0 = semua berhasil, 1 = ada yang gagal, lihat log di folder data\log).
cd /d "%~dp0.."
".venv\Scripts\python.exe" -m pengumpul status
pause
