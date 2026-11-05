@echo off
rem Mengumpulkan data harian. Dipakai juga oleh jadwal Windows (Task Scheduler).
rem Pertama kali: membuat .venv dan memasang library otomatis.
setlocal
cd /d "%~dp0.."

if not exist ".venv\Scripts\python.exe" (
  echo [pengumpul] Menyiapkan Python virtual env pertama kali...
  where py >nul 2>nul && (py -3 -m venv .venv) || (python -m venv .venv)
  if not exist ".venv\Scripts\python.exe" (
    echo [pengumpul] Python 3.11+ belum terpasang. Pasang dari https://www.python.org/downloads/
    echo             dan centang "Add python.exe to PATH" saat instalasi, lalu jalankan lagi.
    exit /b 2
  )
  ".venv\Scripts\python.exe" -m pip install --upgrade pip >nul
)
".venv\Scripts\python.exe" -m pip install -q -r requirements.txt
if errorlevel 1 (
  echo [pengumpul] Gagal memasang library. Cek koneksi internet.
  exit /b 3
)
".venv\Scripts\python.exe" -m pengumpul harian %*
exit /b %errorlevel%
