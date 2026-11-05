using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GudangKalimat
{
    /// <summary>CSV sesuai Excel: tanda kutip ganda, baris ganda di dalam sel, pemisah ; , atau tab (dideteksi otomatis).</summary>
    public static class Csv
    {
        public static char PemisahSistem()
        {
            var s = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            return string.IsNullOrEmpty(s) ? ',' : s[0];
        }

        public static char Deteksi(string teks)
        {
            // hitung kandidat pemisah di baris pertama (di luar tanda kutip)
            int koma = 0, titikKoma = 0, tab = 0;
            bool kutip = false;
            foreach (var c in teks)
            {
                if (c == '"') kutip = !kutip;
                else if (!kutip && (c == '\n' || c == '\r')) break;
                else if (!kutip && c == ',') koma++;
                else if (!kutip && c == ';') titikKoma++;
                else if (!kutip && c == '\t') tab++;
            }
            if (tab > koma && tab > titikKoma) return '\t';
            if (titikKoma > koma) return ';';
            if (koma > 0) return ',';
            return titikKoma > 0 ? ';' : ',';
        }

        public static List<List<string>> Parse(string teks)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(teks)) return rows;
            if (teks.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && teks.Length > 4)
            {
                // baris "sep=;" yang kadang ditulis Excel
                var nl = teks.IndexOf('\n');
                char sepBaris = teks[4];
                teks = nl >= 0 ? teks.Substring(nl + 1) : "";
                return ParseDengan(teks, sepBaris);
            }
            return ParseDengan(teks, Deteksi(teks));
        }

        static List<List<string>> ParseDengan(string teks, char sep)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var sel = new StringBuilder();
            bool kutip = false, adaIsi = false;
            for (int i = 0; i < teks.Length; i++)
            {
                char c = teks[i];
                if (kutip)
                {
                    if (c == '"')
                    {
                        if (i + 1 < teks.Length && teks[i + 1] == '"') { sel.Append('"'); i++; }
                        else kutip = false;
                    }
                    else sel.Append(c);
                    continue;
                }
                if (c == '"') { kutip = true; adaIsi = true; }
                else if (c == sep) { row.Add(sel.ToString()); sel.Clear(); adaIsi = true; }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < teks.Length && teks[i + 1] == '\n') i++;
                    if (adaIsi || sel.Length > 0) { row.Add(sel.ToString()); rows.Add(row); }
                    row = new List<string>();
                    sel.Clear();
                    adaIsi = false;
                }
                else { sel.Append(c); adaIsi = true; }
            }
            if (adaIsi || sel.Length > 0) { row.Add(sel.ToString()); rows.Add(row); }
            return rows;
        }

        public static string Tulis(List<List<string>> rows, char sep)
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                sb.Append(string.Join(sep.ToString(), row.Select(v => Kutip(v ?? "", sep))));
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        static string Kutip(string v, char sep)
        {
            bool perlu = v.IndexOf(sep) >= 0 || v.IndexOf('"') >= 0 || v.IndexOf('\n') >= 0 || v.IndexOf('\r') >= 0
                         || v.StartsWith(" ") || v.EndsWith(" ");
            return perlu ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }
    }
}
