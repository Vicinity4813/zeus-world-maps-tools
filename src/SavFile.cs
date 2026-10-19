// Lecture/reecriture des sauvegardes .sav de Zeus, par parcours du manifeste (meme principe que les cartes MAPS).
// Les blocs sont lus jusqu'a celui des routes commerciales (324 x 232) ; tout ce qui suit est recopie tel quel.
// Une sauvegarde peut contenir plusieurs parties completes (en-tete + manifeste + blocs), les unes a la suite des
// autres : dans une aventure, la partie suivante porte la mappemonde que le jeu reprend a l'episode suivant.
// Chaque partie a ses propres blocs mappemonde et routes ; elles sont reperees et reecrites toutes.
using System;
using System.Collections.Generic;
using System.IO;
using ZeusPak;

public class SavFile
{
    public byte[] Bytes;
    public int Base;                       // debut de la partie dans le fichier
    public byte[] Header;                  // versions + manifeste (8 + 6000 octets)
    public List<Block> Blocks = new List<Block>();
    public int TailStart;                  // fin des blocs lus (absolu) ; la suite est recopiee telle quelle
    public int ElementsIndex = -1, RoutesIndex = -1;
    public List<SavFile> Extra = new List<SavFile>();   // parties suivantes (seulement pour la premiere)

    public static SavFile Load(string path)
    {
        byte[] f = File.ReadAllBytes(path);
        SavFile s = Parse(f, 0);
        // Parties suivantes : un manifeste identique (memes versions, bloc des routes a la meme entree) plus loin.
        int from = s.TailStart, entry = 8 + s.RoutesIndex * 20 + 8;
        for (int i = from; i + 8 <= f.Length; i++)
        {
            if (BitConverter.ToUInt32(f, i) != 324 || BitConverter.ToUInt32(f, i + 4) != 232) continue;
            int b = i - entry;
            if (b < from || !SameBytes(f, 0, f, b, 8)) continue;
            SavFile x;
            try { x = Parse(f, b); } catch (InvalidDataException) { continue; }
            if (x.RoutesIndex != s.RoutesIndex) continue;
            s.Extra.Add(x);
            from = x.TailStart;
            i = from - 1;
        }
        return s;
    }

    static bool SameBytes(byte[] a, int ao, byte[] b, int bo, int n)
    {
        for (int k = 0; k < n; k++) if (a[ao + k] != b[bo + k]) return false;
        return true;
    }

    public static SavFile Parse(byte[] f, int start)
    {
        SavFile s = new SavFile();
        s.Bytes = f; s.Base = start;
        int man = start + 8;
        if (man + 300 * 20 > f.Length) throw new InvalidDataException("manifeste tronque");
        s.Header = MapFile.Sub(f, start, 8 + 300 * 20);
        // reperage des entrees utiles
        for (int i = 0; i < 300; i++)
        {
            int e = man + i * 20;
            uint size = BitConverter.ToUInt32(f, e + 8), count = BitConverter.ToUInt32(f, e + 12);
            if ((size == 72 || size == 40) && count == 200 && s.ElementsIndex < 0) s.ElementsIndex = i;
            if (size == 324 && count == 232 && s.RoutesIndex < 0) s.RoutesIndex = i;
        }
        if (s.ElementsIndex < 0 || s.RoutesIndex < 0) throw new InvalidDataException("blocs de la mappemonde introuvables dans le manifeste");
        int p = man + 300 * 20;
        // entrees 0, 1 et 2 = versions et manifeste, deja lus
        for (int i = 3; i <= s.RoutesIndex; i++)
        {
            int e = man + i * 20;
            uint comp = BitConverter.ToUInt32(f, e), size = BitConverter.ToUInt32(f, e + 8), count = BitConverter.ToUInt32(f, e + 12);
            if (size == 0) throw new InvalidDataException("entree vide avant le bloc des routes");
            Block b = new Block();
            b.Index = i; b.Start = p; b.ElemSize = (int)size; b.Count = (int)count; b.Compressed = comp != 0;
            int rawLen = checked((int)(size * count));
            if (b.Compressed)
            {
                if (p + 4 > f.Length) throw new InvalidDataException("bloc " + i + " tronque");
                int cs = BitConverter.ToInt32(f, p); p += 4;
                if (cs < 0) { if (rawLen > f.Length - p) throw new InvalidDataException("bloc " + i + " tronque"); b.Data = MapFile.Sub(f, p, rawLen); p += rawLen; }
                else
                {
                    if (cs > f.Length - p) throw new InvalidDataException("bloc " + i + " : taille compressee incoherente");
                    try { b.Data = Pk.Explode(MapFile.Sub(f, p, cs)); }
                    catch (Exception ex) { throw new InvalidDataException("bloc " + i + " illisible : " + ex.Message); }
                    p += cs;
                    if (b.Data.Length < rawLen) throw new InvalidDataException("bloc " + i + " trop court apres decompression");
                }
            }
            else { if (rawLen > f.Length - p) throw new InvalidDataException("bloc " + i + " tronque"); b.Data = MapFile.Sub(f, p, rawLen); p += rawLen; }
            b.End = p;
            b.Raw = MapFile.Sub(f, b.Start, b.End - b.Start);
            s.Blocks.Add(b);
        }
        s.TailStart = p;
        return s;
    }

    public Block Get(int index) { foreach (Block b in Blocks) if (b.Index == index) return b; return null; }

    // Toutes les parties : celle-ci puis les suivantes.
    public IEnumerable<SavFile> AllParts() { yield return this; foreach (SavFile x in Extra) yield return x; }

    public byte[] Build()
    {
        MemoryStream ms = new MemoryStream();
        WritePart(ms, this);
        int pos = TailStart;
        foreach (SavFile x in Extra)
        {
            ms.Write(Bytes, pos, x.Base - pos);   // ce qui separe les parties, tel quel
            WritePart(ms, x);
            pos = x.TailStart;
        }
        ms.Write(Bytes, pos, Bytes.Length - pos);
        return ms.ToArray();
    }

    static void WritePart(MemoryStream ms, SavFile s)
    {
        ms.Write(s.Header, 0, s.Header.Length);
        foreach (Block b in s.Blocks)
        {
            if (!b.Modified) { ms.Write(b.Raw, 0, b.Raw.Length); continue; }
            if (!b.Compressed) { ms.Write(b.Data, 0, b.Data.Length); continue; }
            byte[] c = Pk.Implode(b.Data);
            byte[] back = Pk.Explode(c);
            if (back.Length != b.Data.Length) throw new Exception("implode : relecture de taille differente");
            for (int i = 0; i < back.Length; i++) if (back[i] != b.Data[i]) throw new Exception("implode : relecture differente");
            if (c.Length < b.Data.Length) { ms.Write(BitConverter.GetBytes(c.Length), 0, 4); ms.Write(c, 0, c.Length); }
            else { ms.Write(BitConverter.GetBytes(int.MinValue), 0, 4); ms.Write(b.Data, 0, b.Data.Length); }
        }
    }
}
