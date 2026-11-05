using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Gudang Kalimat")]
[assembly: AssemblyProduct("Gudang Kalimat")]
[assembly: AssemblyDescription("Simpan kalimat favorit, cari, acak, dan salin dengan sekali klik.")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace GudangKalimat
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // satu jendela saja: kalau sudah terbuka, pakai pintasan Ctrl+Alt+K atau taskbar
            bool baru;
            using (var mutex = new Mutex(true, "GudangKalimat-satu-jendela", out baru))
            {
                if (!baru && Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    MessageBox.Show("Gudang Kalimat sudah terbuka. Tekan Ctrl+Alt+K atau klik ikonnya di taskbar.", "Gudang Kalimat",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Win.DpiAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) =>
                    MessageBox.Show("Terjadi kesalahan:\r\n" + e.Exception.Message, "Gudang Kalimat", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Run(new MainForm());
            }
        }
    }
}
