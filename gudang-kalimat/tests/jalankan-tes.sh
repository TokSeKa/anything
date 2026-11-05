#!/usr/bin/env sh
# Tes logika data dengan Mono (tanpa jendela).
set -e
cd "$(dirname "$0")/.."
mkdir -p /tmp/gk-tes
mcs -langversion:5 -out:/tmp/gk-tes/Tes.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.IO.Compression.dll \
  -r:System.Xml.dll -r:System.Core.dll src/Model.cs src/Csv.cs src/Xlsx.cs src/Pengaturan.cs src/TeksArt.cs tests/Tes.cs
mono /tmp/gk-tes/Tes.exe tests
