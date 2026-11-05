using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace GudangKalimat
{
    public enum GayaArt { Braille, Blok, Ascii }

    public class OpsiArt
    {
        public GayaArt Gaya = GayaArt.Braille;
        public int Lebar = 40;          // jumlah karakter per baris
        public int Ambang = 50;         // 0..100: makin besar makin banyak bagian yang dianggap gelap (braille)
        public int Kontras = 0;         // -50..50
        public bool Dither = true;      // braille: titik-titik bergradasi (Floyd–Steinberg)
        public bool Balik;              // tukar terang/gelap (untuk mode gelap / latar hitam)
    }

    /// <summary>Mengubah gambar menjadi teks (braille, blok, atau ASCII).</summary>
    public static class TeksArt
    {
        const string RampAscii = " .:-=+*#%@";      // terang → gelap
        static readonly char[] RampBlok = { ' ', '░', '▒', '▓', '█' };

        /// <summary>Kecerahan 0 (hitam) .. 1 (putih) per piksel setelah gambar diperkecil. Transparan = putih.</summary>
        public static float[,] Kecerahan(Image src, int w, int h)
        {
            w = Math.Max(1, w);
            h = Math.Max(1, h);
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(src, new Rectangle(0, 0, w, h));
                }
                var lum = new float[w, h];
                var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var stride = data.Stride;
                    var buf = new byte[stride * h];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, buf.Length);
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int i = y * stride + x * 4;   // B G R A
                            lum[x, y] = (0.114f * buf[i] + 0.587f * buf[i + 1] + 0.299f * buf[i + 2]) / 255f;
                        }
                }
                finally { bmp.UnlockBits(data); }
                return lum;
            }
        }

        /// <summary>Normalisasi rentang terang-gelap, kontras, dan balik warna.</summary>
        static void Atur(float[,] lum, OpsiArt o)
        {
            int w = lum.GetLength(0), h = lum.GetLength(1);
            float min = 1, max = 0;
            foreach (var v in lum) { if (v < min) min = v; if (v > max) max = v; }
            float span = max - min;
            float k = 1f + o.Kontras / 50f;   // 0..2
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = span > 0.05f ? (lum[x, y] - min) / span : lum[x, y];
                    v = (v - 0.5f) * k + 0.5f;
                    if (o.Balik) v = 1 - v;
                    lum[x, y] = Math.Max(0f, Math.Min(1f, v));
                }
        }

        public static string Buat(Image src, OpsiArt o)
        {
            int cols = Math.Max(4, Math.Min(400, o.Lebar));
            double rasio = (double)src.Height / Math.Max(1, src.Width);
            if (o.Gaya == GayaArt.Braille)
            {
                // satu karakter = 2×4 titik; titik braille kira-kira persegi
                int pw = cols * 2;
                int rows = Math.Max(1, (int)Math.Round(pw * rasio / 4.0));
                var lum = Kecerahan(src, pw, rows * 4);
                Atur(lum, o);
                return Braille(lum, cols, rows, o.Ambang / 100f, o.Dither);
            }
            // blok/ASCII: karakter kira-kira 2× lebih tinggi daripada lebarnya
            int r = Math.Max(1, (int)Math.Round(cols * rasio * 0.5));
            var l = Kecerahan(src, cols, r);
            Atur(l, o);
            var baris = new string[r];
            for (int y = 0; y < r; y++)
            {
                var sb = new StringBuilder(cols);
                for (int x = 0; x < cols; x++)
                {
                    float gelap = 1 - l[x, y];
                    if (o.Gaya == GayaArt.Blok)
                        sb.Append(RampBlok[Math.Min(RampBlok.Length - 1, (int)(gelap * RampBlok.Length))]);
                    else
                        sb.Append(RampAscii[Math.Min(RampAscii.Length - 1, (int)(gelap * RampAscii.Length))]);
                }
                baris[y] = sb.ToString().TrimEnd(' ');   // spasi di ujung baris tidak terlihat
            }
            return string.Join("\r\n", baris);
        }

        // bit titik braille: (x, y) → bit; urutan Unicode: 1 4 / 2 5 / 3 6 / 7 8
        static readonly int[,] BitBraille = { { 0x01, 0x08 }, { 0x02, 0x10 }, { 0x04, 0x20 }, { 0x40, 0x80 } };

        static string Braille(float[,] lum, int cols, int rows, float ambang, bool dither)
        {
            int w = lum.GetLength(0), h = lum.GetLength(1);
            var titik = new bool[w, h];
            if (dither)
            {
                var e = (float[,])lum.Clone();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float lama = e[x, y];
                        bool gelap = lama < ambang;
                        titik[x, y] = gelap;
                        float err = lama - (gelap ? 0f : 1f);
                        if (x + 1 < w) e[x + 1, y] += err * 7 / 16f;
                        if (y + 1 < h)
                        {
                            if (x > 0) e[x - 1, y + 1] += err * 3 / 16f;
                            e[x, y + 1] += err * 5 / 16f;
                            if (x + 1 < w) e[x + 1, y + 1] += err * 1 / 16f;
                        }
                    }
            }
            else
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        titik[x, y] = lum[x, y] < ambang;
            }
            var sb = new StringBuilder();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int code = 0;
                    for (int dy = 0; dy < 4; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int x = c * 2 + dx, y = r * 4 + dy;
                            if (x < w && y < h && titik[x, y]) code |= BitBraille[dy, dx];
                        }
                    // sel kosong tetap U+2800 (bukan spasi) supaya lebar baris tetap sama di aplikasi chat
                    sb.Append((char)(0x2800 + code));
                }
                if (r < rows - 1) sb.Append("\r\n");
            }
            return sb.ToString();
        }

        /// <summary>Gambar dari tulisan (untuk "tulisan besar"): teks hitam di latar putih.</summary>
        public static Bitmap GambarTulisan(string teks, string font, bool tebal)
        {
            teks = string.IsNullOrEmpty(teks) ? " " : teks;
            using (var f = new Font(string.IsNullOrEmpty(font) ? "Arial" : font, 64, tebal ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            {
                SizeF ukuran;
                using (var tmp = new Bitmap(1, 1))
                using (var g = Graphics.FromImage(tmp))
                    ukuran = g.MeasureString(teks, f);
                var bmp = new Bitmap(Math.Max(1, (int)Math.Ceiling(ukuran.Width)), Math.Max(1, (int)Math.Ceiling(ukuran.Height)));
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.DrawString(teks, f, Brushes.Black, 0, 0);
                }
                return Potong(bmp);
            }
        }

        /// <summary>Buang pinggiran putih supaya tulisan memenuhi lebar.</summary>
        public static Bitmap Potong(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var lum = Kecerahan(bmp, w, h);
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (lum[x, y] < 0.9f)
                    {
                        if (x < x0) x0 = x;
                        if (y < y0) y0 = y;
                        if (x > x1) x1 = x;
                        if (y > y1) y1 = y;
                    }
            if (x1 < 0) return bmp;
            var hasil = bmp.Clone(new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1), bmp.PixelFormat);
            bmp.Dispose();
            return hasil;
        }

        public static bool TerlihatSepertiArt(string teks)
        {
            if (string.IsNullOrEmpty(teks) || teks.IndexOf('\n') < 0) return false;
            int khusus = 0;
            foreach (var ch in teks)
                if ((ch >= 0x2800 && ch <= 0x28FF) || (ch >= 0x2580 && ch <= 0x259F)) khusus++;
            return khusus > 8;
        }
    }
}
