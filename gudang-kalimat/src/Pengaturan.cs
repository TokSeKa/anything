using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace GudangKalimat
{
    /// <summary>Pengaturan sederhana (teks kunci=nilai) di %APPDATA%\GudangKalimat\pengaturan.txt.</summary>
    public class Pengaturan
    {
        public string FileData = "";
        public string Nama = "";
        public string LinkOnline = "";
        public bool DiAtas;
        public bool SembunyiSetelahSalin;
        public bool LewatiKembar = true;
        public bool Pintasan = true;

        public static string Folder
        {
            get
            {
                var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(app)) app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");
                return Path.Combine(app, "GudangKalimat");
            }
        }

        static string FilePengaturan { get { return Path.Combine(Folder, "pengaturan.txt"); } }
        public static string FileCacheOnline { get { return Path.Combine(Folder, "online-cache.csv"); } }

        /// <summary>Default file data: kalimat.csv di sebelah .exe (portable), atau Dokumen kalau folder itu tidak bisa ditulis.</summary>
        public static string FileDataBawaan()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                var test = Path.Combine(exeDir, ".tes-tulis");
                File.WriteAllText(test, "");
                File.Delete(test);
                return Path.Combine(exeDir, "kalimat.csv");
            }
            catch (Exception)
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(docs, "GudangKalimat", "kalimat.csv");
            }
        }

        public static Pengaturan Muat()
        {
            var p = new Pengaturan();
            try
            {
                if (File.Exists(FilePengaturan))
                {
                    foreach (var line in File.ReadAllLines(FilePengaturan, Encoding.UTF8))
                    {
                        var i = line.IndexOf('=');
                        if (i <= 0) continue;
                        var k = line.Substring(0, i).Trim();
                        var v = line.Substring(i + 1).Trim();
                        switch (k)
                        {
                            case "data": p.FileData = v; break;
                            case "nama": p.Nama = v; break;
                            case "online": p.LinkOnline = v; break;
                            case "diatas": p.DiAtas = v == "1"; break;
                            case "sembunyi": p.SembunyiSetelahSalin = v == "1"; break;
                            case "lewati_kembar": p.LewatiKembar = v != "0"; break;
                            case "pintasan": p.Pintasan = v != "0"; break;
                        }
                    }
                }
            }
            catch (Exception) { /* pengaturan rusak: pakai bawaan */ }
            if (string.IsNullOrEmpty(p.FileData)) p.FileData = FileDataBawaan();
            if (string.IsNullOrEmpty(p.Nama)) p.Nama = Environment.UserName ?? "";
            return p;
        }

        public void Simpan()
        {
            Directory.CreateDirectory(Folder);
            var lines = new List<string>
            {
                "data=" + FileData,
                "nama=" + Nama,
                "online=" + LinkOnline,
                "diatas=" + (DiAtas ? "1" : "0"),
                "sembunyi=" + (SembunyiSetelahSalin ? "1" : "0"),
                "lewati_kembar=" + (LewatiKembar ? "1" : "0"),
                "pintasan=" + (Pintasan ? "1" : "0"),
            };
            File.WriteAllLines(FilePengaturan, lines, new UTF8Encoding(false));
        }
    }

    /// <summary>Sumber online: link Google Sheets (dibagikan "siapa saja yang memiliki link"), atau link CSV lain.</summary>
    public static class Online
    {
        public static string KeLinkUnduh(string link)
        {
            link = (link ?? "").Trim();
            if (link.Length == 0) return "";
            var m = Regex.Match(link, @"docs\.google\.com/spreadsheets/d/([a-zA-Z0-9_-]+)");
            if (m.Success && !link.Contains("/pub") && !link.Contains("/export"))
            {
                var gid = Regex.Match(link, @"[#?&]gid=([0-9]+)");
                return "https://docs.google.com/spreadsheets/d/" + m.Groups[1].Value + "/export?format=csv" +
                       (gid.Success ? "&gid=" + gid.Groups[1].Value : "");
            }
            if (link.Contains("dropbox.com")) return Regex.Replace(link, @"([?&])dl=0", "$1dl=1");
            return link;
        }

        /// <summary>Unduh dan simpan salinan supaya tetap bisa dipakai saat offline.</summary>
        public static string Unduh(string link)
        {
            var url = KeLinkUnduh(link);
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "GudangKalimat/1.0";
                var data = wc.DownloadData(url);
                var teks = Encoding.UTF8.GetString(data);
                if (teks.Length > 0 && teks[0] == '﻿') teks = teks.Substring(1);
                var awal = teks.TrimStart();
                if (awal.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || awal.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Link membuka halaman web, bukan data. Di Google Sheets: Bagikan → " +
                                                   "Akses umum → \"Siapa saja yang memiliki link\" (Viewer), lalu salin link-nya lagi.");
                Directory.CreateDirectory(Pengaturan.Folder);
                File.WriteAllText(Pengaturan.FileCacheOnline, teks, new UTF8Encoding(true));
                return teks;
            }
        }

        public static List<Kalimat> DariTeks(string teks)
        {
            var items = Tabel.KeKalimat(Csv.Parse(teks), "Google Sheets");
            foreach (var k in items) k.Online = true;
            return items;
        }

        public static List<Kalimat> DariCache()
        {
            if (!File.Exists(Pengaturan.FileCacheOnline)) return new List<Kalimat>();
            return DariTeks(Tabel.BacaTeksBersama(Pengaturan.FileCacheOnline));
        }

        public static string LinkEdit(string link)
        {
            var m = Regex.Match(link ?? "", @"docs\.google\.com/spreadsheets/d/([a-zA-Z0-9_-]+)");
            return m.Success ? "https://docs.google.com/spreadsheets/d/" + m.Groups[1].Value + "/edit" : (link ?? "");
        }
    }
}
