using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GudangKalimat
{
    static class Ui
    {
        public static Label Label(string t)
        {
            return new Label { Text = t, AutoSize = true, Margin = new Padding(3, 8, 3, 2) };
        }

        public static ComboBox Combo(IEnumerable<string> pilihan, string nilai)
        {
            var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
            foreach (var v in pilihan.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                cb.Items.Add(v);
            cb.Text = nilai ?? "";
            return cb;
        }

        public static FlowLayoutPanel Tombol(Form f, string ok)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
            var batal = new Button { Text = "Batal", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            var simpan = new Button { Text = ok, AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            p.Controls.Add(batal);
            p.Controls.Add(simpan);
            f.CancelButton = batal;
            simpan.Name = "ok";
            return p;
        }

        public static void Dasar(Form f, string judul, Size ukuran)
        {
            f.Text = judul;
            f.Font = new Font("Segoe UI", 9.5f);
            f.Size = ukuran;
            f.MinimumSize = new Size(Math.Min(ukuran.Width, 420), Math.Min(ukuran.Height, 300));
            f.StartPosition = FormStartPosition.CenterParent;
            f.ShowInTaskbar = false;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
        }
    }

    /// <summary>Tambah / ubah satu kalimat.</summary>
    public class EditForm : Form
    {
        readonly TextBox teks = new TextBox();
        readonly ComboBox dari;
        readonly ComboBox kategori;
        public Kalimat Hasil;

        public EditForm(string judul, Kalimat awal, IEnumerable<string> nama, IEnumerable<string> kategoriAda)
        {
            Ui.Dasar(this, judul, new Size(560, 380));
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            teks.Multiline = true;
            teks.AcceptsReturn = true;
            teks.ScrollBars = ScrollBars.Vertical;
            teks.Dock = DockStyle.Fill;
            teks.Font = new Font(Font.FontFamily, 11f);
            teks.Text = awal.Teks;
            dari = Ui.Combo(nama, awal.Dari);
            kategori = Ui.Combo(kategoriAda, awal.Kategori);
            t.Controls.Add(Ui.Label("Kalimat"), 0, 0);
            t.Controls.Add(teks, 1, 0);
            t.Controls.Add(Ui.Label("Dari"), 0, 1);
            t.Controls.Add(dari, 1, 1);
            t.Controls.Add(Ui.Label("Kategori"), 0, 2);
            t.Controls.Add(kategori, 1, 2);
            var tombol = Ui.Tombol(this, "Simpan");
            var ok = (Button)tombol.Controls["ok"];
            ok.Click += delegate
            {
                if (teks.Text.Trim().Length == 0) { MessageBox.Show(this, "Kalimat tidak boleh kosong.", Text); return; }
                Hasil = new Kalimat { Id = awal.Id, Teks = teks.Text.Trim(), Dari = dari.Text.Trim(), Kategori = kategori.Text.Trim(), Dibuat = awal.Dibuat };
                DialogResult = DialogResult.OK;
            };
            var hint = new Label { Text = "Tips: Ctrl+Enter untuk menyimpan.", AutoSize = true, ForeColor = Color.DimGray, Dock = DockStyle.Bottom, Padding = new Padding(12, 0, 0, 0) };
            Controls.Add(t);
            Controls.Add(hint);
            Controls.Add(tombol);
            teks.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.Enter) { ok.PerformClick(); e.SuppressKeyPress = true; } };
            Shown += delegate { teks.Focus(); teks.SelectionStart = teks.TextLength; };
        }
    }

    /// <summary>Tempel banyak kalimat sekaligus.</summary>
    public class BanyakForm : Form
    {
        readonly TextBox teks = new TextBox();
        readonly RadioButton perBaris = new RadioButton { Text = "Satu kalimat per baris", Checked = true, AutoSize = true };
        readonly RadioButton perParagraf = new RadioButton { Text = "Dipisah baris kosong (satu kalimat boleh beberapa baris)", AutoSize = true };
        readonly ComboBox dari;
        readonly ComboBox kategori;
        readonly Label hitung = new Label { AutoSize = true, ForeColor = Color.DimGray };
        public List<Kalimat> Hasil = new List<Kalimat>();

        public BanyakForm(string nama, string kategoriAwal, IEnumerable<string> semuaNama, IEnumerable<string> semuaKategori)
        {
            Ui.Dasar(this, "Tambah banyak kalimat", new Size(640, 520));
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var petunjuk = new Label { Text = "Tempel kalimat-kalimat di bawah (misalnya dari chat, Word, atau kolom Excel):", AutoSize = true };
            t.Controls.Add(petunjuk, 0, 0);
            t.SetColumnSpan(petunjuk, 2);
            teks.Multiline = true;
            teks.AcceptsReturn = true;
            teks.ScrollBars = ScrollBars.Both;
            teks.WordWrap = false;
            teks.Dock = DockStyle.Fill;
            teks.TextChanged += delegate { Hitung(); };
            t.Controls.Add(teks, 0, 1);
            t.SetColumnSpan(teks, 2);
            var mode = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            mode.Controls.Add(perBaris);
            mode.Controls.Add(perParagraf);
            perBaris.CheckedChanged += delegate { Hitung(); };
            t.Controls.Add(mode, 0, 2);
            t.SetColumnSpan(mode, 2);
            dari = Ui.Combo(semuaNama, nama);
            kategori = Ui.Combo(semuaKategori, kategoriAwal);
            t.Controls.Add(Ui.Label("Dari"), 0, 3);
            t.Controls.Add(dari, 1, 3);
            t.Controls.Add(Ui.Label("Kategori"), 0, 4);
            t.Controls.Add(kategori, 1, 4);
            t.Controls.Add(hitung, 1, 5);
            var tombol = Ui.Tombol(this, "Tambahkan");
            ((Button)tombol.Controls["ok"]).Click += delegate
            {
                Hasil = Pecah().Select(x => new Kalimat { Teks = x, Dari = dari.Text.Trim(), Kategori = kategori.Text.Trim() }).ToList();
                if (Hasil.Count == 0) { MessageBox.Show(this, "Belum ada kalimat.", Text); return; }
                DialogResult = DialogResult.OK;
            };
            Controls.Add(t);
            Controls.Add(tombol);
            Hitung();
        }

        List<string> Pecah()
        {
            var s = teks.Text.Replace("\r\n", "\n");
            IEnumerable<string> bagian = perBaris.Checked
                ? s.Split('\n')
                : System.Text.RegularExpressions.Regex.Split(s, @"\n\s*\n");
            return bagian.Select(x => x.Trim().Replace("\n", "\r\n")).Where(x => x.Length > 0).ToList();
        }

        void Hitung()
        {
            hitung.Text = Pecah().Count + " kalimat akan ditambahkan";
        }
    }

    /// <summary>Pengaturan: nama, file data (bisa di folder bersama), link Google Sheets.</summary>
    public class PengaturanForm : Form
    {
        readonly TextBox nama = new TextBox { Dock = DockStyle.Fill };
        readonly TextBox file = new TextBox { Dock = DockStyle.Fill };
        readonly TextBox link = new TextBox { Dock = DockStyle.Fill };
        readonly CheckBox diAtas = new CheckBox { Text = "Jendela selalu di atas aplikasi lain", AutoSize = true };
        readonly CheckBox sembunyi = new CheckBox { Text = "Kecilkan jendela setelah menyalin (supaya bisa langsung tempel)", AutoSize = true };
        readonly CheckBox kembar = new CheckBox { Text = "Lewati kalimat yang sudah ada saat impor / tambah banyak", AutoSize = true };
        readonly CheckBox pintasan = new CheckBox { Text = "Pintasan Ctrl+Alt+K untuk memunculkan aplikasi dari mana saja", AutoSize = true };
        public Pengaturan Hasil;

        public PengaturanForm(Pengaturan p)
        {
            Ui.Dasar(this, "Pengaturan", new Size(680, 560));
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(10), AutoScroll = true };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            nama.Text = p.Nama;
            file.Text = p.FileData;
            link.Text = p.LinkOnline;
            diAtas.Checked = p.DiAtas;
            sembunyi.Checked = p.SembunyiSetelahSalin;
            kembar.Checked = p.LewatiKembar;
            pintasan.Checked = p.Pintasan;

            int r = 0;
            t.Controls.Add(Ui.Label("Namamu"), 0, r);
            t.Controls.Add(nama, 1, r++);
            t.Controls.Add(Catatan("Dipakai sebagai \"Dari\" untuk kalimat yang kamu tambahkan."), 1, r++);

            t.Controls.Add(Ui.Label("File data"), 0, r);
            t.Controls.Add(file, 1, r);
            var pilih = new Button { Text = "Pilih…", AutoSize = true };
            pilih.Click += delegate { PilihFile(); };
            t.Controls.Add(pilih, 2, r++);
            t.Controls.Add(Catatan("File CSV biasa, bisa dibuka & diedit di Excel. Untuk dipakai ramai-ramai: taruh di folder Google Drive / " +
                                   "OneDrive / Dropbox yang dibagikan, lalu semua orang memilih file yang sama. Perubahan orang lain muncul otomatis."), 1, r++);
            var buka = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var bukaFile = new Button { Text = "Buka di Excel", AutoSize = true };
            bukaFile.Click += delegate { Buka(file.Text); };
            var bukaFolder = new Button { Text = "Buka folder", AutoSize = true };
            bukaFolder.Click += delegate { try { Buka(Path.GetDirectoryName(Path.GetFullPath(file.Text))); } catch (Exception) { } };
            buka.Controls.Add(bukaFile);
            buka.Controls.Add(bukaFolder);
            t.Controls.Add(buka, 1, r++);

            t.Controls.Add(Ui.Label("Google Sheets"), 0, r);
            t.Controls.Add(link, 1, r);
            var tes = new Button { Text = "Tes link", AutoSize = true };
            tes.Click += delegate { TesLink(); };
            t.Controls.Add(tes, 2, r++);
            t.Controls.Add(Catatan("Opsional. Tempel link Google Sheets yang dibagikan \"Siapa saja yang memiliki link\". Kolom: Kalimat, Dari, Kategori. " +
                                   "Kalimatnya ikut tampil (tanda ☁) dan diambil ulang setiap aplikasi dibuka atau tombol Sinkron. Edit kalimatnya langsung di Google Sheets."), 1, r++);

            var cek = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 10, 0, 0) };
            cek.Controls.Add(diAtas);
            cek.Controls.Add(sembunyi);
            cek.Controls.Add(kembar);
            cek.Controls.Add(pintasan);
            t.Controls.Add(cek, 1, r++);

            var tombol = Ui.Tombol(this, "Simpan");
            ((Button)tombol.Controls["ok"]).Click += delegate
            {
                if (file.Text.Trim().Length == 0) { MessageBox.Show(this, "Lokasi file data belum diisi.", Text); return; }
                var ext = Path.GetExtension(file.Text.Trim()).ToLowerInvariant();
                if (ext != ".csv")
                {
                    MessageBox.Show(this, "File data harus berakhiran .csv (Excel bisa membukanya). Untuk file .xlsx, pakai tombol Impor.", Text);
                    return;
                }
                Hasil = new Pengaturan
                {
                    Nama = nama.Text.Trim(), FileData = file.Text.Trim(), LinkOnline = link.Text.Trim(), DiAtas = diAtas.Checked,
                    SembunyiSetelahSalin = sembunyi.Checked, LewatiKembar = kembar.Checked, Pintasan = pintasan.Checked,
                };
                DialogResult = DialogResult.OK;
            };
            Controls.Add(t);
            Controls.Add(tombol);
        }

        static Label Catatan(string s)
        {
            return new Label { Text = s, AutoSize = true, MaximumSize = new Size(470, 0), ForeColor = Color.DimGray, Margin = new Padding(3, 0, 3, 6) };
        }

        void PilihFile()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Pilih atau buat file data (.csv)";
                dlg.Filter = "CSV (*.csv)|*.csv";
                dlg.OverwritePrompt = false;
                dlg.CheckFileExists = false;
                try { dlg.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(file.Text)); } catch (Exception) { }
                dlg.FileName = Path.GetFileName(file.Text);
                if (dlg.ShowDialog(this) == DialogResult.OK) file.Text = dlg.FileName;
            }
        }

        void TesLink()
        {
            if (link.Text.Trim().Length == 0) { MessageBox.Show(this, "Link masih kosong.", Text); return; }
            Cursor = Cursors.WaitCursor;
            try
            {
                var items = Online.DariTeks(Online.Unduh(link.Text));
                MessageBox.Show(this, "Berhasil: " + items.Count + " kalimat ditemukan.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Gagal mengambil data:\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { Cursor = Cursors.Default; }
        }

        void Buka(string path)
        {
            try { System.Diagnostics.Process.Start(path); }
            catch (Exception ex) { MessageBox.Show(this, "Tidak bisa membuka:\r\n" + ex.Message, Text); }
        }
    }
}
