// Fabrication des mappemondes a partir des cartes d'origine 838x738, pour une zone de carte de n'importe quelle taille :
// carte agrandie au plus grand possible (hauteur, ou largeur si la zone est etroite), calee a gauche, au centre ou a droite ;
// le reste en mer prolongee, ou parchemin (portrait du dieu, cartouche) derriere une frise grecque.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class Mappe
{
    public static int W = 1738, H = 1080;
    public static double S = 1080.0 / 738.0;
    public static int MW = (int)Math.Round(838 * S), MH = 1080, DX = 0;   // carte peinte : largeur, hauteur, position
    public static float K = 1f;                         // echelle de la mise en page par rapport au 1080p
    public static int Layout;                           // 0 gauche (comme l'originale), 1 centre, 2 droite

    // Zone de mappemonde w x h, carte placee selon layout (meme regle que le patcheur et le convertisseur).
    public static void Setup(int w, int h, int layout)
    {
        W = w; H = h; Layout = layout;
        S = Math.Min(h / 738.0, w / 838.0);
        MW = Math.Min(w, (int)Math.Round(838 * S)); MH = Math.Min(h, (int)Math.Round(738 * S));
        DX = layout == 0 ? 0 : layout == 2 ? w - MW : (w - MW) / 2;
        K = Math.Min(h / 1080f, 2.5f);
    }

    public static Bitmap Scaled(string origPath)
    {
        using (Image o = Image.FromFile(origPath))
        {
            Bitmap b = new Bitmap(MW, MH, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(b))
            using (ImageAttributes a = new ImageAttributes())
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                a.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(o, new Rectangle(0, 0, MW, MH), 0, 0, o.Width, o.Height, GraphicsUnit.Pixel, a);
            }
            return b;
        }
    }

    static byte[] Bytes(Bitmap b, out int stride)
    {
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        stride = d.Stride;
        byte[] r = new byte[d.Stride * b.Height];
        Marshal.Copy(d.Scan0, r, 0, r.Length);
        b.UnlockBits(d);
        return r;
    }

    static void Put(Bitmap b, byte[] r)
    {
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        Marshal.Copy(r, 0, d.Scan0, r.Length);
        b.UnlockBits(d);
    }

    // Plus grand carre de pleine mer (bleu dominant), cherche par pas, pour servir de motif.
    public static Rectangle SeaPatch(Bitmap m, int size)
    {
        int st; byte[] p = Bytes(m, out st);
        int w = m.Width, h = m.Height;
        int[,] ii = new int[h + 1, w + 1];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * st + x * 3;
                int B = p[i], G = p[i + 1], R = p[i + 2];
                int sea = (B > R + 30 && B > G - 5 && R < 110) ? 0 : 1;
                ii[y + 1, x + 1] = sea + ii[y, x + 1] + ii[y + 1, x] - ii[y, x];
            }
        for (int sz = size; sz >= 96; sz -= 32)
        {
            Rectangle best = Rectangle.Empty; int bestScore = int.MaxValue;
            for (int y = 0; y + sz <= h; y += 8)
                for (int x = 0; x + sz <= w; x += 8)
                {
                    int land = ii[y + sz, x + sz] - ii[y, x + sz] - ii[y + sz, x] + ii[y, x];
                    if (land == 0)
                    {
                        // prefere une mer proche du centre vertical (couleur moyenne), loin des coins illustres
                        int score = Math.Abs(y + sz / 2 - h / 2);
                        if (score < bestScore) { bestScore = score; best = new Rectangle(x, y, sz, sz); }
                    }
                }
            if (!best.IsEmpty) return best;
        }
        return new Rectangle(0, h / 2 - 48, 96, 96);
    }

    // Plusieurs carres de pleine mer de la carte (sans recouvrement), pour servir de motifs.
    static List<Rectangle> SeaPatches(Bitmap m, int size, int max)
    {
        int st; byte[] p = Bytes(m, out st);
        int w = m.Width, h = m.Height;
        int[,] ii = new int[h + 1, w + 1];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * st + x * 3;
                int B = p[i], G = p[i + 1], R = p[i + 2];
                int sea = (B > R + 30 && B > G - 5 && R < 110) ? 0 : 1;
                ii[y + 1, x + 1] = sea + ii[y, x + 1] + ii[y + 1, x] - ii[y, x];
            }
        List<Rectangle> found = new List<Rectangle>();
        for (int sz = size; sz >= 64 && found.Count < max; sz -= 32)
            for (int y = 0; y + sz <= h && found.Count < max; y += 8)
                for (int x = 0; x + sz <= w && found.Count < max; x += 8)
                {
                    if (ii[y + sz, x + sz] - ii[y, x + sz] - ii[y + sz, x] + ii[y, x] != 0) continue;
                    Rectangle r = new Rectangle(x, y, sz, sz);
                    bool free = true;
                    foreach (Rectangle o in found) if (o.IntersectsWith(r)) { free = false; break; }
                    if (free) found.Add(r);
                }
        return found;
    }

    // Eau a la maniere de la carte : morceaux de pleine mer de la carte, retournes au hasard et fondus entre eux
    // (fenetres en cloche qui se recouvrent) : meme couleur et meme pinceau, sans motif qui se repete a l'oeil.
    public static Bitmap SeaTexture(Bitmap m, int w, int h)
    {
        List<Rectangle> pats = SeaPatches(m, 192, 8);
        if (pats.Count == 0) return SeaFill(m, w, h);
        int s = pats[pats.Count - 1].Width;
        foreach (Rectangle r in pats) s = Math.Min(s, r.Width);
        int step = s * 2 / 3;
        int sm; byte[] mp = Bytes(m, out sm);
        double[] acc = new double[w * h * 3], wt = new double[w * h];
        double[] win = new double[s];
        for (int i = 0; i < s; i++) win[i] = Math.Sin(Math.PI * (i + 0.5) / s) + 1e-3;
        Random rnd = new Random(4242);
        for (int gy = -step; gy < h; gy += step)
            for (int gx = -step; gx < w; gx += step)
            {
                Rectangle r = pats[rnd.Next(pats.Count)];
                bool fx = rnd.Next(2) == 1, fy = rnd.Next(2) == 1;
                int ox = r.X + rnd.Next(r.Width - s + 1), oy = r.Y + rnd.Next(r.Height - s + 1);
                for (int y = 0; y < s; y++)
                {
                    int cy = gy + y; if (cy < 0 || cy >= h) continue;
                    int sy = oy + (fy ? s - 1 - y : y);
                    for (int x = 0; x < s; x++)
                    {
                        int cx = gx + x; if (cx < 0 || cx >= w) continue;
                        int sx = ox + (fx ? s - 1 - x : x);
                        double k = win[x] * win[y];
                        int mi = sy * sm + sx * 3, ci = cy * w + cx;
                        acc[ci * 3] += mp[mi] * k; acc[ci * 3 + 1] += mp[mi + 1] * k; acc[ci * 3 + 2] += mp[mi + 2] * k;
                        wt[ci] += k;
                    }
                }
            }
        Bitmap b = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        int sb; byte[] bp = Bytes(b, out sb);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int ci = y * w + x, bi = y * sb + x * 3;
                double k = wt[ci] > 0 ? 1 / wt[ci] : 0;
                bp[bi] = Clamp(acc[ci * 3] * k); bp[bi + 1] = Clamp(acc[ci * 3 + 1] * k); bp[bi + 2] = Clamp(acc[ci * 3 + 2] * k);
            }
        Put(b, bp);
        return b;
    }

    // Fond de mer : couleur moyenne de la mer de la carte, variations lentes, et seulement le grain fin
    // de la peinture (plafonne) preleve sur une zone de pleine mer : aucune tache ne peut se repeter.
    public static Bitmap SeaFill(Bitmap m, int w, int h)
    {
        int sm; byte[] mp = Bytes(m, out sm);
        double mR = 0, mG = 0, mB = 0; long n = 0;
        for (int y = 0; y < m.Height; y += 2)
            for (int x = 0; x < m.Width; x += 2)
            {
                int i = y * sm + x * 3;
                int B = mp[i], G = mp[i + 1], R = mp[i + 2];
                if (B > R + 30 && B > G - 5 && R < 110) { mB += B; mG += G; mR += R; n++; }
            }
        if (n == 0) { mR = 30; mG = 95; mB = 150; } else { mR /= n; mG /= n; mB /= n; }

        Rectangle r = SeaPatch(m, 256);
        int pw = r.Width, ph = r.Height;
        // grain = patch - flou(patch), en luminance, plafonne a +/-10
        double[] lum = new double[pw * ph];
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                int i = (y + r.Y) * sm + (x + r.X) * 3;
                lum[y * pw + x] = 0.3 * mp[i + 2] + 0.59 * mp[i + 1] + 0.11 * mp[i];
            }
        double[] grain = new double[pw * ph];
        const int rad = 6;
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                double s = 0; int c = 0;
                for (int dy = -rad; dy <= rad; dy++)
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        int xx = Math.Abs(x + dx), yy = Math.Abs(y + dy);
                        if (xx >= pw) xx = 2 * pw - 2 - xx;
                        if (yy >= ph) yy = 2 * ph - 2 - yy;
                        s += lum[yy * pw + xx]; c++;
                    }
                grain[y * pw + x] = Math.Max(-10, Math.Min(10, lum[y * pw + x] - s / c));
            }

        // variations lentes (bruit de valeur, cellules ~280 px)
        Random rnd = new Random(12345);
        int gw = w / 280 + 3, gh = h / 280 + 3;
        double[,] lf = new double[gh, gw];
        for (int j = 0; j < gh; j++) for (int i = 0; i < gw; i++) lf[j, i] = rnd.NextDouble() * 2 - 1;

        Bitmap fill = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        int sf; byte[] fp = Bytes(fill, out sf);
        for (int y = 0; y < h; y++)
        {
            double gy = y / 280.0; int y0 = (int)gy; double fy = gy - y0; fy = fy * fy * (3 - 2 * fy);
            for (int x = 0; x < w; x++)
            {
                double gx = x / 280.0; int x0 = (int)gx; double fx = gx - x0; fx = fx * fx * (3 - 2 * fx);
                double low = (lf[y0, x0] * (1 - fx) + lf[y0, x0 + 1] * fx) * (1 - fy) + (lf[y0 + 1, x0] * (1 - fx) + lf[y0 + 1, x0 + 1] * fx) * fy;
                // grain repete en miroir : son amplitude plafonnee le rend imperceptible
                int px = x % (2 * pw); if (px >= pw) px = 2 * pw - 1 - px;
                int py = y % (2 * ph); if (py >= ph) py = 2 * ph - 1 - py;
                double gr = grain[py * pw + px];
                // assombrissement doux vers les bords exterieurs gauche/droit
                double edge = Math.Min(x, w - 1 - x) / 260.0;
                double vig = 0.86 + 0.14 * Math.Min(1.0, edge);
                double k = (1 + low * 0.05) * vig;
                int i = y * sf + x * 3;
                fp[i] = Clamp((mB + gr) * k); fp[i + 1] = Clamp((mG + gr) * k); fp[i + 2] = Clamp((mR + gr * 0.8) * k);
            }
        }
        Put(fill, fp);
        return fill;
    }

    // Colle la carte sur le fond avec un fondu lineaire sur les bords gauche/droit indiques.
    public static void Blend(Bitmap canvas, Bitmap map, int ox, int featherL, int featherR)
    {
        int sc, sm;
        byte[] c = Bytes(canvas, out sc), m = Bytes(map, out sm);
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                double a = 1.0;
                if (featherL > 0 && x < featherL) a = Math.Min(a, (double)x / featherL);
                if (featherR > 0 && x >= map.Width - featherR) a = Math.Min(a, (double)(map.Width - 1 - x) / featherR);
                a = a * a * (3 - 2 * a);   // lissage
                int ci = y * sc + (x + ox) * 3, mi = y * sm + x * 3;
                for (int k = 0; k < 3; k++) c[ci + k] = (byte)(m[mi + k] * a + c[ci + k] * (1 - a));
            }
        Put(canvas, c);
    }

    // ------------------------------------------------------------ composition
    // Carte calee a gauche ou a droite avec assez de place : parchemin (portrait du dieu, cartouche) derriere une frise.
    // Carte au centre, ou place trop etroite pour un parchemin : mer prolongee de part et d'autre (fondu sur les bords).
    // Remplissage de la place a cote de la carte : 0 panneau (Olympus / Atlantis), 1 eau texturee, 2 image personnalisee
    public static int Fill;
    public static string CustomPath;
    public static bool ShowFrieze = true;   // frise entre la carte et le fond (eau texturee, image personnalisee)

    public static Bitmap Compose(string origPath, string title, string sub, string godPath)
    {
        int friezeW = (int)Math.Round(40 * K), side = W - MW;
        if (Fill == 2 && CustomPath != null) return ComposeCustom(origPath);
        bool panel = Fill == 0 && Layout != 1 && side - friezeW >= 260 * K;
        bool twoPanels = Fill == 0 && Layout == 1 && DX - friezeW >= 150 * K;
        using (Bitmap m = Scaled(origPath))
        {
            Bitmap canvas = SeaTexture(m, W, H);
            if (twoPanels)
            {
                // Au centre, avec de la place : portrait a gauche, rose des vents et cartouche a droite
                Blend(canvas, m, DX, 0, 0);
                int lw = DX - friezeW, rx = DX + MW + friezeW, rw = W - rx;
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (Bitmap parch = Parchment(lw, H, 7))
                    {
                        if (godPath != null) God(parch, godPath);
                        g.DrawImageUnscaled(parch, 0, 0);
                    }
                    using (Bitmap parch = Parchment(rw, H, 11)) g.DrawImageUnscaled(parch, rx, 0);
                    Frieze(g, lw, 0, friezeW, H);
                    Frieze(g, DX + MW, 0, friezeW, H);
                    Compass(g, rx + rw / 2, (int)Math.Round(420 * K), (int)Math.Round(Math.Min(170 * K, rw * 0.32)));
                    int cw = (int)Math.Min(rw - 80 * K, 392 * K);
                    int cy = Math.Min((int)Math.Round(760 * K), H - (int)Math.Round(230 * K));
                    Cartouche(g, rx + (rw - cw) / 2, cy, cw, (int)Math.Round(190 * K), title, sub);
                    if (godPath == null) Compass(g, lw / 2, (int)Math.Round(420 * K), (int)Math.Round(Math.Min(170 * K, lw * 0.32)));
                }
                return canvas;
            }
            if (!panel && Fill == 1 && ShowFrieze)
            {
                // Eau texturee derriere une frise de chaque cote ouvert de la carte
                Blend(canvas, m, DX, 0, 0);
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    if (DX >= friezeW) Frieze(g, DX - friezeW, 0, friezeW, H);
                    if (DX + MW + friezeW <= W) Frieze(g, DX + MW, 0, friezeW, H);
                }
                return canvas;
            }
            if (!panel)
            {
                int f = (int)Math.Round(110 * K);
                Blend(canvas, m, DX, DX > 0 ? f : 0, DX + MW < W ? f : 0);
                return canvas;
            }
            Blend(canvas, m, DX, 0, 0);   // la frise borde directement la carte
            int friezeX = Layout == 0 ? MW : DX - friezeW;
            int panelX = Layout == 0 ? MW + friezeW : 0, panelW = side - friezeW;
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Bitmap parch = Parchment(panelW, H, 7))
                {
                    if (godPath != null) God(parch, godPath);
                    g.DrawImageUnscaled(parch, panelX, 0);
                }
                Frieze(g, friezeX, 0, friezeW, H);
                if (godPath == null) Compass(g, panelX + panelW / 2, (int)Math.Round(420 * K), (int)Math.Round(170 * K));
                int cw = (int)Math.Min(panelW - 80 * K, 392 * K);
                int cy = Math.Min((int)Math.Round(760 * K), H - (int)Math.Round(230 * K));
                Cartouche(g, panelX + (panelW - cw) / 2, cy, cw, (int)Math.Round(190 * K), title, sub);
            }
            return canvas;
        }
    }

    // Image personnalisee de chaque cote de la carte (recadree pour remplir sans deformer), derriere une frise.
    static Bitmap ComposeCustom(string origPath)
    {
        int friezeW = ShowFrieze ? (int)Math.Round(40 * K) : 0;
        using (Bitmap m = Scaled(origPath))
        using (Image img = Image.FromFile(CustomPath))
        {
            Bitmap canvas = SeaTexture(m, W, H);
            Blend(canvas, m, DX, 0, 0);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int lw = DX - friezeW, rx = DX + MW + friezeW, rw = W - rx;
                if (lw > 0) { Cover(g, img, new Rectangle(0, 0, lw, H)); if (friezeW > 0) Frieze(g, lw, 0, friezeW, H); }
                if (rw > 0) { Cover(g, img, new Rectangle(rx, 0, rw, H)); if (friezeW > 0) Frieze(g, DX + MW, 0, friezeW, H); }
            }
            return canvas;
        }
    }

    // Image deja composee (reference des cartes de Poseidon) au centre d'une mappemonde plus large (ultrawide) :
    // de chaque cote, le choix des cartes de Poseidon (panneau Atlantis, eau texturee, image personnalisee). Setup fait.
    public static Bitmap Surround(Bitmap center, string title, string sub, string godPath)
    {
        int ox = (W - center.Width) / 2, friezeW = (int)Math.Round(40 * K), rx0 = ox + center.Width;
        if (Fill == 0 && ox - friezeW >= 150 * K)
        {
            Bitmap canvas = new Bitmap(W, H, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int lw = ox - friezeW, rx = rx0 + friezeW, rw = W - rx;
                g.DrawImageUnscaled(center, ox, 0);
                using (Bitmap parch = Parchment(lw, H, 7)) { if (godPath != null) God(parch, godPath); g.DrawImageUnscaled(parch, 0, 0); }
                using (Bitmap parch = Parchment(rw, H, 11)) g.DrawImageUnscaled(parch, rx, 0);
                Frieze(g, lw, 0, friezeW, H);
                Frieze(g, rx0, 0, friezeW, H);
                Compass(g, rx + rw / 2, (int)Math.Round(420 * K), (int)Math.Round(Math.Min(170 * K, rw * 0.32)));
                int cw = (int)Math.Min(rw - 80 * K, 392 * K);
                int cy = Math.Min((int)Math.Round(760 * K), H - (int)Math.Round(230 * K));
                Cartouche(g, rx + (rw - cw) / 2, cy, cw, (int)Math.Round(190 * K), title, sub);
                if (godPath == null) Compass(g, lw / 2, (int)Math.Round(420 * K), (int)Math.Round(Math.Min(170 * K, lw * 0.32)));
            }
            return canvas;
        }
        Bitmap c = SeaTexture(center, W, H);
        if (Fill == 2 && CustomPath != null && ox > friezeW)
        {
            int fw = ShowFrieze ? friezeW : 0;
            Blend(c, center, ox, 0, 0);
            using (Image img = Image.FromFile(CustomPath))
            using (Graphics g = Graphics.FromImage(c))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.SmoothingMode = SmoothingMode.AntiAlias;
                Cover(g, img, new Rectangle(0, 0, ox - fw, H)); Cover(g, img, new Rectangle(rx0 + fw, 0, W - rx0 - fw, H));
                if (fw > 0) { Frieze(g, ox - fw, 0, fw, H); Frieze(g, rx0, 0, fw, H); }
            }
            return c;
        }
        // eau texturee (ou bande trop etroite pour un panneau) : la reference se fond dans la mer
        if (Fill == 1 && ShowFrieze && ox >= friezeW)
        {
            Blend(c, center, ox, 0, 0);
            using (Graphics g = Graphics.FromImage(c)) { Frieze(g, ox - friezeW, 0, friezeW, H); Frieze(g, rx0, 0, friezeW, H); }
            return c;
        }
        int f = Math.Min((int)Math.Round(110 * K), Math.Max(8, center.Width / 6));
        Blend(c, center, ox, f, f);
        return c;
    }

    // Dessine img dans r en le recadrant au centre (comme un fond « couvrir »).
    static void Cover(Graphics g, Image img, Rectangle r)
    {
        double s = Math.Max((double)r.Width / img.Width, (double)r.Height / img.Height);
        int sw = (int)Math.Round(r.Width / s), sh = (int)Math.Round(r.Height / s);
        int sx = (img.Width - sw) / 2, sy = (img.Height - sh) / 2;
        g.DrawImage(img, r, sx, sy, sw, sh, GraphicsUnit.Pixel);
    }

    // Parchemin : bruit de valeur sur plusieurs octaves + assombrissement des bords.
    static Bitmap Parchment(int w, int h, int seed)
    {
        Random rnd = new Random(seed);
        double[] acc = new double[w * h];
        int[] cells = { 4, 9, 21, 47, 103 };
        double[] amp = { 1.0, 0.6, 0.35, 0.2, 0.12 };
        for (int o = 0; o < cells.Length; o++)
        {
            int cw = cells[o], ch = Math.Max(2, cells[o] * h / w);
            double[,] grid = new double[ch + 2, cw + 2];
            for (int j = 0; j < ch + 2; j++) for (int i = 0; i < cw + 2; i++) grid[j, i] = rnd.NextDouble() * 2 - 1;
            for (int y = 0; y < h; y++)
            {
                double gy = (double)y / h * ch; int y0 = (int)gy; double fy = gy - y0; fy = fy * fy * (3 - 2 * fy);
                for (int x = 0; x < w; x++)
                {
                    double gx = (double)x / w * cw; int x0 = (int)gx; double fx = gx - x0; fx = fx * fx * (3 - 2 * fx);
                    double v = (grid[y0, x0] * (1 - fx) + grid[y0, x0 + 1] * fx) * (1 - fy) + (grid[y0 + 1, x0] * (1 - fx) + grid[y0 + 1, x0 + 1] * fx) * fy;
                    acc[y * w + x] += v * amp[o];
                }
            }
        }
        Bitmap b = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        int st; byte[] p = Bytes(b, out st);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double n = acc[y * w + x];
                double ex = Math.Min(x, w - 1 - x) / (60.0 * K), ey = Math.Min(y, h - 1 - y) / (60.0 * K);
                double edge = Math.Min(1.0, Math.Min(ex, ey));
                double dark = 0.72 + 0.28 * edge;
                double R = (226 + n * 22) * dark, G = (206 + n * 20) * dark, Bc = (164 + n * 18) * dark;
                int i = y * st + x * 3;
                p[i] = Clamp(Bc); p[i + 1] = Clamp(G); p[i + 2] = Clamp(R);
            }
        Put(b, p);
        return b;
    }

    static byte Clamp(double v) { return (byte)Math.Max(0, Math.Min(255, v)); }

    // Portrait (ecran de chargement 800x600) recadre au centre, fondu dans le parchemin :
    // masque en super-ellipse aux bords tres progressifs, legere teinte sepia, grain du papier conserve.
    static void God(Bitmap parch, string path)
    {
        const int cropW = 440, cropH = 600;
        int slotW = Math.Min(Math.Min(parch.Width - 16, (int)Math.Round(456 * K)), (int)(parch.Height * 0.6 * cropW / cropH));
        int slotH = (int)Math.Round(cropH * (double)slotW / cropW);
        int sx = (parch.Width - slotW) / 2, sy = (int)Math.Round(40 * K);
        using (Image src = Image.FromFile(path))
        using (Bitmap img = new Bitmap(slotW, slotH, PixelFormat.Format24bppRgb))
        {
            using (Graphics g = Graphics.FromImage(img))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, slotW, slotH), (src.Width - cropW) / 2, 0, cropW, cropH, GraphicsUnit.Pixel);
            }
            int sp, si; byte[] pp = Bytes(parch, out sp), ip = Bytes(img, out si);
            for (int y = 0; y < slotH; y++)
                for (int x = 0; x < slotW; x++)
                {
                    double nx = Math.Abs((x + 0.5) / slotW * 2 - 1), ny = Math.Abs((y + 0.5) / slotH * 2 - 1);
                    double d = Math.Pow(Math.Pow(nx, 2.6) + Math.Pow(ny, 2.6), 1 / 2.6);
                    double t = Math.Max(0, Math.Min(1, (d - 0.55) / 0.45));
                    double a = 1 - t * t * (3 - 2 * t);
                    if (a <= 0) continue;
                    int ii = y * si + x * 3, pi = (y + sy) * sp + (x + sx) * 3;
                    double B = ip[ii], G = ip[ii + 1], R = ip[ii + 2];
                    // sepia leger (20 %) + grain du papier (multiplication douce)
                    double lum = 0.3 * R + 0.59 * G + 0.11 * B;
                    double sR = lum * 1.07, sG = lum * 0.92, sB = lum * 0.72;
                    R = R * 0.8 + sR * 0.2; G = G * 0.8 + sG * 0.2; B = B * 0.8 + sB * 0.2;
                    double paper = (0.3 * pp[pi + 2] + 0.59 * pp[pi + 1] + 0.11 * pp[pi]) / 210.0;
                    double k = 0.88 + 0.12 * paper;
                    R *= k; G *= k; B *= k;
                    pp[pi] = Clamp(B * a + pp[pi] * (1 - a));
                    pp[pi + 1] = Clamp(G * a + pp[pi + 1] * (1 - a));
                    pp[pi + 2] = Clamp(R * a + pp[pi + 2] * (1 - a));
                }
            Put(parch, pp);
        }
    }

    // Frise grecque (meandre) verticale, or sur fond brun sombre.
    static void Frieze(Graphics g, int x, int y, int w, int h)
    {
        using (SolidBrush bg = new SolidBrush(Color.FromArgb(58, 38, 22)))
            g.FillRectangle(bg, x, y, w, h);
        Color gold = Color.FromArgb(214, 172, 92);
        using (Pen edge = new Pen(gold, 2 * K))
        {
            g.DrawLine(edge, x + 3 * K, y, x + 3 * K, y + h);
            g.DrawLine(edge, x + w - 4 * K, y, x + w - 4 * K, y + h);
        }
        int u = (int)Math.Round(26 * K);                      // hauteur d'un motif
        float l = x + 9 * K, r = x + w - 9 * K;  // largeur utile
        float cw = (r - l);
        using (Pen p = new Pen(gold, 3.2f * K))
        {
            p.LineJoin = LineJoin.Miter;
            for (int yy = y - u; yy < y + h + u; yy += u)
            {
                // meandre : crochet carre qui s'enroule, repete verticalement
                PointF[] pts = {
                    new PointF(l, yy), new PointF(r, yy), new PointF(r, yy + u * 0.75f),
                    new PointF(l + cw * 0.33f, yy + u * 0.75f), new PointF(l + cw * 0.33f, yy + u * 0.35f),
                    new PointF(l + cw * 0.67f, yy + u * 0.35f)
                };
                g.DrawLines(p, pts);
                g.DrawLine(p, l, yy, l, yy + u);
            }
        }
    }

    // Rose des vents a 8 branches.
    static void Compass(Graphics g, int cx, int cy, int r)
    {
        Color ink = Color.FromArgb(92, 58, 30), gold = Color.FromArgb(196, 150, 70), light = Color.FromArgb(238, 222, 186);
        using (Pen pc = new Pen(ink, 2.2f))
        {
            g.DrawEllipse(pc, cx - r * 0.92f, cy - r * 0.92f, r * 1.84f, r * 1.84f);
            g.DrawEllipse(pc, cx - r * 0.84f, cy - r * 0.84f, r * 1.68f, r * 1.68f);
            for (int t = 0; t < 32; t++)
            {
                double a = t * Math.PI / 16;
                float r1 = r * 0.84f, r2 = r * (t % 4 == 0 ? 0.74f : 0.79f);
                g.DrawLine(pc, cx + (float)Math.Cos(a) * r1, cy + (float)Math.Sin(a) * r1, cx + (float)Math.Cos(a) * r2, cy + (float)Math.Sin(a) * r2);
            }
        }
        for (int pass = 0; pass < 2; pass++)
        {
            int n = pass == 0 ? 4 : 4;
            double off = pass == 0 ? Math.PI / 4 : 0;
            float len = pass == 0 ? r * 0.55f : r * 0.98f, wid = pass == 0 ? r * 0.10f : r * 0.14f;
            for (int i = 0; i < n; i++)
            {
                double a = off + i * Math.PI / 2 - Math.PI / 2;
                PointF tip = new PointF(cx + (float)Math.Cos(a) * len, cy + (float)Math.Sin(a) * len);
                PointF lft = new PointF(cx + (float)Math.Cos(a - Math.PI / 2) * wid, cy + (float)Math.Sin(a - Math.PI / 2) * wid);
                PointF rgt = new PointF(cx + (float)Math.Cos(a + Math.PI / 2) * wid, cy + (float)Math.Sin(a + Math.PI / 2) * wid);
                PointF c = new PointF(cx, cy);
                using (SolidBrush b1 = new SolidBrush(pass == 0 ? light : gold))
                using (SolidBrush b2 = new SolidBrush(ink))
                {
                    g.FillPolygon(b1, new[] { c, lft, tip });
                    g.FillPolygon(b2, new[] { c, rgt, tip });
                }
                using (Pen po = new Pen(ink, 1.4f)) g.DrawPolygon(po, new[] { lft, tip, rgt, c });
            }
        }
        using (SolidBrush b = new SolidBrush(gold)) g.FillEllipse(b, cx - 9, cy - 9, 18, 18);
        using (Pen po = new Pen(ink, 1.6f)) g.DrawEllipse(po, cx - 9, cy - 9, 18, 18);
        using (Font f = new Font("Georgia", 26, FontStyle.Bold))
        using (SolidBrush b = new SolidBrush(ink))
        {
            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("N", f, b, cx, cy - r * 1.15f, sf);
            g.DrawString("S", f, b, cx, cy + r * 1.15f, sf);
            g.DrawString("E", f, b, cx + r * 1.15f, cy, sf);
            g.DrawString("O", f, b, cx - r * 1.15f, cy, sf);
        }
    }

    // Cartouche decoratif (cadre double + petite frise), sans texte specifique : l'image est partagee par plusieurs scenarios.
    static void Cartouche(Graphics g, int x, int y, int w, int h, string title, string sub)
    {
        Color ink = Color.FromArgb(92, 58, 30);
        using (Pen p1 = new Pen(ink, 3 * K)) g.DrawRectangle(p1, x, y, w, h);
        using (Pen p2 = new Pen(ink, 1.2f * K)) g.DrawRectangle(p2, x + 8 * K, y + 8 * K, w - 16 * K, h - 16 * K);
        using (Font f = new Font("Georgia", 30 * K, FontStyle.Italic))
        using (Font f2 = new Font("Georgia", 15 * K))
        using (SolidBrush b = new SolidBrush(ink))
        {
            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(title, f, b, x + w / 2f, y + h * 0.38f, sf);
            g.DrawString(sub, f2, b, x + w / 2f, y + h * 0.68f, sf);
        }
    }

    public static void SaveJpg(Bitmap b, string path, long q)
    {
        ImageCodecInfo codec = null;
        foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders()) if (c.MimeType == "image/jpeg") codec = c;
        EncoderParameters ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, q);
        b.Save(path, codec, ep);
    }
}
