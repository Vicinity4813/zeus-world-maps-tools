// Outils PE minimaux : conversion adresse <-> position dans le fichier, lecture d'octets, recherche de motifs.
using System;
using System.Collections.Generic;
using System.Text;

public class Pe
{
    public byte[] B;
    public uint ImageBase;
    public List<uint[]> Secs = new List<uint[]>();   // VA, VirtSize, RawOff, RawSize, Flags, offset de l'en-tete
    public List<string> Names = new List<string>();

    public Pe(byte[] b)
    {
        B = b;
        int pe = BitConverter.ToInt32(b, 0x3C);
        int n = BitConverter.ToUInt16(b, pe + 6), opt = BitConverter.ToUInt16(b, pe + 20), s0 = pe + 24 + opt;
        ImageBase = BitConverter.ToUInt32(b, pe + 24 + 28);
        for (int i = 0; i < n; i++)
        {
            int o = s0 + 40 * i;
            Names.Add(Encoding.ASCII.GetString(b, o, 8).TrimEnd('\0'));
            Secs.Add(new uint[] { BitConverter.ToUInt32(b, o + 12) + ImageBase, BitConverter.ToUInt32(b, o + 8),
                BitConverter.ToUInt32(b, o + 20), BitConverter.ToUInt32(b, o + 16), BitConverter.ToUInt32(b, o + 36), (uint)o });
        }
    }

    public int Off(uint va)
    {
        foreach (uint[] s in Secs) if (va >= s[0] && va < s[0] + Math.Max(s[1], s[3]) && va - s[0] < s[3]) return (int)(va - s[0] + s[2]);
        return -1;
    }

    public uint Va(int off)
    {
        foreach (uint[] s in Secs) if (off >= s[2] && off < s[2] + s[3]) return (uint)(off - s[2]) + s[0];
        return 0;
    }

    public string Hex(uint va, int len)
    {
        int o = Off(va); if (o < 0) return "(hors fichier)";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < len; i++) sb.Append(B[o + i].ToString("X2")).Append(' ');
        return sb.ToString().Trim();
    }

    public byte[] Get(uint va, int len) { byte[] r = new byte[len]; Array.Copy(B, Off(va), r, 0, len); return r; }

    // Toutes les positions (adresses) ou se trouve le motif ; null dans le motif = octet quelconque.
    public List<uint> Find(int?[] pat)
    {
        List<uint> r = new List<uint>();
        for (int i = 0; i + pat.Length <= B.Length; i++)
        {
            bool ok = true;
            for (int k = 0; k < pat.Length && ok; k++) if (pat[k].HasValue && B[i + k] != pat[k].Value) ok = false;
            if (ok) r.Add(Va(i));
        }
        return r;
    }

    public List<uint> Find(byte[] pat) { int?[] p = new int?[pat.Length]; for (int i = 0; i < pat.Length; i++) p[i] = pat[i]; return Find(p); }
}
