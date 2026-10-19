// Zeus.exe du jeu : compilation, langue, resolution et etat de l'editeur de scenarios ; patch de l'editeur pour la
// mappemonde choisie (meme correction que celle validee en jeu sur l'exe francais, retrouvee dans l'exe anglais 2.1.4.0).
// Une copie « Zeus.exe.bak » de l'original est faite avant la premiere modification.
using System;
using System.Collections.Generic;
using System.IO;

public class ExeState
{
    public string Path, Build;        // Build : « anglais 2.1.4.0 », « français » ou null (non reconnu)
    public int[] Resolution;          // largeur, hauteur ecrites dans l'exe, ou null
    public bool EditorPatched;        // saut de l'editeur en place
    public int[] EditorLimits;        // limites actuelles (gauche, droite, haut, bas) si patche
}

public static class EditorCore
{
    public static ExeState Read(string path)
    {
        ExeState s = new ExeState { Path = path };
        byte[] b = File.ReadAllBytes(path);
        s.Build = EditorPatch.Recognised(b);
        if (s.Build == "anglais 2.1.4.0")
        {
            int o = 0x10BA28;   // exe anglais reconnu seulement avec sa resolution a sa place (les exe russes : non)
            if (o + 10 > b.Length || b[o] != 0xBE || b[o + 5] != 0xB9) s.Build = null;
        }
        if (s.Build == null) return s;
        s.Resolution = ExeInfo.Resolution(b);
        List<Site> sites = EditorPatch.Build(b, null);
        s.EditorPatched = sites[0].StateIn(b) == SiteState.Patched;
        if (s.EditorPatched)
        {
            s.EditorLimits = new int[4];
            for (int i = 0; i < 4; i++) s.EditorLimits[i] = Math.Abs(BitConverter.ToInt32(b, sites[2 + i].Offset));
        }
        return s;
    }

    public static bool English(ExeState s) { return s.Build == "anglais 2.1.4.0"; }

    // L'editeur est-il patche pour cette mappemonde ?
    public static bool EditorFits(ExeState s, int w, int h, int layout)
    {
        if (!s.EditorPatched) return false;
        int[] l = EditorPatch.Limits(w, h, (MapLayout)layout);
        for (int i = 0; i < 4; i++) if (l[i] != s.EditorLimits[i]) return false;
        return true;
    }

    // Patch de l'editeur ; renvoie un message (ok = true si reussi ou deja fait).
    public static string PatchEditor(string path, int w, int h, int layout, out bool ok)
    {
        ok = false;
        byte[] exe = File.ReadAllBytes(path);
        List<Site> sites = EditorPatch.Build(exe, EditorPatch.Limits(w, h, (MapLayout)layout));
        if (sites == null) return L.T("Exe non reconnu : éditeur non patché.", "Exe not recognised: editor not patched.");
        Survey sv = Survey.Of(sites, exe);
        if (!sv.Patchable) return L.T("Éditeur déjà modifié par un autre patch : non patché.", "Editor already changed by another patch: not patched.");
        if (sv.Original == 0) { ok = true; return L.T("Éditeur déjà patché pour cette mappemonde.", "Editor already patched for this world map."); }
        byte[] outb = (byte[])exe.Clone();
        foreach (Site x in sites) Array.Copy(x.Patched, 0, outb, x.Offset, x.Patched.Length);
        string bak = path + ".bak", tmp = path + ".tmp";
        try
        {
            if (!File.Exists(bak)) File.Copy(path, bak);
            FileAttributes a = File.GetAttributes(path);
            if ((a & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, a & ~FileAttributes.ReadOnly);
            File.WriteAllBytes(tmp, outb);
            File.Copy(tmp, path, true);
            File.Delete(tmp);
        }
        catch (Exception e)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return L.T("Impossible d'écrire l'exe (jeu ouvert ? dossier protégé ?) : ", "Could not write the exe (game running? protected folder?): ") + e.Message;
        }
        ok = true;
        return L.T("Éditeur patché pour cette mappemonde (original gardé en Zeus.exe.bak).", "Editor patched for this world map (original kept as Zeus.exe.bak).");
    }
}
