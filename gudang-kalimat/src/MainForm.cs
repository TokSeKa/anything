using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GudangKalimat
{
    public class MainForm : Form
    {
        // ------------------------------------------------------------ komponen
        readonly TextBox cari = new TextBox();
        readonly ComboBox filterDari = new ComboBox();
        readonly ComboBox filterKategori = new ComboBox();
        readonly ListView daftar = new ListView();
        readonly TextBox pratinjau = new TextBox();
        readonly Label info = new Label();
        readonly Label status = new Label();
        readonly CheckBox diAtas = new CheckBox();
        readonly System.Windows.Forms.Timer timerMuat = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer timerStatus = new System.Windows.Forms.Timer();
        readonly ToolTip tip = new ToolTip();

        // ------------------------------------------------------------ data
        Pengaturan set;
        Gudang gudang;
        List<Kalimat> online = new List<Kalimat>();
        List<Kalimat> tampil = new List<Kalimat>();
        FileSystemWatcher watcher;
        readonly Random acak = new Random();
        string idAcakTerakhir = "";
        int kolomUrut = -1;
        bool urutTurun;
        bool sedangMengisiFilter;

        const int HotkeyId = 0x4B4B;

        public MainForm()
        {
            set = Pengaturan.Muat();
            Text = "Gudang Kalimat";
            Font = new Font("Segoe UI", 9.5f);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(860, 600);
            MinimumSize = new Size(560, 420);
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            BangunTampilan();
            gudang = new Gudang(set.FileData);
            Load += delegate { MulaiAwal(); };
        }

        // ------------------------------------------------------------ tampilan
        Button Tombol(string teks, string tooltip, EventHandler klik, bool utama = false)
        {
            var b = new Button { Text = teks, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6, 3, 6, 3), Margin = new Padding(3) };
            if (utama)
            {
                b.BackColor = Color.FromArgb(37, 99, 235);
                b.ForeColor = Color.White;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.Font = new Font(Font, FontStyle.Bold);
            }
            b.Click += klik;
            if (!string.IsNullOrEmpty(tooltip)) tip.SetToolTip(b, tooltip);
            return b;
        }

        void BangunTampilan()
        {
            var atas = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 6, AutoSize = true, Padding = new Padding(8, 8, 8, 2) };
            atas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            atas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            atas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            atas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            atas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            atas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var lblCari = new Label { Text = "Cari:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 4, 0) };
            cari.Dock = DockStyle.Fill;
            cari.Margin = new Padding(0, 3, 6, 3);
            cari.TextChanged += delegate { Saring(); };
            cari.KeyDown += CariKeyDown;
            filterDari.DropDownStyle = ComboBoxStyle.DropDownList;
            filterDari.Width = 140;
            filterDari.SelectedIndexChanged += delegate { if (!sedangMengisiFilter) Saring(); };
            filterKategori.DropDownStyle = ComboBoxStyle.DropDownList;
            filterKategori.Width = 140;
            filterKategori.SelectedIndexChanged += delegate { if (!sedangMengisiFilter) Saring(); };
            tip.SetToolTip(filterDari, "Tampilkan kalimat dari orang tertentu");
            tip.SetToolTip(filterKategori, "Tampilkan kategori tertentu");
            var btnCari = Tombol("Cari", "Cari kalimat (Ctrl+F). Bisa beberapa kata sekaligus.", delegate { cari.Focus(); cari.SelectAll(); Saring(); });
            var btnBersih = Tombol("✕", "Kosongkan pencarian & filter (Esc)", delegate { BersihkanFilter(); });
            atas.Controls.Add(lblCari, 0, 0);
            atas.Controls.Add(cari, 1, 0);
            atas.Controls.Add(btnCari, 2, 0);
            atas.Controls.Add(filterDari, 3, 0);
            atas.Controls.Add(filterKategori, 4, 0);
            atas.Controls.Add(btnBersih, 5, 0);
            filterDari.Margin = filterKategori.Margin = new Padding(3, 4, 3, 3);

            daftar.Dock = DockStyle.Fill;
            daftar.View = View.Details;
            daftar.FullRowSelect = true;
            daftar.HideSelection = false;
            daftar.MultiSelect = true;
            daftar.GridLines = false;
            daftar.ShowItemToolTips = true;
            daftar.Columns.Add("Kalimat", 520);
            daftar.Columns.Add("Dari", 130);
            daftar.Columns.Add("Kategori", 130);
            daftar.SelectedIndexChanged += delegate { TampilkanPilihan(); };
            daftar.DoubleClick += delegate { SalinPilihan(); };
            daftar.KeyDown += DaftarKeyDown;
            daftar.ColumnClick += (s, e) => Urutkan(e.Column);
            daftar.Resize += delegate { AturLebarKolom(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Salin", null, delegate { SalinPilihan(); });
            menu.Items.Add("Ubah…", null, delegate { UbahPilihan(); });
            menu.Items.Add("Hapus", null, delegate { HapusPilihan(); });
            daftar.ContextMenuStrip = menu;
            var tengah = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4) };
            tengah.Controls.Add(daftar);

            var bawah = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 1, AutoSize = true, Padding = new Padding(8, 0, 8, 6) };
            info.AutoSize = true;
            info.ForeColor = Color.DimGray;
            info.Margin = new Padding(3, 2, 3, 2);
            pratinjau.Multiline = true;
            pratinjau.ScrollBars = ScrollBars.Vertical;
            pratinjau.Height = 78;
            pratinjau.Dock = DockStyle.Fill;
            pratinjau.Font = new Font(Font.FontFamily, 11f);
            tip.SetToolTip(pratinjau, "Kalimat yang dipilih. Boleh diubah dulu di sini sebelum disalin (tidak mengubah database).");

            var baris1 = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0) };
            baris1.Controls.Add(Tombol("📋 Salin", "Salin kalimat di kotak atas ke clipboard (Enter / klik dua kali)", delegate { SalinPratinjau(); }, true));
            baris1.Controls.Add(Tombol("🎲 Acak + Salin", "Ambil satu kalimat acak dari daftar yang tampil, langsung tersalin (Ctrl+R)", delegate { AcakSalin(); }, true));
            baris1.Controls.Add(Pemisah());
            baris1.Controls.Add(Tombol("Tambah", "Tambah satu kalimat (Ctrl+N)", delegate { TambahSatu(); }));
            baris1.Controls.Add(Tombol("Tambah banyak", "Tempel banyak kalimat sekaligus", delegate { TambahBanyak(); }));
            baris1.Controls.Add(Tombol("Ubah", "Ubah kalimat yang dipilih (F2)", delegate { UbahPilihan(); }));
            baris1.Controls.Add(Tombol("Hapus", "Hapus kalimat yang dipilih (Delete)", delegate { HapusPilihan(); }));
            baris1.Controls.Add(Pemisah());
            baris1.Controls.Add(Tombol("Impor…", "Tambahkan kalimat dari file Excel (.xlsx), CSV, atau TXT", delegate { Impor(); }));
            baris1.Controls.Add(Tombol("Ekspor…", "Simpan kalimat yang sedang tampil ke Excel (.xlsx), CSV, atau TXT", delegate { Ekspor(); }));
            baris1.Controls.Add(Tombol("Sinkron", "Muat ulang file data dan Google Sheets (F5)", delegate { MuatSemua(true); }));
            baris1.Controls.Add(Tombol("Pengaturan", "Nama, lokasi file data, link Google Sheets", delegate { BukaPengaturan(); }));

            var baris2 = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            baris2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            baris2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            status.AutoSize = true;
            status.ForeColor = Color.FromArgb(22, 128, 61);
            status.Font = new Font(Font, FontStyle.Bold);
            status.Margin = new Padding(3, 6, 3, 3);
            diAtas.Text = "Tetap di atas";
            diAtas.AutoSize = true;
            diAtas.CheckedChanged += delegate { TopMost = diAtas.Checked; set.DiAtas = diAtas.Checked; SimpanPengaturan(); };
            baris2.Controls.Add(status, 0, 0);
            baris2.Controls.Add(diAtas, 1, 0);

            bawah.Controls.Add(info);
            bawah.Controls.Add(pratinjau);
            bawah.Controls.Add(baris1);
            bawah.Controls.Add(baris2);

            Controls.Add(tengah);
            Controls.Add(bawah);
            Controls.Add(atas);

            timerMuat.Interval = 700;
            timerMuat.Tick += delegate { timerMuat.Stop(); MuatLokal(true); };
            timerStatus.Interval = 4000;
            timerStatus.Tick += delegate { timerStatus.Stop(); status.Text = ""; };
            KeyDown += FormKeyDown;
        }

        Control Pemisah()
        {
            return new Label { Width = 1, Height = 26, BorderStyle = BorderStyle.Fixed3D, Margin = new Padding(6, 4, 6, 0) };
        }

        void AturLebarKolom()
        {
            if (daftar.Columns.Count < 3) return;
            int sisa = daftar.ClientSize.Width - daftar.Columns[1].Width - daftar.Columns[2].Width - 4;
            daftar.Columns[0].Width = Math.Max(200, sisa);
        }

        // ------------------------------------------------------------ mulai
        void MulaiAwal()
        {
            diAtas.Checked = set.DiAtas;
            TopMost = set.DiAtas;
            Win.CueBanner(cari, "ketik kata kunci… (Ctrl+F)");
            if (!File.Exists(set.FileData)) BuatContoh();
            online = Online.DariCache();
            MuatLokal(false);
            PasangWatcher();
            if (set.Pintasan) Win.DaftarHotkey(this, HotkeyId);
            MuatOnlineLatar(false);
            cari.Focus();
        }

        void BuatContoh()
        {
            try
            {
                var nama = set.Nama;
                var contoh = new[]
                {
                    new Kalimat { Teks = "Terima kasih banyak, sangat membantu!", Dari = nama, Kategori = "Sapaan" },
                    new Kalimat { Teks = "Mohon maaf atas keterlambatan balasannya.", Dari = nama, Kategori = "Kerja" },
                    new Kalimat { Teks = "Baik, saya cek dulu dan kabari secepatnya ya.", Dari = nama, Kategori = "Kerja" },
                    new Kalimat { Teks = "Selamat ulang tahun! Semoga sehat selalu dan makin sukses.", Dari = nama, Kategori = "Ucapan" },
                    new Kalimat { Teks = "Kalimat ini contoh. Hapus atau ganti dengan kalimatmu sendiri.", Dari = nama, Kategori = "Contoh" },
                };
                gudang.Tambah(contoh, true);
            }
            catch (Exception) { /* folder tidak bisa ditulis: biarkan kosong */ }
        }

        void PasangWatcher()
        {
            if (watcher != null) { watcher.EnableRaisingEvents = false; watcher.Dispose(); watcher = null; }
            try
            {
                var full = Path.GetFullPath(set.FileData);
                var dir = Path.GetDirectoryName(full);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                watcher = new FileSystemWatcher(dir, Path.GetFileName(full));
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
                FileSystemEventHandler h = (s, e) => FileBerubah();
                watcher.Changed += h;
                watcher.Created += h;
                watcher.Renamed += (s, e) => FileBerubah();
                watcher.SynchronizingObject = this;
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception) { watcher = null; }
        }

        void FileBerubah()
        {
            if ((DateTime.UtcNow - gudang.TerakhirTulis).TotalSeconds < 1.5) return;   // perubahan dari kita sendiri
            timerMuat.Stop();
            timerMuat.Start();
        }

        // ------------------------------------------------------------ muat data
        void MuatLokal(bool beriTahu)
        {
            var dipilih = Pilihan().Select(k => k.Id).ToList();
            try
            {
                gudang.Muat();
                if (beriTahu) Status("File data diperbarui.");
            }
            catch (IOException ex)
            {
                Status("Tidak bisa membaca file data: " + ex.Message, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "File data tidak bisa dibaca:\r\n" + set.FileData + "\r\n\r\n" + ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            IsiFilter();
            Saring(dipilih);
        }

        void MuatSemua(bool beriTahu)
        {
            MuatLokal(false);
            if (!string.IsNullOrWhiteSpace(set.LinkOnline)) MuatOnlineLatar(beriTahu);
            else if (beriTahu) Status("Dimuat ulang dari file data.");
        }

        void MuatOnlineLatar(bool beriTahu)
        {
            var link = set.LinkOnline;
            if (string.IsNullOrWhiteSpace(link)) { online = new List<Kalimat>(); return; }
            if (beriTahu) Status("Mengambil dari Google Sheets…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<Kalimat> hasil = null;
                string err = null;
                try { hasil = Online.DariTeks(Online.Unduh(link)); }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (hasil != null)
                        {
                            online = hasil;
                            IsiFilter();
                            Saring(Pilihan().Select(k => k.Id).ToList());
                            if (beriTahu) Status("Google Sheets: " + hasil.Count + " kalimat.");
                        }
                        else Status("Google Sheets tidak bisa diambil (memakai salinan terakhir): " + err, true);
                    });
                }
                catch (InvalidOperationException) { /* jendela sudah ditutup */ }
            });
        }

        List<Kalimat> Semua()
        {
            var lokal = gudang.Isi;
            var kunciLokal = new HashSet<string>(lokal.Select(k => Kalimat.Kunci(k.Teks)));
            return lokal.Concat(online.Where(k => !kunciLokal.Contains(Kalimat.Kunci(k.Teks)))).ToList();
        }

        void IsiFilter()
        {
            sedangMengisiFilter = true;
            var semua = Semua();
            IsiCombo(filterDari, "Semua orang", semua.Select(k => k.Dari));
            IsiCombo(filterKategori, "Semua kategori", semua.Select(k => k.Kategori));
            sedangMengisiFilter = false;
        }

        static void IsiCombo(ComboBox cb, string semuaLabel, IEnumerable<string> nilai)
        {
            var lama = cb.SelectedItem as string;
            cb.BeginUpdate();
            cb.Items.Clear();
            cb.Items.Add(semuaLabel);
            foreach (var v in nilai.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                cb.Items.Add(v);
            var idx = lama != null ? cb.Items.IndexOf(lama) : -1;
            cb.SelectedIndex = idx >= 0 ? idx : 0;
            cb.EndUpdate();
        }

        // ------------------------------------------------------------ cari & tampil
        void Saring() { Saring(null); }

        void Saring(List<string> pilihId)
        {
            var kata = cari.Text.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var dari = filterDari.SelectedIndex > 0 ? filterDari.SelectedItem as string : null;
            var kat = filterKategori.SelectedIndex > 0 ? filterKategori.SelectedItem as string : null;
            IEnumerable<Kalimat> q = Semua();
            if (dari != null) q = q.Where(k => string.Equals(k.Dari, dari, StringComparison.OrdinalIgnoreCase));
            if (kat != null) q = q.Where(k => string.Equals(k.Kategori, kat, StringComparison.OrdinalIgnoreCase));
            if (kata.Length > 0)
                q = q.Where(k =>
                {
                    var hay = (k.Teks + " " + k.Dari + " " + k.Kategori).ToLowerInvariant();
                    return kata.All(w => hay.Contains(w));
                });
            tampil = q.ToList();
            if (kolomUrut >= 0)
            {
                Func<Kalimat, string> kunci = kolomUrut == 0 ? (Func<Kalimat, string>)(k => k.Teks) : kolomUrut == 1 ? (Func<Kalimat, string>)(k => k.Dari) : (k => k.Kategori);
                tampil = (urutTurun ? tampil.OrderByDescending(kunci, StringComparer.CurrentCultureIgnoreCase)
                                    : tampil.OrderBy(kunci, StringComparer.CurrentCultureIgnoreCase)).ToList();
            }
            daftar.BeginUpdate();
            daftar.Items.Clear();
            foreach (var k in tampil)
            {
                var satuBaris = k.Teks.Replace("\r\n", " ⏎ ");
                var it = new ListViewItem(satuBaris) { Tag = k, ToolTipText = k.Teks };
                it.SubItems.Add(k.Dari + (k.Online ? " ☁" : ""));
                it.SubItems.Add(k.Kategori);
                if (k.Online) it.ForeColor = Color.FromArgb(30, 64, 175);
                daftar.Items.Add(it);
            }
            if (pilihId != null && pilihId.Count > 0)
            {
                foreach (ListViewItem it in daftar.Items)
                    if (pilihId.Contains(((Kalimat)it.Tag).Id)) it.Selected = true;
                if (daftar.SelectedItems.Count > 0) daftar.SelectedItems[0].EnsureVisible();
            }
            daftar.EndUpdate();
            int total = Semua().Count;
            info.Text = tampil.Count == total
                ? total + " kalimat · file: " + set.FileData + (online.Count > 0 ? " · ☁ " + online.Count + " dari Google Sheets" : "")
                : tampil.Count + " dari " + total + " kalimat cocok";
            if (daftar.SelectedItems.Count == 0) pratinjau.Text = "";
            AturLebarKolom();
        }

        void Urutkan(int kolom)
        {
            if (kolomUrut == kolom) urutTurun = !urutTurun;
            else { kolomUrut = kolom; urutTurun = false; }
            Saring(Pilihan().Select(k => k.Id).ToList());
        }

        void BersihkanFilter()
        {
            cari.Text = "";
            if (filterDari.Items.Count > 0) filterDari.SelectedIndex = 0;
            if (filterKategori.Items.Count > 0) filterKategori.SelectedIndex = 0;
            Saring();
            cari.Focus();
        }

        List<Kalimat> Pilihan()
        {
            return daftar.SelectedItems.Cast<ListViewItem>().Select(i => (Kalimat)i.Tag).ToList();
        }

        void TampilkanPilihan()
        {
            var p = Pilihan();
            if (p.Count == 0) return;
            var k = p[0];
            pratinjau.Text = k.Teks;
        }

        // ------------------------------------------------------------ salin
        bool KeClipboard(string teks)
        {
            if (string.IsNullOrEmpty(teks)) return false;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetText(teks);
                    return true;
                }
                catch (ExternalException) { Thread.Sleep(60); }   // clipboard sedang dipakai aplikasi lain
            }
            Status("Clipboard sedang dipakai aplikasi lain, coba lagi.", true);
            return false;
        }

        void SetelahSalin(string teks)
        {
            var pendek = teks.Replace("\r\n", " ");
            if (pendek.Length > 60) pendek = pendek.Substring(0, 57) + "…";
            Status("Tersalin ✓  " + pendek);
            if (set.SembunyiSetelahSalin) WindowState = FormWindowState.Minimized;
        }

        void SalinPratinjau()
        {
            var teks = pratinjau.Text;
            if (string.IsNullOrEmpty(teks))
            {
                var p = Pilihan();
                if (p.Count == 0) { Status("Pilih kalimat dulu, atau tekan Acak + Salin.", true); return; }
                teks = string.Join("\r\n", p.Select(k => k.Teks));
            }
            if (KeClipboard(teks)) SetelahSalin(teks);
        }

        void SalinPilihan()
        {
            var p = Pilihan();
            if (p.Count == 0) return;
            var teks = string.Join("\r\n", p.Select(k => k.Teks));
            pratinjau.Text = p.Count == 1 ? p[0].Teks : teks;
            if (KeClipboard(teks)) SetelahSalin(teks);
        }

        void AcakSalin()
        {
            var sumber = tampil.Count > 0 ? tampil : Semua();
            if (sumber.Count == 0) { Status("Belum ada kalimat. Tekan Tambah dulu.", true); return; }
            var calon = sumber.Count > 1 ? sumber.Where(x => x.Id != idAcakTerakhir).ToList() : sumber;
            var k = calon[acak.Next(calon.Count)];
            idAcakTerakhir = k.Id;
            daftar.SelectedItems.Clear();
            foreach (ListViewItem it in daftar.Items)
            {
                if (((Kalimat)it.Tag).Id == k.Id) { it.Selected = true; it.Focused = true; it.EnsureVisible(); break; }
            }
            pratinjau.Text = k.Teks;
            if (KeClipboard(k.Teks)) SetelahSalin(k.Teks);
        }

        // ------------------------------------------------------------ ubah data
        bool Jalankan(Action aksi)
        {
            try
            {
                aksi();
                return true;
            }
            catch (IOException ex)
            {
                MessageBox.Show(this, "File data tidak bisa disimpan. Mungkin sedang dibuka di Excel atau program lain.\r\n" +
                                      "Tutup dulu file itu, lalu coba lagi.\r\n\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(this, "Tidak punya izin menulis file data:\r\n" + set.FileData + "\r\n\r\nPindahkan lokasinya di Pengaturan.\r\n\r\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return false;
        }

        IEnumerable<string> SemuaNama() { return Semua().Select(k => k.Dari).Concat(new[] { set.Nama }); }
        IEnumerable<string> SemuaKategori() { return Semua().Select(k => k.Kategori); }

        string KategoriAktif()
        {
            return filterKategori.SelectedIndex > 0 ? filterKategori.SelectedItem as string : "";
        }

        void TambahSatu()
        {
            var awal = new Kalimat { Dari = set.Nama, Kategori = KategoriAktif() };
            using (var f = new EditForm("Tambah kalimat", awal, SemuaNama(), SemuaKategori()))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                var baru = f.Hasil;
                if (!Jalankan(() => gudang.Tambah(new[] { baru }, false))) return;
                Saring(new List<string>());
                PilihTeks(baru.Teks);
                Status("Kalimat ditambahkan.");
            }
        }

        void TambahBanyak()
        {
            using (var f = new BanyakForm(set.Nama, KategoriAktif(), SemuaNama(), SemuaKategori()))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                int n = 0;
                if (!Jalankan(() => n = gudang.Tambah(f.Hasil, set.LewatiKembar))) return;
                IsiFilter();
                Saring();
                int lewat = f.Hasil.Count - n;
                Status(n + " kalimat ditambahkan" + (lewat > 0 ? " (" + lewat + " dilewati karena sudah ada)" : "") + ".");
            }
        }

        void PilihTeks(string teks)
        {
            IsiFilter();
            Saring();
            foreach (ListViewItem it in daftar.Items)
            {
                if (((Kalimat)it.Tag).Teks == teks) { it.Selected = true; it.EnsureVisible(); break; }
            }
        }

        bool TolakOnline(List<Kalimat> p)
        {
            if (!p.Any(k => k.Online)) return false;
            var r = MessageBox.Show(this, "Kalimat bertanda ☁ berasal dari Google Sheets, jadi diubah atau dihapus langsung di sana.\r\n\r\nBuka Google Sheets sekarang?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r == DialogResult.Yes) BukaLink(Online.LinkEdit(set.LinkOnline));
            return true;
        }

        void UbahPilihan()
        {
            var p = Pilihan();
            if (p.Count == 0) { Status("Pilih kalimat yang mau diubah.", true); return; }
            if (TolakOnline(p.Take(1).ToList())) return;
            var lama = p[0];
            using (var f = new EditForm("Ubah kalimat", lama, SemuaNama(), SemuaKategori()))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                bool ok = false;
                if (!Jalankan(() => ok = gudang.Perbarui(lama, f.Hasil))) return;
                if (!ok) Status("Kalimat itu sudah dihapus/diubah orang lain. Daftar dimuat ulang.", true);
                else Status("Kalimat diubah.");
                IsiFilter();
                Saring(new List<string> { lama.Id });
                TampilkanPilihan();
            }
        }

        void HapusPilihan()
        {
            var p = Pilihan();
            if (p.Count == 0) { Status("Pilih kalimat yang mau dihapus.", true); return; }
            var lokal = p.Where(k => !k.Online).ToList();
            if (lokal.Count == 0) { TolakOnline(p); return; }
            var pesan = lokal.Count == 1 ? "Hapus kalimat ini?\r\n\r\n" + Potong(lokal[0].Teks, 200) : "Hapus " + lokal.Count + " kalimat terpilih?";
            if (p.Count > lokal.Count) pesan += "\r\n\r\n(Kalimat ☁ dari Google Sheets tidak ikut dihapus.)";
            if (MessageBox.Show(this, pesan, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            int n = 0;
            if (!Jalankan(() => n = gudang.Hapus(lokal))) return;
            pratinjau.Text = "";
            IsiFilter();
            Saring();
            Status(n + " kalimat dihapus.");
        }

        static string Potong(string s, int n)
        {
            return s.Length > n ? s.Substring(0, n - 1) + "…" : s;
        }

        // ------------------------------------------------------------ impor / ekspor
        void Impor()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Impor kalimat";
                dlg.Filter = "Semua yang didukung|*.xlsx;*.csv;*.tsv;*.txt|Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv|Teks, satu kalimat per baris (*.txt)|*.txt";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                List<Kalimat> items;
                try { items = Tabel.BacaFile(dlg.FileName, set.Nama); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "File tidak bisa dibaca:\r\n" + ex.Message + "\r\n\r\nUntuk .xls lama, simpan dulu sebagai .xlsx di Excel.",
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (items.Count == 0)
                {
                    MessageBox.Show(this, "Tidak ada kalimat di file itu.\r\nKolom kalimat boleh berjudul \"Kalimat\" (opsional: \"Dari\", \"Kategori\"), " +
                                          "atau cukup kolom pertama.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int n = 0;
                foreach (var k in items) k.Id = "";   // ID baru di gudang ini
                if (!Jalankan(() => n = gudang.Tambah(items, set.LewatiKembar))) return;
                IsiFilter();
                Saring();
                Status("Impor: " + n + " kalimat ditambahkan" + (items.Count > n ? ", " + (items.Count - n) + " dilewati (sudah ada)" : "") + ".");
            }
        }

        void Ekspor()
        {
            var items = tampil.Count > 0 ? tampil : Semua();
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Ekspor " + items.Count + " kalimat";
                dlg.Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv|Teks, satu kalimat per baris (*.txt)|*.txt";
                dlg.FileName = "kalimat-" + DateTime.Now.ToString("yyyy-MM-dd") + ".xlsx";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Tabel.TulisFile(dlg.FileName, items);
                    Status("Ekspor: " + items.Count + " kalimat → " + Path.GetFileName(dlg.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Gagal menyimpan:\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // ------------------------------------------------------------ pengaturan
        void BukaPengaturan()
        {
            using (var f = new PengaturanForm(set))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                var fileLama = set.FileData;
                var linkLama = set.LinkOnline;
                var pintasLama = set.Pintasan;
                set = f.Hasil;
                SimpanPengaturan();
                diAtas.Checked = set.DiAtas;
                if (pintasLama != set.Pintasan)
                {
                    if (set.Pintasan) Win.DaftarHotkey(this, HotkeyId); else Win.LepasHotkey(this, HotkeyId);
                }
                if (fileLama != set.FileData)
                {
                    gudang = new Gudang(set.FileData);
                    if (!File.Exists(set.FileData))
                    {
                        var r = MessageBox.Show(this, "File data baru belum ada. Salin kalimat yang sekarang ke file itu?", Text,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (r == DialogResult.Yes)
                        {
                            var lama = new Gudang(fileLama);
                            try { lama.Muat(); } catch (Exception) { }
                            Jalankan(() => gudang.Tambah(lama.Isi, true));
                        }
                    }
                    PasangWatcher();
                }
                if (linkLama != set.LinkOnline)
                {
                    online = new List<Kalimat>();
                    try { if (File.Exists(Pengaturan.FileCacheOnline)) File.Delete(Pengaturan.FileCacheOnline); } catch (Exception) { }
                }
                MuatSemua(true);
            }
        }

        void SimpanPengaturan()
        {
            try { set.Simpan(); } catch (Exception) { }
        }

        void BukaLink(string url)
        {
            try { Process.Start(url); } catch (Exception ex) { Status("Tidak bisa membuka: " + ex.Message, true); }
        }

        // ------------------------------------------------------------ keyboard
        void CariKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && daftar.Items.Count > 0)
            {
                daftar.Focus();
                daftar.SelectedItems.Clear();
                daftar.Items[0].Selected = true;
                daftar.Items[0].Focused = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // Enter di kotak cari: salin hasil pertama
                if (daftar.Items.Count > 0)
                {
                    daftar.SelectedItems.Clear();
                    daftar.Items[0].Selected = true;
                    SalinPilihan();
                }
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                BersihkanFilter();
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        void DaftarKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || (e.Control && e.KeyCode == Keys.C)) { SalinPilihan(); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Delete) { HapusPilihan(); e.Handled = true; }
            else if (e.KeyCode == Keys.F2) { UbahPilihan(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem it in daftar.Items) it.Selected = true;
                e.Handled = true;
            }
        }

        void FormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F) { cari.Focus(); cari.SelectAll(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.R) { AcakSalin(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.N) { TambahSatu(); e.Handled = true; }
            else if (e.KeyCode == Keys.F5) { MuatSemua(true); e.Handled = true; }
        }

        // ------------------------------------------------------------ status & sistem
        void Status(string teks, bool peringatan = false)
        {
            status.ForeColor = peringatan ? Color.FromArgb(185, 28, 28) : Color.FromArgb(22, 128, 61);
            status.Text = teks;
            timerStatus.Stop();
            timerStatus.Start();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Show();
                Activate();
                cari.Focus();
                cari.SelectAll();
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Win.LepasHotkey(this, HotkeyId);
            if (watcher != null) watcher.Dispose();
            base.OnFormClosed(e);
        }
    }

    /// <summary>Fungsi khusus Windows. Di sistem lain (misalnya tes di Linux) diam-diam dilewati.</summary>
    static class Win
    {
        public const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
        const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        static bool IsWindows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }

        public static void DaftarHotkey(Form f, int id)
        {
            if (!IsWindows) return;
            try { RegisterHotKey(f.Handle, id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.K); } catch (Exception) { }
        }

        public static void LepasHotkey(Form f, int id)
        {
            if (!IsWindows) return;
            try { UnregisterHotKey(f.Handle, id); } catch (Exception) { }
        }

        public static void CueBanner(TextBox t, string teks)
        {
            if (!IsWindows) return;
            try { SendMessage(t.Handle, EM_SETCUEBANNER, (IntPtr)1, teks); } catch (Exception) { }
        }

        public static void DpiAware()
        {
            if (!IsWindows) return;
            try { SetProcessDPIAware(); } catch (Exception) { }
        }
    }
}
