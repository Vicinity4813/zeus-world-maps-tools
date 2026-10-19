// Mappemondes de Poseidon (Poseidon_map01-04) : la carte au centre d'une reference « 1440p » (vue de 2338 x 1410),
// fabriquee a partir de VOTRE jeu :
//   - map01, map02, map03 sont la meme peinture du monde (recalees entre elles) : leurs vrais pixels se prolongent
//     les uns les autres (les petites bandes non couvertes en haut et en bas : cotes reelles du recalage global) ;
//   - la ou aucune carte ne couvre (ouest de map03, est de map02, les deux cotes de map04, dessin different) : les vraies
//     cotes (Natural Earth, domaine public, Cotes.cs) recalees sur chaque bord, avec le relief, l'eau et le liseré de
//     cote de la carte elle-meme ; la cote de la carte se transforme progressivement en cote reelle ; detroits ouverts ;
//     pas de lacs (les peintures n'en montrent aucun).
// Les resolutions plus petites sont des recadrages de la reference ; plus haute, elle est refaite a la hauteur voulue ;
// plus large (ultrawide), la reference reste au centre et le choix « a cote de la carte » remplit le reste.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class Poseidon
{
    public const int RefW = 2338, RefH = 1410;   // vue d'un exe 2560 x 1440

    // carte -> map01 (pixels d'origine) : u = s * b + t
    static readonly Dictionary<string, double[]> Tf = new Dictionary<string, double[]> {
        { "Poseidon_map01", new[] { 1.0, 0, 0 } },
        { "Poseidon_map03", new[] { 1.0, -222, 60 } },
        { "Poseidon_map02", new[] { 1.157, 218, -63.5 } },
        { "Poseidon_map04", new[] { 0.488 * 1.157, 1.157 * 428 + 218, 1.157 * 140 - 63.5 } } };
    // voisines peintes de la meme main (map04 est un autre dessin)
    static readonly Dictionary<string, string[]> Nb = new Dictionary<string, string[]> {
        { "Poseidon_map01", new[] { "Poseidon_map03", "Poseidon_map02" } },
        { "Poseidon_map03", new[] { "Poseidon_map01", "Poseidon_map02" } },
        { "Poseidon_map02", new[] { "Poseidon_map01", "Poseidon_map03" } },
        { "Poseidon_map04", new string[0] } };
    // cotes reelles -> carte, Mercator : x = sx * lon + tx ; y = ty - sy * G(lat) ; null : le bord n'en a pas besoin
    static readonly Dictionary<string, double[][]> Side = new Dictionary<string, double[][]> {
        { "Poseidon_map01", new double[][] { null, null } },
        { "Poseidon_map03", new[] { new[] { 8.943, 11.835525, 954.7, 702.7 }, null } },
        { "Poseidon_map02", new[] { null, new[] { 7.9806806395851346, 10.319792566983578, 480.14315038893693, 685.70441568430215 } } },
        { "Poseidon_map04", new[] { new[] { 16.829026276265637, 22.838885189225948, 2.7216196494609051, 1082.1321797963142 },
                                    new[] { 17.027014820692294, 20.354099069101828, 33.971619649460905, 1155.0545857715688 } } } };
    // cotes reelles -> map01 (recalage global du monde de map01-03)
    static readonly double[] P01 = { 8.943, 11.94, 735.2, 757.7 };
    // detroits toujours ouverts (lon, lat de part et d'autre) : Gibraltar, Bab-el-Mandeb, Ormuz
    static readonly double[][] Straits = { new[] { -6.3, 35.9, -5.0, 36.05 }, new[] { 43.0, 12.75, 43.7, 12.45 }, new[] { 55.9, 26.7, 56.7, 26.2 } };

    static double G(double lat) { return Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360)) * 180 / Math.PI; }
    static double Smooth(double t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }

    // Terre (true) / mer des cotes reelles dans une grille w x h : pixel (x, y) = point (x0 + x * step, y * step) de la carte
    static bool[] Raster(int w, int h, double x0, double step, double[] p, double straitPx)
    {
        using (Bitmap b = new Bitmap(w, h, PixelFormat.Format24bppRgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Black);
                using (GraphicsPath gp = new GraphicsPath(FillMode.Winding))
                {
                    foreach (double[][] r in Cotes.Rings())
                    {
                        PointF[] pts = new PointF[r.Length];
                        for (int i = 0; i < r.Length; i++) pts[i] = new PointF((float)((p[0] * r[i][0] + p[2] - x0) / step), (float)((p[3] - p[1] * G(r[i][1])) / step));
                        gp.AddPolygon(pts);
                    }
                    g.FillPath(Brushes.White, gp);
                }
                if (straitPx > 0)
                    using (Pen pen = new Pen(Color.Black, (float)straitPx))
                        foreach (double[] s in Straits)
                            g.DrawLine(pen, (float)((p[0] * s[0] + p[2] - x0) / step), (float)((p[3] - p[1] * G(s[1])) / step), (float)((p[0] * s[2] + p[2] - x0) / step), (float)((p[3] - p[1] * G(s[3])) / step));
            }
            int st; byte[] px = Voisins.Bytes(b, out st); bool[] m = new bool[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) m[y * w + x] = px[y * st + x * 3] > 127;
            return m;
        }
    }

    static bool[] MaskOf(Bitmap b)
    {
        int st; byte[] p = Voisins.Bytes(b, out st); bool[] m = new bool[b.Width * b.Height];
        for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) { int i = y * st + x * 3; m[y * b.Width + x] = !Voisins.Sea(p[i], p[i + 1], p[i + 2]); }
        return m;
    }

    static Bitmap Load(string path) { using (Bitmap b = new Bitmap(path)) return b.Clone(new Rectangle(0, 0, b.Width, b.Height), PixelFormat.Format24bppRgb); }

    static Bitmap Scale(Bitmap o, double f)
    {
        Bitmap b = new Bitmap(Math.Max(1, (int)Math.Round(o.Width * f)), Math.Max(1, (int)Math.Round(o.Height * f)), PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(b)) using (ImageAttributes a = new ImageAttributes())
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; a.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(o, new Rectangle(0, 0, b.Width, b.Height), 0, 0, o.Width, o.Height, GraphicsUnit.Pixel, a);
        }
        return b;
    }

    // Les cotes reelles collent-elles a ce bord de la carte ? (sinon : pas les memes cartes d'origine). Seulement les 36 px du bord :
    // les zones gardees dans Cotes.cs ne debordent que de 40 px dans la carte.
    static bool Agrees(bool[] k, double[] p, bool left)
    {
        int w = 838 / 3, h = 738 / 3; bool[] m = Raster(w, h, 0, 3, p, 0); int ok = 0, n = 0;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { if (left ? x * 3 >= 36 : x * 3 < 802) continue; n++; if (m[y * w + x] == k[(y * 3) * 838 + x * 3]) ok++; }
        return n > 0 && ok >= 0.8 * n;
    }

    // Reference de la carte 'name' pour une vue w x h (carte au centre). orig(nom) : chemin de la carte d'origine, ou null.
    // Renvoie null si une carte necessaire manque ou ne correspond pas.
    public static Bitmap Reference(string name, Func<string, string> orig, int W, int H)
    {
        if (!Tf.ContainsKey(name) || orig(name) == null) return null;
        Mappe.Setup(W, H, 1);
        double So = Mappe.S, f = H / 1080.0; int DX = Mappe.DX, MW = Mappe.MW, E = DX + MW, F = (int)Math.Round(110 * Mappe.K);
        double[] T = Tf[name];
        using (Bitmap tOrig = Load(orig(name)))
        using (Bitmap own = Mappe.Scaled(orig(name)))
        {
            if (tOrig.Width != 838 || tOrig.Height != 738) return null;
            bool[] kT = MaskOf(tOrig);
            double[][] sd = (double[][])Side[name].Clone();
            for (int s = 0; s < 2; s++) if (sd[s] != null && !Agrees(kT, sd[s], s == 0)) return null;
            // bords couverts par les voisines : cotes reelles du recalage global, pour les petites bandes qu'elles ne couvrent pas
            if (name != "Poseidon_map04") for (int s = 0; s < 2; s++) if (sd[s] == null) sd[s] = new[] { P01[0] / T[0], P01[1] / T[0], (P01[2] - T[1]) / T[0], (P01[3] - T[2]) / T[0] };
            int sm; byte[] mp = Voisins.Bytes(own, out sm); int mw = own.Width, mh = own.Height;
            bool[] ml = new bool[mw * mh];
            for (int y = 0; y < mh; y++) for (int x = 0; x < mw; x++) { int i = y * sm + x * 3; ml[y * mw + x] = !Voisins.Sea(mp[i], mp[i + 1], mp[i + 2]); }

            // --- masque : la carte dans son cadre ; autour, sa cote prolongee en miroir qui devient la cote reelle
            bool[][] ne = new bool[2][], ne0 = new bool[2][];
            for (int s = 0; s < 2; s++)
                if (sd[s] != null) { ne0[s] = Raster(W, H, -DX / So, 1 / So, sd[s], 0); ne[s] = Raster(W, H, -DX / So, 1 / So, sd[s], 14 * f); }
            bool[] mir = new bool[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { int xx = x < DX ? 2 * DX - x : x >= E ? 2 * (E - 1) - x : x; xx = Math.Max(DX, Math.Min(E - 1, xx)); mir[y * W + x] = ml[Math.Min(mh - 1, y) * mw + (xx - DX)]; }
            bool[] neM = new bool[W * H];
            for (int i = 0; i < W * H; i++) { int s = i % W < DX + MW / 2 ? 0 : 1; neM[i] = ne[s] != null ? ne[s][i] : mir[i]; }
            float[] dG = Voisins.Signed(mir, W, H), dN = Voisins.Signed(neM, W, H);
            Random rnd = new Random(77); double[,] n1 = Voisins.Grid(rnd, 64), n2 = Voisins.Grid(rnd, 64);
            bool[] land = new bool[W * H]; double Tr = 170 * f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (x >= DX && x < E) { land[i] = ml[Math.Min(mh - 1, y) * mw + (x - DX)]; continue; }
                    double outd = x < DX ? DX - x : x - E + 1;
                    double wx = x, wy = y, amp = 7 * f, per = 60 * f;
                    for (int o = 0; o < 4; o++) { wx += amp * Voisins.Noise(n1, x / per, y / per); wy += amp * Voisins.Noise(n2, x / per + 17, y / per + 5); amp *= 0.5; per *= 0.5; }
                    int j = Math.Max(0, Math.Min(H - 1, (int)wy)) * W + Math.Max(0, Math.Min(W - 1, (int)wx));
                    double t = Smooth(outd / Tr), g = Math.Min(30 * f, dG[i]);   // terre prolongee : poids limite ; une mer qui touche le bord continue
                    land[i] = (1 - t) * g + t * dN[j] > 0;
                }
            for (int i = 0; i < W * H; i++)
            {
                int x = i % W; if (x >= DX && x < E) continue; int s = x < DX + MW / 2 ? 0 : 1;
                if (ne[s] != null && ne0[s][i] && !ne[s][i]) land[i] = false;   // detroits rouverts
            }
            Voisins.Label(land, W, H, mem => { if (mem.Count < 60 * f * f) foreach (int i in mem) land[i] = false; });

            // --- rendu : relief de la carte (morceaux non retournes, contraste garde), eau texturee, liseré de cote mesure
            float[] md = Voisins.Signed(ml, mw, mh); int R = (int)Math.Round(48 * f);
            double[,] prof = new double[2 * R + 1, 3]; double[] pc = new double[2 * R + 1];
            for (int y = 0; y < mh; y++) for (int x = 0; x < mw; x++) { int d = (int)Math.Round(md[y * mw + x]); if (d < -R || d > R) continue; int i = y * sm + x * 3; for (int c = 0; c < 3; c++) prof[d + R, c] += mp[i + c]; pc[d + R]++; }
            for (int k = 0; k <= 2 * R; k++) for (int c = 0; c < 3; c++) prof[k, c] /= Math.Max(1, pc[k]);
            int ps = (int)Math.Round(120 * f);
            List<Rectangle> wins = Voisins.LandWindows(md, mw, mh, (int)(ps * 1.4), 12 * f);
            if (wins.Count < 3) wins = Voisins.LandWindows(md, mw, mh, ps, 8 * f);
            if (wins.Count == 0) return null;
            List<Rectangle> wl = new List<Rectangle>(wins), wr = new List<Rectangle>(wins);
            wl.Sort((p, q) => p.X.CompareTo(q.X)); wr.Sort((p, q) => q.Right.CompareTo(p.Right));
            if (wl.Count > 6) wl = wl.GetRange(0, 6); if (wr.Count > 6) wr = wr.GetRange(0, 6);
            double[] acc = new double[W * H * 3], wt = new double[W * H], wt2 = new double[W * H];
            double[] muL = Voisins.Tex(acc, wt, wt2, W, H, 0, DX + MW / 2, wl, ps, mp, sm, 31), muR = Voisins.Tex(acc, wt, wt2, W, H, DX + MW / 2, W, wr, ps, mp, sm, 32);
            Bitmap cv = Mappe.SeaTexture(own, W, H); int cs; byte[] cp = Voisins.Bytes(cv, out cs);
            float[] cd = Voisins.Signed(land, W, H);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int ci = y * W + x, o = y * cs + x * 3; double[] mu = x < DX + MW / 2 ? muL : muR; double d = cd[ci];
                    for (int c = 0; c < 3; c++)
                    {
                        double farL = prof[2 * R, c], farS = prof[0, c];
                        double baseL = wt[ci] > 0 ? mu[c] + (acc[ci * 3 + c] / wt[ci] - mu[c]) * Math.Min(1.8, wt[ci] / Math.Sqrt(wt2[ci])) : farL;
                        cp[o + c] = Voisins.C(d >= 0 ? baseL + prof[(int)Math.Min(R, Math.Round(d)) + R, c] - farL : cp[o + c] + prof[(int)Math.Max(-R, Math.Round(d)) + R, c] - farS);
                    }
                }

            // --- vrais pixels des voisines (meme peinture) par-dessus, fondus a leurs bords
            foreach (string n in Nb[name])
            {
                string pth = orig(n); if (pth == null) continue;
                double[] S = Tf[n]; double fsc = So * S[0] / T[0];
                using (Bitmap o0 = Load(pth))
                {
                    if (o0.Width != 838 || o0.Height != 738) continue;
                    using (Bitmap sc = Scale(o0, fsc))
                    {
                        int st; byte[] p = Voisins.Bytes(sc, out st); int sw0 = sc.Width, sh0 = sc.Height;
                        for (int y = 0; y < H; y++)
                            for (int x = 0; x < W; x++)
                            {
                                double ux = T[0] * (x - DX) / So + T[1], uy = T[0] * y / So + T[2], ox = (ux - S[1]) / S[0], oy = (uy - S[2]) / S[0];
                                if (ox < 0 || ox > 837) continue;
                                if (oy < 3 || oy > 734) continue;   // les rangees du bord de l'image d'origine sont plus claires
                                double w = Smooth(Math.Min(ox, 837 - ox) / 25.0) * Smooth(Math.Min(oy - 3, 734 - oy) / 25.0); if (w <= 0) continue;
                                double sx = ox * fsc, sy = oy * fsc; int ix = Math.Min(sw0 - 2, (int)sx), iy = Math.Min(sh0 - 2, (int)sy); double fx = sx - ix, fy = sy - iy; int o = y * cs + x * 3;
                                for (int c = 0; c < 3; c++)
                                {
                                    double v = (p[iy * st + ix * 3 + c] * (1 - fx) + p[iy * st + (ix + 1) * 3 + c] * fx) * (1 - fy) + (p[(iy + 1) * st + ix * 3 + c] * (1 - fx) + p[(iy + 1) * st + (ix + 1) * 3 + c] * fx) * fy;
                                    cp[o + c] = Voisins.C(v * w + cp[o + c] * (1 - w));
                                }
                            }
                    }
                }
            }
            Voisins.Put(cv, cp);
            Mappe.Blend(cv, own, DX, F, F);
            return cv;
        }
    }

    // Taille de reference pour une vue w x h : 1410 px de haut (ou la hauteur de la vue si elle est plus haute)
    // Une vue a peine plus large (bandes trop etroites pour un panneau, 4K par exemple) : la reference est elargie a la vue.
    public static int[] RefSize(int w, int h)
    {
        int rh = Math.Max(RefH, h), rw = (int)Math.Round(RefW * rh / (double)RefH);
        double k = Math.Min(h / 738.0, w / 838.0) / (rh / 738.0);
        int need = (int)Math.Ceiling(w / k);
        if (need > rw && (need - rw) / 2.0 * k < 150 * Math.Min(h / 1080f, 2.5f)) rw = need;
        return new[] { rw, rh };
    }

    // Mappemonde w x h a partir de la reference : recadrage centre, ou reference au centre et le choix « a cote de la
    // carte » de chaque cote si la vue est plus large. Mappe.Fill / CustomPath / ShowFrieze deja regles.
    public static Bitmap Fit(Bitmap reference, int w, int h, string title, string sub, string godPath)
    {
        double sRef = reference.Height / 738.0, sT = Math.Min(h / 738.0, w / 838.0), k = sT / sRef;
        int rw = (int)Math.Round(reference.Width * k);
        if (rw >= w) return Voisins.Crop(reference, w, h);
        using (Bitmap scaled = Voisins.Crop(reference, rw, h))
        {
            Mappe.Setup(w, h, 1);
            return Mappe.Surround(scaled, title, sub, godPath);
        }
    }
}
