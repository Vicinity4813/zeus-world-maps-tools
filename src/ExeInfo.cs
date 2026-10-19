// Taille de mappemonde pour un Zeus.exe donne : celle de la mappemonde installee a cote (DATA\Zeus_MapOfGreece01.jpg,
// si elle a deja ete agrandie), sinon celle de la vue d'apres la resolution ecrite dans l'exe (meme lecture que le
// patcheur : exe anglais 2.1.4.0, mov esi,largeur / mov ecx,hauteur a l'offset 0x10BA28 ; exe francais, 0x57C8CF / 0x57C8D9).
using System;
using System.Drawing;
using System.IO;

public static class ExeInfo
{
    // { largeur, hauteur } de la mappemonde et explication, ou null
    public static int[] MapSizeFor(string exePath, out string how)
    {
        how = null;
        string data = Path.Combine(Path.GetDirectoryName(exePath), "DATA");
        if (Directory.Exists(data))
            foreach (string f in Directory.GetFiles(data, "Zeus_MapOfGreece01.*"))
            {
                if (!f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    using (Image im = Image.FromFile(f))
                        if (im.Width != 838 || im.Height != 738)
                        {
                            how = string.Format(L.T("mappemonde installée ({0} x {1})", "installed world map ({0} x {1})"), im.Width, im.Height);
                            return new[] { im.Width, im.Height };
                        }
                }
                catch { }
            }
        int[] r = Resolution(File.ReadAllBytes(exePath));
        if (r == null) return null;
        int[] v = ViewOf(r[0], r[1]);
        how = string.Format(L.T("résolution de l'exe {0} x {1}", "exe resolution {0} x {1}"), r[0], r[1]);
        return v;
    }

    // Vue (et mappemonde) pour une resolution : cases de 60 px moins 2 en largeur, rangees de 15 px sous la barre du haut.
    public static int[] ViewOf(int w, int h) { return new[] { 60 * ((w - 180) / 60) - 2, ((h - 30) / 15) * 15 }; }

    public static int[] Resolution(byte[] b)
    {
        int o = 0x10BA28;
        if (o + 10 <= b.Length && b[o] == 0xBE && b[o + 5] == 0xB9)
        {
            int w = BitConverter.ToInt32(b, o + 1), h = BitConverter.ToInt32(b, o + 6);
            if (w >= 800 && w <= 8000 && h >= 600 && h <= 5000) return new[] { w, h };
        }
        try
        {
            Pe pe = new Pe(b);
            int fw = pe.Off(0x0057C8CF), fh = pe.Off(0x0057C8D9);
            if (fw > 0 && fh > 0 && b[fw] == 0xC7 && b[fw + 1] == 0x05 && b[fw + 2] == 0x1C && b[fh] == 0xC7 && b[fh + 1] == 0x05 && b[fh + 2] == 0x18)
                return new[] { BitConverter.ToInt32(b, fw + 6), BitConverter.ToInt32(b, fh + 6) };
        }
        catch { }
        return null;
    }
}
