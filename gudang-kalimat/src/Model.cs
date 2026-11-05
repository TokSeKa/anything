// Gudang Kalimat - model data dan penyimpanan file.
// Ditulis dengan C# 5 supaya bisa dikompilasi compiler bawaan Windows (csc .NET Framework 4).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GudangKalimat
{
    public class Kalimat
    {
        public string Id = "";
        public string Teks = "";
        public string Dari = "";
        public string Kategori = "";
        public string Dibuat = "";
        public bool Online;            // berasal dari Google Sheets (hanya bisa diubah di sana)

        public Kalimat Salin()
        {
            return (Kalimat)MemberwiseClone();
        }

        public static string IdDari(string teks, string dari)
        {
            using (var md5 = MD5.Create())
            {
                var h = md5.ComputeHash(Encoding.UTF8.GetBytes((teks ?? "").Trim() + "\u0001" + (dari ?? "").Trim()));
                return "k" + BitConverter.ToString(h, 0, 5).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string IdBaru()
        {
            return "k" + Guid.NewGuid().ToString("N").Substring(0, 10);
        }

        public static string Kunci(string teks)
        {
            return string.Join(" ", (teks ?? "").Trim().ToLowerInvariant().Split(new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries));
        }
    }

    /// <summary>Tabel sederhana: baris pertama = judul kolom. Dipakai bersama oleh CSV, XLSX, dan Google Sheets.</summary>
    public static class Tabel
    {
        static readonly string[] KolomTeks = { "kalimat", "teks", "text", "sentence", "isi", "quote", "kutipan" };
        static readonly string[] KolomDari = { "dari", "oleh", "penulis", "pengirim", "from", "author", "nama", "by" };
        static readonly string[] KolomKategori = { "kategori", "category", "tag", "label", "jenis", "topik" };
        static readonly string[] KolomId = { "id" };
        static readonly string[] KolomDibuat = { "dibuat", "tanggal", "created", "date", "waktu" };

        static int Cari(List<string> header, string[] nama)
        {
            for (int i = 0; i < header.Count; i++)
            {
                var h = (header[i] ?? "").Trim().ToLowerInvariant();
                if (nama.Contains(h)) return i;
            }
            return -1;
        }

        /// <summary>Ubah baris tabel menjadi kalimat. Kalau tidak ada judul kolom yang dikenal, kolom pertama = kalimat.</summary>
        public static List<Kalimat> KeKalimat(List<List<string>> rows, string dariBawaan)
        {
            var hasil = new List<Kalimat>();
            if (rows.Count == 0) return hasil;
            var header = rows[0];
            int iTeks = Cari(header, KolomTeks), iDari = Cari(header, KolomDari), iKat = Cari(header, KolomKategori);
            int iId = Cari(header, KolomId), iDibuat = Cari(header, KolomDibuat);
            int mulai = 1;
            if (iTeks < 0)
            {
                iTeks = 0;
                // tidak ada judul kolom: baris pertama juga data (misalnya daftar kalimat polos)
                if (iDari < 0 && iKat < 0 && iId < 0) mulai = 0;
            }
            for (int r = mulai; r < rows.Count; r++)
            {
                var row = rows[r];
                Func<int, string> ambil = i => (i >= 0 && i < row.Count) ? (row[i] ?? "").Trim() : "";
                var teks = ambil(iTeks);
                if (teks.Length == 0) continue;
                var k = new Kalimat();
                k.Teks = teks.Replace("\r\n", "\n").Replace("\n", "\r\n");
                k.Dari = ambil(iDari);
                if (k.Dari.Length == 0) k.Dari = dariBawaan ?? "";
                k.Kategori = ambil(iKat);
                k.Dibuat = ambil(iDibuat);
                k.Id = ambil(iId);
                if (k.Id.Length == 0) k.Id = Kalimat.IdDari(k.Teks, k.Dari);
                hasil.Add(k);
            }
            // ID kembar (misalnya baris disalin di Excel): beri ID baru supaya tetap bisa diubah satu-satu
            var seen = new HashSet<string>();
            foreach (var k in hasil)
            {
                if (!seen.Add(k.Id)) { k.Id = Kalimat.IdBaru(); seen.Add(k.Id); }
            }
            return hasil;
        }

        public static List<List<string>> DariKalimat(IEnumerable<Kalimat> items)
        {
            var rows = new List<List<string>>();
            rows.Add(new List<string> { "Kalimat", "Dari", "Kategori", "Dibuat", "ID" });
            foreach (var k in items)
                rows.Add(new List<string> { k.Teks, k.Dari, k.Kategori, k.Dibuat, k.Id });
            return rows;
        }

        /// <summary>Baca file apa pun yang didukung: .csv, .tsv, .txt (satu kalimat per baris), .xlsx.</summary>
        public static List<Kalimat> BacaFile(string path, string dariBawaan)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".xlsx") return KeKalimat(Xlsx.Baca(path), dariBawaan);
            var teks = BacaTeksBersama(path);
            if (ext == ".txt")
            {
                var rows = teks.Replace("\r\n", "\n").Split('\n')
                    .Where(l => l.Trim().Length > 0)
                    .Select(l => new List<string> { l.Trim() }).ToList();
                rows.Insert(0, new List<string> { "Kalimat" });
                return KeKalimat(rows, dariBawaan);
            }
            return KeKalimat(Csv.Parse(teks), dariBawaan);
        }

        public static void TulisFile(string path, IEnumerable<Kalimat> items)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".xlsx") { Xlsx.Tulis(path, DariKalimat(items)); return; }
            if (ext == ".txt")
            {
                File.WriteAllText(path, string.Join("\r\n", items.Select(k => k.Teks.Replace("\r\n", " "))) + "\r\n",
                    new UTF8Encoding(true));
                return;
            }
            File.WriteAllText(path, Csv.Tulis(DariKalimat(items), Csv.PemisahSistem()), new UTF8Encoding(true));
        }

        /// <summary>Baca teks walaupun file sedang dibuka Excel; deteksi UTF-8/UTF-16, kalau gagal pakai ANSI Windows.</summary>
        public static string BacaTeksBersama(string path)
        {
            byte[] data;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                data = ms.ToArray();
            }
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return Encoding.Unicode.GetString(data, 2, data.Length - 2);
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return Encoding.UTF8.GetString(data, 3, data.Length - 3);
            try
            {
                return new UTF8Encoding(false, true).GetString(data);
            }
            catch (DecoderFallbackException)
            {
                // CSV lama dari Excel (ANSI). Di Windows ini code page sistem, di sistem lain anggap Windows-1252.
                var cp = Encoding.Default.CodePage;
                var ansi = (cp == 65001 || cp == 20127) ? Encoding.GetEncoding(1252) : Encoding.Default;
                return ansi.GetString(data);
            }
        }
    }

    /// <summary>File data utama (CSV). Setiap perubahan membaca ulang file dulu lalu menerapkan perubahan
    /// berdasarkan ID, supaya aman dipakai bersama lewat folder Google Drive/OneDrive.</summary>
    public class Gudang
    {
        public string Path;
        public List<Kalimat> Isi = new List<Kalimat>();
        public DateTime TerakhirTulis = DateTime.MinValue;

        public Gudang(string path)
        {
            Path = path;
        }

        public void Muat()
        {
            Isi = File.Exists(Path) ? Tabel.BacaFile(Path, "") : new List<Kalimat>();
        }

        void Simpan(List<Kalimat> items)
        {
            var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, Csv.Tulis(Tabel.DariKalimat(items), Csv.PemisahSistem()), new UTF8Encoding(true));
            if (File.Exists(Path))
            {
                try
                {
                    File.Replace(tmp, Path, null);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Delete(Path);
                    File.Move(tmp, Path);
                }
            }
            else File.Move(tmp, Path);
            TerakhirTulis = DateTime.UtcNow;
            Isi = items;
        }

        /// <summary>Baca versi terbaru di disk, ubah, tulis. Lempar IOException kalau file terkunci (misalnya dibuka Excel).</summary>
        public void Ubah(Action<List<Kalimat>> aksi)
        {
            var terbaru = File.Exists(Path) ? Tabel.BacaFile(Path, "") : new List<Kalimat>();
            aksi(terbaru);
            Simpan(terbaru);
        }

        public int Tambah(IEnumerable<Kalimat> baru, bool lewatiKembar)
        {
            int n = 0;
            var daftar = baru.ToList();
            Ubah(items =>
            {
                var ada = new HashSet<string>(items.Select(k => Kalimat.Kunci(k.Teks)));
                foreach (var k in daftar)
                {
                    var kunci = Kalimat.Kunci(k.Teks);
                    if (kunci.Length == 0) continue;
                    if (lewatiKembar && ada.Contains(kunci)) continue;
                    var c = k.Salin();
                    c.Online = false;
                    if (string.IsNullOrEmpty(c.Id) || items.Any(x => x.Id == c.Id)) c.Id = Kalimat.IdBaru();
                    if (string.IsNullOrEmpty(c.Dibuat)) c.Dibuat = DateTime.Now.ToString("yyyy-MM-dd");
                    items.Add(c);
                    ada.Add(kunci);
                    n++;
                }
            });
            return n;
        }

        public bool Perbarui(Kalimat lama, Kalimat baru)
        {
            bool ketemu = false;
            Ubah(items =>
            {
                var idx = items.FindIndex(x => x.Id == lama.Id);
                if (idx < 0) idx = items.FindIndex(x => Kalimat.Kunci(x.Teks) == Kalimat.Kunci(lama.Teks) && x.Dari == lama.Dari);
                if (idx < 0) return;
                var c = baru.Salin();
                c.Id = items[idx].Id;
                c.Dibuat = items[idx].Dibuat;
                items[idx] = c;
                ketemu = true;
            });
            return ketemu;
        }

        public int Hapus(IEnumerable<Kalimat> target)
        {
            var ids = new HashSet<string>(target.Select(k => k.Id));
            var kunci = new HashSet<string>(target.Select(k => Kalimat.Kunci(k.Teks) + "\u0001" + k.Dari));
            int n = 0;
            Ubah(items => { n = items.RemoveAll(x => ids.Contains(x.Id) || kunci.Contains(Kalimat.Kunci(x.Teks) + "\u0001" + x.Dari)); });
            return n;
        }
    }
}
