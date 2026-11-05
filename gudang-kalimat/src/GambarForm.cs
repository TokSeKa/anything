using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace GudangKalimat
{
    /// <summary>Ubah gambar (atau tulisan) menjadi teks yang bisa ditempel di chat.</summary>
    public class GambarForm : Form
    {
        readonly RadioButton sumberGambar = new RadioButton { Text = "Gambar", Checked = true, AutoSize = true };
        readonly RadioButton sumberTulisan = new RadioButton { Text = "Tulisan besar", AutoSize = true };
        readonly Button buka = new Button { Text = "Buka gambar…", AutoSize = true };
        readonly Button tempel = new Button { Text = "Tempel (Ctrl+V)", AutoSize = true };
        readonly TextBox tulisan = new TextBox { Text = "HALO", Width = 250 };
        readonly CheckBox tebal = new CheckBox { Text = "Huruf tebal", Checked = true, AutoSize = true };
        readonly ComboBox gaya = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
        readonly NumericUpDown lebar = new NumericUpDown { Minimum = 8, Maximum = 200, Value = 40, Width = 70 };
        readonly TrackBar ambang = new TrackBar { Minimum = 5, Maximum = 95, Value = 50, TickFrequency = 10, Width = 250 };
        readonly TrackBar kontras = new TrackBar { Minimum = -50, Maximum = 50, Value = 0, TickFrequency = 10, Width = 250 };
        readonly CheckBox dither = new CheckBox { Text = "Gradasi titik (dither)", Checked = true, AutoSize = true };
        readonly CheckBox balik = new CheckBox { Text = "Balik terang/gelap (latar gelap)", AutoSize = true };
        readonly Label info = new Label { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(255, 0) };
        readonly TextBox hasil = new TextBox();
        readonly Timer timer = new Timer { Interval = 150 };
        Image gambar;

        public GambarForm(Func<string, bool> simpanKalimat)
        {
            Ui.Dasar(this, "Gambar → Teks", new Size(980, 680));
            MaximizeBox = true;
            MinimumSize = new Size(700, 480);
            KeyPreview = true;
            AllowDrop = true;

            gaya.Items.AddRange(new object[] { "Braille ⣿ (paling detail, cocok untuk chat)", "Blok █▓▒░", "ASCII klasik (@#*+:.)" });
            gaya.SelectedIndex = 0;

            var kiri = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 295, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                                             AutoScroll = true, Padding = new Padding(10) };
            var baris = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            baris.Controls.Add(sumberGambar);
            baris.Controls.Add(sumberTulisan);
            kiri.Controls.Add(Judul("Sumber"));
            kiri.Controls.Add(baris);
            kiri.Controls.Add(buka);
            kiri.Controls.Add(tempel);
            kiri.Controls.Add(Catatan("Atau seret file gambar ke jendela ini."));
            kiri.Controls.Add(tulisan);
            kiri.Controls.Add(tebal);
            kiri.Controls.Add(Judul("Gaya"));
            kiri.Controls.Add(gaya);
            var bl = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            bl.Controls.Add(new Label { Text = "Lebar (karakter):", AutoSize = true, Margin = new Padding(3, 6, 3, 0) });
            bl.Controls.Add(lebar);
            kiri.Controls.Add(bl);
            kiri.Controls.Add(Catatan("WhatsApp di HP: ±30–35 karakter supaya tidak terpotong. Discord/PC: boleh lebih lebar."));
            kiri.Controls.Add(new Label { Text = "Ketebalan (braille)", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            kiri.Controls.Add(ambang);
            kiri.Controls.Add(new Label { Text = "Kontras", AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
            kiri.Controls.Add(kontras);
            kiri.Controls.Add(dither);
            kiri.Controls.Add(balik);
            kiri.Controls.Add(info);
            var salin = new Button { Text = "📋 Salin", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            var simpan = new Button { Text = "Simpan jadi kalimat", AutoSize = true };
            var txt = new Button { Text = "Simpan .txt…", AutoSize = true };
            var bawah = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            bawah.Controls.Add(salin);
            bawah.Controls.Add(simpan);
            bawah.Controls.Add(txt);

            hasil.Multiline = true;
            hasil.ReadOnly = true;
            hasil.BackColor = Color.White;
            hasil.WordWrap = false;
            hasil.ScrollBars = ScrollBars.Both;
            hasil.Dock = DockStyle.Fill;
            hasil.Font = new Font("Consolas", 10f);
            hasil.AllowDrop = true;

            Controls.Add(hasil);
            Controls.Add(bawah);
            Controls.Add(kiri);

            EventHandler ubah = delegate { timer.Stop(); timer.Start(); };
            foreach (Control c in new Control[] { lebar, ambang, kontras, dither, balik, gaya, tulisan, tebal, sumberGambar, sumberTulisan })
            {
                if (c is NumericUpDown) ((NumericUpDown)c).ValueChanged += ubah;
                else if (c is TrackBar) ((TrackBar)c).ValueChanged += ubah;
                else if (c is CheckBox) ((CheckBox)c).CheckedChanged += ubah;
                else if (c is RadioButton) ((RadioButton)c).CheckedChanged += ubah;
                else if (c is ComboBox) ((ComboBox)c).SelectedIndexChanged += ubah;
                else c.TextChanged += ubah;
            }
            timer.Tick += delegate { timer.Stop(); Buat(); };
            buka.Click += delegate { BukaFile(); };
            tempel.Click += delegate { TempelClipboard(); };
            salin.Click += delegate { Salin(); };
            simpan.Click += delegate
            {
                if (hasil.Text.Length == 0) return;
                if (simpanKalimat != null && simpanKalimat(hasil.Text)) info.Text = "Disimpan ke daftar kalimat (kategori \"Gambar\").";
            };
            txt.Click += delegate { SimpanTxt(); };
            DragEnter += DragMasuk;
            DragDrop += DragLepas;
            hasil.DragEnter += DragMasuk;
            hasil.DragDrop += DragLepas;
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.V && !tulisan.Focused) { TempelClipboard(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.C && !tulisan.Focused && hasil.SelectionLength == 0) { Salin(); e.Handled = true; }
            };
            Shown += delegate
            {
                if (Clipboard.ContainsImage()) TempelClipboard();
                else Buat();
            };
            FormClosed += delegate { if (gambar != null) gambar.Dispose(); };
        }

        static Label Judul(string s)
        {
            return new Label { Text = s.ToUpperInvariant(), AutoSize = true, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                               Margin = new Padding(3, 12, 3, 2) };
        }

        static Label Catatan(string s)
        {
            return new Label { Text = s, AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(255, 0), Margin = new Padding(3, 0, 3, 4) };
        }

        OpsiArt Opsi()
        {
            return new OpsiArt
            {
                Gaya = gaya.SelectedIndex == 1 ? GayaArt.Blok : gaya.SelectedIndex == 2 ? GayaArt.Ascii : GayaArt.Braille,
                Lebar = (int)lebar.Value, Ambang = ambang.Value, Kontras = kontras.Value, Dither = dither.Checked, Balik = balik.Checked,
            };
        }

        public void Buat()
        {
            bool modeTulisan = sumberTulisan.Checked;
            tulisan.Enabled = tebal.Enabled = modeTulisan;
            buka.Enabled = tempel.Enabled = !modeTulisan;
            ambang.Enabled = dither.Enabled = gaya.SelectedIndex == 0;
            try
            {
                if (modeTulisan)
                {
                    using (var bmp = TeksArt.GambarTulisan(tulisan.Text, "Arial", tebal.Checked))
                    {
                        var o = Opsi();
                        o.Dither = false;   // huruf lebih tajam tanpa gradasi
                        Tampilkan(TeksArt.Buat(bmp, o));
                    }
                }
                else if (gambar != null) Tampilkan(TeksArt.Buat(gambar, Opsi()));
                else
                {
                    hasil.Text = "";
                    info.Text = "Buka gambar, tempel dari clipboard (Ctrl+V), seret file ke sini, atau pilih \"Tulisan besar\".";
                }
            }
            catch (Exception ex)
            {
                info.Text = "Gagal membuat teks: " + ex.Message;
            }
        }

        void Tampilkan(string teks)
        {
            hasil.Text = teks;
            int barisN = teks.Length == 0 ? 0 : teks.Split('\n').Length;
            info.Text = barisN + " baris × " + lebar.Value + " karakter · " + teks.Length + " huruf.\r\nTips: kalau berantakan di chat, perkecil lebarnya.";
        }

        public void SetGambar(Image img)
        {
            if (gambar != null) gambar.Dispose();
            gambar = img;
            sumberGambar.Checked = true;
            Buat();
        }

        void BukaFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Pilih gambar";
                dlg.Filter = "Gambar|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico|Semua file|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK) MuatFile(dlg.FileName);
            }
        }

        void MuatFile(string path)
        {
            try
            {
                // salin ke memori supaya file tidak terkunci selama jendela terbuka
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                    SetGambar(new Bitmap(img));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "File itu bukan gambar yang bisa dibuka (" + ex.Message + ").\r\nCoba PNG atau JPG.", Text);
            }
        }

        void TempelClipboard()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    if (img != null) { SetGambar(new Bitmap(img)); img.Dispose(); return; }
                }
                if (Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Count > 0)
                {
                    MuatFile(Clipboard.GetFileDropList()[0]);
                    return;
                }
                info.Text = "Clipboard tidak berisi gambar. Salin gambar dulu (misalnya klik kanan → Copy image, atau Win+Shift+S).";
            }
            catch (Exception ex) { info.Text = "Tidak bisa membaca clipboard: " + ex.Message; }
        }

        void DragMasuk(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap)) e.Effect = DragDropEffects.Copy;
        }

        void DragLepas(object s, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) { MuatFile(files[0]); return; }
            var bmp = e.Data.GetData(DataFormats.Bitmap) as Image;
            if (bmp != null) SetGambar(new Bitmap(bmp));
        }

        void Salin()
        {
            if (hasil.Text.Length == 0) return;
            try
            {
                Clipboard.SetText(hasil.Text);
                info.Text = "Tersalin ✓ Tempel di chat. Kalau bentuknya bergeser di WhatsApp, apit dengan ``` (tiga backtick).";
            }
            catch (Exception ex) { info.Text = "Clipboard sedang dipakai: " + ex.Message; }
        }

        void SimpanTxt()
        {
            if (hasil.Text.Length == 0) return;
            using (var dlg = new SaveFileDialog { Filter = "Teks (*.txt)|*.txt", FileName = "gambar-teks.txt" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dlg.FileName, hasil.Text, new UTF8Encoding(true));
                info.Text = "Disimpan: " + Path.GetFileName(dlg.FileName);
            }
        }
    }
}
