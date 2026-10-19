// Convertisseur de scenarios Zeus / Poseidon pour les mappemondes grand ecran (1080p, 1440p ou n'importe quelle taille).
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("World Map Converter")]
[assembly: System.Reflection.AssemblyDescription("Adapte les scénarios (.pak, .set, .map) et les sauvegardes (.sav) de Zeus : Le Maître de l'Olympe / Poséidon aux mappemondes plein écran, quelle que soit leur taille. Aucune connexion réseau ; l'original est gardé en .bak.")]
[assembly: System.Reflection.AssemblyCompany("Zeus - World Maps Tools (fan project)")]
[assembly: System.Reflection.AssemblyProduct("Zeus - World Maps Tools")]
[assembly: System.Reflection.AssemblyCopyright("© 2026 Vicinity4813 - MIT License")]
[assembly: System.Resources.NeutralResourcesLanguage("fr-FR")]
[assembly: System.Runtime.InteropServices.ComVisible(false)]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.0")]

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        L.Load();
        // Mode ligne de commande : World Map Converter.exe [--resolution 1080|1440] [--sortie DOSSIER] fichier.pak ...
        if (args.Length > 0) return Cli(args);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }

    static int[] Pair(string s) { string[] p = s.ToLowerInvariant().Split('x'); return new[] { int.Parse(p[0]), int.Parse(p[1]) }; }

    // World Map Converter.exe [--resolution 1080|1440|LxH] [--taille LxH] [--exe Zeus.exe [--editeur]] [--position gauche|centre|droite] [--sortie DOSSIER] fichiers...
    static int Cli(string[] args)
    {
        string outDir = null, res = null, exe = null; int errors = 0, layout = 0; int[] size = null; bool editor = false;
        List<string> files = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--sortie" && i + 1 < args.Length) outDir = args[++i];
            else if (args[i] == "--resolution" && i + 1 < args.Length) res = args[++i];
            else if (args[i] == "--taille" && i + 1 < args.Length) size = Pair(args[++i]);
            else if (args[i] == "--exe" && i + 1 < args.Length) exe = args[++i];
            else if (args[i] == "--editeur") editor = true;
            else if (args[i] == "--position" && i + 1 < args.Length) { string p = args[++i]; layout = p.StartsWith("c") ? 1 : p.StartsWith("d") || p.StartsWith("r") ? 2 : 0; }
            else files.Add(args[i]);
        }
        Target target = Target.R1080;
        if (size != null) target = Target.ForMap(size[0], size[1], layout);
        else if (exe != null)
        {
            string how; int[] m = ExeInfo.MapSizeFor(exe, out how);
            if (m == null) { Console.WriteLine("ERREUR résolution introuvable dans " + exe); return 1; }
            Console.WriteLine("mappemonde " + m[0] + "x" + m[1] + " (" + how + ")");
            target = Target.ForMap(m[0], m[1], layout);
        }
        if (editor)
        {
            if (exe == null) { Console.WriteLine("ERREUR --editeur demande --exe"); return 1; }
            int[] m2 = size; string how2; if (m2 == null) m2 = ExeInfo.MapSizeFor(exe, out how2);
            bool ok; Console.WriteLine(EditorCore.PatchEditor(exe, m2[0], m2[1], layout, out ok)); if (!ok) errors++;
        }
        else if (res != null && res.Contains("x")) target = Target.ForResolution(Pair(res)[0], Pair(res)[1], layout);
        else if (res != null) target = res.StartsWith("1440") ? Target.R1440 : Target.R1080;
        foreach (string f in files)
        {
            string dst = outDir == null ? f : Path.Combine(outDir, Path.GetFileName(f));
            if (outDir != null) Directory.CreateDirectory(outDir);
            ConvertResult r = Converter.Convert(f, dst, target);
            Console.WriteLine(Path.GetFileName(f) + " : " + r.Message);
            if (r.Outcome == Outcome.Error) errors++;
        }
        return errors == 0 ? 0 : 1;
    }
}

class MainForm : Form
{
    ListView list;
    RadioButton rbReplace, rbCopy;
    ComboBox cboPreset, cboPos;
    NumericUpDown nW, nH;
    TextBox tExe;
    Button bExe;
    Label lblX, lblExe, lblEd;
    CheckBox chkSub, chkMap, chkEditor;
    // Chemin de chaque fichier relatif au dossier ajoute : recree les sous-dossiers dans le dossier de destination
    readonly Dictionary<string, string> relPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    TextBox outDir;
    Button bAddFiles, bAddFolder, bClear, bConvert, bRestore, bBrowse;
    Label help, note, lblLang;
    GroupBox gExe, gRes;
    ComboBox cboLang;
    BackgroundWorker worker;
    ExeState exe;   // Zeus.exe du jeu, choisi d'abord

    public MainForm()
    {
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        ClientSize = new Size(760, 720);
        MinimumSize = new Size(680, 620);
        Font = new Font("Segoe UI", 9.5f);
        AllowDrop = true;
        DragEnter += (s, e) => { if (exe != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        DragDrop += (s, e) => { if (exe != null) AddPaths((string[])e.Data.GetData(DataFormats.FileDrop)); };

        help = new Label { Location = new Point(12, 10), Size = new Size(560, 60), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        // Choix de la langue, en haut a droite
        lblLang = new Label { Location = new Point(580, 13), Size = new Size(68, 22), TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        cboLang = new ComboBox { Location = new Point(650, 10), Size = new Size(98, 24), DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        cboLang.Items.AddRange(new object[] { "Français", "English" });
        cboLang.SelectedIndex = L.En ? 1 : 0;
        cboLang.SelectedIndexChanged += (s, e) => { L.En = cboLang.SelectedIndex == 1; L.Save(); ApplyLanguage(); ShowExe(); };

        // 1. Zeus.exe du jeu : version, langue, resolution, etat de l'editeur
        gExe = new GroupBox { Location = new Point(12, 74), Size = new Size(736, 78), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        tExe = new TextBox { Location = new Point(12, 22), Size = new Size(560, 24), ReadOnly = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        bExe = new Button { Location = new Point(580, 20), Size = new Size(144, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        lblExe = new Label { Location = new Point(12, 52), Size = new Size(712, 20), ForeColor = Color.FromArgb(90, 50, 20), Font = new Font(Font, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        bExe.Click += (s, e) =>
        {
            using (OpenFileDialog d = new OpenFileDialog { Filter = "Zeus.exe|*.exe", Title = L.T("Zeus.exe du jeu à adapter", "Zeus.exe of the game to adapt") })
                if (d.ShowDialog(this) == DialogResult.OK) LoadExe(d.FileName);
        };
        gExe.Controls.AddRange(new Control[] { tExe, bExe, lblExe });

        // 2. Mappemonde : pack 1080p / 1440p ou autre taille, position de la carte, patch de l'editeur
        gRes = new GroupBox { Location = new Point(12, 158), Size = new Size(736, 88), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        cboPreset = new ComboBox { Location = new Point(12, 24), Size = new Size(190, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        nW = new NumericUpDown { Location = new Point(212, 25), Width = 70, Minimum = 838, Maximum = 7680, Value = 1738 };
        lblX = new Label { Location = new Point(284, 28), AutoSize = true, Text = "x" };
        nH = new NumericUpDown { Location = new Point(300, 25), Width = 70, Minimum = 600, Maximum = 4320, Value = 1080 };
        cboPos = new ComboBox { Location = new Point(380, 24), Size = new Size(150, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        chkEditor = new CheckBox { Location = new Point(12, 56), Size = new Size(470, 24), Checked = true };
        lblEd = new Label { Location = new Point(490, 59), Size = new Size(234, 20), ForeColor = Color.DimGray, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        cboPreset.SelectedIndexChanged += (s, e) =>
        {
            if (cboPreset.SelectedIndex == 0) { nW.Value = 1738; nH.Value = 1080; cboPos.SelectedIndex = 0; }
            else if (cboPreset.SelectedIndex == 1) { nW.Value = 2376; nH.Value = 1410; cboPos.SelectedIndex = 0; }
            nW.Enabled = nH.Enabled = cboPos.Enabled = cboPreset.SelectedIndex == 2;
            ShowEditor();
        };
        nW.ValueChanged += (s, e) => ShowEditor();
        nH.ValueChanged += (s, e) => ShowEditor();
        cboPos.SelectedIndexChanged += (s, e) => ShowEditor();
        chkEditor.CheckedChanged += (s, e) => ShowEditor();
        gRes.Controls.AddRange(new Control[] { cboPreset, nW, lblX, nH, cboPos, chkEditor, lblEd });

        // 3. Fichiers a convertir
        bAddFiles = Btn(12, 256, 170);
        bAddFolder = Btn(190, 256, 170);
        bClear = Btn(368, 256, 120);
        bAddFiles.Click += (s, e) =>
        {
            using (OpenFileDialog d = new OpenFileDialog
            {
                Filter = L.T("Scénarios, cartes et sauvegardes Zeus (*.pak;*.set;*.map;*.sav)", "Zeus scenarios, maps and saves (*.pak;*.set;*.map;*.sav)") + "|*.pak;*.set;*.map;*.sav",
                Multiselect = true, Title = L.T("Choisir des scénarios", "Choose scenarios")
            })
                if (d.ShowDialog(this) == DialogResult.OK) AddPaths(d.FileNames);
        };
        bAddFolder.Click += (s, e) =>
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = L.T("Dossier contenant des scénarios (.pak, .set, .map) ou des sauvegardes (.sav)", "Folder containing scenarios (.pak, .set, .map) or saves (.sav)") })
                if (d.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { d.SelectedPath });
        };
        bClear.Click += (s, e) => { list.Items.Clear(); relPaths.Clear(); };
        chkSub = new CheckBox { Location = new Point(14, 292), Size = new Size(470, 22), Checked = true };
        chkMap = new CheckBox { Location = new Point(500, 292), Size = new Size(248, 22), Checked = Converter.MapsEnabled, Enabled = Converter.MapsEnabled, Anchor = AnchorStyles.Top | AnchorStyles.Right };

        list = new ListView
        {
            Location = new Point(12, 320), Size = new Size(736, 234), View = View.Details, FullRowSelect = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AllowDrop = true
        };
        list.Columns.Add("", 290);
        list.Columns.Add("", 420);
        list.DragEnter += (s, e) => { if (exe != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        list.DragDrop += (s, e) => { if (exe != null) AddPaths((string[])e.Data.GetData(DataFormats.FileDrop)); };

        rbReplace = new RadioButton { Location = new Point(12, 562), Size = new Size(736, 24), Checked = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        rbCopy = new RadioButton { Location = new Point(12, 588), Size = new Size(330, 24), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        outDir = new TextBox { Location = new Point(344, 588), Size = new Size(300, 24), Enabled = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        bBrowse = Btn(650, 586, 98); bBrowse.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; bBrowse.Enabled = false;
        rbCopy.CheckedChanged += (s, e) => { outDir.Enabled = bBrowse.Enabled = rbCopy.Checked; };
        bBrowse.Click += (s, e) =>
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = L.T("Dossier où enregistrer les scénarios convertis", "Folder where the converted scenarios are saved") })
                if (d.ShowDialog(this) == DialogResult.OK) outDir.Text = d.SelectedPath;
        };

        bConvert = Btn(12, 628, 200); bConvert.Height = 40; bConvert.Font = new Font(Font, FontStyle.Bold);
        bConvert.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        bConvert.Click += (s, e) => Run(false);
        bRestore = Btn(220, 628, 200); bRestore.Height = 40;
        bRestore.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        bRestore.Click += (s, e) => Run(true);
        note = new Label { Location = new Point(430, 624), Size = new Size(318, 84), ForeColor = Color.DimGray, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

        Controls.AddRange(new Control[] { help, lblLang, cboLang, gExe, gRes, bAddFiles, bAddFolder, bClear, chkSub, chkMap, list, rbReplace, rbCopy, outDir, bBrowse, bConvert, bRestore, note });

        worker = new BackgroundWorker { WorkerReportsProgress = true };
        worker.DoWork += Work;
        worker.ProgressChanged += (s, e) => { object[] p = (object[])e.UserState; ListViewItem it = (ListViewItem)p[0]; it.Name = ""; it.SubItems[1].Text = (string)p[1]; it.ForeColor = (Color)p[2]; };
        worker.RunWorkerCompleted += (s, e) => { SetBusy(false); MessageBox.Show(this, (string)e.Result, Text, MessageBoxButtons.OK, MessageBoxIcon.Information); };
        ApplyLanguage();
        ShowExe();
    }

    // Zeus.exe choisi : version, langue, resolution ; taille de mappemonde proposee (installee a cote, sinon vue de l'exe)
    void LoadExe(string path)
    {
        try { exe = EditorCore.Read(path); }
        catch (Exception ex) { exe = null; MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); ShowExe(); return; }
        tExe.Text = path;
        string how; int[] m = ExeInfo.MapSizeFor(path, out how);
        if (m != null)
        {
            if (m[0] == 1738 && m[1] == 1080) cboPreset.SelectedIndex = 0;
            else if (m[0] == 2376 && m[1] == 1410) cboPreset.SelectedIndex = 1;
            else
            {
                cboPreset.SelectedIndex = 2;
                nW.Value = Math.Max(nW.Minimum, Math.Min(nW.Maximum, m[0])); nH.Value = Math.Max(nH.Minimum, Math.Min(nH.Maximum, m[1]));
            }
        }
        chkEditor.Checked = exe.Build != null;
        ShowExe();
    }

    void ShowExe()
    {
        bool ready = exe != null;
        foreach (Control c in new Control[] { gRes, bAddFiles, bAddFolder, bClear, chkSub, list, bConvert, bRestore }) c.Enabled = ready;
        chkMap.Enabled = ready && Converter.MapsEnabled;
        if (!ready) { lblExe.Text = L.T("Choisissez d'abord le Zeus.exe du jeu à adapter.", "First choose the Zeus.exe of the game to adapt."); ShowEditor(); return; }
        string res = exe.Resolution == null ? "?" : exe.Resolution[0] + " x " + exe.Resolution[1];
        if (exe.Build == null) lblExe.Text = L.T("Exe non reconnu (russe, CD protégé…) : conversion possible, éditeur non patchable.", "Exe not recognised (Russian, protected CD…): conversion possible, editor cannot be patched.");
        else lblExe.Text = (EditorCore.English(exe) ? L.T("Exe anglais 2.1.4.0", "English exe 2.1.4.0") : L.T("Exe français", "French exe")) + " — " + res
            + " — " + (exe.EditorPatched ? L.T("éditeur patché", "editor patched") : L.T("éditeur d'origine", "original editor"));
        ShowEditor();
    }

    // Etat de l'editeur pour la mappemonde choisie
    void ShowEditor()
    {
        bool can = exe != null && exe.Build != null;
        chkEditor.Enabled = can;
        if (!can) { lblEd.Text = ""; return; }
        if (EditorCore.EditorFits(exe, (int)nW.Value, (int)nH.Value, cboPos.SelectedIndex)) lblEd.Text = L.T("déjà patché pour cette mappemonde", "already patched for this world map");
        else lblEd.Text = chkEditor.Checked ? L.T("sera patché à la conversion", "will be patched on Convert") : L.T("non patché", "not patched");
    }

    // Tous les textes de la fenetre dans la langue choisie.
    void ApplyLanguage()
    {
        Text = "World Map Converter — Zeus - World Maps Tools";
        lblLang.Text = L.T("Langue :", "Language:");
        help.Text = L.T(
            "Choisissez le Zeus.exe du jeu, sa mappemonde, puis les scénarios (.pak, .set, .map) et sauvegardes (.sav) : "
            + "cités, noms de régions et routes sont replacés sur la mappemonde agrandie, quelle que soit sa taille.",
            "Choose the game's Zeus.exe, its world map, then the scenarios (.pak, .set, .map) and saved games (.sav): "
            + "cities, region names and trade routes are moved onto the enlarged world map, whatever its size.");
        gExe.Text = L.T("1. Zeus.exe du jeu", "1. The game's Zeus.exe");
        bExe.Text = L.T("Choisir…", "Choose…");
        gRes.Text = L.T("2. Mappemonde du jeu", "2. The game's world map");
        int ip = cboPreset.SelectedIndex < 0 ? 0 : cboPreset.SelectedIndex, ipos = cboPos.SelectedIndex < 0 ? 0 : cboPos.SelectedIndex;
        cboPreset.Items.Clear();
        cboPreset.Items.AddRange(new object[] { L.T("1738 x 1080 (1080p)", "1738 x 1080 (1080p)"), L.T("2376 x 1410 (1440p)", "2376 x 1410 (1440p)"), L.T("Autre taille :", "Other size:") });
        cboPos.Items.Clear();
        cboPos.Items.AddRange(new object[] { L.T("Carte à gauche", "Map on the left"), L.T("Carte au centre", "Map centred"), L.T("Carte à droite", "Map on the right") });
        cboPos.SelectedIndex = ipos; cboPreset.SelectedIndex = ip;
        chkEditor.Text = L.T("Patcher l'éditeur de scénarios de cet exe pour cette mappemonde", "Patch this exe's scenario editor for this world map");
        bAddFiles.Text = L.T("Ajouter des fichiers…", "Add files…");
        bAddFolder.Text = L.T("Ajouter un dossier…", "Add a folder…");
        bClear.Text = L.T("Vider la liste", "Clear the list");
        chkMap.Text = L.T("Inclure les .map de l'éditeur", "Include editor .map files");
        chkSub.Text = L.T("Inclure les sous-dossiers (pour « Ajouter un dossier » et le glisser-déposer)", "Include subfolders (for \"Add a folder\" and drag and drop)");
        list.Columns[0].Text = L.T("3. Fichier", "3. File");
        list.Columns[1].Text = L.T("État", "Status");
        rbReplace.Text = L.T("Remplacer les fichiers (une copie de l'original « .bak » est créée à côté)", "Replace the files (a copy of the original, \".bak\", is created next to it)");
        rbCopy.Text = L.T("Enregistrer les fichiers convertis dans ce dossier :", "Save the converted files in this folder:");
        bBrowse.Text = L.T("Parcourir…", "Browse…");
        bConvert.Text = L.T("Convertir", "Convert");
        bRestore.Text = L.T("Restaurer les originaux", "Restore the originals");
        note.Text = L.T("Jouez ensuite avec les mappemondes de cette taille. Ajoutez aussi vos sauvegardes (.sav) en cours.",
            "Then play with world maps of this size. Also add your saves (.sav) in progress.");
        // Fichiers encore en attente (pas encore traites)
        foreach (ListViewItem it in list.Items) if (it.Name == "pending") it.SubItems[1].Text = PendingText((string)it.Tag);
    }

    static string PendingText(string f)
    {
        return File.Exists(f + ".bak") ? L.T("En attente (une sauvegarde .bak existe)", "Waiting (a .bak backup exists)") : L.T("En attente", "Waiting");
    }

    Button Btn(int x, int y, int w) { return new Button { Location = new Point(x, y), Size = new Size(w, 30) }; }

    void AddPaths(IEnumerable<string> paths)
    {
        HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ListViewItem it in list.Items) known.Add((string)it.Tag);
        foreach (string p in paths)
        {
            List<string> files = new List<string>();
            string root = null;
            if (Directory.Exists(p))
            {
                root = p.TrimEnd('\\');
                SearchOption depth = chkSub.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (string ext in chkMap.Checked ? new[] { "*.pak", "*.set", "*.map", "*.sav" } : new[] { "*.pak", "*.set", "*.sav" })
                    try { files.AddRange(Directory.GetFiles(p, ext, depth)); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
            else if (p.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".set", StringComparison.OrdinalIgnoreCase)
                     || (chkMap.Checked && p.EndsWith(".map", StringComparison.OrdinalIgnoreCase)) || p.EndsWith(".sav", StringComparison.OrdinalIgnoreCase)) files.Add(p);
            foreach (string f in files)
            {
                if (!known.Add(f)) continue;
                string rel = root != null && f.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) ? f.Substring(root.Length + 1) : Path.GetFileName(f);
                relPaths[f] = rel;
                ListViewItem it = new ListViewItem(rel) { Tag = f, ToolTipText = f, Name = "pending" };
                it.SubItems.Add(PendingText(f));
                list.Items.Add(it);
            }
        }
    }

    void SetBusy(bool busy)
    {
        foreach (Control c in Controls) if (c is Button || c is RadioButton || c is GroupBox || c is ComboBox) c.Enabled = !busy;
        outDir.Enabled = bBrowse.Enabled = !busy && rbCopy.Checked;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    void Run(bool restore)
    {
        if (exe == null) { MessageBox.Show(this, L.T("Choisissez d'abord le Zeus.exe du jeu.", "First choose the game's Zeus.exe."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Editeur de scenarios de l'exe : patche pour la mappemonde choisie avant la conversion, si demande
        if (!restore && chkEditor.Enabled && chkEditor.Checked && !EditorCore.EditorFits(exe, (int)nW.Value, (int)nH.Value, cboPos.SelectedIndex))
        {
            bool ok;
            string m = EditorCore.PatchEditor(exe.Path, (int)nW.Value, (int)nH.Value, cboPos.SelectedIndex, out ok);
            try { exe = EditorCore.Read(exe.Path); } catch { }
            ShowExe();
            MessageBox.Show(this, m, Text, MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            if (!ok) return;
            if (list.Items.Count == 0) return;
        }
        if (list.Items.Count == 0) { MessageBox.Show(this, L.T("Ajoutez d'abord des fichiers .pak, .set, .map ou .sav.", "First add some .pak, .set, .map or .sav files."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (!restore && rbCopy.Checked && (outDir.Text.Trim() == "" || !Directory.Exists(outDir.Text.Trim())))
        { MessageBox.Show(this, L.T("Choisissez un dossier de destination existant.", "Choose an existing destination folder."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        List<object[]> jobs = new List<object[]>();
        foreach (ListViewItem it in list.Items)
        {
            string rel;
            if (!relPaths.TryGetValue((string)it.Tag, out rel)) rel = Path.GetFileName((string)it.Tag);
            jobs.Add(new object[] { it, (string)it.Tag, rel });
        }
        SetBusy(true);
        worker.RunWorkerAsync(new object[] { restore, rbCopy.Checked ? outDir.Text.Trim() : null, jobs, Target.ForMap((int)nW.Value, (int)nH.Value, cboPos.SelectedIndex) });
    }

    void Work(object sender, DoWorkEventArgs e)
    {
        object[] a = (object[])e.Argument;
        bool restore = (bool)a[0]; string dir = (string)a[1]; List<object[]> jobs = (List<object[]>)a[2]; Target target = (Target)a[3];
        int ok = 0, skip = 0, err = 0;
        foreach (object[] j in jobs)
        {
            ListViewItem it = (ListViewItem)j[0]; string f = (string)j[1];
            worker.ReportProgress(0, new object[] { it, restore ? L.T("Restauration…", "Restoring…") : L.T("Conversion…", "Converting…"), Color.Black });
            if (restore)
            {
                string m;
                bool done = Converter.Restore(f, out m);
                if (done) ok++; else skip++;
                worker.ReportProgress(0, new object[] { it, m, done ? Color.DarkGreen : Color.DimGray });
                continue;
            }
            string dst = dir == null ? f : Path.Combine(dir, (string)j[2]);   // sous-dossiers recrees dans la destination
            if (dir != null) Directory.CreateDirectory(Path.GetDirectoryName(dst));
            ConvertResult r = Converter.Convert(f, dst, target);
            Color c = r.Outcome == Outcome.Error ? Color.Firebrick : (r.Outcome == Outcome.AlreadyConverted ? Color.DimGray : Color.DarkGreen);
            if (r.Outcome == Outcome.Error) err++; else if (r.Outcome == Outcome.AlreadyConverted) skip++; else ok++;
            worker.ReportProgress(0, new object[] { it, r.Message, c });
        }
        e.Result = restore
            ? string.Format(L.T("Terminé : {0} restauré(s), {1} sans sauvegarde.", "Done: {0} restored, {1} without backup."), ok, skip)
            : string.Format(L.T("Terminé : {0} converti(s), {1} déjà converti(s), {2} erreur(s).", "Done: {0} converted, {1} already converted, {2} error(s)."), ok, skip, err);
    }
}
