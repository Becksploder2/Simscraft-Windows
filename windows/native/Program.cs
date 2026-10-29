using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;

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
            else {
                var info = new Label { Text = title + "\n\nNative migration in progress. The previous HTML tool has not yet been ported.", AutoSize = true, Location = new Point(30, 35), ForeColor = Color.White };
                page.Controls.Add(info);
            }
        }
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