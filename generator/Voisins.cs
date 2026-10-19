// Mappemondes de la Grece (Zeus_MapOfGreece01-10) : la carte au centre, entouree des terres voisines.
// Une reference « 3440 x 1440 » (vue de 3238 x 1410) est fabriquee a partir de VOTRE jeu :
//   - a gauche, l'Italie (talon, golfe de Tarente, Calabre, Sicile) ; a droite, l'Anatolie, la mer Noire, Chypre, le Levant ;
//   - leur contour vient de Poseidon_map04 (recalee sur la carte de la Grece), leur relief de morceaux de terre de la
//     carte de la Grece, leurs cotes du liseré mesure sur les vraies cotes de la carte ;
// puis chaque resolution plus petite en est un recadrage centre (memes reliefs partout). Au-dela de 1410 px de haut,
// la reference est refaite a la hauteur voulue (memes reglages, proportionnels).
// Les dessins du coin bas-gauche (Greece01-07) sont deplaces dans le coin bas-gauche de la mappemonde agrandie ;
// a leur ancienne place, la mer de la carte sans dessin (Greece08).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class Voisins
{
    public const int RefW = 3238, RefH = 1410;            // vue d'un exe 3440 x 1440

    // Recalage de Poseidon_map04 sur la carte de la Grece (pixels d'origine 838 x 738) : grece = s * poseidon + t
    const double RegS = 3.32, RegTx = -1052, RegTy = -672;

    internal static byte[] Bytes(Bitmap b, out int st)
    {
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        st = d.Stride; byte[] r = new byte[st * b.Height]; Marshal.Copy(d.Scan0, r, 0, r.Length); b.UnlockBits(d); return r;
    }
    internal static void Put(Bitmap b, byte[] r)
    {
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        Marshal.Copy(r, 0, d.Scan0, r.Length); b.UnlockBits(d);
    }
    internal static byte C(double v) { return (byte)Math.Max(0, Math.Min(255, v)); }
    internal static bool Sea(int B, int G, int R) { return B > R + 30 && B > G - 5 && R < 110; }

    // Distance a la cote : > 0 dans la terre, < 0 en mer (pixels, chanfrein)
    internal static float[] Signed(bool[] land, int w, int h)
    {
        float[] din = Dist(land, w, h, true), dout = Dist(land, w, h, false), r = new float[w * h];
        for (int i = 0; i < w * h; i++) r[i] = land[i] ? din[i] - 0.5f : -(dout[i] - 0.5f);
        return r;
    }
    // Distance au plus proche pixel dont land != inside (0 sur ces pixels)
    internal static float[] Dist(bool[] land, int w, int h, bool inside)
    {
        float[] d = new float[w * h]; const float a = 1f, b = 1.4142f;
        for (int i = 0; i < w * h; i++) d[i] = land[i] == inside ? 1e6f : 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x; if (d[i] == 0) continue; float v = d[i];
                if (x > 0) v = Math.Min(v, d[i - 1] + a);
                if (y > 0) { v = Math.Min(v, d[i - w] + a); if (x > 0) v = Math.Min(v, d[i - w - 1] + b); if (x < w - 1) v = Math.Min(v, d[i - w + 1] + b); }
                d[i] = v;
            }
        for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x; if (d[i] == 0) continue; float v = d[i];
                if (x < w - 1) v = Math.Min(v, d[i + 1] + a);
                if (y < h - 1) { v = Math.Min(v, d[i + w] + a); if (x < w - 1) v = Math.Min(v, d[i + w + 1] + b); if (x > 0) v = Math.Min(v, d[i + w - 1] + b); }
                d[i] = v;
            }
        return d;
    }

    // Bruit de valeur lisse (grille n x n, periodique)
    internal static double[,] Grid(Random r, int n) { double[,] g = new double[n, n]; for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) g[j, i] = r.NextDouble() * 2 - 1; return g; }
    internal static double Noise(double[,] g, double x, double y)
    {
        int n = g.GetLength(0) - 1;
        x = ((x % n) + n) % n; y = ((y % n) + n) % n;
        int x0 = (int)x, y0 = (int)y; double fx = x - x0, fy = y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        return (g[y0, x0] * (1 - fx) + g[y0, x0 + 1] * fx) * (1 - fy) + (g[y0 + 1, x0] * (1 - fx) + g[y0 + 1, x0 + 1] * fx) * fy;
    }

    // Masque adouci (3 x 3) d'une image : 1 terre, 0 mer ; keep filtre les pixels de terre gardes
    static double[] SoftMask(bool[] land, int w, int h, Func<int, bool> keep)
    {
        double[] r = new double[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double s = 0, c = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        int j = yy * w + xx; s += land[j] && keep(j) ? 1 : 0; c++;
                    }
                r[y * w + x] = s / c;
            }
        return r;
    }
    static double Sample(double[] m, int w, int h, double x, double y)
    {
        if (x < 0 || y < 0 || x >= w - 1 || y >= h - 1) return 0;
        int x0 = (int)x, y0 = (int)y; double fx = x - x0, fy = y - y0;
        return (m[y0 * w + x0] * (1 - fx) + m[y0 * w + x0 + 1] * fx) * (1 - fy) + (m[(y0 + 1) * w + x0] * (1 - fx) + m[(y0 + 1) * w + x0 + 1] * fx) * fy;
    }

    // Composantes connexes (4-voisinage) d'un masque
    internal static int[] Label(bool[] land, int w, int h, Action<List<int>> each)
    {
        int[] lab = new int[w * h]; int n = 0; Queue<int> q = new Queue<int>();
        for (int i0 = 0; i0 < w * h; i0++)
        {
            if (!land[i0] || lab[i0] != 0) continue;
            n++; lab[i0] = n; q.Enqueue(i0); List<int> mem = new List<int>();
            while (q.Count > 0)
            {
                int i = q.Dequeue(); mem.Add(i); int x = i % w, y = i / w;
                for (int k = 0; k < 4; k++)
                {
                    int xx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), yy = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                    int j = yy * w + xx; if (!land[j] || lab[j] != 0) continue;
                    lab[j] = n; q.Enqueue(j);
                }
            }
            if (each != null) each(mem);
        }
        return lab;
    }

    // ------------------------------------------------------------ reference
    // greece : carte de la Grece sans dessin (838 x 738) ; poseidon : Poseidon_map04 (838 x 738). Vue w x h, carte au centre.
    public static Bitmap Reference(string greece, string poseidon, int w, int h)
    {
        Mappe.Setup(w, h, 1);
        int W = Mappe.W, H = Mappe.H, MW = Mappe.MW, DX = Mappe.DX, E = DX + MW; double S = Mappe.S, f = H / 1080.0;
        using (Bitmap m = Mappe.Scaled(greece))
        using (Bitmap P = new Bitmap(poseidon))
        {
            int sm; byte[] mp = Bytes(m, out sm); int mw = m.Width, mh = m.Height;
            bool[] ml = new bool[mw * mh];
            for (int y = 0; y < mh; y++) for (int x = 0; x < mw; x++) { int i = y * sm + x * 3; ml[y * mw + x] = !Sea(mp[i], mp[i + 1], mp[i + 2]); }

            // Poseidon_map04 : terre / mer, la Sicile a part (on garde un detroit avec la botte)
            int pst; byte[] pp; using (Bitmap p24 = P.Clone(new Rectangle(0, 0, P.Width, P.Height), PixelFormat.Format24bppRgb)) pp = Bytes(p24, out pst);
            int pw = P.Width, ph = P.Height; bool[] pl = new bool[pw * ph];
            for (int y = 0; y < ph; y++) for (int x = 0; x < pw; x++) { int i = y * pst + x * 3; pl[y * pw + x] = !Sea(pp[i], pp[i + 1], pp[i + 2]); }
            int[] comp = Label(pl, pw, ph, null);
            int sic = comp[287 * pw + 232];
            double[] pAll = SoftMask(pl, pw, ph, j => true), pIt = SoftMask(pl, pw, ph, j => comp[j] != sic), pSic = SoftMask(pl, pw, ph, j => comp[j] == sic);

            Random rnd = new Random(77); double[,] n1 = Grid(rnd, 64), n2 = Grid(rnd, 64), n3 = Grid(rnd, 64);
            Func<int, int, double[]> Warp = (x, y) =>
            {
                double wx = x, wy = y, amp = 26 * f, per = 150 * f;
                for (int o = 0; o < 5; o++) { wx += amp * Noise(n1, x / per, y / per); wy += amp * Noise(n2, x / per + 17, y / per + 5); amp *= 0.5; per *= 0.5; }
                return new[] { wx, wy };
            };
            Func<double, double, double[]> ToPos = (gx, gy) => new[] { (gx - RegTx) / RegS, (gy - RegTy) / RegS };   // Grece d'origine -> Poseidon_map04

            bool[] land = new bool[W * H];
            // --- a gauche : l'Italie, decalee de 120 px (1080p) pour tenir dans la bande, la Sicile separee par un detroit
            int gapW = DX + (int)(60 * f), shiftL = (int)Math.Round(120 * f);
            bool[] lI = new bool[W * H], lS = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < gapW && x < DX - 25 * f; x++)
                {
                    double[] wv = Warp(x, y);
                    double[] p = ToPos((wv[0] + shiftL - DX) / S, wv[1] / S);
                    double nz = 0.10 * Noise(n3, x / (9.0 * f), y / (9.0 * f));
                    lI[y * W + x] = Sample(pIt, pw, ph, p[0], p[1]) + nz > 0.5;
                    lS[y * W + x] = Sample(pSic, pw, ph, p[0], p[1]) + nz > 0.5;
                }
            float gap = (float)(9 * f); float[] dI = Dist(lI, W, H, false), dS = Dist(lS, W, H, false);
            for (int i = 0; i < W * H; i++) if ((lI[i] && dS[i] > gap) || (lS[i] && dI[i] > gap)) land[i] = true;
            // talon de la botte epaissi (Poseidon_map04 le dessine tres fin)
            {
                float[] sd = Signed(land, W, H);
                double hx = DX - 107 * f, hy = 150 * f, hr = 130 * f, hmax = 20 * f;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < DX - 25 * f; x++)
                    {
                        double r = Math.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy)) / hr; if (r >= 1) continue;
                        double b = 1 - r * r; b *= b; if (sd[y * W + x] > -hmax * b) land[y * W + x] = true;
                    }
            }

            // --- a droite : l'Anatolie ; on part des cotes de la carte au bord (deformees) et on rejoint Poseidon_map04
            int x0r = E - (int)(130 * f); double T = 150 * f;
            Func<int, int, double> GS = (x, y) =>
            {
                double s = 0; int c = 0;
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) { int xx = Math.Max(0, Math.Min(mw - 1, x + dx)), yy = Math.Max(0, Math.Min(mh - 1, y + dy)); s += ml[yy * mw + xx] ? 1 : 0; c++; }
                return s / c;
            };
            for (int y = 0; y < H; y++)
                for (int x = x0r; x < W; x++)
                {
                    double[] wv = Warp(x, y);
                    double t = Math.Max(0, Math.Min(1, (x - E) / T));
                    int gyw = (int)Math.Round(y + (wv[1] - y) * t);
                    double vG = x < E ? GS(x - DX, y) : GS(MW - 1, Math.Max(0, Math.Min(H - 1, gyw)));
                    double[] p = ToPos((wv[0] - DX) / S, wv[1] / S);
                    double v04 = Sample(pAll, pw, ph, p[0], p[1]) + 0.10 * Noise(n3, x / (9.0 * f), y / (9.0 * f));
                    double ts = t * t * (3 - 2 * t);
                    land[y * W + x] = (1 - ts) * vG + ts * v04 > 0.5;
                }

            // ilots minuscules, et terres qui n'apparaissent qu'au ras du bas a gauche (cote africaine) : retires
            Label(land, W, H, mem =>
            {
                int top = H, maxx = 0; foreach (int i in mem) { top = Math.Min(top, i / W); maxx = Math.Max(maxx, i % W); }
                if (mem.Count < 150 * f * f || (maxx < DX && top > H - 200 * f)) foreach (int i in mem) land[i] = false;
            });

            // --- liseré de cote mesure sur la vraie carte : couleur moyenne selon la distance a la cote
            float[] md = Signed(ml, mw, mh); int R = (int)Math.Round(48 * f);
            double[,] prof = new double[2 * R + 1, 3]; double[] pc = new double[2 * R + 1];
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    int d = (int)Math.Round(md[y * mw + x]); if (d < -R || d > R) continue;
                    int i = y * sm + x * 3; prof[d + R, 0] += mp[i]; prof[d + R, 1] += mp[i + 1]; prof[d + R, 2] += mp[i + 2]; pc[d + R]++;
                }
            for (int k = 0; k <= 2 * R; k++) for (int c = 0; c < 3; c++) prof[k, c] /= Math.Max(1, pc[k]);

            // --- reliefs : morceaux de terre de la carte (sans les retourner : l'eclairage des montagnes doit rester le meme)
            int ps = (int)Math.Round(160 * f), win = (int)Math.Round(ps * 1.5);
            List<Rectangle> wins = LandWindows(md, mw, mh, win, 14 * f);
            if (wins.Count < 4) wins = LandWindows(md, mw, mh, ps, 14 * f);
            List<Rectangle> left = new List<Rectangle>(wins), right = new List<Rectangle>(wins);
            left.Sort((a, b) => Yellow(mp, sm, a).CompareTo(Yellow(mp, sm, b)));          // a gauche : terres vertes et brunes
            right.Sort((a, b) => b.Right.CompareTo(a.Right));                              // a droite : terres du bord droit (Anatolie)
            if (left.Count > 8) left = left.GetRange(0, 8);
            if (right.Count > 6) right = right.GetRange(0, 6);
            double[] acc = new double[W * H * 3], wt = new double[W * H], wt2 = new double[W * H];
            double[] muL = Tex(acc, wt, wt2, W, H, 0, gapW, left, ps, mp, sm, 99), muR = Tex(acc, wt, wt2, W, H, x0r, W, right, ps, mp, sm, 199);

            // --- composition : terre (relief) ou mer (eau texturee), plus le liseré selon la distance a la cote
            Bitmap sea = Mappe.SeaTexture(m, W, H); int ss; byte[] sp = Bytes(sea, out ss); sea.Dispose();
            float[] cd = Signed(land, W, H);
            Bitmap canvas = new Bitmap(W, H, PixelFormat.Format24bppRgb); int cs; byte[] cp = Bytes(canvas, out cs);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int ci = y * W + x, o = y * cs + x * 3, si = y * ss + x * 3;
                    if (x >= gapW && x < x0r) { cp[o] = sp[si]; cp[o + 1] = sp[si + 1]; cp[o + 2] = sp[si + 2]; continue; }
                    double[] mu = x < gapW ? muL : muR; double d = cd[ci];
                    for (int c = 0; c < 3; c++)
                    {
                        double farL = prof[2 * R, c], farS = prof[0, c], v;
                        // melange qui garde le contraste des morceaux (sinon le fondu delave le relief)
                        double baseL = wt[ci] > 0 ? mu[c] + (acc[ci * 3 + c] / wt[ci] - mu[c]) * Math.Min(1.8, wt[ci] / Math.Sqrt(wt2[ci])) : farL;
                        if (d >= 0) v = baseL + prof[(int)Math.Min(R, Math.Round(d)) + R, c] - farL;
                        else v = sp[si + c] + prof[(int)Math.Max(-R, Math.Round(d)) + R, c] - farS;
                        cp[o + c] = C(v);
                    }
                }
            Put(canvas, cp);
            int F = (int)Math.Round(110 * Mappe.K);
            Mappe.Blend(canvas, m, DX, F, F);
            return canvas;
        }
    }

    // Fenetres de terre (sans cote a moins de 'margin' px) de la carte, sans recouvrement
    internal static List<Rectangle> LandWindows(float[] md, int mw, int mh, int size, double margin)
    {
        int[,] ii = new int[mh + 1, mw + 1];
        for (int y = 0; y < mh; y++) for (int x = 0; x < mw; x++) ii[y + 1, x + 1] = (md[y * mw + x] < margin ? 1 : 0) + ii[y, x + 1] + ii[y + 1, x] - ii[y, x];
        List<Rectangle> r = new List<Rectangle>();
        for (int y = 0; y + size <= mh; y += 8)
            for (int x = mw - size; x >= 0; x -= 8)
            {
                if (ii[y + size, x + size] - ii[y, x + size] - ii[y + size, x] + ii[y, x] != 0) continue;
                Rectangle w = new Rectangle(x, y, size, size); bool free = true;
                foreach (Rectangle o in r) if (o.IntersectsWith(w)) { free = false; break; }
                if (free) r.Add(w);
            }
        return r;
    }

    static double Yellow(byte[] mp, int sm, Rectangle r)
    {
        double s = 0; int n = 0;
        for (int y = r.Y; y < r.Bottom; y += 8) for (int x = r.X; x < r.Right; x += 8) { int i = y * sm + x * 3; s += mp[i + 2] + mp[i + 1] - 2 * mp[i]; n++; }
        return s / n;
    }

    // Relief de la bande [x0, x1) : morceaux ps x ps pris au hasard dans les fenetres, fondus en cloche ; renvoie leur couleur moyenne
    internal static double[] Tex(double[] acc, double[] wt, double[] wt2, int W, int H, int x0, int x1, List<Rectangle> wins, int ps, byte[] mp, int sm, int seed)
    {
        double[] mu = new double[3]; long n = 0;
        foreach (Rectangle r in wins) for (int y = r.Y; y < r.Bottom; y += 4) for (int x = r.X; x < r.Right; x += 4) { int i = y * sm + x * 3; mu[0] += mp[i]; mu[1] += mp[i + 1]; mu[2] += mp[i + 2]; n++; }
        for (int c = 0; c < 3; c++) mu[c] /= Math.Max(1, n);
        double[] bell = new double[ps]; for (int i = 0; i < ps; i++) bell[i] = Math.Sin(Math.PI * (i + 0.5) / ps) + 1e-3;
        int step = ps * 2 / 3; Random rr = new Random(seed);
        for (int gy = -step; gy < H; gy += step)
            for (int gx = x0 - step; gx < x1; gx += step)
            {
                Rectangle r = wins[rr.Next(wins.Count)];
                int ox = r.X + rr.Next(r.Width - ps + 1), oy = r.Y + rr.Next(r.Height - ps + 1);
                for (int y = 0; y < ps; y++)
                {
                    int cy = gy + y; if (cy < 0 || cy >= H) continue;
                    for (int x = 0; x < ps; x++)
                    {
                        int cx = gx + x; if (cx < x0 || cx >= x1) continue;
                        double k = bell[x] * bell[y]; int mi = (oy + y) * sm + (ox + x) * 3, ci = cy * W + cx;
                        acc[ci * 3] += mp[mi] * k; acc[ci * 3 + 1] += mp[mi + 1] * k; acc[ci * 3 + 2] += mp[mi + 2] * k; wt[ci] += k; wt2[ci] += k * k;
                    }
                }
            }
        return mu;
    }

    // ------------------------------------------------------------ mappemondes de la Grece
    // Recadrage centre de la reference pour une vue w x h (la carte de la Grece a la meme taille que Mappe.Setup(w, h, 1)).
    public static Bitmap Crop(Bitmap reference, int w, int h)
    {
        double sRef = reference.Height / 738.0, sT = Math.Min(h / 738.0, w / 838.0), k = sT / sRef;
        double cx = reference.Width / 2.0;
        double srcW = w / k, srcH = h / k, srcX = cx - srcW / 2;
        Bitmap b = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(b))
        using (ImageAttributes a = new ImageAttributes())
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            a.SetWrapMode(WrapMode.TileFlipXY);   // au-dela de la reference (vue tres large ou tres haute) : prolongee en miroir
            g.DrawImage(reference, new Rectangle(0, 0, w, h), (float)srcX, 0f, (float)srcW, (float)srcH, GraphicsUnit.Pixel, a);
        }
        return b;
    }

    // Dessin du coin bas-gauche de 'withArt' (difference avec 'clean', la meme carte sans dessin), deplace dans le coin
    // bas-gauche de la mappemonde 'canvas' (vue w x h), a l'echelle de la carte.
    public static void MoveArt(Bitmap canvas, string withArt, string clean)
    {
        using (Bitmap a0 = new Bitmap(withArt)) using (Bitmap c0 = new Bitmap(clean))
        using (Bitmap a = a0.Clone(new Rectangle(0, 0, a0.Width, a0.Height), PixelFormat.Format24bppRgb))
        using (Bitmap c = c0.Clone(new Rectangle(0, 0, c0.Width, c0.Height), PixelFormat.Format24bppRgb))
        {
            int sa, sc; byte[] pa = Bytes(a, out sa), pcl = Bytes(c, out sc);
            int w0 = a.Width, h0 = a.Height;
            // alpha du dessin : ecart a la carte sans dessin (adouci), seulement dans le quart bas-gauche
            double[] al = new double[w0 * h0];
            for (int y = h0 / 2; y < h0; y++)
                for (int x = 0; x < w0 / 2; x++)
                {
                    int i = y * sa + x * 3, j = y * sc + x * 3;
                    double d = Math.Abs(pa[i] - pcl[j]) + Math.Abs(pa[i + 1] - pcl[j + 1]) + Math.Abs(pa[i + 2] - pcl[j + 2]);
                    al[y * w0 + x] = Math.Max(0, Math.Min(1, (d - 18) / 60.0));
                }
            double[] alb = new double[w0 * h0];
            int bx = w0, by = h0;
            for (int y = h0 / 2; y < h0; y++)
                for (int x = 0; x < w0 / 2; x++)
                {
                    double s = 0; int n = 0;
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) { int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= w0 || yy >= h0) continue; s += al[yy * w0 + xx]; n++; }
                    double v = s / n; v = v * v * (3 - 2 * v); alb[y * w0 + x] = v;
                    if (v > 0.02) { bx = Math.Min(bx, x); by = Math.Min(by, y); }
                }
            if (bx >= w0) return;
            int aw = w0 / 2 - bx, ah = h0 - by;
            // le dessin (couleurs + alpha) a l'echelle de la carte, colle dans le coin bas-gauche de la mappemonde
            double S = Math.Min(canvas.Height / 738.0, canvas.Width / 838.0);
            int tw = (int)Math.Round((bx + aw) * S), th = (int)Math.Round(ah * S);
            int cs; byte[] cp = Bytes(canvas, out cs);
            int W = canvas.Width, H = canvas.Height;
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    int X = x, Y = H - th + y; if (X >= W || Y < 0) continue;
                    double sx = x / S, sy = by + y / S;
                    int ix = Math.Min(w0 - 2, (int)sx), iy = Math.Min(h0 - 2, (int)sy); double fx = sx - ix, fy = sy - iy;
                    double A = Lerp(alb, w0, ix, iy, fx, fy); if (A <= 0.001) continue;
                    int o = Y * cs + X * 3;
                    for (int k = 0; k < 3; k++)
                    {
                        double v = (pa[iy * sa + ix * 3 + k] * (1 - fx) + pa[iy * sa + (ix + 1) * 3 + k] * fx) * (1 - fy)
                                 + (pa[(iy + 1) * sa + ix * 3 + k] * (1 - fx) + pa[(iy + 1) * sa + (ix + 1) * 3 + k] * fx) * fy;
                        cp[o + k] = C(v * A + cp[o + k] * (1 - A));
                    }
                }
            Put(canvas, cp);
        }
    }

    static double Lerp(double[] m, int w, int x, int y, double fx, double fy)
    {
        return (m[y * w + x] * (1 - fx) + m[y * w + x + 1] * fx) * (1 - fy) + (m[(y + 1) * w + x] * (1 - fx) + m[(y + 1) * w + x + 1] * fx) * fy;
    }
}
