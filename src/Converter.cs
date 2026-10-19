// Conversion des scenarios (.pak, .set), des cartes de l'editeur (.map) et des sauvegardes (.sav) pour les mappemondes grand ecran :
// positions des cites, noms de regions et routes agrandies (x 1080/738 en 1080p, carte 1738x1080 ; x 1410/738 en 1440p,
// 2376x1410 ; ou n'importe quelle taille de mappemonde, carte calee a gauche, au centre ou a droite : x' = x * echelle + decalage).
// Un fichier deja converti autrement est reconverti depuis son .bak d'origine s'il existe, sinon ses positions sont
// directement passees d'une geometrie a l'autre (arrondi de 1 px au plus).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZeusPak;

public enum Outcome { Converted, AlreadyConverted, NoWorldMap, Error }

// Geometrie de la mappemonde : echelle de la carte peinte et decalage horizontal (la carte reste calee en haut).
public class Target
{
    public double S; public int Dx; public string Label;
    public static readonly Target R1080 = new Target(1080.0 / 738.0, 0, "1080p");
    public static readonly Target R1440 = new Target(1410.0 / 738.0, 0, "1440p");
    public Target(double s, int dx, string label) { S = s; Dx = dx; Label = label; }

    // Mappemonde w x h, carte placee a gauche (0), au centre (1) ou a droite (2) : meme regle que le generateur et le patcheur.
    public static Target ForMap(int w, int h, int layout)
    {
        double s = Math.Min(h / 738.0, w / 838.0);
        int mw = Math.Min(w, (int)Math.Round(838 * s));
        int dx = layout == 0 ? 0 : layout == 2 ? w - mw : (w - mw) / 2;
        Target t = new Target(s, dx, w + "x" + h + (layout == 0 ? "" : layout == 1 ? L.T(" centre", " centred") : L.T(" droite", " right")));
        if (t.SameAs(R1080)) return R1080;
        if (t.SameAs(R1440)) return R1440;
        return t;
    }

    // Mappemonde a la taille de la vue d'un exe en w x h : cases de 60 px moins 2 en largeur, rangees de 15 px sous la barre du haut.
    public static Target ForResolution(int w, int h, int layout) { return ForMap(60 * ((w - 180) / 60) - 2, ((h - 30) / 15) * 15, layout); }

    // Echelle notee sur 16 bits (x 10000) dans les signatures : deux geometries egales a cette precision sont identiques.
    public int S16 { get { return (int)Math.Round(S * 10000); } }
    public bool SameAs(Target o) { return o != null && S16 == o.S16 && Dx == o.Dx; }
    public bool Legacy { get { return SameAs(R1080) || SameAs(R1440); } }
    public override string ToString() { return Label; }
}

public class ConvertResult
{
    public Outcome Outcome;
    public string Message;
}

// Transformation des positions : x' = x * F + OffX, y' = y * F.
public struct Xf
{
    public double F, OffX;
    public Xf(double f, double offX) { F = f; OffX = offX; }
    public static implicit operator Xf(double f) { return new Xf(f, 0); }
    // de la geometrie a vers la geometrie b (a = null : positions d'origine)
    public static Xf Between(Target a, Target b)
    {
        if (a == null) return new Xf(b.S, b.Dx);
        double f = b.S / a.S;
        return new Xf(f, b.Dx - a.Dx * f);
    }
}

public static class Converter
{
    // Cartes .map de l'editeur : prises en charge.
    public static readonly bool MapsEnabled = true;

    public static double ScaleOf(Target t) { return t.S; }
    public static string NameOf(Target t) { return t.Label; }

    // ---- signatures
    // .pak / .set : 16 octets a la fin (toujours vide) de la zone reservee a la copie de carte des reglages.
    // 1080p / 1440p : « ZEUS-MAPPE-1080 » / « ZEUS-MAPPE-1440 » + 01 (inchange). Autre geometrie : « ZEUS-MAPPE- »,
    // echelle x 10000 et decalage (16 bits chacun), puis 02 ; les versions precedentes la voient comme inconnue et l'ignorent.
    static byte[] PakMarker(Target t)
    {
        if (t.SameAs(Target.R1080)) return Encoding.ASCII.GetBytes("ZEUS-MAPPE-1080\u0001");
        if (t.SameAs(Target.R1440)) return Encoding.ASCII.GetBytes("ZEUS-MAPPE-1440\u0001");
        byte[] m = new byte[16];
        Encoding.ASCII.GetBytes("ZEUS-MAPPE-").CopyTo(m, 0);
        BitConverter.GetBytes((ushort)t.S16).CopyTo(m, 11);
        BitConverter.GetBytes((ushort)t.Dx).CopyTo(m, 13);
        m[15] = 2;
        return m;
    }
    // .sav / .map : octet 323 (remplissage, toujours nul) des routes. Route 0 = 5A ; route 1 = 10 (1080p), 14 (1440p),
    // ou 7E (autre geometrie : echelle x 10000 dans les routes 2-3, decalage dans les routes 4-5).
    const int SAV_MARK_OFFSET = 323;
    const byte SAV_MARK = 0x5A, SAV_V2 = 0x7E;
    const int SAV_MARK_ROUTES = 6;

    static void WriteRouteMark(byte[] t, Target target)
    {
        t[SAV_MARK_OFFSET] = SAV_MARK;
        if (target.SameAs(Target.R1080)) { t[324 + SAV_MARK_OFFSET] = 0x10; return; }
        if (target.SameAs(Target.R1440)) { t[324 + SAV_MARK_OFFSET] = 0x14; return; }
        t[324 + SAV_MARK_OFFSET] = SAV_V2;
        byte[] s = BitConverter.GetBytes((ushort)target.S16), d = BitConverter.GetBytes((ushort)target.Dx);
        t[2 * 324 + SAV_MARK_OFFSET] = s[0]; t[3 * 324 + SAV_MARK_OFFSET] = s[1];
        t[4 * 324 + SAV_MARK_OFFSET] = d[0]; t[5 * 324 + SAV_MARK_OFFSET] = d[1];
    }

    // Zone de signature vide (fichier jamais converti)
    static bool RouteMarkEmpty(byte[] t)
    {
        for (int q = 0; q < SAV_MARK_ROUTES; q++) if (t[q * 324 + SAV_MARK_OFFSET] != 0) return false;
        return true;
    }

    // Etat d'un fichier : non converti, converti (Res), ou signature inconnue (Unknown = true).
    class State { public bool Marked, Unknown; public Target Res; }

    static State PakState(PakFile k)
    {
        State s = new State();
        byte[] b = k.Bytes; int o = k.PaddedEnd - 16;
        string m = Encoding.ASCII.GetString(b, o, 15);
        if (!m.StartsWith("ZEUS-MAPPE-")) return s;
        s.Marked = true;
        if (m == "ZEUS-MAPPE-1080") s.Res = Target.R1080;
        else if (m == "ZEUS-MAPPE-1440") s.Res = Target.R1440;
        else if (b[o + 15] == 2) s.Res = FromMark(BitConverter.ToUInt16(b, o + 11), BitConverter.ToUInt16(b, o + 13));
        else s.Unknown = true;
        return s;
    }

    static Target FromMark(int s16, int dx)
    {
        Target t = new Target(s16 / 10000.0, dx, string.Format(L.T("échelle {0:F4}, décalage {1} px", "scale {0:F4}, offset {1} px"), s16 / 10000.0, dx));
        return t;
    }

    static State SavState(SavFile f) { return RouteState(f.Get(f.RoutesIndex).Data); }

    // Carte de l'editeur (.map) : meme signature que les .sav, dans le bloc des routes (18).
    static State MapState(MapFile m) { return RouteState(m.Get(18).Data); }

    static State RouteState(byte[] t)
    {
        State s = new State();
        if (t[SAV_MARK_OFFSET] != SAV_MARK) return s;
        s.Marked = true;
        byte r = t[324 + SAV_MARK_OFFSET];
        if (r == 0x10) s.Res = Target.R1080;
        else if (r == 0x14) s.Res = Target.R1440;
        else if (r == SAV_V2) s.Res = FromMark(t[2 * 324 + SAV_MARK_OFFSET] | (t[3 * 324 + SAV_MARK_OFFSET] << 8), t[4 * 324 + SAV_MARK_OFFSET] | (t[5 * 324 + SAV_MARK_OFFSET] << 8));
        else s.Unknown = true;
        return s;
    }

    enum Kind { Pak, Sav, Map }

    static Kind KindOf(string path)
    {
        if (path.EndsWith(".sav", StringComparison.OrdinalIgnoreCase)) return Kind.Sav;
        if (path.EndsWith(".map", StringComparison.OrdinalIgnoreCase)) return Kind.Map;
        return Kind.Pak;
    }

    static MapFile LoadMap(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        MapFile m = MapFile.Parse(b, 0);
        if (m.End != b.Length) throw new InvalidDataException(L.T("format non reconnu (données après la carte)", "unknown format (data after the map)"));
        return m;
    }

    static State StateOf(Kind k, string path)
    {
        if (k == Kind.Sav) return SavState(SavFile.Load(path));
        if (k == Kind.Map) return MapState(LoadMap(path));
        return PakState(PakFile.Load(path));
    }

    static ConvertResult Do(Kind k, string read, string src, string dst, Target target, Xf factor, bool alreadyMarked)
    {
        if (k == Kind.Sav) return ConvertSav(src, dst, target);
        if (k == Kind.Map) return DoMap(read, src, dst, target, factor, alreadyMarked);
        return DoPak(read, src, dst, target, factor);
    }

    static bool Same(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // Multiplie une coordonnee. Les valeurs >= 0x8000 (ex. 0xFFFF, 52378) ne sont pas des positions mais des marqueurs
    // d'elements hors carte : laissees telles quelles, dans tous les sens de conversion.
    static void Scale16(byte[] d, int o, double s, double off)
    {
        ushort v = BitConverter.ToUInt16(d, o);
        if (v >= 0x8000) return;
        Array.Copy(BitConverter.GetBytes((ushort)Math.Max(0, Math.Min(0x7FFF, Math.Round(v * s + off)))), 0, d, o, 2);
    }

    // Multiplie les positions des elements de mappemonde (bloc e) et des points de routes (bloc t).
    static void ScaleBlocks(Block e, Block t, Xf x, out int elements, out int points)
    {
        elements = 0; points = 0;
        for (int i = 0; i < e.Count; i++)
        {
            int o = i * e.ElemSize;
            if (e.Data[o] == 0) continue;
            Scale16(e.Data, o + 4, x.F, x.OffX);
            Scale16(e.Data, o + 6, x.F, 0);
            elements++;
        }
        for (int r = 0; r < t.Count; r++)
        {
            int o = r * 324, c = t.Data[o + 321];
            if (c > 50) throw new InvalidDataException("route " + r + " : " + c + " points");
            for (int q = 0; q < c; q++)
            {
                int p = o + 8 + 6 * q;
                Scale16(t.Data, p, x.F, x.OffX);
                Scale16(t.Data, p + 2, x.F, 0);
                points++;
            }
        }
        e.Modified = true; t.Modified = true;
    }

    // Remplacement : copie .bak de l'original (si elle n'existe pas encore) puis mise en place du fichier verifie.
    static void Finish(string src, string dst, string tmp)
    {
        if (string.Equals(Path.GetFullPath(src), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase))
        {
            string bak = src + ".bak";
            if (!File.Exists(bak)) File.Copy(src, bak);
        }
        if (File.Exists(dst)) { File.SetAttributes(dst, File.GetAttributes(dst) & ~FileAttributes.ReadOnly); File.Delete(dst); }   // fichiers d'un CD : lecture seule
        File.Move(tmp, dst);
    }

    // Point d'entree : src = fichier choisi, dst = destination (egale a src en mode remplacement).
    public static ConvertResult Convert(string src, string dst, Target target)
    {
        try
        {
            Kind kind = KindOf(src);
            if (kind == Kind.Sav) return ConvertSav(src, dst, target);   // etat partie par partie
            State st = StateOf(kind, src);
            if (st.Marked && st.Unknown) return Result(Outcome.AlreadyConverted, L.T("Fichier modifié par une autre conversion (signature inconnue), ignoré.", "File changed by another conversion (unknown signature), skipped."));
            if (st.Marked && st.Res.SameAs(target)) return Result(Outcome.AlreadyConverted, L.T("Déjà converti en " + NameOf(target) + ", ignoré.", "Already converted to " + NameOf(target) + ", skipped."));

            if (st.Marked)
            {
                // Converti dans l'autre resolution : de preference depuis l'original (.bak), sinon changement direct.
                string bak = src + ".bak";
                if (File.Exists(bak))
                {
                    State bs = StateOf(kind, bak);
                    if (!bs.Marked)
                    {
                        ConvertResult r = Do(kind, bak, src, dst, target, Xf.Between(null, target), false);
                        if (r.Outcome != Outcome.Error) r.Message = L.T("Reconverti en " + NameOf(target) + " depuis l'original (.bak) : ", "Converted again to " + NameOf(target) + " from the original (.bak): ") + r.Message;
                        return r;
                    }
                }
                Xf f = Xf.Between(st.Res, target);
                ConvertResult rr = Do(kind, src, src, dst, target, f, true);
                if (rr.Outcome != Outcome.Error) rr.Message = L.T("Passé de " + NameOf(st.Res) + " à " + NameOf(target) + " (sans original : arrondi de 1 px au plus) : ", "Changed from " + NameOf(st.Res) + " to " + NameOf(target) + " (no original: rounding of 1 px at most): ") + rr.Message;
                return rr;
            }
            return Do(kind, src, src, dst, target, Xf.Between(null, target), false);
        }
        catch (Exception e)
        {
            try { if (File.Exists(dst + ".tmp")) File.Delete(dst + ".tmp"); } catch { }
            return Result(Outcome.Error, L.T("Erreur, fichier non modifié : ", "Error, file not changed: ") + e.Message);
        }
    }

    static ConvertResult Result(Outcome o, string m) { ConvertResult r = new ConvertResult(); r.Outcome = o; r.Message = m; return r; }

    // Scenario (.pak / .set) : read = fichier lu (original, .bak ou fichier deja converti), src = fichier remplace.
    static ConvertResult DoPak(string read, string src, string dst, Target target, Xf factor)
    {
        PakFile k = PakFile.Load(read);
        if (!Same(k.Build(), k.Bytes)) throw new InvalidDataException(L.T("format non reconnu (la relecture ne redonne pas le fichier d'origine)", "unknown format (reading it back does not give the original file)"));
        int elements = 0, points = 0;
        foreach (MapFile m in k.AllMaps())
        {
            int e, p;
            ScaleBlocks(m.Get(17), m.Get(18), factor, out e, out p);
            elements += e; points += p;
        }
        byte[] marker = PakMarker(target);
        byte[] output = k.Build();
        if (k.Settings.End > k.PaddedEnd - marker.Length - 64) throw new InvalidDataException(L.T("pas de place pour la signature", "no room for the signature"));
        Array.Copy(marker, 0, output, k.PaddedEnd - marker.Length, marker.Length);

        // Verification : seuls les blocs des cites (17), des routes (18) et la signature different.
        string tmp = dst + ".tmp";
        File.WriteAllBytes(tmp, output);
        PakFile k0 = PakFile.Load(read), k2 = PakFile.Load(tmp);
        MapFile[] a0 = new System.Collections.Generic.List<MapFile>(k0.AllMaps()).ToArray();
        MapFile[] a2 = new System.Collections.Generic.List<MapFile>(k2.AllMaps()).ToArray();
        if (a0.Length != a2.Length) throw new Exception(L.T("vérification : nombre de cartes différent", "check: different number of maps"));
        for (int i = 0; i < a0.Length; i++)
            foreach (Block b in a0[i].Blocks)
                if (b.Index != 17 && b.Index != 18 && !Same(b.Data, a2[i].Get(b.Index).Data))
                    throw new Exception(L.T("vérification : bloc " + b.Index + " modifié par erreur", "check: block " + b.Index + " changed by mistake"));
        for (int j = 0; j < k0.Settings.Start; j++)
            if (k0.Bytes[j] != k2.Bytes[j]) throw new Exception(L.T("vérification : réglages modifiés par erreur", "check: settings changed by mistake"));
        for (int j = k0.PaddedEnd; j < k0.DataLength; j++)
            if (k0.Bytes[j] != k2.Bytes[j]) throw new Exception(L.T("vérification : réglages modifiés par erreur", "check: settings changed by mistake"));
        State s2 = PakState(k2);
        if (!s2.Marked || s2.Unknown || !s2.Res.SameAs(target)) throw new Exception(L.T("vérification : signature absente", "check: signature missing"));

        Finish(src, dst, tmp);
        if (elements == 0 && points == 0)
            return Result(Outcome.NoWorldMap, L.T("Pas de cité sur la mappemonde (rien à agrandir), fichier marqué " + NameOf(target) + ".", "No city on the world map (nothing to enlarge), file marked " + NameOf(target) + "."));
        return Result(Outcome.Converted, string.Format(L.T("{0} : {1} éléments et {2} points de routes.", "{0}: {1} elements and {2} route points."), NameOf(target), elements, points));
    }

    // Sauvegarde (.sav) : memes blocs mappemonde et routes qu'un scenario, dans chacune de ses parties.
    // Une sauvegarde d'aventure contient plusieurs parties (la suivante sert a l'episode suivant) : chacune a sa
    // propre signature et est convertie selon son propre etat. Les versions precedentes ne convertissaient que la
    // premiere : une sauvegarde deja marquee peut donc encore avoir des parties d'origine, qui sont completees ici.
    static ConvertResult ConvertSav(string src, string dst, Target target)
    {
        SavFile cur = SavFile.Load(src);
        List<SavFile> parts = new List<SavFile>(cur.AllParts());
        List<State> states = new List<State>();
        foreach (SavFile x in parts) states.Add(RouteState(x.Get(x.RoutesIndex).Data));
        bool allDone = true, anyMarked = false;
        foreach (State st in states)
        {
            if (st.Marked && st.Unknown) return Result(Outcome.AlreadyConverted, L.T("Fichier modifié par une autre conversion (signature inconnue), ignoré.", "File changed by another conversion (unknown signature), skipped."));
            if (!st.Marked || !st.Res.SameAs(target)) allDone = false;
            if (st.Marked) anyMarked = true;
        }
        if (allDone) return Result(Outcome.AlreadyConverted, L.T("Déjà converti en " + NameOf(target) + ", ignoré.", "Already converted to " + NameOf(target) + ", skipped."));

        // De preference depuis l'original (.bak) : meme nombre de parties, toutes d'origine
        string read = src, note = "";
        string bak = src + ".bak";
        if (anyMarked && File.Exists(bak))
        {
            try
            {
                SavFile b = SavFile.Load(bak);
                List<SavFile> bp = new List<SavFile>(b.AllParts());
                bool clean = bp.Count == parts.Count;
                foreach (SavFile x in bp) if (RouteState(x.Get(x.RoutesIndex).Data).Marked) clean = false;
                if (clean)
                {
                    read = bak; cur = b; parts = bp;
                    states.Clear(); foreach (SavFile x in parts) states.Add(new State());
                    note = L.T("depuis l'original (.bak) ; ", "from the original (.bak); ");
                }
            }
            catch (InvalidDataException) { }
        }
        if (!Same(cur.Build(), cur.Bytes)) throw new InvalidDataException(L.T("format non reconnu (la relecture ne redonne pas le fichier d'origine)", "unknown format (reading it back does not give the original file)"));

        int elements = 0, points = 0, done = 0, repaired = 0;
        bool[] changed = new bool[parts.Count];
        for (int k = 0; k < parts.Count; k++)
        {
            SavFile x = parts[k]; State st = states[k];
            Block e = x.Get(x.ElementsIndex), t = x.Get(x.RoutesIndex);
            Xf factor;
            bool repairRoutes = false;
            if (st.Marked)
            {
                if (st.Res.SameAs(target)) continue;
                factor = Xf.Between(st.Res, target);
            }
            else
            {
                if (OutsideOldMap(e))
                {
                    // Sauvegarde faite apres un changement d'episode avec une version precedente : cites agrandies
                    // (reprises de la partie convertie) mais routes d'origine. On n'agrandit que les routes, si les
                    // cites correspondent bien a la resolution choisie.
                    if (RoutesInsideOldMap(t) && ElementsFit(e, target)) { repairRoutes = true; repaired++; }
                    else if (k == 0) return Result(Outcome.AlreadyConverted, L.T("Cités déjà hors de l'ancienne carte : sauvegarde sans doute déjà convertie, ignorée.", "Cities already outside the old map: save probably already converted, skipped."));
                    else continue;   // partie suivante deja agrandie par un autre moyen : laissee telle quelle
                }
                if (!RouteMarkEmpty(t.Data)) throw new InvalidDataException(L.T("zone de signature inattendue (non vide)", "unexpected signature area (not empty)"));
                factor = Xf.Between(null, target);
            }
            int ee, pp;
            if (repairRoutes) { Block none = new Block(); none.Data = new byte[0]; ScaleBlocks(none, t, factor, out ee, out pp); }
            else ScaleBlocks(e, t, factor, out ee, out pp);
            WriteRouteMark(t.Data, target);
            elements += ee; points += pp; done++; changed[k] = true;
        }
        if (done == 0) return Result(Outcome.AlreadyConverted, L.T("Déjà converti en " + NameOf(target) + ", ignoré.", "Already converted to " + NameOf(target) + ", skipped."));
        byte[] output = cur.Build();

        // Verification : memes parties, en-tetes, autres blocs et octets entre/apres les parties identiques ; signatures presentes.
        string tmp = dst + ".tmp";
        File.WriteAllBytes(tmp, output);
        List<SavFile> p0 = new List<SavFile>(SavFile.Load(read).AllParts()), p2 = new List<SavFile>(SavFile.Load(tmp).AllParts());
        if (p0.Count != p2.Count) throw new Exception(L.T("vérification : nombre de parties différent", "check: different number of parts"));
        for (int k = 0; k < p0.Count; k++)
        {
            SavFile a0 = p0[k], a2 = p2[k];
            if (!Same(a0.Header, a2.Header)) throw new Exception(L.T("vérification : en-tête modifié", "check: header changed"));
            foreach (Block b in a0.Blocks)
            {
                if (b.Index == a0.ElementsIndex || b.Index == a0.RoutesIndex) continue;
                if (!Same(b.Data, a2.Get(b.Index).Data)) throw new Exception(L.T("vérification : bloc " + b.Index + " modifié par erreur", "check: block " + b.Index + " changed by mistake"));
            }
            int end0 = k + 1 < p0.Count ? p0[k + 1].Base : a0.Bytes.Length, end2 = k + 1 < p2.Count ? p2[k + 1].Base : a2.Bytes.Length;
            if (end0 - a0.TailStart != end2 - a2.TailStart) throw new Exception(L.T("vérification : fin de fichier modifiée", "check: end of file changed"));
            for (int j = 0; j < end0 - a0.TailStart; j++) if (a0.Bytes[a0.TailStart + j] != a2.Bytes[a2.TailStart + j]) throw new Exception(L.T("vérification : fin de fichier modifiée", "check: end of file changed"));
            State st2 = RouteState(a2.Get(a2.RoutesIndex).Data);
            if (changed[k] && (!st2.Marked || st2.Unknown || !st2.Res.SameAs(target))) throw new Exception(L.T("vérification : signature absente", "check: signature missing"));
        }

        Finish(src, dst, tmp);
        string partsNote = parts.Count > 1 ? string.Format(L.T(" ({0} parties sur {1})", " ({0} of {1} parts)"), done, parts.Count) : "";
        if (repaired > 0) note += L.T("réparation (cités déjà agrandies, routes d'origine) ; ", "repair (cities already enlarged, routes not); ");
        return Result(elements == 0 && points == 0 ? Outcome.NoWorldMap : Outcome.Converted,
            note + string.Format(L.T("sauvegarde {0} : {1} éléments et {2} points de routes", "save {0}: {1} elements and {2} route points"), NameOf(target), elements, points) + partsNote + ".");
    }

    // Tous les points de routes dans l'ancienne carte (838 x 738), et au moins un point
    static bool RoutesInsideOldMap(Block t)
    {
        int n = 0;
        for (int r = 0; r < t.Count; r++)
        {
            int o = r * 324, c = t.Data[o + 321];
            if (c > 50) return false;
            for (int q = 0; q < c; q++)
            {
                int x = BitConverter.ToUInt16(t.Data, o + 8 + 6 * q), y = BitConverter.ToUInt16(t.Data, o + 10 + 6 * q);
                if ((x > 838 && x < 0x8000) || (y > 738 && y < 0x8000)) return false;
                n++;
            }
        }
        return n > 0;
    }

    // Les cites ont-elles ete agrandies pour la resolution choisie ? Au-dela de la carte 1080p : 1440p ; sinon 1080p.
    static bool ElementsFit(Block e, Target target)
    {
        bool beyond1080 = false;
        double s1080 = ScaleOf(Target.R1080), s = ScaleOf(target);
        for (int i = 0; i < e.Count; i++)
        {
            int o = i * e.ElemSize;
            if (e.Data[o] == 0) continue;
            int x = BitConverter.ToUInt16(e.Data, o + 4), y = BitConverter.ToUInt16(e.Data, o + 6);
            if ((x < 0x8000 && (x > target.Dx + 838 * s + 2 || x < target.Dx - 2)) || (y < 0x8000 && y > 738 * s + 2)) return false;
            if ((x < 0x8000 && x > 838 * s1080 + 2) || (y < 0x8000 && y > 738 * s1080 + 2)) beyond1080 = true;
        }
        if (!target.Legacy) return true;   // autre geometrie : cites dans sa carte
        return target.SameAs(Target.R1440) == beyond1080;
    }

    // Une cite au-dela de l'ancienne carte (838 x 738) : partie deja agrandie
    static bool OutsideOldMap(Block e)
    {
        for (int i = 0; i < e.Count; i++)
        {
            int o = i * e.ElemSize;
            if (e.Data[o] == 0) continue;
            int x = BitConverter.ToUInt16(e.Data, o + 4), y = BitConverter.ToUInt16(e.Data, o + 6);
            if ((x > 838 && x < 0x8000) || (y > 738 && y < 0x8000)) return true;   // >= 0x8000 : marqueur, pas une position
        }
        return false;
    }

    // Carte de l'editeur d'aventures (.map) : une seule structure MAPS, avec les blocs mappemonde (17) et routes (18).
    static ConvertResult DoMap(string read, string src, string dst, Target target, Xf factor, bool alreadyMarked)
    {
        MapFile m = LoadMap(read);
        byte[] original = File.ReadAllBytes(read);
        Block e = m.Get(17), t = m.Get(18);
        if (e == null || t == null) return Result(Outcome.NoWorldMap, L.T("Pas de mappemonde dans cette carte, ignorée.", "No world map in this map, skipped."));
        if (!alreadyMarked)
        {
            for (int i = 0; i < e.Count; i++)
            {
                int o = i * e.ElemSize;
                if (e.Data[o] == 0) continue;
                ushort x = BitConverter.ToUInt16(e.Data, o + 4), y = BitConverter.ToUInt16(e.Data, o + 6);
                if ((x < 0x8000 && x > 838) || (y < 0x8000 && y > 738))
                    return Result(Outcome.AlreadyConverted, L.T("Cités déjà hors de l'ancienne carte : carte sans doute déjà convertie, ignorée.", "Cities already outside the old map: map probably already converted, skipped."));
            }
            if (!RouteMarkEmpty(t.Data)) throw new InvalidDataException(L.T("zone de signature inattendue (non vide)", "unexpected signature area (not empty)"));
        }
        if (!Same(m.Build(), original)) throw new InvalidDataException(L.T("format non reconnu (la relecture ne redonne pas le fichier d'origine)", "unknown format (reading it back does not give the original file)"));

        int elements, points;
        ScaleBlocks(e, t, factor, out elements, out points);
        WriteRouteMark(t.Data, target);
        byte[] output = m.Build();

        // Verification : en-tete et autres blocs identiques ; signature presente.
        string tmp = dst + ".tmp";
        File.WriteAllBytes(tmp, output);
        MapFile m0 = LoadMap(read), m2 = LoadMap(tmp);
        if (!Same(m0.Header, m2.Header)) throw new Exception(L.T("vérification : en-tête modifié", "check: header changed"));
        foreach (Block b in m0.Blocks)
        {
            if (b.Index == 17 || b.Index == 18) continue;
            if (!Same(b.Data, m2.Get(b.Index).Data)) throw new Exception(L.T("vérification : bloc " + b.Index + " modifié par erreur", "check: block " + b.Index + " changed by mistake"));
        }
        State st = MapState(m2);
        if (!st.Marked || st.Unknown || !st.Res.SameAs(target)) throw new Exception(L.T("vérification : signature absente", "check: signature missing"));

        Finish(src, dst, tmp);
        return Result(elements == 0 && points == 0 ? Outcome.NoWorldMap : Outcome.Converted,
            string.Format(L.T("carte de l'éditeur {0} : {1} éléments et {2} points de routes.", "editor map {0}: {1} elements and {2} route points."), NameOf(target), elements, points));
    }

    // Remet le .bak a la place du fichier converti ; renvoie vrai si un original a ete remis.
    public static bool Restore(string file, out string message)
    {
        string bak = file + ".bak";
        message = L.T("Aucune sauvegarde .bak trouvée.", "No .bak backup found.");
        if (!File.Exists(bak)) return false;
        File.Copy(bak, file, true);
        File.Delete(bak);
        message = L.T("Original restauré.", "Original restored.");
        return true;
    }
}
