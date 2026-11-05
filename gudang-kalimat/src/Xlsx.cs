using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using System.Xml;

namespace GudangKalimat
{
    /// <summary>Baca/tulis .xlsx tanpa library tambahan (xlsx = zip berisi XML). Hanya sheet pertama, hanya isi sel.</summary>
    public static class Xlsx
    {
        const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string NsPkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static List<List<string>> Baca(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                var shared = new List<string>();
                var ss = Entri(zip, "xl/sharedStrings.xml");
                if (ss != null)
                {
                    var doc = MuatXml(ss);
                    var ns = Ns(doc);
                    foreach (XmlNode si in doc.SelectNodes("//m:si", ns))
                        shared.Add(string.Concat(si.SelectNodes(".//m:t", ns).Cast<XmlNode>().Select(t => t.InnerText)));
                }
                var sheetPath = SheetPertama(zip);
                var sheet = Entri(zip, sheetPath);
                if (sheet == null) throw new InvalidDataException("Sheet tidak ditemukan di file Excel.");
                var sd = MuatXml(sheet);
                var sn = Ns(sd);
                var rows = new List<List<string>>();
                foreach (XmlNode r in sd.SelectNodes("//m:sheetData/m:row", sn))
                {
                    var row = new List<string>();
                    foreach (XmlNode c in r.SelectNodes("m:c", sn))
                    {
                        var refAttr = c.Attributes["r"];
                        int col = refAttr != null ? Kolom(refAttr.Value) : row.Count;
                        while (row.Count < col) row.Add("");
                        var tAttr = c.Attributes["t"];
                        var t = tAttr != null ? tAttr.Value : "";
                        string val;
                        if (t == "inlineStr")
                            val = string.Concat(c.SelectNodes(".//m:t", sn).Cast<XmlNode>().Select(x => x.InnerText));
                        else
                        {
                            var v = c.SelectSingleNode("m:v", sn);
                            val = v != null ? v.InnerText : "";
                            int idx;
                            if (t == "s" && int.TryParse(val, out idx) && idx >= 0 && idx < shared.Count) val = shared[idx];
                            else if (t == "b") val = val == "1" ? "TRUE" : "FALSE";
                        }
                        row.Add(val);
                    }
                    // baris kosong di tengah tetap dilewati
                    if (row.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(row);
                }
                return rows;
            }
        }

        static string SheetPertama(ZipArchive zip)
        {
            var wb = Entri(zip, "xl/workbook.xml");
            var rels = Entri(zip, "xl/_rels/workbook.xml.rels");
            if (wb == null || rels == null) return "xl/worksheets/sheet1.xml";
            var w = MuatXml(wb);
            var ns = Ns(w);
            ns.AddNamespace("r", NsRel);
            var first = w.SelectSingleNode("//m:sheets/m:sheet", ns);
            if (first == null) return "xl/worksheets/sheet1.xml";
            var rid = first.Attributes["id", NsRel];
            if (rid == null) return "xl/worksheets/sheet1.xml";
            var rd = MuatXml(rels);
            var rns = new XmlNamespaceManager(rd.NameTable);
            rns.AddNamespace("p", NsPkgRel);
            foreach (XmlNode rel in rd.SelectNodes("//p:Relationship", rns))
            {
                if (rel.Attributes["Id"] != null && rel.Attributes["Id"].Value == rid.Value)
                {
                    var target = rel.Attributes["Target"].Value.TrimStart('/');
                    return target.StartsWith("xl/") ? target : "xl/" + target;
                }
            }
            return "xl/worksheets/sheet1.xml";
        }

        static ZipArchiveEntry Entri(ZipArchive zip, string name)
        {
            return zip.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), name, StringComparison.OrdinalIgnoreCase));
        }

        static XmlDocument MuatXml(ZipArchiveEntry e)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var s = e.Open()) doc.Load(s);
            return doc;
        }

        static XmlNamespaceManager Ns(XmlDocument d)
        {
            var ns = new XmlNamespaceManager(d.NameTable);
            ns.AddNamespace("m", NsMain);
            return ns;
        }

        public static int Kolom(string cellRef)
        {
            int col = 0;
            foreach (var ch in cellRef)
            {
                if (ch >= 'A' && ch <= 'Z') col = col * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z') col = col * 26 + (ch - 'a' + 1);
                else break;
            }
            return Math.Max(0, col - 1);
        }

        public static string NamaKolom(int idx)
        {
            var s = "";
            idx++;
            while (idx > 0)
            {
                int m = (idx - 1) % 26;
                s = (char)('A' + m) + s;
                idx = (idx - 1) / 26;
            }
            return s;
        }

        public static void Tulis(string path, List<List<string>> rows)
        {
            if (File.Exists(path)) File.Delete(path);
            using (var fs = new FileStream(path, FileMode.CreateNew))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Tambah(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                    "</Types>");
                Tambah(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"" + NsPkgRel + "\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");
                Tambah(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"" + NsMain + "\" xmlns:r=\"" + NsRel + "\">" +
                    "<sheets><sheet name=\"Kalimat\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Tambah(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"" + NsPkgRel + "\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");
                Tambah(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"" + NsMain + "\">" +
                    "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                    "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                    "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf></cellXfs>" +
                    "</styleSheet>");
                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sb.Append("<worksheet xmlns=\"" + NsMain + "\">");
                sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
                sb.Append("<cols><col min=\"1\" max=\"1\" width=\"70\" customWidth=\"1\"/><col min=\"2\" max=\"3\" width=\"18\" customWidth=\"1\"/><col min=\"4\" max=\"5\" width=\"14\" customWidth=\"1\"/></cols>");
                sb.Append("<sheetData>");
                for (int r = 0; r < rows.Count; r++)
                {
                    sb.Append("<row r=\"" + (r + 1) + "\">");
                    for (int c = 0; c < rows[r].Count; c++)
                    {
                        var v = rows[r][c] ?? "";
                        int style = r == 0 ? 1 : (c == 0 ? 2 : 0);
                        sb.Append("<c r=\"" + NamaKolom(c) + (r + 1) + "\" t=\"inlineStr\" s=\"" + style + "\"><is><t xml:space=\"preserve\">");
                        sb.Append(SecurityElement.Escape(BersihkanXml(v)));
                        sb.Append("</t></is></c>");
                    }
                    sb.Append("</row>");
                }
                sb.Append("</sheetData></worksheet>");
                Tambah(zip, "xl/worksheets/sheet1.xml", sb.ToString());
            }
        }

        static string BersihkanXml(string s)
        {
            // karakter kontrol tidak boleh ada di XML
            return new string(s.Where(ch => ch == '\t' || ch == '\n' || ch == '\r' || ch >= ' ').ToArray());
        }

        static void Tambah(ZipArchive zip, string name, string isi)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(isi);
        }
    }
}
