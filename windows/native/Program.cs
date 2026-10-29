using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;

ApplicationConfiguration.Initialize();
Application.Run(new StudioWindow());

sealed class StudioWindow : Form {
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    readonly TextBox name = new() { Width = 450 };
    readonly ComboBox kind = new() { Width = 450, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox description = new() { Width = 650, Height = 190, Multiline = true, ScrollBars = ScrollBars.Vertical };
    readonly CheckBox compatibility = new() { Text = "Include compatibility checks", Checked = true, AutoSize = true };
    public StudioWindow() {
        Text = "Simcraft Studio — Native Desktop Preview"; Width = 1100; Height = 760;
        BackColor = Color.FromArgb(24, 31, 48); ForeColor = Color.White;
        tabs.Font = new Font("Segoe UI", 11);
        Controls.Add(tabs);
        foreach(var title in new[]{"Character Studio","Hair & Glasses","Clothing SmartAssist","CC Library","Face Texture Studio","Glasses Creator","CAS Mod Studio","AI Mod Studio","UV Face Alignment","Buildcraft","Settings"}) {
            var page = new TabPage(title) { BackColor = Color.FromArgb(27, 37, 57), ForeColor = Color.White, AutoScroll = true };
            tabs.TabPages.Add(page);
            if(title == "AI Mod Studio") BuildModTab(page);
            else if(title == "Character Studio") BuildCharacterTab(page);
            else if(title == "Hair & Glasses") BuildPreferencesTab(page);
            else if(title == "Clothing SmartAssist") BuildClothingTab(page);
            else if(title == "CC Library") BuildCatalogTab(page);
            else if(title == "Settings") BuildSettingsTab(page);
            else {
                var info = new Label { Text = title + "\n\nNative migration in progress. The previous HTML tool has not yet been ported.", AutoSize = true, Location = new Point(30, 35), ForeColor = Color.White };
                page.Controls.Add(info);
            }
        }
    }

    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out WinRect rect);
    [StructLayout(LayoutKind.Sequential)] struct WinRect { public int Left,Top,Right,Bottom; }
    static FlowLayoutPanel Panel(TabPage page) { var p = new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Padding=new Padding(22) }; page.Controls.Add(p); return p; }
    static void Caption(FlowLayoutPanel p,string value) => p.Controls.Add(new Label { Text=value, AutoSize=true, ForeColor=Color.White, Margin=new Padding(4,12,4,5) });
    void BuildCharacterTab(TabPage page) {
        var p=Panel(page); Caption(p,"Character Studio — reference photo and templates");
        var preview=new PictureBox { Size=new Size(420,330), SizeMode=PictureBoxSizeMode.Zoom, BorderStyle=BorderStyle.FixedSingle };
        var gender=new ComboBox { Width=240, DropDownStyle=ComboBoxStyle.DropDownList };
        gender.Items.AddRange(new object[]{"Female","Male"}); gender.SelectedIndex=0;
        var template=new ComboBox { Width=240, DropDownStyle=ComboBoxStyle.DropDownList };
        void Fill() { template.Items.Clear(); for(int i=1;i<=19;i++) template.Items.Add((gender.SelectedIndex==0?"F":"M")+i); template.SelectedIndex=0; }
        gender.SelectedIndexChanged+=(_,_)=>Fill(); Fill();
        var choose=new Button { Text="Import reference photo", AutoSize=true };
        choose.Click+=(_,_)=>{ using var dialog=new OpenFileDialog { Filter="Images|*.png;*.jpg;*.jpeg;*.bmp" }; if(dialog.ShowDialog()!=DialogResult.OK)return; try { var old=preview.Image; using var img=Image.FromFile(dialog.FileName); preview.Image=new Bitmap(img); old?.Dispose(); } catch(Exception ex) { MessageBox.Show(ex.Message); } };
        p.Controls.Add(choose);p.Controls.Add(preview);Caption(p,"Template group");p.Controls.Add(gender);Caption(p,"Template selection (manual; auto-detection not ported)");p.Controls.Add(template);
        var casStatus=new Label { Text="CAS not checked", AutoSize=true, ForeColor=Color.White };
        var detect=new Button { Text="Detect The Sims 4",AutoSize=true };
        detect.Click+=(_,_)=>{var games=Process.GetProcesses().Where(x=>x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase)).ToArray();casStatus.Text=games.Length>0?"The Sims 4 process detected ("+games.Length+"). Enter CAS for capture.":"The Sims 4 process not detected.";foreach(var game in games)game.Dispose();};
        var capture=new Button { Text="Capture Sims 4 window",AutoSize=true };
        capture.Click+=(_,_)=>{try{var process=Process.GetProcesses().FirstOrDefault(x=>x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase));if(process==null||process.MainWindowHandle==IntPtr.Zero){casStatus.Text="No visible Sims 4 window found.";return;}if(!GetWindowRect(process.MainWindowHandle,out var rect))throw new Exception("Cannot read game window.");int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;if(width<100||height<100||width>10000||height>10000)throw new Exception("Invalid window dimensions.");using var image=new Bitmap(width,height);using(var graphics=Graphics.FromImage(image)){graphics.CopyFromScreen(rect.Left,rect.Top,0,0,new Size(width,height));}using var dialog=new SaveFileDialog{Filter="PNG image|*.png",FileName="Simcraft-CAS-Capture.png"};if(dialog.ShowDialog()==DialogResult.OK){image.Save(dialog.FileName,System.Drawing.Imaging.ImageFormat.Png);casStatus.Text="Captured game window to "+dialog.FileName+". CAS mode is not automatically verified.";}process.Dispose();}catch(Exception ex){casStatus.Text="Capture failed: "+ex.Message;}};
        p.Controls.Add(detect);p.Controls.Add(capture);p.Controls.Add(casStatus);
        var backgroundPreview=new PictureBox { Size=new Size(440,220),SizeMode=PictureBoxSizeMode.Zoom,BorderStyle=BorderStyle.FixedSingle };
        var backgroundButton=new Button { Text="Choose CAS workspace background",AutoSize=true };
        backgroundButton.Click+=(_,_)=>{using var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp"};if(dialog.ShowDialog()!=DialogResult.OK)return;try{using var img=Image.FromFile(dialog.FileName);var old=backgroundPreview.Image;backgroundPreview.Image=new Bitmap(img);old?.Dispose();}catch(Exception ex){MessageBox.Show(ex.Message);}};
        Caption(p,"CAS workspace background (local preview)");p.Controls.Add(backgroundButton);p.Controls.Add(backgroundPreview);
        Caption(p,"Window detection and capture only. Automatic morphing and CAS controls are not yet implemented.");
    }
    void BuildPreferencesTab(TabPage page) {
        var p=Panel(page);Caption(p,"Hair & Glasses");
        var glasses=new ComboBox { Width=300,DropDownStyle=ComboBoxStyle.DropDownList };
        glasses.Items.AddRange(new object[]{"Yes","Sometimes","No"});glasses.SelectedIndex=0;
        var hair=new ComboBox { Width=300,DropDownStyle=ComboBoxStyle.DropDownList };
        hair.Items.AddRange(new object[]{"Match reference","Suggest flattering styles","Manual choice"});hair.SelectedIndex=0;
        Caption(p,"Does your Sim wear glasses?");p.Controls.Add(glasses);Caption(p,"Hair approach");p.Controls.Add(hair);
        Caption(p,"Preferences are editable; item recommendations require a loaded CAS catalog.");
    }
    void BuildClothingTab(TabPage page) {
        var p=Panel(page);Caption(p,"Clothing SmartAssist — outfit planning");
        var styles=new CheckedListBox { Width=350,Height=250,CheckOnClick=true };
        styles.Items.AddRange(new object[]{"Sporty","Basics","Outdoorsy","Polished","Preppy","Rocker","Country","Hipster","Boho","Streetwear","Gamer","Gala","Africana"});p.Controls.Add(styles);
        var occasion=new ComboBox { Width=280,DropDownStyle=ComboBoxStyle.DropDownList };
        occasion.Items.AddRange(new object[]{"Everyday","Formal","Athletic","Sleep","Party","Swimwear","Hot Weather","Cold Weather"});occasion.SelectedIndex=0;p.Controls.Add(occasion);
        var export=new Button { Text="Export outfit plan", AutoSize=true };
        export.Click+=(_,_)=>{using var dialog=new SaveFileDialog { Filter="JSON|*.json",FileName="simcraft-outfit.json" };if(dialog.ShowDialog()!=DialogResult.OK)return;File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new { occasion=occasion.Text,styles=styles.CheckedItems.Cast<object>().Select(x=>x.ToString()).ToArray() },new JsonSerializerOptions { WriteIndented=true }));};
        p.Controls.Add(export);
    }
    void BuildCatalogTab(TabPage page) {
        var p=Panel(page);Caption(p,"CC Library — import and inspect JSON/CSV catalog");
        var status=new Label { Text="No catalog imported",AutoSize=true,ForeColor=Color.White };
        var results=new TextBox { Width=650,Height=340,Multiline=true,ScrollBars=ScrollBars.Both,ReadOnly=true };
        var button=new Button { Text="Import catalog",AutoSize=true };
        button.Click+=(_,_)=>{using var dialog=new OpenFileDialog { Filter="Catalog|*.json;*.csv" };if(dialog.ShowDialog()!=DialogResult.OK)return;try{var content=File.ReadAllText(dialog.FileName);if(Path.GetExtension(dialog.FileName).Equals(".json",StringComparison.OrdinalIgnoreCase)){using var parsed=JsonDocument.Parse(content);status.Text="JSON catalog loaded";}else status.Text="CSV catalog loaded";results.Text=content.Length>16000?content[..16000]:content;}catch(Exception ex){MessageBox.Show(ex.Message);}};
        p.Controls.Add(button);p.Controls.Add(status);p.Controls.Add(results);
        Caption(p,"Read-only import; Sims 4 package resource parsing is not implemented.");
    }
    void BuildSettingsTab(TabPage page) {
        var p=Panel(page);Caption(p,"Simcraft Studio — native preview");
        Caption(p,"Export and import of mod and outfit plans are available in their respective tabs.");
        Caption(p,"Photo previews and manual F1–F19 / M1–M19 selection are available in Character Studio.");
        Caption(p,"The original HTML workspace is not used by this native interface.");
        var updateStatus=new Label { Text="Update status: not checked",AutoSize=true,ForeColor=Color.White };
        var check=new Button { Text="Check for updates",AutoSize=true };
        check.Click+=async (_,_)=>await CheckForUpdates(updateStatus);
        p.Controls.Add(check);p.Controls.Add(updateStatus);
    }
    const string CurrentVersion = "1.0.6";
    static readonly HttpClient UpdateClient = new() { Timeout=TimeSpan.FromSeconds(15) };
    async System.Threading.Tasks.Task CheckForUpdates(Label status) {
        status.Text="Checking GitHub releases...";
        try {
            using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.github.com/repos/Becksploder2/Simscraft-Windows/releases/latest");
            request.Headers.UserAgent.ParseAdd("SimcraftStudio/1.0.6");
            using var response=await UpdateClient.SendAsync(request);response.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag=json.RootElement.GetProperty("tag_name").GetString()??"";
            if(!Version.TryParse(tag.TrimStart('v','V'),out var latest)||latest<=new Version(CurrentVersion)){status.Text="Up to date (v"+CurrentVersion+").";return;}
            status.Text="Update available: "+tag;
            if(MessageBox.Show("Simcraft Studio "+tag+" is available. Open the official release page?","Update available",MessageBoxButtons.YesNo)==DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://github.com/Becksploder2/Simscraft-Windows/releases/latest"){UseShellExecute=true});
        } catch(Exception ex){status.Text="Update check failed: "+ex.Message;}
    }
    void BuildModTab(TabPage page) {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(22) };
        page.Controls.Add(panel);
        void Label(string text) => panel.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(4, 14, 4, 4), ForeColor = Color.White });
        Label("AI Mod Studio — Project Specification Builder");
        Label("Mod name"); panel.Controls.Add(name);
        Label("Mod category"); kind.Items.AddRange(new object[]{"Gameplay interactions","School and careers","CAS and accessories","Objects and tuning"}); kind.SelectedIndex=0; panel.Controls.Add(kind);
        Label("Describe your mod"); panel.Controls.Add(description); panel.Controls.Add(compatibility);
        var export = new Button { Text = "Export mod project JSON", AutoSize = true, Padding = new Padding(12) };
        export.Click += (_,_) => {
            if(string.IsNullOrWhiteSpace(description.Text)){MessageBox.Show("Describe your mod first.");return;}
            using var dialog = new SaveFileDialog { Filter = "Simcraft project (*.simcraft.json)|*.simcraft.json", FileName = "mod.simcraft.json" };
            if(dialog.ShowDialog()!=DialogResult.OK)return;
            var data = new { format="simcraft-mod-project", schemaVersion=1, name=name.Text, category=kind.Text, description=description.Text, compatibilityChecks=compatibility.Checked, output="specification-only" };
            File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
            MessageBox.Show("Project saved. This is not an installable mod.");
        };
        panel.Controls.Add(export);
    }
}