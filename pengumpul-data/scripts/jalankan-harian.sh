#!/usr/bin/env sh
# Mengumpulkan data harian (Linux/Mac). Untuk otomatis, tambahkan ke crontab (lihat README).
set -e
cd "$(dirname "$0")/.."
if [ ! -x .venv/bin/python ]; then
  echo "[pengumpul] Menyiapkan virtual env pertama kali..."
  python3 -m venv .venv
fi
.venv/bin/python -m pip install -q -r requirements.txt
exec .venv/bin/python -m pengumpul harian "$@"
