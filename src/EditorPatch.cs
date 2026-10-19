// Patch de l'editeur de mappemonde pour des mappemondes plus grandes que l'originale (838 x 738).
// Correction validee en jeu sur l'exe francais (fonction de placement 0x408E50-0x409028), retrouvee dans la
// compilation anglaise 2.1.4.0 (0x406CE0-0x406F00) :
//   1. le controle « souris dans un rectangle 555 x 556 » (taille de la mappemonde d'origine) est saute ;
//   2. le controle de la position de l'icone s'applique avec tous les outils (sinon cites possibles hors carte) ;
//   3. les limites de ce controle, autour du centre de l'image (299 / 299 / 285 / 285 a l'origine), suivent la
//      mappemonde installee : la carte peinte (838 x 738 agrandie) peut etre calee a gauche (comme l'originale),
//      au centre ou a droite de l'image.
using System;
using System.Collections.Generic;

public enum MapLayout { Left, Center, Right }

public static class EditorPatch
{
    // Une compilation : adresses du controle de la souris, du test du mode, du saut a neutraliser et des 4 limites.
    class Build_
    {
        public string Name;
        public uint MouseCheck, ModeTest, ModeJne;
        public byte[] JneOrig;
        public uint[] Lim;            // gauche, droite, haut, bas (valeurs 32 bits)
        public bool NegLeftTop;       // anglais : lea reg,[eax-L] (valeur negative) ; francais : sub eax,L
        public byte[][] LimCtx;       // octets juste avant chaque limite (reconnaissance de la fonction)
    }

    static readonly Build_[] Builds = {
        new Build_ { Name = "anglais 2.1.4.0", MouseCheck = 0x00406D0B, ModeTest = 0x00406DD0, ModeJne = 0x00406DD8,
            JneOrig = new byte[] { 0x0F, 0x85, 0x1A, 0x01, 0x00, 0x00 },
            Lim = new uint[] { 0x00406EB1, 0x00406EC0, 0x00406ED8, 0x00406EE7 }, NegLeftTop = true,
            LimCtx = new[] { new byte[] { 0x8D, 0x90 }, new byte[] { 0x05 }, new byte[] { 0x8D, 0x88 }, new byte[] { 0x05 } } },
        new Build_ { Name = "français", MouseCheck = 0x00408E50, ModeTest = 0x00408F34, ModeJne = 0x00408F3B,
            JneOrig = new byte[] { 0x0F, 0x85, 0xED, 0x00, 0x00, 0x00 },
            Lim = new uint[] { 0x00408F88, 0x00408F9A, 0x00408FAC, 0x00408FBE }, NegLeftTop = false,
            LimCtx = new[] { new byte[] { 0x2D }, new byte[] { 0x05 }, new byte[] { 0x2D }, new byte[] { 0x05 } } },
    };

    // Limites (gauche, droite, haut, bas) autour du centre d'une image w x h. La carte peinte est agrandie au plus
    // grand possible dans l'image (hauteur, ou largeur si l'image est etroite), calee en haut, et placee selon layout.
    public static int[] Limits(int w, int h, MapLayout layout)
    {
        int[] r = MapRect(w, h, layout);
        return new[] { w / 2 - r[0], r[0] + r[2] - w / 2, h / 2 - r[1], r[1] + r[3] - h / 2 };
    }

    // Rectangle de la carte peinte dans une image w x h : x, y, largeur, hauteur (meme regle pour le generateur
    // de mappemondes et le convertisseur).
    public static int[] MapRect(int w, int h, MapLayout layout)
    {
        double s = Math.Min(h / 738.0, w / 838.0);
        int mw = Math.Min(w, (int)Math.Round(838 * s)), mh = Math.Min(h, (int)Math.Round(738 * s));
        int dx = layout == MapLayout.Left ? 0 : layout == MapLayout.Right ? w - mw : (w - mw) / 2;
        return new[] { dx, 0, mw, mh };
    }

    static Build_ Find(byte[] exe)
    {
        Pe pe;
        try { pe = new Pe(exe); } catch { return null; }
        foreach (Build_ b in Builds)
        {
            bool ok = Has(exe, pe, b.ModeTest, b == Builds[0] ? new byte[] { 0xA1 } : new byte[] { 0x83, 0x3D });
            for (int i = 0; i < 4 && ok; i++) ok = Has(exe, pe, (uint)(b.Lim[i] - b.LimCtx[i].Length), b.LimCtx[i]);
            if (ok) return b;
        }
        return null;
    }

    // Nom de la compilation reconnue (fonction de placement retrouvee), ou null.
    public static string Recognised(byte[] exe) { Build_ b = Find(exe); return b == null ? null : b.Name; }

    // Endroits du patch de l'editeur. lim : limites voulues (Limits), ou les valeurs d'origine si null.
    public static List<Site> Build(byte[] exe, int[] lim)
    {
        Build_ b = Find(exe);
        if (b == null) return null;
        Pe pe = new Pe(exe);
        List<Site> s = new List<Site>();
        byte[] jmp = new byte[5];
        jmp[0] = 0xE9; Array.Copy(BitConverter.GetBytes((int)(b.ModeTest - (b.MouseCheck + 5))), 0, jmp, 1, 4);
        s.Add(new Site("éditeur : contrôle de la souris", pe.Off(b.MouseCheck), new byte[] { 0x6A, 0x0C, 0x6A, 0x03, 0xE8 }, jmp));
        s.Add(new Site("éditeur : contrôle de l'icône", pe.Off(b.ModeJne), b.JneOrig, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 }));
        int[] v = lim ?? new[] { 299, 299, 285, 285 };
        int[] o = { 299, 299, 285, 285 };
        string[] names = { "gauche", "droite", "haute", "basse" };
        for (int i = 0; i < 4; i++)
        {
            bool neg = b.NegLeftTop && (i == 0 || i == 2);
            Site x = new Site("éditeur : limite " + names[i], pe.Off(b.Lim[i]), BitConverter.GetBytes(neg ? -o[i] : o[i]), BitConverter.GetBytes(neg ? -v[i] : v[i]));
            x.Value = true;
            s.Add(x);
        }
        return s;
    }

    public static bool FunctionLooksRight(byte[] exe) { return Find(exe) != null; }

    static bool Has(byte[] exe, Pe pe, uint va, params byte[] b)
    {
        int o = pe.Off(va);
        if (o < 0 || o + b.Length > exe.Length) return false;
        for (int i = 0; i < b.Length; i++) if (exe[o + i] != b[i]) return false;
        return true;
    }
}
