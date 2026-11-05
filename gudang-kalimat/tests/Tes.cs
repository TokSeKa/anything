// Tes logika data (tanpa jendela). Jalankan: ./tests/jalankan-tes.sh
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GudangKalimat;

static class Tes
{
    static int gagal, lulus;

    static void Cek(bool kondisi, string nama)
    {
        if (kondisi) { lulus++; }
        else { gagal++; Console.WriteLine("GAGAL: " + nama); }
    }

    static string Tmp(string nama)
    {
        var dir = Path.Combine(Path.GetTempPath(), "gk-tes-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, nama);
    }

    static int Main(string[] args)
    {
        var folderTes = args.Length > 0 ? args[0] : "tests";

        // ---- CSV: kutip, baris ganda, pemisah titik koma (Excel Indonesia), BOM
        var csv = "﻿Kalimat;Dari;Kategori\r\n\"Halo; apa kabar?\";Ani;Sapaan\r\n\"Baris satu\r\nbaris \"\"dua\"\"\";Budi;\r\n;Kosong;\r\n";
        var p1 = Tmp("a.csv");
        File.WriteAllText(p1, csv, new UTF8Encoding(false));
        var a = Tabel.BacaFile(p1, "Saya");
        Cek(a.Count == 2, "csv: baris kosong dilewati");
        Cek(a[0].Teks == "Halo; apa kabar?" && a[0].Dari == "Ani" && a[0].Kategori == "Sapaan", "csv: titik koma di dalam kutip");
        Cek(a[1].Teks == "Baris satu\r\nbaris \"dua\"", "csv: baris ganda & kutip ganda");
        Cek(a.All(k => k.Id.Length > 0) && a[0].Id != a[1].Id, "csv: ID dibuat otomatis");
        Cek(Tabel.BacaFile(p1, "Saya")[0].Id == a[0].Id, "csv: ID tanpa kolom ID tetap sama tiap dibaca");

        // ---- CSV koma + tanpa judul kolom = daftar polos
        var p2 = Tmp("b.csv");
        File.WriteAllText(p2, "Kalimat pertama\nKalimat kedua\n", Encoding.UTF8);
        var b = Tabel.BacaFile(p2, "Saya");
        Cek(b.Count == 2 && b[0].Teks == "Kalimat pertama" && b[0].Dari == "Saya", "csv tanpa judul: baris pertama ikut");

        // ---- CSV ANSI (Excel lama) dengan huruf é
        var p3 = Tmp("c.csv");
        File.WriteAllBytes(p3, Encoding.GetEncoding(1252).GetBytes("Kalimat,Dari\r\nCafé enak,Ani\r\n"));
        var c = Tabel.BacaFile(p3, "");
        Cek(c.Count == 1 && c[0].Teks == "Café enak", "csv ANSI terbaca");

        // ---- TXT satu kalimat per baris
        var p4 = Tmp("d.txt");
        File.WriteAllText(p4, "satu\r\n\r\ndua\r\n", Encoding.UTF8);
        Cek(Tabel.BacaFile(p4, "X").Select(k => k.Teks).SequenceEqual(new[] { "satu", "dua" }), "txt per baris");

        // ---- XLSX dari aplikasi lain (sharedStrings, urutan kolom beda, angka, baris kosong)
        var x = Tabel.BacaFile(Path.Combine(folderTes, "contoh-excel.xlsx"), "Saya");
        Cek(x.Count == 3, "xlsx: 3 kalimat (baris kosong dilewati), dapat " + x.Count);
        Cek(x[0].Teks == "Siap, segera saya kerjakan." && x[0].Dari == "Budi" && x[0].Kategori == "Kerja", "xlsx: kolom dikenali dari judul");
        Cek(x[1].Teks == "Selamat ya!\r\nSemoga lancar.", "xlsx: isi beberapa baris");
        Cek(x[2].Teks == "12345", "xlsx: angka jadi teks");

        // ---- XLSX tulis lalu baca lagi
        var p5 = Tmp("e.xlsx");
        var asli = new List<Kalimat> {
            new Kalimat { Id = "k1", Teks = "Pakai <tanda> & \"kutip\"", Dari = "Ani", Kategori = "Tes", Dibuat = "2026-11-05" },
            new Kalimat { Id = "k2", Teks = "Dua\r\nbaris", Dari = "Budi" },
        };
        Tabel.TulisFile(p5, asli);
        var e = Tabel.BacaFile(p5, "");
        Cek(e.Count == 2 && e[0].Teks == asli[0].Teks && e[0].Id == "k1" && e[1].Teks == "Dua\r\nbaris", "xlsx: tulis-baca bolak-balik");

        // ---- Gudang: tambah, kembar, ubah, hapus, aman dipakai berdua
        var p6 = Tmp("kalimat.csv");
        var g1 = new Gudang(p6);
        var g2 = new Gudang(p6);   // "orang lain" memakai file yang sama
        int n = g1.Tambah(new[] { new Kalimat { Teks = "Halo", Dari = "Ani" }, new Kalimat { Teks = "  halo  ", Dari = "Budi" } }, true);
        Cek(n == 1, "tambah: kalimat kembar dilewati");
        g2.Muat();
        g2.Tambah(new[] { new Kalimat { Teks = "Dari Budi", Dari = "Budi" } }, true);
        // g1 masih memegang data lama, lalu menambah: tambahan Budi tidak boleh hilang
        g1.Tambah(new[] { new Kalimat { Teks = "Dari Ani lagi", Dari = "Ani" } }, true);
        g1.Muat();
        Cek(g1.Isi.Count == 3, "dua orang menambah bergantian: tidak ada yang hilang");
        var halo = g1.Isi.First(k => k.Teks == "Halo");
        Cek(g1.Perbarui(halo, new Kalimat { Teks = "Halo semua", Dari = "Ani", Kategori = "Sapaan" }), "ubah berhasil");
        g2.Muat();
        Cek(g2.Isi.Any(k => k.Teks == "Halo semua" && k.Id == halo.Id && k.Kategori == "Sapaan"), "ubah: ID tetap, terlihat orang lain");
        Cek(g2.Hapus(g2.Isi.Where(k => k.Dari == "Budi")) == 1, "hapus");
        g1.Muat();
        Cek(g1.Isi.Count == 2 && !g1.Isi.Any(k => k.Dari == "Budi"), "hapus terlihat orang lain");
        Cek(!g1.Perbarui(new Kalimat { Id = "tidak-ada", Teks = "zzz" }, new Kalimat { Teks = "x" }), "ubah kalimat yang sudah hilang = false");

        // file diedit manusia di Excel: baris baru tanpa ID tetap bisa diubah
        var sep = Csv.PemisahSistem().ToString();
        File.AppendAllText(p6, "Ditambah di Excel" + sep + "Citra" + sep + sep + sep + "\r\n", new UTF8Encoding(false));
        g1.Muat();
        var dariExcel = g1.Isi.FirstOrDefault(k => k.Teks == "Ditambah di Excel");
        Cek(dariExcel != null && dariExcel.Dari == "Citra", "baris tambahan dari Excel terbaca");
        Cek(dariExcel != null && g1.Perbarui(dariExcel, new Kalimat { Teks = "Diubah di aplikasi", Dari = "Citra" }), "baris tanpa ID bisa diubah");

        // ---- link Google Sheets
        Cek(Online.KeLinkUnduh("https://docs.google.com/spreadsheets/d/AbC_123-x/edit?usp=sharing") ==
            "https://docs.google.com/spreadsheets/d/AbC_123-x/export?format=csv", "link sheets → csv");
        Cek(Online.KeLinkUnduh("https://docs.google.com/spreadsheets/d/AbC/edit#gid=42") ==
            "https://docs.google.com/spreadsheets/d/AbC/export?format=csv&gid=42", "link sheets dengan tab");
        Cek(Online.KeLinkUnduh("https://docs.google.com/spreadsheets/d/e/2PACX/pub?output=csv").EndsWith("pub?output=csv"), "link publish dibiarkan");
        Cek(Online.KeLinkUnduh("https://www.dropbox.com/s/x/k.csv?dl=0").EndsWith("dl=1"), "link dropbox");
        var on = Online.DariTeks("Kalimat,Dari\nDari sheet,Dewi\n");
        Cek(on.Count == 1 && on[0].Online && on[0].Dari == "Dewi", "data online ditandai");

        Console.WriteLine(lulus + " lulus, " + gagal + " gagal");
        return gagal == 0 ? 0 : 1;
    }
}
