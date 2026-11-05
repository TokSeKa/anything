#!/usr/bin/env sh
# Build di Linux/Mac dengan Mono (hasilnya .exe yang jalan di Windows).
set -e
cd "$(dirname "$0")"
mkdir -p dist
mcs -langversion:5 -target:winexe -optimize+ -out:dist/GudangKalimat.exe -win32icon:src/ikon.ico \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.IO.Compression.dll -r:System.Xml.dll -r:System.Core.dll \
  src/*.cs
echo "OK: dist/GudangKalimat.exe"
