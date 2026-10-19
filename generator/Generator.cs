// Generateur de mappemondes : refait les 14 mappemondes du jeu (DATA) pour la taille de la vue de n'importe quel exe,
// a partir des cartes d'origine 838x738 du jeu de l'utilisateur (rien n'est fourni). Les originales sont gardees dans
// « <jeu>\Mappemondes d'origine » et peuvent etre remises en place.
// En ligne de commande : World Map Generator.exe --jeu <dossier> (--resolution 2560x1440 | --taille 2338x1410)
//                        [--fond panneau|eau|image] [--image <fichier>]
//                        World Map Generator.exe --jeu <dossier> --restaurer
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("World Map Generator")]
[assembly: System.Reflection.AssemblyDescription("Mappemondes de Zeus : Le Maitre de l'Olympe a la taille de n'importe quelle resolution")]
[assembly: System.Reflection.AssemblyCompany("Outils Zeus (projet amateur)")]
[assembly: System.Reflection.AssemblyProduct("Zeus - World Maps Tools")]
[assembly: System.Reflection.AssemblyCopyright("© 2026 Vicinity4813 - MIT License")]
[assembly: System.Resources.NeutralResourcesLanguage("fr-FR")]
[assembly: System.Runtime.InteropServices.ComVisible(false)]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.0")]

public static class Gen
{
    public const string BackupDir = "Mappemondes d'origine";
    // Les 14 mappemondes : nom, titre et sous-titre du cartouche, portrait (ecran de chargement)
    static readonly string[][] Maps = {
        new[] { "Zeus_MapOfGreece01", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" }, new[] { "Zeus_MapOfGreece02", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" },
        new[] { "Zeus_MapOfGreece03", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" }, new[] { "Zeus_MapOfGreece04", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" },
        new[] { "Zeus_MapOfGreece05", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" }, new[] { "Zeus_MapOfGreece06", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" },
        new[] { "Zeus_MapOfGreece07", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" }, new[] { "Zeus_MapOfGreece08", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" },
        new[] { "Zeus_MapOfGreece09", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" }, new[] { "Zeus_MapOfGreece10", "Olympus", "ΟΛΥΜΠΟΣ", "Zeus_Load1" },
        new[] { "Poseidon_map01", "Atlantis", "ΑΤΛΑΝΤΙΣ", "Poseidon_Load1" }, new[] { "Poseidon_map02", "Atlantis", "ΑΤΛΑΝΤΙΣ", "Poseidon_Load1" },
        new[] { "Poseidon_map03", "Atlantis", "ΑΤΛΑΝΤΙΣ", "Poseidon_Load1" }, new[] { "Poseidon_map04", "Atlantis", "ΑΤΛΑΝΤΙΣ", "Poseidon_Load1" },
    };

    // Taille de la vue (et donc de la mappemonde) pour une resolution : cases de 60 px moins 2 en largeur
    // (le panneau lateral prend le reste) ; rangees de 15 px sous la barre du haut (30 px) en hauteur.
    public static int[] ViewOf(int w, int h) { return new[] { 60 * ((w - 180) / 60) - 2, ((h - 30) / 15) * 15 }; }

    static string FindImage(string dir, string name)
    {
        if (!Directory.Exists(dir)) return null;
        foreach (string f in Directory.GetFiles(dir, name + ".*"))
            if (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) return f;
        return null;
    }

    static int[] SizeOf(string path) { using (Image im = Image.FromFile(path)) return new[] { im.Width, im.Height }; }

    public static bool Generate(string game, int w, int h, int layout, int fill, string image, bool frieze, Action<string, bool> log)
    {
        if (fill == 2 && (image == null || !File.Exists(image))) { log(L.T("Choisissez l'image personnalisée.", "Choose the custom picture."), true); return false; }
        Mappe.Fill = fill; Mappe.CustomPath = fill == 2 ? image : null; Mappe.ShowFrieze = frieze;
        string data = Path.Combine(game, "DATA"), bak = Path.Combine(game, BackupDir);
        if (!Directory.Exists(data)) { log(L.T("Dossier DATA introuvable : choisissez le dossier du jeu (celui qui contient Zeus.exe).", "DATA folder not found: choose the game folder (the one with Zeus.exe)."), true); return false; }
        if (w < 838 || h < 600) { log(L.T("Taille trop petite.", "Size too small."), true); return false; }
        layout = 1;   // carte toujours au centre : la Grece entre l'Italie et l'Anatolie (meme position pour le convertisseur)
        Mappe.Setup(w, h, layout);
        string where = layout == 0 ? L.T("à gauche", "on the left") : layout == 2 ? L.T("à droite", "on the right") : L.T("au centre", "centred");
        log(string.Format(L.T("Mappemondes de {0} x {1} px ; carte agrandie {2:F3} fois ({3} x {4} px), {5}.", "{0} x {1} px world maps; map enlarged {2:F3} times ({3} x {4} px), {5}."), w, h, Mappe.S, Mappe.MW, Mappe.MH, where), false);

        // Originales : copiees dans la sauvegarde la premiere fois ; ensuite on repart toujours de la sauvegarde
        List<string[]> todo = new List<string[]>();
        foreach (string[] m in Maps)
        {
            string cur = FindImage(data, m[0]), saved = FindImage(bak, m[0]);
            if (saved == null && cur != null)
            {
                int[] sz = SizeOf(cur);
                if (sz[0] == 838 && sz[1] == 738)
                {
                    Directory.CreateDirectory(bak);
                    saved = Path.Combine(bak, Path.GetFileName(cur));
                    File.Copy(cur, saved);
                }
            }
            if (saved == null)
            {
                log(string.Format(L.T("{0} : carte d'origine (838 x 738) introuvable, ni dans DATA ni dans « {1} ».", "{0}: original map (838 x 738) not found, neither in DATA nor in \"{1}\"."), m[0], BackupDir), true);
                continue;
            }
            todo.Add(new[] { saved, cur ?? Path.Combine(data, Path.GetFileName(saved)), m[1], m[2], m[3] });
        }
        if (todo.Count == 0) return false;

        // Mappemondes de la Grece : recadrages de la reference 3440 (Grece au centre, Italie et Anatolie autour),
        // faite a partir de la carte sans dessin et de Poseidon_map04 ; sans Poseidon, eau texturee autour.
        string clean = FindImage(bak, "Zeus_MapOfGreece08") ?? FindImage(bak, "Zeus_MapOfGreece09") ?? FindImage(bak, "Zeus_MapOfGreece10");
        string pos04 = FindImage(bak, "Poseidon_map04");
        Bitmap reference = null;
        if (clean != null && pos04 != null)
        {
            // reference a 1410 px de haut (vue d'un exe 3440 x 1440), ou plus haute / plus large si la vue l'exige
            int rh = Math.Max(Voisins.RefH, h);
            double k = Math.Min(h / 738.0, w / 838.0) / (rh / 738.0);
            int rw = Math.Max((int)Math.Round(Voisins.RefW * rh / (double)Voisins.RefH), (int)Math.Ceiling(w / k) + 2);
            log(string.Format(L.T("Référence de la Grèce : {0} x {1} px, Italie et Anatolie autour (d'après Poseidon_map04)…", "Greece reference: {0} x {1} px, Italy and Anatolia around it (from Poseidon_map04)…"), rw, rh), false);
            reference = Voisins.Reference(clean, pos04, rw, rh);
            Mappe.Setup(w, h, layout);
        }
        else if (clean != null)
            log(L.T("Poseidon_map04 introuvable : la Grèce sera entourée d'eau texturée.", "Poseidon_map04 not found: Greece will be surrounded by textured water."), false);

        // Mappemondes de Poseidon : reference « 1440p » de chaque carte (vraies voisines et vraies cotes autour)
        int[] prs = Poseidon.RefSize(w, h);
        bool posLogged = false;

        int done = 0;
        foreach (string[] t in todo)
        {
            string god = FindImage(data, t[4]);
            string name = Path.GetFileNameWithoutExtension(t[0]);
            bool greece = name.StartsWith("Zeus_MapOfGreece", StringComparison.OrdinalIgnoreCase);
            Bitmap posRef = null;
            if (name.StartsWith("Poseidon_map", StringComparison.OrdinalIgnoreCase))
            {
                if (!posLogged) { log(string.Format(L.T("Références de Poséidon : {0} x {1} px, cartes voisines et vraies côtes autour…", "Poseidon references: {0} x {1} px, neighbouring maps and real coastlines around…"), prs[0], prs[1]), false); posLogged = true; }
                posRef = Poseidon.Reference(name, n => FindImage(bak, n), prs[0], prs[1]);
                if (posRef == null) log(string.Format(L.T("  {0} : cartes d'origine inattendues, carte entourée selon le choix « à côté de la carte ».", "  {0}: unexpected original maps, map surrounded as chosen \"beside the map\"."), name), false);
            }
            Mappe.Setup(w, h, layout);
            int keepFill = Mappe.Fill; bool keepFrieze = Mappe.ShowFrieze;
            if (greece && reference == null) { Mappe.Fill = 1; Mappe.ShowFrieze = false; }
            using (Bitmap b = greece && reference != null ? Voisins.Crop(reference, w, h)
                            : posRef != null ? Poseidon.Fit(posRef, w, h, t[2], t[3], god)
                            : Mappe.Compose(greece && clean != null ? clean : t[0], t[2], t[3], god))
            {
                if (posRef != null) posRef.Dispose();
                Mappe.Setup(w, h, layout);
                Mappe.Fill = keepFill; Mappe.ShowFrieze = keepFrieze;
                // dessin du coin bas-gauche (Greece01-07) : deplace dans le coin bas-gauche de la mappemonde agrandie
                if (greece && clean != null) Voisins.MoveArt(b, t[0], clean);
                string tmp = t[1] + ".tmp";
                Mappe.SaveJpg(b, tmp, 92);
                Writable(t[1]);
                if (File.Exists(t[1])) File.Delete(t[1]);
                File.Move(tmp, t[1]);
            }
            done++;
            log("  " + Path.GetFileName(t[1]), false);
        }
        if (reference != null) reference.Dispose();
        log(string.Format(L.T("{0} mappemondes faites. Originales gardées dans « {1} ».", "{0} world maps made. Originals kept in \"{1}\"."), done, BackupDir), false);
        log(L.T("Convertissez maintenant les scénarios et sauvegardes avec le convertisseur, à la même taille.", "Now convert scenarios and saves with the converter, at the same size."), false);
        return done == todo.Count;
    }

    public static bool Restore(string game, Action<string, bool> log)
    {
        string data = Path.Combine(game, "DATA"), bak = Path.Combine(game, BackupDir);
        if (!Directory.Exists(bak)) { log(L.T("Pas de mappemondes d'origine sauvegardées dans ce jeu.", "No original world maps saved in this game."), true); return false; }
        int n = 0;
        foreach (string f in Directory.GetFiles(bak))
        {
            string dst = FindImage(data, Path.GetFileNameWithoutExtension(f)) ?? Path.Combine(data, Path.GetFileName(f));
            Writable(dst);
            File.Copy(f, dst, true);
            n++;
        }
        log(string.Format(L.T("{0} mappemondes d'origine remises en place.", "{0} original world maps put back."), n), false);
        return true;
    }

    static void Writable(string path)
    {
        if (File.Exists(path)) { FileAttributes a = File.GetAttributes(path); if ((a & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, a & ~FileAttributes.ReadOnly); }
    }
}

static class Program
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

    [STAThread]
    static int Main(string[] args)
    {
        L.Load();
        if (args.Length > 0) { AttachConsole(-1); return Cli(args); }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GenForm());
        return 0;
    }

    static int[] Pair(string s) { string[] p = s.ToLowerInvariant().Split('x'); return new[] { int.Parse(p[0]), int.Parse(p[1]) }; }

    static int Cli(string[] a)
    {
        string game = null, image = null; int[] size = null; int layout = 0, fill = 0; bool restore = false, frieze = true;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == "--jeu" && i + 1 < a.Length) game = a[++i];
            else if (a[i] == "--resolution" && i + 1 < a.Length) { int[] r = Pair(a[++i]); size = Gen.ViewOf(r[0], r[1]); }
            else if (a[i] == "--taille" && i + 1 < a.Length) size = Pair(a[++i]);
            else if (a[i] == "--fond" && i + 1 < a.Length) { string f = a[++i]; fill = f.StartsWith("e") || f.StartsWith("w") ? 1 : f.StartsWith("i") ? 2 : 0; }
            else if (a[i] == "--image" && i + 1 < a.Length) { image = a[++i]; fill = 2; }
            else if (a[i] == "--sans-frise") frieze = false;
            else if (a[i] == "--restaurer") restore = true;
        }
        if (game == null || (size == null && !restore)) { Console.WriteLine("World Map Generator.exe --jeu <dossier> (--resolution LxH | --taille LxH) [--fond panneau|eau|image --image <fichier>] [--sans-frise] | --restaurer"); return 2; }
        bool err = false;
        Action<string, bool> log = (m, e) => { Console.WriteLine((e ? "ERREUR " : "") + m); if (e) err = true; };
        bool ok = restore ? Gen.Restore(game, log) : Gen.Generate(game, size[0], size[1], layout, fill, image, frieze, log);
        return ok && !err ? 0 : 1;
    }
}

class GenForm : Form
{
    ComboBox lang;
    Label title, intro, lGame, lRes, lX, lView, lPos;
    TextBox tGame;
    NumericUpDown nW, nH;
    Button bGame, bExe, bGen, bRestore;
    RadioButton rLeft, rCenter, rRight, rPanel, rSea, rCustom;
    GroupBox gFill;
    CheckBox chkFrieze;
    Button bImg;
    Label lImg;
    string image;
    RichTextBox log;
    bool busy;

    public GenForm()
    {
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 690);
        MinimumSize = new Size(640, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(244, 238, 226);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        title = new Label { Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Color.FromArgb(90, 50, 20), AutoSize = true, Location = new Point(16, 12) };
        lang = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        lang.Items.AddRange(new object[] { "Français", "English" });
        lang.SelectedIndex = L.En ? 1 : 0;
        lang.Location = new Point(ClientSize.Width - lang.Width - 16, 14);
        lang.SelectedIndexChanged += delegate { L.En = lang.SelectedIndex == 1; L.Save(); ApplyLanguage(); };
        intro = new Label { Location = new Point(16, 52), Size = new Size(728, 64), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        lGame = new Label { Location = new Point(16, 122), AutoSize = true };
        tGame = new TextBox { Location = new Point(16, 142), Width = 620, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        bGame = new Button { Location = new Point(644, 140), Size = new Size(100, 27), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        lRes = new Label { Location = new Point(16, 180), AutoSize = true };
        nW = new NumericUpDown { Location = new Point(16, 202), Width = 90, Minimum = 800, Maximum = 7680, Value = 2560 };
        lX = new Label { Location = new Point(110, 205), AutoSize = true, Text = "x" };
        nH = new NumericUpDown { Location = new Point(126, 202), Width = 90, Minimum = 600, Maximum = 4320, Value = 1440 };
        lView = new Label { Location = new Point(232, 205), AutoSize = true, ForeColor = Color.FromArgb(90, 50, 20) };
        bExe = new Button { Location = new Point(560, 199), Size = new Size(184, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        lPos = new Label { Location = new Point(16, 240), AutoSize = true };
        rLeft = new RadioButton { Location = new Point(24, 262), AutoSize = true, Visible = false };
        rCenter = new RadioButton { Location = new Point(24, 262), AutoSize = true, Checked = true, Visible = false };
        rRight = new RadioButton { Location = new Point(450, 262), AutoSize = true, Visible = false };
        gFill = new GroupBox { Location = new Point(16, 290), Size = new Size(728, 84), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        rPanel = new RadioButton { Location = new Point(12, 24), AutoSize = true, Checked = true };
        rSea = new RadioButton { Location = new Point(250, 24), AutoSize = true };
        rCustom = new RadioButton { Location = new Point(400, 24), AutoSize = true };
        bImg = new Button { Location = new Point(560, 20), Size = new Size(156, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        chkFrieze = new CheckBox { Location = new Point(12, 54), AutoSize = true, Checked = true };
        gFill.Controls.AddRange(new Control[] { rPanel, rSea, rCustom, bImg, chkFrieze });
        lImg = new Label { Location = new Point(16, 376), Size = new Size(728, 18), ForeColor = Color.DimGray, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        bImg.Click += delegate
        {
            using (OpenFileDialog d = new OpenFileDialog { Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp" })
                if (d.ShowDialog(this) == DialogResult.OK) { image = d.FileName; rCustom.Checked = true; lImg.Text = image; }
        };
        bGen = new Button { Location = new Point(16, 398), Size = new Size(260, 34), Font = new Font(Font, FontStyle.Bold) };
        bRestore = new Button { Location = new Point(286, 398), Size = new Size(260, 34) };
        log = new RichTextBox { Location = new Point(16, 444), Size = new Size(728, 230), ReadOnly = true, BackColor = Color.White, Font = new Font("Consolas", 9f),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

        nW.ValueChanged += delegate { ShowView(); };
        nH.ValueChanged += delegate { ShowView(); };
        bGame.Click += delegate
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = L.T("Dossier du jeu (celui qui contient Zeus.exe et DATA)", "Game folder (the one with Zeus.exe and DATA)");
                if (d.ShowDialog(this) == DialogResult.OK) tGame.Text = d.SelectedPath;
            }
        };
        bExe.Click += delegate
        {
            using (OpenFileDialog d = new OpenFileDialog { Filter = "Zeus.exe|*.exe" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                tGame.Text = Path.GetDirectoryName(d.FileName);
                int[] r = ExeInfo.Resolution(File.ReadAllBytes(d.FileName));
                if (r == null) { MessageBox.Show(this, L.T("Résolution introuvable dans cet exe : indiquez-la à la main.", "Resolution not found in this exe: enter it by hand."), Text); return; }
                nW.Value = Math.Max(nW.Minimum, Math.Min(nW.Maximum, r[0])); nH.Value = Math.Max(nH.Minimum, Math.Min(nH.Maximum, r[1]));
            }
        };
        bGen.Click += delegate
        {
            int[] v = Gen.ViewOf((int)nW.Value, (int)nH.Value);
            int layout = rCenter.Checked ? 1 : rRight.Checked ? 2 : 0, fill = rSea.Checked ? 1 : rCustom.Checked ? 2 : 0;
            string img = image; bool fr = chkFrieze.Checked;
            Run(lg => Gen.Generate(tGame.Text.Trim(), v[0], v[1], layout, fill, img, fr, lg));
        };
        bRestore.Click += delegate { Run(lg => Gen.Restore(tGame.Text.Trim(), lg)); };

        Controls.AddRange(new Control[] { title, lang, intro, lGame, tGame, bGame, lRes, nW, lX, nH, lView, bExe, lPos, rLeft, rCenter, rRight, gFill, lImg, bGen, bRestore, log });
        ApplyLanguage();
    }

    void ShowView()
    {
        int[] v = Gen.ViewOf((int)nW.Value, (int)nH.Value);
        lView.Text = string.Format(L.T("→ vue et mappemondes de {0} x {1} px", "→ view and world maps of {0} x {1} px"), v[0], v[1]);
    }

    void ApplyLanguage()
    {
        Text = "World Map Generator — Zeus - World Maps Tools";
        title.Text = L.T("Mappemondes pour n'importe quelle résolution", "World maps for any resolution");
        intro.Text = L.T("Refait les 14 mappemondes du jeu à la taille de la vue de votre exe, à partir des cartes d'origine de VOTRE jeu (rien n'est fourni). Les originales sont gardées et peuvent être remises. Ensuite, convertissez scénarios et sauvegardes avec le convertisseur, qui patche aussi l'éditeur de scénarios.",
                         "Remakes the game's 14 world maps at the view size of your exe, from the original maps of YOUR game (nothing is provided). The originals are kept and can be put back. Then convert scenarios and saves with the converter, which also patches the scenario editor.");
        lGame.Text = L.T("Dossier du jeu :", "Game folder:");
        bGame.Text = L.T("Parcourir…", "Browse…");
        lRes.Text = L.T("Résolution de l'exe (largeur x hauteur) :", "Exe resolution (width x height):");
        lPos.Text = L.T("Carte au centre : la Grèce entre l'Italie et l'Anatolie, les dessins des coins déplacés dans le coin bas-gauche.", "Map centred: Greece between Italy and Anatolia, the corner drawings moved to the bottom-left corner.");
        rLeft.Text = L.T("À gauche (comme l'originale)", "Left (like the original)");
        rCenter.Text = L.T("Au centre", "Centred");
        rRight.Text = L.T("À droite", "Right");
        gFill.Text = L.T("Mappemondes de Poséidon, écrans plus larges que le 1440p : de chaque côté", "Poseidon world maps, screens wider than 1440p: on each side");
        rPanel.Text = L.T("Panneau Olympus / Atlantis", "Olympus / Atlantis panel");
        rSea.Text = L.T("Eau texturée", "Textured water");
        rCustom.Text = L.T("Image personnalisée", "Custom picture");
        bImg.Text = L.T("Choisir l'image…", "Choose the picture…");
        chkFrieze.Text = L.T("Frise grecque entre la carte et le fond (eau texturée, image personnalisée)", "Greek key border between the map and the background (textured water, custom picture)");
        bGen.Text = L.T("Faire les mappemondes", "Make the world maps");
        bRestore.Text = L.T("Remettre les mappemondes d'origine", "Put the original world maps back");
        bExe.Text = L.T("D'après un Zeus.exe…", "From a Zeus.exe…");
        ShowView();
    }

    void Run(Func<Action<string, bool>, bool> work)
    {
        if (busy) return;
        busy = true;
        log.Clear();
        foreach (Control c in new Control[] { bGen, bRestore, bGame, bExe, gFill }) c.Enabled = false;
        Action<string, bool> lg = (m, e) => BeginInvoke((Action)(() =>
        {
            log.SelectionStart = log.TextLength;
            log.SelectionColor = e ? Color.FromArgb(180, 20, 20) : Color.Black;
            log.AppendText(m + Environment.NewLine);
            log.ScrollToCaret();
        }));
        Thread t = new Thread(() =>
        {
            try { work(lg); }
            catch (Exception ex) { lg(L.T("Erreur : ", "Error: ") + ex.Message, true); }
            BeginInvoke((Action)(() => { busy = false; foreach (Control c in new Control[] { bGen, bRestore, bGame, bExe, gFill }) c.Enabled = true; }));
        });
        t.IsBackground = true;
        t.Start();
    }
}
