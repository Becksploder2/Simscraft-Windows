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
    private Image? roomBackground;
    private string? selectedPhoto;
    public StudioWindow() {
        Text = "Simcraft Studio"; ClientSize = new Size(1250, 800); MinimumSize = new Size(950, 650); StartPosition=FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(24, 31, 48); ForeColor = Color.White;
        tabs.Font = new Font("Segoe UI", 11);
        Controls.Add(tabs);
        // Test builds never interrupt the user with update prompts on startup.
        try {
            var path=Path.Combine(AppContext.BaseDirectory,"assets","CAS_Background.png");
            if(File.Exists(path)) {
                using var file=Image.FromFile(path);
                roomBackground=new Bitmap(1250,800);
                using var g=Graphics.FromImage(roomBackground);
                g.DrawImage(file,new Rectangle(0,0,1250,800));
            }
        } catch(Exception) { roomBackground?.Dispose(); roomBackground=null; }
        foreach(var title in new[]{"Character Studio","Hair & Glasses","Clothing SmartAssist","CC Library","Face Texture Studio","Glasses Creator","CAS Mod Studio","AI Mod Studio","UV Face Alignment","Buildcraft","Settings"}) {
            var page = new TabPage(title) { BackColor = Color.FromArgb(27, 37, 57), ForeColor = Color.White, AutoScroll = true };
            if(roomBackground != null) { page.BackgroundImage=roomBackground; page.BackgroundImageLayout=ImageLayout.Stretch; }
            tabs.TabPages.Add(page);
            if(title == "AI Mod Studio") BuildModTab(page);
            else if(title == "Character Studio") BuildCharacterTab(page);
            else if(title == "Hair & Glasses") BuildPreferencesTab(page);
            else if(title == "Clothing SmartAssist") BuildClothingTab(page);
            else if(title == "CC Library") BuildCatalogTab(page);
            else if(title == "CAS Mod Studio") BuildPackageTab(page);
            else if(title == "Buildcraft") BuildBuildcraftTab(page);
            else if(title == "Settings") BuildSettingsTab(page);
            else {
                var info = new Label { Text = title + "\n\nNative migration in progress. The previous HTML tool has not yet been ported.", AutoSize = true, Location = new Point(30, 35), ForeColor = Color.White };
                page.Controls.Add(info);
            }
        }
    }

    static bool SimsRunning() {
        try {
            foreach(var game in Process.GetProcesses()) {
                using(game) {
                    if(game.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase) ||
                       game.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
        } catch { }
        return false;
    }
    [DllImport("user32.dll")] static extern void keybd_event(byte bVk,byte bScan,uint dwFlags,UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extraInfo);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out WinRect rect);
    [StructLayout(LayoutKind.Sequential)] struct WinRect { public int Left,Top,Right,Bottom; }
    static FlowLayoutPanel Panel(TabPage page) { var p = new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Padding=new Padding(22), BackColor=Color.FromArgb(36,45,64), BackgroundImage=page.BackgroundImage, BackgroundImageLayout=ImageLayout.Stretch }; page.Controls.Add(p); return p; }
    static void Caption(FlowLayoutPanel p,string value) => p.Controls.Add(new Label { Text=value, AutoSize=true, ForeColor=Color.White, Margin=new Padding(4,12,4,5) });
    void BuildCharacterTab(TabPage page) {
        var p=Panel(page); Caption(p,"Character Studio — reference photo and templates");
        var preview=new PictureBox { Size=new Size(420,330), SizeMode=PictureBoxSizeMode.Zoom, BorderStyle=BorderStyle.FixedSingle };
        var gender=new ComboBox { Width=240, DropDownStyle=ComboBoxStyle.DropDownList };
        gender.Items.AddRange(new object[]{"Female","Male"}); gender.SelectedIndex=0;
        var template=new ComboBox { Width=240, DropDownStyle=ComboBoxStyle.DropDownList };
        void Fill() { template.Items.Clear(); for(int i=1;i<=19;i++) template.Items.Add((gender.SelectedIndex==0?"F":"M")+i); template.SelectedIndex=0; }
        gender.SelectedIndexChanged+=(_,_)=>Fill(); Fill();
        var choose=new Button { Text="Browse for Photo", AutoSize=true };
        var importStatus=new Label { Text="Ready to import a reference photo",AutoSize=true,ForeColor=Color.White };
        choose.Click+=async (_,_)=>{
            choose.Enabled=false;
            try {
                // Run the shell picker on its own STA thread so a stalled Windows shell extension cannot block the main UI.
                var selection=new System.Threading.Tasks.TaskCompletionSource<string?>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
                var pickerThread=new System.Threading.Thread(()=>{
                    try {
                        using var dialog=new OpenFileDialog {
                            Filter="Images|*.png;*.jpg;*.jpeg;*.bmp",
                            CheckFileExists=true, AutoUpgradeEnabled=false,
                            RestoreDirectory=true, DereferenceLinks=true
                        };
                        selection.TrySetResult(dialog.ShowDialog()==DialogResult.OK?dialog.FileName:null);
                    } catch(Exception ex){selection.TrySetException(ex);}
                });
                pickerThread.IsBackground=true;
                pickerThread.SetApartmentState(System.Threading.ApartmentState.STA);
                pickerThread.Start();
                importStatus.Text="Opening photo picker...";
                var completed=await System.Threading.Tasks.Task.WhenAny(selection.Task,System.Threading.Tasks.Task.Delay(30000));
                if(completed!=selection.Task){importStatus.Text="Photo picker is not responding. Please try again.";return;}
                var file=await selection.Task;
                if(string.IsNullOrEmpty(file)){importStatus.Text="Import canceled.";return;}
                importStatus.Text="Loading photo...";
                var decode=System.Threading.Tasks.Task.Run(()=>{
                    var info=new FileInfo(file);
                    if(info.Length>40_000_000)throw new InvalidDataException("Image exceeds 40 MB limit.");
                    using var image=Image.FromFile(file);
                    if(image.Width>8192||image.Height>8192||((long)image.Width*image.Height)>30_000_000)
                        throw new InvalidDataException("Image dimensions too large. Resize the photo first.");
                    var thumbnail=new Bitmap(800,600);
                    using(var g=Graphics.FromImage(thumbnail)){
                        g.Clear(Color.FromArgb(20,25,35));
                        var scale=Math.Min(800.0/image.Width,600.0/image.Height);
                        int w=(int)(image.Width*scale),h=(int)(image.Height*scale);
                        g.DrawImage(image,(800-w)/2,(600-h)/2,w,h);
                    }
                    return thumbnail;
                });
                completed=await System.Threading.Tasks.Task.WhenAny(decode,System.Threading.Tasks.Task.Delay(15000));
                if(completed!=decode){importStatus.Text="Photo decoding timed out; try a smaller image.";return;}
                var loaded=await decode;
                if(preview.IsDisposed){loaded.Dispose();return;}
                var previous=preview.Image;
                preview.Image=loaded;selectedPhoto=file;
                previous?.Dispose();
                importStatus.Text="Photo imported successfully.";
            } catch(Exception ex){importStatus.Text="Import failed: "+ex.Message;}
            finally {if(!choose.IsDisposed)choose.Enabled=true;}
        };
        p.Controls.Add(choose);p.Controls.Add(importStatus);p.Controls.Add(preview);
        Caption(p,"SIM MORPH STUDIO — ORIGINAL CONTROLS");
        var refreshPreview=new Button { Text="Refresh Preview",AutoSize=true };
        refreshPreview.Click+=(_,_)=>{preview.Invalidate();importStatus.Text="Preview refreshed.";};
        p.Controls.Add(refreshPreview);
        var faceStatus=new Label { Text="Face recognition: no photo analyzed",AutoSize=true,ForeColor=Color.White };
        var analyze=new Button { Text="Analyze Faces",AutoSize=true };
        analyze.Click+=async (_,_)=>{
            if(string.IsNullOrEmpty(selectedPhoto)){faceStatus.Text="Import a photo first.";return;}
            var root=AppContext.BaseDirectory;
            var script=Path.Combine(root,"FaceEngine","analyze_faces.py");
            var model=Path.Combine(root,"Models","face_landmarker.task");
            if(!File.Exists(script)||!File.Exists(model)){
                faceStatus.Text=!File.Exists(script)?"Face analyzer script missing from this build.":"Face landmark model missing from this build.";return;
            }
            var analyzerExe=Path.Combine(root,"FaceEngine","BundledAnalyzer","SimcraftFaceAnalyzer.exe");
            if(!File.Exists(analyzerExe)){
                faceStatus.Text="Bundled face analyzer executable missing. Download the newest complete portable ZIP.";
                return;
            }
            analyze.Enabled=false;faceStatus.Text="Analyzing face...";
            try {
                var output=Path.Combine(Path.GetTempPath(),"simcraft-face-"+Guid.NewGuid().ToString("N")+".json");
                using var process=new Process { StartInfo=new ProcessStartInfo {
                    FileName=analyzerExe,UseShellExecute=false,CreateNoWindow=true,
                    RedirectStandardError=true,RedirectStandardOutput=true
                }};
                process.StartInfo.ArgumentList.Add(selectedPhoto);
                process.StartInfo.ArgumentList.Add("--model");
                process.StartInfo.ArgumentList.Add(model);
                process.StartInfo.ArgumentList.Add("--output");
                process.StartInfo.ArgumentList.Add(output);
                process.StartInfo.ArgumentList.Add("--templates");
                process.StartInfo.ArgumentList.Add(Path.Combine(root,"Templates"));
                process.StartInfo.ArgumentList.Add("--gender");
                process.StartInfo.ArgumentList.Add(gender.SelectedIndex==0?"F":"M");
                process.Start();
                using var cancel=new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(120));
                try { await process.WaitForExitAsync(cancel.Token); }
                catch(OperationCanceledException){process.Kill(entireProcessTree:true);faceStatus.Text="Face analysis timed out.";return;}
                if(process.ExitCode!=0||!File.Exists(output)){faceStatus.Text="Face analysis failed. Check the MediaPipe runtime.";return;}
                using var data=JsonDocument.Parse(await File.ReadAllTextAsync(output));
                int count=data.RootElement.GetProperty("reference").GetProperty("face_count").GetInt32();
                if(count>0 && data.RootElement.TryGetProperty("template_matches",out var matches) && matches.GetArrayLength()>0){
                    var best=matches[0].GetProperty("template").GetString();
                    if(best!=null){
                        var prefix=best.StartsWith("F")?0:1;
                        if(gender.SelectedIndex!=prefix)gender.SelectedIndex=prefix;
                        template.SelectedItem=best;
                        faceStatus.Text="Face analyzed. Suggested template: "+best+" (estimated from facial geometry; verify in CAS).";
                    }
                }else faceStatus.Text=count>0?"Face recognized; landmarks extracted. No reliable template match found.":"No face detected. Try a clear frontal photo.";
                File.Delete(output);
            } catch(Exception ex){faceStatus.Text="Face recognition unavailable: "+ex.Message;}
            finally {if(!analyze.IsDisposed)analyze.Enabled=true;}
        };
        p.Controls.Add(analyze);p.Controls.Add(faceStatus);
        // Assisted automatic morphing: analyze both images, then apply only calibrated movements.
        var morphCalibration = new Simcraft.Cas.CasController(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Simcraft Studio","cas-points.json"));
        var morphFeature = new ComboBox { Width=220, DropDownStyle=ComboBoxStyle.DropDownList };
        morphFeature.Items.AddRange(new object[]{"eyes","eyebrows","nose","cheeks","mouth_lips","chin_jaw","forehead"});
        morphFeature.SelectedIndex=0;
        var completedFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Preview hit regions are approximate until landmark-driven hit testing is available.
        // Restrict hit testing to the displayed image (PictureBox Zoom letterboxing).
        string? PreviewFeature(Point location) {
            if(preview.Image==null)return null;
            double scale=Math.Min((double)preview.ClientSize.Width/preview.Image.Width,
                                  (double)preview.ClientSize.Height/preview.Image.Height);
            double w=preview.Image.Width*scale,h=preview.Image.Height*scale;
            double x=(location.X-(preview.ClientSize.Width-w)/2)/w;
            double y=(location.Y-(preview.ClientSize.Height-h)/2)/h;
            if(x<0||x>1||y<0||y>1)return null;
            // Central face region; do not select hair, clothing, or background.
            if(x<.25||x>.75||y<.12||y>.88)return null;
            if(y<.29)return "forehead";
            if(y<.39)return "eyebrows";
            if(y<.50)return "eyes";
            if(y<.67)return Math.Abs(x-.5)<.115?"nose":"cheeks";
            if(y<.76)return "mouth_lips";
            return "chin_jaw";
        }
        void SelectPreviewFeature(Point point) {
            var feature=PreviewFeature(point);
            if(feature==null||completedFeatures.Contains(feature))return;
            if(!string.Equals(morphFeature.SelectedItem?.ToString(),feature,StringComparison.OrdinalIgnoreCase))
                morphFeature.SelectedItem=feature;
        }
        preview.MouseMove+=(_,e)=>SelectPreviewFeature(e.Location);
        preview.MouseClick+=(_,e)=>SelectPreviewFeature(e.Location);
        var completedStatus = new Label { Text="Completed features: none",AutoSize=true,ForeColor=Color.White };
        var morphProgressLabel=new Label { Text="Face Morph Progress: 0 / 7 (0%)",AutoSize=true,ForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold) };
        var morphProgressBar=new ProgressBar { Minimum=0,Maximum=7,Value=0,Width=330,Height=22,Style=ProgressBarStyle.Continuous };
        void UpdateMorphProgress() {
            int count=Math.Min(7,completedFeatures.Count);
            morphProgressBar.Value=count;
            morphProgressLabel.Text="Face Morph Progress: "+count+" / 7 ("+(count*100/7)+"%)";
        }
        var resetCompleted = new Button { Text="Reset Completed Features (New Sim)",AutoSize=true };
        var morphScaleX = new NumericUpDown { Minimum=-5000,Maximum=5000,Value=500,Width=100 };
        var morphScaleY = new NumericUpDown { Minimum=-5000,Maximum=5000,Value=500,Width=100 };
        var morphStatus = new Label { AutoSize=true,ForeColor=Color.White,Text="Auto Morph changes only the selected feature. Calibrate each facial region independently in Detail Edit." };
        // Hover selection shares the same calibrated points as Auto Morph; no second coordinate store.
        var hoverSelection = new CheckBox { Text="One-Click CAS: select facial feature automatically on hover",Checked=true,AutoSize=true };
        var hoverStatus = new Label { Text="Hover selection uses nearby calibrated points; manual selection remains available.",AutoSize=true,ForeColor=Color.White };
        resetCompleted.Click+=(_,_)=>{completedFeatures.Clear();completedStatus.Text="Completed features: none";UpdateMorphProgress();hoverStatus.Text="All features available again.";};
        var hoverTimer = new System.Windows.Forms.Timer { Interval=75 };
        string lastHoverFeature="";
        string hoverCandidate="";
        int hoverCandidateTicks=0;
        bool morphInProgress=false;
        DateTime hoverResumeAt=DateTime.MinValue;
        hoverTimer.Tick+=(_,_)=>{
            if(morphInProgress||DateTime.UtcNow<hoverResumeAt)return;
            if(!hoverSelection.Checked || !GetCursorPos(out var cursor))return;
            using var game=Process.GetProcesses().FirstOrDefault(x=>(x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase))&&x.MainWindowHandle==GetForegroundWindow());
            if(game==null||!GetWindowRect(game.MainWindowHandle,out var rect))return;
            if(cursor.X<rect.Left||cursor.X>=rect.Right||cursor.Y<rect.Top||cursor.Y>=rect.Bottom)return;
            string? nearest=null;
            double nearestDistance=double.MaxValue;
            foreach(var item in morphCalibration.Points) {
                if(completedFeatures.Contains(item.Key))continue;
                double dx=cursor.X-rect.Left-item.Value.X,dy=cursor.Y-rect.Top-item.Value.Y;
                double distance=dx*dx+dy*dy;
                if(distance<nearestDistance){nearestDistance=distance;nearest=item.Key;}
            }
            // Never select a distant facial region, e.g. hair when hovering over lips.
            if(nearest==null||nearestDistance>95*95) {
                hoverCandidate="";hoverCandidateTicks=0;return;
            }
            if(nearest==lastHoverFeature)return;
            if(nearest!=hoverCandidate){hoverCandidate=nearest;hoverCandidateTicks=1;return;}
            if(++hoverCandidateTicks<2)return;
            int index=morphFeature.FindStringExact(nearest);
            if(index<0)return;
            morphFeature.SelectedIndex=index;
            lastHoverFeature=nearest;
            hoverStatus.Text="Hover selected: "+nearest;
        };
        morphFeature.SelectedIndexChanged+=(_,_)=>{lastHoverFeature=morphFeature.SelectedItem?.ToString()??"";};
        hoverSelection.CheckedChanged+=(_,_)=>{if(hoverSelection.Checked)hoverTimer.Start();else hoverTimer.Stop();};
        hoverTimer.Start();
        p.Disposed+=(_,_)=>{hoverTimer.Stop();hoverTimer.Dispose();};
        // Auto-calibration: select a feature in the preview/dropdown, then click that
        // feature in foreground CAS. No separate calibration button or countdown.
        // Do not guess an uncalibrated feature from arbitrary game-screen coordinates.
        bool previousLeftDown=false;
        var autoCalibrationTimer=new System.Windows.Forms.Timer { Interval=75 };
        autoCalibrationTimer.Tick+=(_,_)=>{
            bool down=(GetAsyncKeyState(0x01)&0x8000)!=0;
            bool justPressed=down&&!previousLeftDown;
            previousLeftDown=down;
            if(!justPressed||!hoverSelection.Checked||morphInProgress||DateTime.UtcNow<hoverResumeAt)return;
            string feature=morphFeature.SelectedItem?.ToString()??"";
            if(string.IsNullOrEmpty(feature)||completedFeatures.Contains(feature))return;
            using var game=Process.GetProcesses().FirstOrDefault(x=>(x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase))&&x.MainWindowHandle==GetForegroundWindow());
            if(game==null||!GetWindowRect(game.MainWindowHandle,out var rect)||!GetCursorPos(out var cursor))return;
            if(cursor.X<rect.Left||cursor.X>=rect.Right||cursor.Y<rect.Top||cursor.Y>=rect.Bottom)return;
            morphCalibration.Calibrate(feature,new Point(cursor.X-rect.Left,cursor.Y-rect.Top));
            morphStatus.Text=feature+" calibrated from CAS click. Confirm you clicked the correct facial region.";
        };
        autoCalibrationTimer.Start();
        page.Disposed+=(_,_)=>autoCalibrationTimer.Dispose();
        var hairClearance=new CheckBox { Text="Forehead/Eyebrows: require hair cleared before morphing",Checked=true,AutoSize=true };
        var hairClearanceHelp=new Label { Text="For bangs or covered brows, temporarily choose a bald/clear hairstyle in CAS, then restore your original style afterward. Automatic restoration is not yet supported.",AutoSize=true,MaximumSize=new Size(530,0),ForeColor=Color.White };
        var autoMorph = new Button { Text="Auto Morph Selected Feature",AutoSize=true };
        autoMorph.Click+=async (_,_)=>{
            if(string.IsNullOrEmpty(selectedPhoto)){morphStatus.Text="Import a reference photograph first.";return;}
            autoMorph.Enabled=false;
            morphInProgress=true;
            string? output=null;
            string? capturedPath=null;
            try {
                var root=AppContext.BaseDirectory;
                var exe=Path.Combine(root,"FaceEngine","BundledAnalyzer","SimcraftFaceAnalyzer.exe");
                var model=Path.Combine(root,"Models","face_landmarker.task");
                if(!File.Exists(exe)||!File.Exists(model))throw new FileNotFoundException("Bundled face analyzer or model missing.");
                // Capture directly from the running game; no user-supplied CAS image is required.
                morphStatus.Text="Focus The Sims 4 in CAS. Capturing the current face in 4 seconds...";
                await System.Threading.Tasks.Task.Delay(4000);
                using var activeCaptureGame=Process.GetProcesses().FirstOrDefault(x=>(x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase))&&x.MainWindowHandle==GetForegroundWindow());
                if(activeCaptureGame==null)throw new InvalidOperationException("Focus the visible Sims 4 CAS window before capture.");
                using var liveCapture=CaptureSimsWindow();
                capturedPath=Path.Combine(Path.GetTempPath(),"simcraft-cas-live-"+Guid.NewGuid().ToString("N")+".png");
                liveCapture.Save(capturedPath,System.Drawing.Imaging.ImageFormat.Png);
                output=Path.Combine(Path.GetTempPath(),"simcraft-morph-"+Guid.NewGuid().ToString("N")+".json");
                using(var proc=new Process { StartInfo=new ProcessStartInfo { FileName=exe,UseShellExecute=false,CreateNoWindow=true } }) {
                    foreach(var arg in new[]{selectedPhoto,"--cas",capturedPath,"--model",model,"--output",output})proc.StartInfo.ArgumentList.Add(arg);
                    proc.Start();
                    using var timeout=new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(120));
                    try{await proc.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){proc.Kill(entireProcessTree:true);throw new TimeoutException("Face analysis timed out.");}
                    if(proc.ExitCode!=0)throw new InvalidOperationException("Face analysis failed.");
                }
                // Auto Morph acts only on the selected/hovered feature, never on the whole face.
                var selectedFeature=morphFeature.SelectedItem?.ToString();
                if(string.IsNullOrWhiteSpace(selectedFeature))throw new InvalidOperationException("Select a facial feature first.");
                if(completedFeatures.Contains(selectedFeature))throw new InvalidOperationException(selectedFeature+" is already completed. Reset completed features to morph it again.");
                if(!morphCalibration.IsCalibrated(selectedFeature))throw new InvalidOperationException("Calibrate "+selectedFeature+" before Auto Morph.");
                var sensitivity=new Dictionary<string,(double X,double Y)> {
                    [selectedFeature]=((double)morphScaleX.Value,(double)morphScaleY.Value)
                };
                var steps=Simcraft.Cas.FaceMorphPlanner.Plan(await File.ReadAllTextAsync(output),sensitivity);
                // Never allow the planner to move a different region than the requested one.
                if(steps.Any(step=>!string.Equals(step.Feature,selectedFeature,StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Planner returned a different facial region; morph canceled.");
                if(hairClearance.Checked&&(selectedFeature=="eyebrows"||selectedFeature=="forehead")) {
                    var cleared=MessageBox.Show("Hair can obstruct "+selectedFeature+". In The Sims 4, temporarily choose a bald or clear-forehead hairstyle before continuing.\\n\\nHave you cleared the hair and confirmed the intended facial region is visible?","Confirm clear facial region",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2,MessageBoxOptions.DefaultDesktopOnly);
                    if(cleared!=DialogResult.Yes){morphStatus.Text="Morph paused until hair is cleared.";return;}
                }
                if(steps.Count==0){morphStatus.Text="No movement calculated for "+selectedFeature+".";return;}
                // Topmost dialog is deliberately shown over the game, including fullscreen-windowed CAS.
                using var prompt=new Form { Text="Simscraft — Confirm Auto Morph",TopMost=true,StartPosition=FormStartPosition.CenterScreen,Size=new Size(470,205),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=true };
                var promptText=new Label { Text="Apply "+steps.Count+" calculated CAS drags to "+selectedFeature+"?\\n\\nKeep The Sims 4 in Detail Edit mode. Press Esc during a drag to cancel.",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Padding=new Padding(18) };
                var promptButtons=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft };
                var yes=new Button { Text="Apply",DialogResult=DialogResult.OK,Width=100 };
                var no=new Button { Text="Cancel",DialogResult=DialogResult.Cancel,Width=100 };
                promptButtons.Controls.Add(yes);promptButtons.Controls.Add(no);
                prompt.Controls.Add(promptText);prompt.Controls.Add(promptButtons);
                prompt.AcceptButton=yes;prompt.CancelButton=no;
                var confirmation=prompt.ShowDialog();
                if(confirmation!=DialogResult.OK)return;
                morphStatus.Text="Focus Sims 4 now...";
                await System.Threading.Tasks.Task.Delay(5000);
                bool IsSimsWindow(IntPtr handle) {
                    foreach(var game in Process.GetProcesses().Where(x=>x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase))) {
                        using(game)if(game.MainWindowHandle==handle)return true;
                    }
                    return false;
                }
                int applied=0;
                foreach(var step in steps) {
                    using var activeGame=Process.GetProcesses().FirstOrDefault(x=>(x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase))&&x.MainWindowHandle==GetForegroundWindow());
                    if(activeGame==null||!GetWindowRect(activeGame.MainWindowHandle,out var bounds))throw new InvalidOperationException("Sims 4 is not the foreground window.");
                    if(!morphCalibration.TryGetPoint(step.Feature,out var relative))throw new InvalidOperationException("Feature is not calibrated: "+step.Feature);
                    morphCalibration.Calibrate(step.Feature,new Point(bounds.Left+relative.X,bounds.Top+relative.Y));
                    try { await morphCalibration.DragAsync(step.Feature,step.Dx,step.Dy,IsSimsWindow); }
                    finally { morphCalibration.Calibrate(step.Feature,relative); }
                    applied++;
                    completedFeatures.Add(step.Feature);
                    // Keep the feature selected in Simscraft after the drag.
                    morphFeature.SelectedItem=step.Feature;
                    completedStatus.Text="Completed features: "+string.Join(", ",completedFeatures.OrderBy(x=>x));
                    UpdateMorphProgress();
                    lastHoverFeature=step.Feature;hoverCandidate="";hoverCandidateTicks=0;
                    morphStatus.Text="Applied "+applied+"/"+steps.Count+" features. Review results before another pass.";
                    await System.Threading.Tasks.Task.Delay(250);
                }
            }catch(Exception ex){morphStatus.Text="Auto morph stopped: "+ex.Message;}
            finally{if(output!=null&&File.Exists(output))try{File.Delete(output);}catch{} if(capturedPath!=null&&File.Exists(capturedPath))try{File.Delete(capturedPath);}catch{} autoMorph.Enabled=true;morphInProgress=false;hoverResumeAt=DateTime.UtcNow.AddSeconds(2);}
        };
        Caption(p,"Assisted Auto Face Morph — only the selected facial feature is adjusted");
        p.Controls.Add(hoverSelection);p.Controls.Add(hoverStatus);p.Controls.Add(morphFeature);p.Controls.Add(hairClearance);p.Controls.Add(hairClearanceHelp);
        Caption(p,"Sensitivity: horizontal / vertical (signed; tune for your CAS camera)");
        p.Controls.Add(morphScaleX);p.Controls.Add(morphScaleY);
        p.Controls.Add(autoMorph);p.Controls.Add(morphStatus);p.Controls.Add(morphProgressLabel);p.Controls.Add(morphProgressBar);p.Controls.Add(completedStatus);p.Controls.Add(resetCompleted);

        importStatus.TextChanged+=(_,_)=>{if(importStatus.Text=="Photo imported successfully." && !analyze.IsDisposed)analyze.PerformClick();};
        Caption(p,"Template group");p.Controls.Add(gender);Caption(p,"Template selection (automatic matching pending landmark calibration)");p.Controls.Add(template);
        // A template recommendation is not an in-game selection. Require CAS calibration
        // and verified foreground game focus before allowing any future preset automation.
        var templateApplyStatus=new Label { AutoSize=true,ForeColor=Color.White,
            Text="In-game preset selection not calibrated. Suggested templates are not applied automatically." };
        var templateApplyInfo=new Button { Text="In-game Template Selection Status",AutoSize=true };
        templateApplyInfo.Click+=(_,_)=>{
            var id=template.SelectedItem?.ToString()??"(none)";
            templateApplyStatus.Text="Suggested "+id+": open the Sims 4 CAS face presets and select it manually. "+
                "Automatic preset clicking is disabled until preset grid positions and selection verification are calibrated.";
        };
        p.Controls.Add(templateApplyInfo);p.Controls.Add(templateApplyStatus);
        var templatePreview=new PictureBox { Size=new Size(260,260),SizeMode=PictureBoxSizeMode.Zoom,BorderStyle=BorderStyle.FixedSingle };
        void RefreshTemplatePreview(){
            try {
                var id=template.SelectedItem?.ToString();
                if(string.IsNullOrWhiteSpace(id))return;
                var folder=id.StartsWith("F")?"Female":"Male";
                var path=Path.Combine(AppContext.BaseDirectory,"Templates",folder,id+".png");
                if(!File.Exists(path))return;
                using var image=Image.FromFile(path);
                var copy=new Bitmap(image);
                var previous=templatePreview.Image;
                templatePreview.Image=copy;previous?.Dispose();
            }catch(Exception ex){faceStatus.Text="Template preview unavailable: "+ex.Message;}
        }
        template.SelectedIndexChanged+=(_,_)=>RefreshTemplatePreview();
        p.Controls.Add(templatePreview);
        RefreshTemplatePreview();
        var casStatus=new Label { Text="CAS not checked", AutoSize=true, ForeColor=Color.White };
        var detect=new Button { Text="Detect The Sims 4",AutoSize=true };
        detect.Click+=(_,_)=>{try{var games=Process.GetProcesses().Where(x=>x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase)).ToArray();casStatus.Text=games.Length>0?"The Sims 4 process detected ("+games.Length+"). Enter CAS for capture.":"The Sims 4 process not detected.";foreach(var game in games)game.Dispose();}catch(Exception ex){casStatus.Text="Detection unavailable: "+ex.Message;}};
        var importCasShot=new Button { Text="Capture Current CAS Face",AutoSize=true };
        importCasShot.Click+=async (_,_)=>{
            importCasShot.Enabled=false;
            casStatus.Text="Opening screenshot picker...";
            try {
                var file=await PickScreenshotAsync();
                casStatus.Text=file==null?"Screenshot selection canceled.":"CAS screenshot selected: "+Path.GetFileName(file)+". CAS mode not verified.";
            }catch(TimeoutException){casStatus.Text="Screenshot picker timed out. App remains responsive.";}
             catch(Exception ex){casStatus.Text="Screenshot selection failed: "+ex.Message;}
            finally{importCasShot.Enabled=true;}
        };
        var casDrop=new Label { Text="Or drop a CAS screenshot here (no file picker)",AutoSize=false,Width=570,Height=50,BorderStyle=BorderStyle.FixedSingle,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White };
        EnableScreenshotDrop(casDrop,file=>casStatus.Text="CAS screenshot selected: "+Path.GetFileName(file)+". Mode not verified.");
        p.Controls.Add(detect);p.Controls.Add(importCasShot);p.Controls.Add(casDrop);p.Controls.Add(casStatus);
        // Process/window titles cannot prove CAS mode. Do not overwrite user feedback
        // every 2.5 seconds with a misleading process-only warning.
        var casReference=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Simcraft Studio","CAS-Reference.png");
        var calibrateCas=new Button { Text="Set CAS reference screenshot",AutoSize=true };
        calibrateCas.Click+=async (_,_)=>{
            calibrateCas.Enabled=false;
            try {
                var file=await PickScreenshotAsync();
                if(file==null){casStatus.Text="CAS reference selection canceled.";return;}
                await System.Threading.Tasks.Task.Run(()=>{
                    using var img=new Bitmap(file);
                    if((long)img.Width*img.Height>30_000_000)throw new InvalidDataException("Image too large.");
                    Directory.CreateDirectory(Path.GetDirectoryName(casReference)!);
                    img.Save(casReference,System.Drawing.Imaging.ImageFormat.Png);
                }).WaitAsync(TimeSpan.FromSeconds(15));
                casStatus.Text="CAS reference saved. Use Check CAS screen while the game is visible.";
            }catch(Exception ex){casStatus.Text="CAS reference failed: "+ex.Message;}
            finally{calibrateCas.Enabled=true;}
        };
        var checkCas=new Button { Text="Check CAS screen",AutoSize=true };
        checkCas.Click+=async (_,_)=>{
            checkCas.Enabled=false;
            casStatus.Text="Checking current game screen against CAS reference...";
            try {
                if(!File.Exists(casReference))throw new InvalidOperationException("Set CAS reference screenshot first.");
                using var reference=new Bitmap(casReference);
                using var current=await CaptureSimsUnobstructedAsync();
                double similarity=BuildUiSimilarity(reference,current);
                casStatus.Text=similarity>=0.87
                    ? "CAS screen visually resembles saved reference ("+similarity.ToString("P0")+"). This is a heuristic, not verified game state."
                    : "CAS not visually confirmed ("+similarity.ToString("P0")+"). Ensure Sims 4 is visible and the screenshot matches the current resolution.";
            }catch(Exception ex){casStatus.Text="CAS check unavailable: "+ex.Message;}
            finally{checkCas.Enabled=true;}
        };
        p.Controls.Add(calibrateCas);p.Controls.Add(checkCas);
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
        export.Click+=(_,_)=>{try{using var dialog=new SaveFileDialog { Filter="JSON|*.json",FileName="simcraft-outfit.json" };if(dialog.ShowDialog()!=DialogResult.OK)return;File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new { occasion=occasion.Text,styles=styles.CheckedItems.Cast<object>().Select(x=>x.ToString()).ToArray() },new JsonSerializerOptions { WriteIndented=true }));}catch(Exception ex){MessageBox.Show("Export failed: "+ex.Message);}};
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
    void BuildPackageTab(TabPage page) {
        var p=Panel(page);
        Caption(p,"CAS Mod Studio — inspect existing Sims 4 packages (read-only)");
        var status=new Label { Text="No package imported.",AutoSize=true,ForeColor=Color.White };
        var report=new TextBox { Width=800,Height=370,Multiline=true,ScrollBars=ScrollBars.Both,ReadOnly=true,Font=new Font("Consolas",10) };
        var import=new Button { Text="Import .package reference",AutoSize=true };
        import.Click+=async (_,_)=>{
            using var dialog=new OpenFileDialog { Filter="Sims 4 package|*.package",CheckFileExists=true };
            if(dialog.ShowDialog()!=DialogResult.OK)return;
            import.Enabled=false;status.Text="Inspecting package...";
            try {
                var path=dialog.FileName;
                var result=await System.Threading.Tasks.Task.Run(()=>{
                    using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                    if(stream.Length<96||stream.Length>2_000_000_000L)throw new InvalidDataException("Invalid or oversized package.");
                    using var reader=new BinaryReader(stream);
                    var magic=System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if(magic!="DBPF")throw new InvalidDataException("Not a DBPF Sims package.");
                    stream.Position=36;
                    var count=reader.ReadUInt32();
                    if(count>2_000_000)throw new InvalidDataException("Resource count exceeds safety limit.");
                    return $"File: {Path.GetFileName(path)}\\r\\nSize: {stream.Length:N0} bytes\\r\\nFormat: DBPF\\r\\nHeader resource count (preliminary): {count:N0}\\r\\n\\r\\nRead-only reference import. Resource extraction, tuning interpretation, and package editing are not implemented yet.";
                });
                report.Text=result;status.Text="Package header inspected. Original file unchanged.";
            }catch(Exception ex){status.Text="Import failed: "+ex.Message;}
            finally{import.Enabled=true;}
        };
        p.Controls.Add(import);p.Controls.Add(status);p.Controls.Add(report);
        Caption(p,"Use existing packages for research. Do not redistribute third-party assets without permission.");
    }
    static void EnableScreenshotDrop(Control target, Action<string> received) {
        // Drag/drop registration is deliberately disabled: WinForms can throw\n        // DragDrop registration did not succeed on machines without an available OLE context.\n        // Keep the app running; the screenshot import buttons remain available.\n        return;\n        // target.AllowDrop=true;
        target.DragEnter+=(_,e)=>{
            if(e.Data?.GetDataPresent(DataFormats.FileDrop)==true)e.Effect=DragDropEffects.Copy;
        };
        target.DragDrop+=(_,e)=>{
            if(e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length>0 &&
               new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(files[0]).ToLowerInvariant()))
                received(files[0]);
        };
    }
    static async System.Threading.Tasks.Task<string?> PickScreenshotAsync() {
        var completion=new System.Threading.Tasks.TaskCompletionSource<string?>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new System.Threading.Thread(()=>{
            try {
                using var dialog=new OpenFileDialog {
                    Filter="Screenshots|*.png;*.jpg;*.jpeg",CheckFileExists=true,
                    AutoUpgradeEnabled=false,RestoreDirectory=true,DereferenceLinks=true
                };
                completion.TrySetResult(dialog.ShowDialog()==DialogResult.OK?dialog.FileName:null);
            }catch(Exception ex){completion.TrySetException(ex);}
        });
        thread.IsBackground=true;
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    static Bitmap CaptureSimsWindow() {
        // Prefer the actual foreground game window; background/helper processes may
        // have zero-sized, minimized, or otherwise unusable MainWindowHandle bounds.
        var foreground=GetForegroundWindow();
        var candidates=Process.GetProcesses().Where(x=>
            x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase) ||
            x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase)).ToArray();
        try {
            if(candidates.Length==0)throw new InvalidOperationException("The Sims 4 is not running.");
            var windows=new List<(IntPtr Handle,int X,int Y,int W,int H,string Name)>();
            foreach(var game in candidates) {
                if(game.MainWindowHandle==IntPtr.Zero||!GetWindowRect(game.MainWindowHandle,out var rect))continue;
                int w=rect.Right-rect.Left,h=rect.Bottom-rect.Top;
                if(w>=300&&h>=300&&w<=8192&&h<=8192&&((long)w*h)<=30_000_000)
                    windows.Add((game.MainWindowHandle,rect.Left,rect.Top,w,h,game.ProcessName));
            }
            if(windows.Count==0) {
                var sizes=string.Join("; ",candidates.Select(game=>{
                    if(game.MainWindowHandle==IntPtr.Zero)return game.ProcessName+": no main window";
                    if(!GetWindowRect(game.MainWindowHandle,out var rect))return game.ProcessName+": bounds unavailable";
                    return game.ProcessName+": "+(rect.Right-rect.Left)+"x"+(rect.Bottom-rect.Top);
                }));
                throw new InvalidDataException("No usable Sims 4 game window. Detected: "+sizes+
                    ". Restore the game window and make sure it is not minimized.");
            }
            var chosen=windows.FirstOrDefault(x=>x.Handle==foreground);
            if(chosen.Handle==IntPtr.Zero)chosen=windows.OrderByDescending(x=>(long)x.W*x.H).First();
            var image=new Bitmap(chosen.W,chosen.H);
            try {
                using(var graphics=Graphics.FromImage(image))
                    graphics.CopyFromScreen(chosen.X,chosen.Y,0,0,new Size(chosen.W,chosen.H));
                return image;
            }catch {image.Dispose();throw;}
        }finally{foreach(var game in candidates)game.Dispose();}
    }
    // UI-only heuristic: compare several stable toolbar patches, never the changing lot.
    // This is not a verified game state; low confidence must block construction.
    // Compare edge/structure signatures in Build/Buy UI regions, not lot colors.
    // Still a visual heuristic; construction must remain dry-run until validated.
    static double BuildUiSimilarity(Bitmap a,Bitmap b) {
        if(Math.Abs((double)a.Width/a.Height-(double)b.Width/b.Height)>0.08)return 0;
        static double Patch(Bitmap a,Bitmap b,double x0,double y0,double w,double h) {
            const int nx=48,ny=18;
            double[,] aa=new double[ny,nx],bb=new double[ny,nx];
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
                double u=x0+w*(x+.5)/nx,v=y0+h*(y+.5)/ny;
                int ax=Math.Clamp((int)(u*a.Width),0,a.Width-1),ay=Math.Clamp((int)(v*a.Height),0,a.Height-1);
                int bx=Math.Clamp((int)(u*b.Width),0,b.Width-1),by=Math.Clamp((int)(v*b.Height),0,b.Height-1);
                var ca=a.GetPixel(ax,ay);var cb=b.GetPixel(bx,by);
                aa[y,x]=.2126*ca.R+.7152*ca.G+.0722*ca.B;
                bb[y,x]=.2126*cb.R+.7152*cb.G+.0722*cb.B;
            }
            double difference=0,activity=0;
            for(int y=1;y<ny-1;y++)for(int x=1;x<nx-1;x++){
                double ea=Math.Abs(aa[y,x+1]-aa[y,x-1])+Math.Abs(aa[y+1,x]-aa[y-1,x]);
                double eb=Math.Abs(bb[y,x+1]-bb[y,x-1])+Math.Abs(bb[y+1,x]-bb[y-1,x]);
                difference+=Math.Abs(ea-eb);
                activity+=Math.Max(ea,eb);
            }
            // Avoid treating two featureless patches as a verified interface.
            if(activity<1000)return 0;
            return Math.Clamp(1-difference/activity,0,1);
        }
        double left=Patch(a,b,.02,.82,.35,.16);
        double center=Patch(a,b,.38,.91,.23,.07);
        double right=Patch(a,b,.72,.84,.26,.14);
        return Math.Min(left,Math.Max(center,right));
    }
    // Capture with our own UI hidden so it cannot cover the game's catalog.
    async System.Threading.Tasks.Task<Bitmap> CaptureSimsUnobstructedAsync() {
        bool visible=Visible;
        try {
            if(visible)Hide();
            await System.Threading.Tasks.Task.Delay(400);
            return await System.Threading.Tasks.Task.Run(()=>CaptureSimsWindow());
        } finally {
            if(visible) { Show();Activate(); }
        }
    }
    bool ContainsSimsWindowOcclusion() {
        // Clicking ARM necessarily makes Simscraft foreground. Foreground focus
        // does NOT mean its window overlaps the Sims capture rectangle.
        // Check actual screen geometry, including multi-monitor coordinates.
        if(!Visible || WindowState==FormWindowState.Minimized)return false;
        var studioBounds=RectangleToScreen(ClientRectangle);
        var games=Process.GetProcesses().Where(x=>
            x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase) ||
            x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase)).ToArray();
        try {
            foreach(var game in games) {
                if(game.MainWindowHandle==IntPtr.Zero || !GetWindowRect(game.MainWindowHandle,out var rect))continue;
                var gameBounds=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
                if(gameBounds.Width>=300 && gameBounds.Height>=300 &&
                   studioBounds.IntersectsWith(gameBounds))return true;
            }
            return false;
        } finally { foreach(var game in games)game.Dispose(); }
    }
    // Reference-free, conservative visual heuristic for the expanded Build/Buy
    // catalog. It does not claim to identify individual Sims UI controls.
    static (bool Likely,double Score,string Detail) InspectBuildBuyUi(Bitmap image) {
        // Bottom catalog should contain a dense band of icons/edges, unlike the lot.
        static double EdgeDensity(Bitmap bmp,double x0,double y0,double w,double h) {
            const int nx=72,ny=20;
            double[,] lum=new double[ny,nx];
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++) {
                int px=Math.Clamp((int)((x0+w*(x+.5)/nx)*bmp.Width),0,bmp.Width-1);
                int py=Math.Clamp((int)((y0+h*(y+.5)/ny)*bmp.Height),0,bmp.Height-1);
                var c=bmp.GetPixel(px,py);
                lum[y,x]=.2126*c.R+.7152*c.G+.0722*c.B;
            }
            int edges=0,total=0;
            for(int y=1;y<ny-1;y++)for(int x=1;x<nx-1;x++) {
                double d=Math.Abs(lum[y,x+1]-lum[y,x-1])+Math.Abs(lum[y+1,x]-lum[y-1,x]);
                if(d>55)edges++;
                total++;
            }
            return (double)edges/total;
        }
        double bottom=EdgeDensity(image,.05,.79,.90,.18);
        double middle=EdgeDensity(image,.20,.36,.60,.30);
        double left=EdgeDensity(image,.02,.82,.32,.15);
        double right=EdgeDensity(image,.72,.82,.26,.15);
        // A visual indication only: do not authorize automated input on this alone.
        bool likely=bottom>.105 && left>.065 && right>.065 && bottom>middle*.70;
        double score=Math.Clamp(bottom/.22,0,1);
        return (likely,score,"catalog "+bottom.ToString("P0")+", left "+left.ToString("P0")+", right "+right.ToString("P0")+", lot "+middle.ToString("P0"));
    }
    // Experimental periodicity estimate. It does not identify the lot origin.
    static (bool Found,int StepX,int StepY,double Confidence) EstimateLotGrid(Bitmap source) {
        const int n=192;
        var horizontal=new double[n];
        var vertical=new double[n];
        var light=new double[n,n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++) {
            int sx=Math.Clamp((int)(source.Width*(.20+.60*(x+.5)/n)),0,source.Width-1);
            int sy=Math.Clamp((int)(source.Height*(.18+.56*(y+.5)/n)),0,source.Height-1);
            var c=source.GetPixel(sx,sy);
            light[y,x]=.2126*c.R+.7152*c.G+.0722*c.B;
        }
        for(int y=1;y<n-1;y++)for(int x=1;x<n-1;x++) {
            horizontal[x]+=Math.Abs(light[y,x+1]-light[y,x-1]);
            vertical[y]+=Math.Abs(light[y+1,x]-light[y-1,x]);
        }
        static (int Lag,double Score) Period(double[] p) {
            double mean=p.Average(),best=0;
            int lagBest=0;
            for(int lag=8;lag<=48;lag++) {
                double dot=0,a=0,b=0;
                for(int i=0;i<p.Length-lag;i++) {
                    double u=p[i]-mean,v=p[i+lag]-mean;
                    dot+=u*v;a+=u*u;b+=v*v;
                }
                double score=dot/Math.Sqrt(Math.Max(1e-8,a*b));
                if(score>best){best=score;lagBest=lag;}
            }
            return (lagBest,best);
        }
        var px=Period(horizontal);var py=Period(vertical);
        double confidence=Math.Min(px.Score,py.Score);
        int dx=(int)Math.Round(px.Lag*source.Width*.60/n);
        int dy=(int)Math.Round(py.Lag*source.Height*.56/n);
        return (confidence>=.72&&dx>=6&&dy>=6,dx,dy,confidence);
    }

    void ShowHouseDemo() {
        using var window=new Form { Text="Simscraft Buildcraft — Full House Blueprint Demo",Width=1050,Height=780,StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(24,31,48),ForeColor=Color.White };
        var top=new FlowLayoutPanel { Dock=DockStyle.Top,Height=50,Padding=new Padding(8) };
        var export=new Button { Text="Export Blueprint PNG",AutoSize=true };
        var savePlan=new Button { Text="Export Room Plan JSON",AutoSize=true };
        top.Controls.Add(export);top.Controls.Add(savePlan);
        window.Controls.Add(top);
        var note=new Label { Dock=DockStyle.Bottom,Height=46,Text="DEMO PREVIEW — 2 bedrooms, bath, kitchen, living room, dining, hallway, entrance. No Sims 4 game input.",TextAlign=ContentAlignment.MiddleCenter };
        window.Controls.Add(note);
        var canvas=new PictureBox { Dock=DockStyle.Fill,BackColor=Color.FromArgb(245,242,231),SizeMode=PictureBoxSizeMode.Zoom };
        window.Controls.Add(canvas);
        canvas.BringToFront();
        var rooms=new (string Name,int X,int Y,int W,int H,Color Color)[] {
            ("LIVING ROOM",0,0,12,10,Color.FromArgb(204,224,231)),
            ("KITCHEN",12,0,8,7,Color.FromArgb(242,217,178)),
            ("DINING",12,7,8,3,Color.FromArgb(233,220,188)),
            ("HALL",0,10,20,3,Color.FromArgb(226,225,217)),
            ("BEDROOM 1",0,13,9,9,Color.FromArgb(214,206,232)),
            ("BATHROOM",9,13,5,9,Color.FromArgb(195,226,219)),
            ("BEDROOM 2",14,13,6,9,Color.FromArgb(232,208,221))
        };
        Bitmap Render() {
            var bmp=new Bitmap(1000,1080);
            using var g=Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(245,242,231));
            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var titleFont=new Font("Segoe UI",22,FontStyle.Bold);
            using var roomFont=new Font("Segoe UI",12,FontStyle.Bold);
            using var smallFont=new Font("Segoe UI",10);
            g.DrawString("SIMSCRAFT  |  BUILDCRAFT",titleFont,Brushes.Black,55,20);
            g.DrawString("DEMO HOUSE — 20 × 22 tiles  •  1 floor  •  2 bedrooms",smallFont,Brushes.Black,58,66);
            const int tile=39, left=105, upper=130;
            foreach(var room in rooms) {
                var rect=new Rectangle(left+room.X*tile,upper+room.Y*tile,room.W*tile,room.H*tile);
                using var brush=new SolidBrush(room.Color);
                g.FillRectangle(brush,rect);
                using var wallPen=new Pen(Color.FromArgb(42,50,60),8);
                g.DrawRectangle(wallPen,rect);
                var labelSize=g.MeasureString(room.Name,roomFont);
                g.DrawString(room.Name,roomFont,Brushes.Black,rect.X+(rect.Width-labelSize.Width)/2,rect.Y+(rect.Height-labelSize.Height)/2);
            }
            using var doorPen=new Pen(Color.White,13);
            // Diagrammatic door openings, not Sims 4 placement coordinates.
            g.DrawLine(doorPen,left+5*tile,upper+10*tile,left+7*tile,upper+10*tile);
            g.DrawLine(doorPen,left+15*tile,upper+10*tile,left+17*tile,upper+10*tile);
            g.DrawLine(doorPen,left+3*tile,upper+13*tile,left+5*tile,upper+13*tile);
            g.DrawLine(doorPen,left+10*tile,upper+13*tile,left+12*tile,upper+13*tile);
            g.DrawLine(doorPen,left+16*tile,upper+13*tile,left+18*tile,upper+13*tile);
            g.DrawLine(doorPen,left+9*tile,upper+22*tile,left+11*tile,upper+22*tile);
            g.DrawString("CONCEPT BLUEPRINT — NOT YET BUILT IN THE SIMS 4",smallFont,Brushes.DarkRed,110,1025);
            return bmp;
        }
        var picture=Render();
        canvas.Image=picture;
        export.Click+=(_,_)=>{
            using var dialog=new SaveFileDialog { Filter="PNG image (*.png)|*.png",FileName="simscraft-house-blueprint.png" };
            if(dialog.ShowDialog(window)==DialogResult.OK) {
                try { picture.Save(dialog.FileName,System.Drawing.Imaging.ImageFormat.Png); }
                catch(Exception ex) { MessageBox.Show(window,ex.Message,"Export failed"); }
            }
        };
        savePlan.Click+=(_,_)=>{
            using var dialog=new SaveFileDialog { Filter="JSON plan (*.json)|*.json",FileName="simscraft-house-plan.json" };
            if(dialog.ShowDialog(window)==DialogResult.OK) {
                try {
                    var plan=new { schema="simscraft-house-demo-v1",lotWidth=20,lotDepth=22,floors=1,
                        rooms=rooms.Select(r=>new { name=r.Name,x=r.X,y=r.Y,width=r.W,depth=r.H }).ToArray(),
                        constructionStatus="blueprint-only",gamePlacementVerified=false };
                    File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(plan,new JsonSerializerOptions { WriteIndented=true }));
                }catch(Exception ex){MessageBox.Show(window,ex.Message,"Export failed");}
            }
        };
        window.FormClosed+=(_,_)=>picture.Dispose();
        window.ShowDialog(this);
    }

    void BuildBuildcraftTab(TabPage page) {
        var p=Panel(page);
        Caption(p,"Buildcraft — Sims 4 Build Mode workspace (early prototype)");
        var demoButton=new Button { Text="DEMO: Generate Full House Blueprint",AutoSize=true,BackColor=Color.FromArgb(45,100,140),ForeColor=Color.White };
        demoButton.Click+=(_,_)=>ShowHouseDemo();
        p.Controls.Add(demoButton);
        Caption(p,"Demo generates a complete multi-room blueprint and PNG export; it does NOT construct the house inside Sims 4.");
        Caption(p,"Describe the building or lot");
        var prompt=new TextBox { Width=760,Height=110,Multiline=true,ScrollBars=ScrollBars.Vertical };
        p.Controls.Add(prompt);
        // Reference images are project-local and never initiate Sims 4 input.
        var referenceDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Simscraft","Buildcraft","DefaultProject");
        var referenceStatus=new Label { Text="References are saved in the local Buildcraft project. No game actions are triggered.",AutoSize=true,ForeColor=Color.White };
        var referenceList=new FlowLayoutPanel { Width=760,Height=172,AutoScroll=true,WrapContents=true,FlowDirection=FlowDirection.LeftToRight,BackColor=Color.FromArgb(30,38,54) };
        Simcraft.Buildcraft.BuildReferenceProject referenceProject;
        try { referenceProject=Simcraft.Buildcraft.BuildReferenceStore.Load(referenceDirectory); }
        catch(Exception ex) { referenceProject=new Simcraft.Buildcraft.BuildReferenceProject(); referenceStatus.Text="Could not load saved references: "+ex.Message; }
        if(string.IsNullOrWhiteSpace(prompt.Text)) prompt.Text=referenceProject.Prompt;
        void RefreshReferenceList() {
            referenceList.Controls.Clear();
            foreach(var reference in referenceProject.References.ToArray()) {
                var tile=new Panel { Width=174,Height=145,BackColor=Color.FromArgb(44,54,73) };
                var picture=new PictureBox { Left=7,Top=5,Width=158,Height=94,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(24,31,48) };
                var path=Path.Combine(referenceDirectory,"references",reference.FileName);
                try {
                    // System.Drawing does not decode WEBP on all Windows installations.
                    // Do not display a guessed or misleading preview when unsupported.
                    using var source=Image.FromFile(path);
                    picture.Image=new Bitmap(source);
                } catch { picture.Image=null; }
                tile.Controls.Add(picture);
                var name=new Label { Text=reference.OriginalName+" ("+reference.Width+"×"+reference.Height+")",Left=7,Top=101,Width=158,Height=19,AutoEllipsis=true,ForeColor=Color.White };
                tile.Controls.Add(name);
                var remove=new Button { Text="Remove",Left=7,Top=121,Width=75,Height=22 };
                remove.Click+=(_,_)=>{
                    referenceProject.References.RemoveAll(x=>x.Id==reference.Id);
                    try { Simcraft.Buildcraft.BuildReferenceStore.Save(referenceDirectory,referenceProject); RefreshReferenceList(); }
                    catch(Exception ex) { referenceStatus.Text="Could not save reference removal: "+ex.Message; }
                };
                tile.Controls.Add(remove);
                referenceList.Controls.Add(tile);
                tile.Disposed+=(_,_)=>picture.Image?.Dispose();
            }
        }
        var importReference=new Button { Text="Import Reference Images (PNG / JPG / WEBP)",AutoSize=true };
        importReference.Click+=(_,_)=>{
            using var dialog=new OpenFileDialog { Filter="Images|*.png;*.jpg;*.jpeg;*.webp",Multiselect=true,Title="Choose Buildcraft reference images" };
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            int imported=0;
            foreach(var file in dialog.FileNames) {
                try { Simcraft.Buildcraft.BuildReferenceStore.Import(referenceDirectory,file); imported++; }
                catch(Exception ex) { referenceStatus.Text="Import failed for "+Path.GetFileName(file)+": "+ex.Message; }
            }
            try { referenceProject=Simcraft.Buildcraft.BuildReferenceStore.Load(referenceDirectory); RefreshReferenceList(); if(imported==dialog.FileNames.Length)referenceStatus.Text=imported+" reference(s) imported. Image interpretation and automatic construction are not implemented."; }
            catch(Exception ex) { referenceStatus.Text="Could not refresh references: "+ex.Message; }
        };
        prompt.Leave+=(_,_)=>{
            try { referenceProject.Prompt=prompt.Text; Simcraft.Buildcraft.BuildReferenceStore.Save(referenceDirectory,referenceProject); }
            catch(Exception ex) { referenceStatus.Text="Could not save prompt: "+ex.Message; }
        };
        p.Controls.Add(importReference);
        p.Controls.Add(referenceStatus);
        p.Controls.Add(referenceList);
        RefreshReferenceList();
        Caption(p,"Lot width (tiles)");
        var width=new NumericUpDown { Minimum=10,Maximum=64,Value=30,Width=120 };
        p.Controls.Add(width);
        Caption(p,"Lot depth (tiles)");
        var depth=new NumericUpDown { Minimum=10,Maximum=64,Value=20,Width=120 };
        p.Controls.Add(depth);
        var structuredPreview=new TextBox { Width=760,Height=145,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical };
        var previewDraft=new Button { Text="Preview structured footprint (no game input)",AutoSize=true };
        previewDraft.Click+=(_,_)=>{
            try {
                var draft=Simcraft.Buildcraft.BuildPromptPlanner.Preview(prompt.Text,(int)width.Value,(int)depth.Value,referenceProject.References);
                var lines=new List<string> {
                    "STRUCTURED DRAFT — PREVIEW ONLY",
                    "Footprint: "+draft.Width+" × "+draft.Depth+" tiles",
                    "Commands: "+draft.Plan.Commands.Count+" (4 perimeter walls + floor)",
                    "Reference images linked: "+draft.ReferenceIds.Count,
                    "Camera: unchanged; no game commands sent."
                };
                lines.AddRange(draft.Warnings.Select(x=>"NOTE: "+x));
                structuredPreview.Lines=lines.ToArray();
            } catch(Exception ex) { structuredPreview.Text="Cannot generate draft: "+ex.Message; }
        };
        p.Controls.Add(previewDraft);
        p.Controls.Add(structuredPreview);
        var status=new Label { Text="Checking Sims 4 connection...",AutoSize=true,ForeColor=Color.White };
        var detect=new Button { Text="Detect The Sims 4",AutoSize=true };
        detect.Click+=(_,_)=>{
            try {
                var games=Process.GetProcesses().Where(x=>x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase)).ToArray();
                status.Text=games.Length>0?"The Sims 4 detected. Build/Buy detection runs automatically while this tab is open.":"The Sims 4 not detected.";
                foreach(var game in games)game.Dispose();
            }catch(Exception ex){status.Text="Detection failed: "+ex.Message;}
        };
        p.Controls.Add(detect);p.Controls.Add(status);
        var verify=new Button { Text="Check Build/Buy now",AutoSize=true };
        async System.Threading.Tasks.Task CheckBuildModeAsync() {
            if(!SimsRunning()) { status.Text="The Sims 4 is not running.";return; }
            try {
                // Never hide or reactivate the Studio window during background polling.
                // CopyFromScreen reads the visible desktop; if Studio is in front,
                // skip rather than incorrectly interpreting its UI as Sims Build Mode.
                if(ContainsSimsWindowOcclusion()) {
                    status.Text="The Sims 4 detected. Build/Buy visual check paused while Simscraft overlaps the game.";
                    return;
                }
                using var current=await System.Threading.Tasks.Task.Run(()=>CaptureSimsWindow());
                var detection=InspectBuildBuyUi(current);
                status.Text=detection.Likely
                    ? "Build/Buy interface likely detected automatically ("+detection.Detail+"). Visual estimate only."
                    : "Sims 4 detected; Build/Buy catalog not confidently recognized ("+detection.Detail+"). Open the Build/Buy catalog.";
            } catch(Exception ex) { status.Text="Automatic Build/Buy check failed: "+ex.Message; }
        }
        bool checkingMode=false;
        verify.Click+=async (_,_)=>{
            if(checkingMode)return;
            checkingMode=true;verify.Enabled=false;
            try { await CheckBuildModeAsync(); }
            finally { checkingMode=false;verify.Enabled=true; }
        };
        p.Controls.Add(verify);
        Caption(p,"Construction controller — safety-first preview");
        var constructionState=new Label { Text="Idle. No game input authorized.",AutoSize=true,ForeColor=Color.White };
        var allowControl=new CheckBox { Text="I approve Buildcraft controlling The Sims 4 after verification",AutoSize=true,ForeColor=Color.White };
        var arm=new Button { Text="Arm construction (verify mode)",AutoSize=true };
        var stop=new Button { Text="EMERGENCY STOP",AutoSize=true,BackColor=Color.DarkRed,ForeColor=Color.White };
        var constructionCancel=new System.Threading.CancellationTokenSource();
        bool armed=false;
        bool gridReady=false;
        stop.Click+=(_,_)=>{
            constructionCancel.Cancel();
            constructionCancel.Dispose();
            constructionCancel=new System.Threading.CancellationTokenSource();
            armed=false;
            allowControl.Checked=false;
            constructionState.Text="STOPPED. Construction disarmed. No game input will be sent.";
        };
        arm.Click+=async (_,_)=>{
            if(!allowControl.Checked){constructionState.Text="Approve game control first.";return;}
            if(string.IsNullOrWhiteSpace(prompt.Text)){constructionState.Text="Describe the house before arming.";return;}
            if(!SimsRunning()){constructionState.Text="The Sims 4 is not running.";return;}
            arm.Enabled=false;
            constructionState.Text="Checking Build/Buy without hiding or resetting Simscraft...";
            try {
                // Do not Hide()/Show() the main window during ARM: that can
                // recreate the visible form and appear to reset the workspace.
                // A blocked capture is a recoverable preflight failure.
                if(ContainsSimsWindowOcclusion()) {
                    constructionState.Text="Simscraft overlaps the Sims 4 capture rectangle. Move the windows apart, then retry ARM.";
                    return;
                }
                using var current=await System.Threading.Tasks.Task.Run(()=>CaptureSimsWindow());
                var detection=InspectBuildBuyUi(current);
                if(!detection.Likely) {
                    constructionState.Text="Not armed: Build/Buy not confirmed ("+detection.Detail+").";
                    return;
                }
                armed=true;
                constructionState.Text=gridReady
                    ?"ARMED. Existing grid calibration retained. Confirm camera and Wall Tool, then run the 4x4 test."
                    :"ARMED. Build/Buy detected, but no grid calibration exists yet. Calibrate once on this lot.";
            } catch(Exception ex) {
                armed=false;
                constructionState.Text="ARM failed safely: "+ex.Message;
            } finally {
                if(!arm.IsDisposed)arm.Enabled=true;
            }
        };
        p.Controls.Add(allowControl);p.Controls.Add(arm);p.Controls.Add(stop);p.Controls.Add(constructionState);
        page.Disposed+=(_,_)=>constructionCancel.Dispose();
        // Poll while Buildcraft is selected. The capture briefly hides our window
        // so it cannot accidentally be mistaken for the game's bottom catalog.
        var connectionMonitor=new System.Windows.Forms.Timer { Interval=8000 };
        connectionMonitor.Tick+=async (_,_)=>{
            if(status.IsDisposed || tabs.SelectedTab!=page || checkingMode)return;
            if(!SimsRunning()){status.Text="The Sims 4 is not running.";return;}
            checkingMode=true;
            try { await CheckBuildModeAsync(); }
            finally { checkingMode=false; }
        };
        connectionMonitor.Start();
        page.Disposed+=(_,_)=>connectionMonitor.Dispose();
        status.Text=SimsRunning()?"The Sims 4 detected. Automatic Build/Buy monitoring is active.":"The Sims 4 is not running.";
        Caption(p,"Lot grid calibration — three tile-center clicks (dry run)");
        var gridStatus=new Label { Text="Not calibrated. Enter Build Mode, then start calibration.",AutoSize=true,ForeColor=Color.White };
        var startGrid=new Button { Text="Calibrate lot grid (3 clicks)",AutoSize=true };
        var clearGrid=new Button { Text="Reset grid",AutoSize=true };
        var gridPreview=new TextBox { Width=760,Height=105,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical };
        var gridPoints=new List<System.Drawing.Point>();
        bool collectingGrid=false,gridMouseWasDown=false;
        System.Drawing.Point gridOrigin=default,gridWidthStep=default,gridDepthStep=default;
        var gridTimer=new System.Windows.Forms.Timer { Interval=40 };
        var autoGrid=new Button { Text="Analyze Grid Spacing (origin requires 3 clicks)",AutoSize=true };
        autoGrid.Click+=async (_,_)=>{
            if(!SimsRunning()){gridStatus.Text="Launch The Sims 4 and open the intended lot in Build Mode.";return;}
            if(collectingGrid){gridStatus.Text="Finish the three grid clicks or Reset grid first.";return;}
            if(gridReady){gridStatus.Text="Grid already calibrated. Existing calibration preserved; use Reset grid only if lot or camera changed.";return;}
            autoGrid.Enabled=false;
            try {
                gridStatus.Text="Analyzing the visible Sims 4 grid. Keep the intended lot visible.";
                // Capture the actual game window; do not require focus or hide/reopen Studio.
                using var screenshot=await System.Threading.Tasks.Task.Run(()=>CaptureSimsWindow());
                var estimate=await System.Threading.Tasks.Task.Run(()=>EstimateLotGrid(screenshot));
                gridPreview.Text="Grid spacing confidence: "+estimate.Confidence.ToString("P0")+
                    ". Estimated horizontal spacing: "+estimate.StepX+" px, vertical: "+estimate.StepY+
                    " px."+Environment.NewLine+
                    "The image cannot reliably determine the active lot or its grid origin. Click three adjacent tile centers on the intended lot to finish calibration.";
                gridStatus.Text=estimate.Found
                    ?"Grid spacing found. Complete three-click origin calibration on the intended lot."
                    :"Grid spacing uncertain. Complete three-click calibration on the intended lot.";
                // Continue to the interactive calibration phase rather than dead-ending.
                gridPoints.Clear();
                collectingGrid=true;
                gridMouseWasDown=(GetAsyncKeyState(0x01)&0x8000)!=0;
                gridStatus.Text+=" Click tile 1 of 3 in Sims 4 now.";
                gridTimer.Start();
            }catch(Exception ex){
                gridStatus.Text="Grid analysis unavailable: "+ex.Message+". Manual calibration remains available.";
                gridPreview.Text="Open the correct lot, choose Build Mode, and click Calibrate lot grid (3 clicks).";
            }finally{autoGrid.Enabled=true;}
        };
        p.Controls.Add(autoGrid);
        startGrid.Click+=(_,_)=>{
            if(!SimsRunning()){gridStatus.Text="Launch The Sims 4 before calibrating.";return;}
            gridPoints.Clear();gridReady=false;collectingGrid=true;gridMouseWasDown=true;
            gridStatus.Text="On the INTENDED lot, click the CENTER of a visible buildable tile in Sims 4 (point 1 of 3).";
            gridPreview.Text="First open the intended lot in Build Mode. Click 1: tile center at desired wall location. Click 2: adjacent tile along WIDTH. Click 3: adjacent tile along DEPTH. Keep camera and lot fixed.";
            gridTimer.Start();
        };
        clearGrid.Click+=(_,_)=>{
            collectingGrid=false;gridTimer.Stop();gridPoints.Clear();gridReady=false;
            gridStatus.Text="Grid calibration reset.";gridPreview.Clear();
        };
        gridTimer.Tick+=(_,_)=>{
            if(!collectingGrid)return;
            bool down=(GetAsyncKeyState(0x01)&0x8000)!=0;
            bool fresh=down&&!gridMouseWasDown;
            gridMouseWasDown=down;
            if(!fresh)return;
            using var game=Process.GetProcesses().FirstOrDefault(x=>
                (x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||
                 x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase))&&
                x.MainWindowHandle==GetForegroundWindow());
            if(game==null||!GetCursorPos(out var cursor))return;
            gridPoints.Add(cursor);
            if(gridPoints.Count<3){
                gridStatus.Text=gridPoints.Count==1
                    ? "Origin captured. Click the CENTER of the next tile along lot WIDTH (point 2)."
                    : "Width step captured. Click the CENTER of the tile next to the origin along lot DEPTH (point 3).";
                return;
            }
            collectingGrid=false;gridTimer.Stop();
            gridOrigin=gridPoints[0];
            gridWidthStep=new System.Drawing.Point(gridPoints[1].X-gridOrigin.X,gridPoints[1].Y-gridOrigin.Y);
            gridDepthStep=new System.Drawing.Point(gridPoints[2].X-gridOrigin.X,gridPoints[2].Y-gridOrigin.Y);
            double cross=(double)gridWidthStep.X*gridDepthStep.Y-(double)gridWidthStep.Y*gridDepthStep.X;
            double wlen=Math.Sqrt((double)gridWidthStep.X*gridWidthStep.X+(double)gridWidthStep.Y*gridWidthStep.Y);
            double dlen=Math.Sqrt((double)gridDepthStep.X*gridDepthStep.X+(double)gridDepthStep.Y*gridDepthStep.Y);
            gridReady=wlen>=5&&dlen>=5&&Math.Abs(cross)>wlen*dlen*.15;
            if(!gridReady){
                gridStatus.Text="Calibration rejected: choose three distinct adjacent tile centers forming two grid directions. Try again.";
                return;
            }
            gridStatus.Text="Screen grid calibrated, NOT lot-verified. Confirm the footprint lies on the intended lot. Keep camera, zoom and window position unchanged.";
            gridPreview.Text="Origin: ("+gridOrigin.X+","+gridOrigin.Y+") | Width/tile: ("+gridWidthStep.X+","+gridWidthStep.Y+") | Depth/tile: ("+gridDepthStep.X+","+gridDepthStep.Y+")"+
                Environment.NewLine+"Tile (x,y) maps to origin + x * width vector + y * depth vector. Preview only — no mouse input.";
        };
        p.Controls.Add(startGrid);p.Controls.Add(clearGrid);p.Controls.Add(gridStatus);p.Controls.Add(gridPreview);
        // Camera setup is explicit because the Sims 4 T shortcut toggles top-down.
        // Never silently toggle during wall drags or reuse calibration afterwards.
        var cameraTopDown=new CheckBox { Text="Sims 4 camera is already in top-down view",AutoSize=true,ForeColor=Color.White };
        var cameraButton=new Button { Text="Set camera: toggle top-down (T)",AutoSize=true };
        var cameraStatus=new Label { Text="Camera not confirmed. Camera changes invalidate the grid.",AutoSize=true,ForeColor=Color.White };
        cameraButton.Click+=async (_,_)=>{
            if(!SimsRunning()){cameraStatus.Text="Launch The Sims 4 first.";return;}
            if(MessageBox.Show(this,"Sims 4 uses T to TOGGLE top-down view. If already top-down, leave it alone and check the confirmation box instead. Send T after switching to Sims 4?","Camera setup",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            cameraButton.Enabled=false;
            cameraTopDown.Checked=false;gridReady=false;gridPoints.Clear();
            gridStatus.Text="Camera changed. Recalibrate the three tile centers.";
            cameraStatus.Text="Switch to Sims 4 within 10 seconds to toggle the camera.";
            try {
                bool focused=false;
                for(int i=0;i<50;i++){
                    constructionCancel.Token.ThrowIfCancellationRequested();
                    if((GetAsyncKeyState(0x1B)&0x8000)!=0)throw new OperationCanceledException();
                    using var game=Process.GetProcesses().FirstOrDefault(x=>
                        (x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||
                         x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase))&&
                         x.MainWindowHandle==GetForegroundWindow());
                    if(game!=null){focused=true;break;}
                    await System.Threading.Tasks.Task.Delay(200,constructionCancel.Token);
                }
                if(!focused)throw new InvalidOperationException("Sims 4 was not focused in time.");
                // Send the documented T toggle only while the game is foreground.
                keybd_event(0x54,0,0,UIntPtr.Zero);
                await System.Threading.Tasks.Task.Delay(80,constructionCancel.Token);
                keybd_event(0x54,0,0x0002,UIntPtr.Zero);
                cameraStatus.Text="T sent. Check the view is top-down, tick confirmation, then recalibrate grid.";
            }catch(OperationCanceledException){cameraStatus.Text="Camera setup canceled.";}
             catch(Exception ex){cameraStatus.Text="Camera setup failed: "+ex.Message;}
            finally{cameraButton.Enabled=true;}
        };
        cameraTopDown.CheckedChanged+=(_,_)=>{
            if(!cameraTopDown.Checked){gridReady=false;gridStatus.Text="Camera not confirmed; grid calibration invalidated.";}
        };
        p.Controls.Add(cameraButton);p.Controls.Add(cameraTopDown);p.Controls.Add(cameraStatus);
        var wallToolReady=new CheckBox { Text="I selected the straight Wall Tool in Sims 4, and the calibrated area is empty",AutoSize=true,ForeColor=Color.White };
        var buildTest=new Button { Text="BUILD TEST: place 4x4 room walls",AutoSize=true };
        var buildProgress=new Label { Text="Ready for first wall-placement test. ESC aborts.",AutoSize=true,ForeColor=Color.White };
        bool building=false;
        bool SimsForeground() {
            using var game=Process.GetProcesses().FirstOrDefault(x=>
                (x.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||
                 x.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase))&&
                x.MainWindowHandle==GetForegroundWindow());
            return game!=null;
        }
        buildTest.Click+=async (_,_)=>{
            if(building)return;
            // One-click preflight: verify game mode and arm automatically, but
            // never assume the camera, tool, or origin from a screen heuristic.
            if(!SimsRunning()){buildProgress.Text="The Sims 4 is not running.";return;}
            if(string.IsNullOrWhiteSpace(prompt.Text)){buildProgress.Text="Enter a house description first.";return;}
            if(!gridReady){
                buildProgress.Text="A verified tile origin is required. Auto spacing alone is insufficient.";
                MessageBox.Show(this,"Automatic origin detection is not implemented yet. Calibrate three tile centers before the live wall test.","Buildcraft calibration required",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }
            if(!cameraTopDown.Checked||!wallToolReady.Checked){
                buildProgress.Text="Confirm top-down camera and straight Wall Tool before live placement.";
                MessageBox.Show(this,"Buildcraft cannot yet reliably recognize the camera angle or selected wall tool. Set top-down view, select the straight Wall Tool, and check both confirmations. The other setup checks will run automatically.","Buildcraft tool verification",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }
            if(!allowControl.Checked){
                if(MessageBox.Show(this,"Allow Buildcraft to control the mouse in The Sims 4 for this four-wall test? It may spend in-game funds and modify your lot.","Approve game control",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
                allowControl.Checked=true;
            }
            if(!armed){
                buildProgress.Text="Automatically checking Build Mode and arming...";
                try{
                    using var current=await CaptureSimsUnobstructedAsync();
                    var detection=InspectBuildBuyUi(current);
                    if(!detection.Likely){
                        buildProgress.Text="Cannot arm automatically: Build/Buy screen not recognized ("+detection.Detail+").";
                        return;
                    }
                    armed=true;
                    constructionState.Text="Auto-armed after Build/Buy visual check ("+detection.Detail+").";
                }catch(Exception ex){
                    buildProgress.Text="Automatic preflight failed: "+ex.Message;
                    return;
                }
            }
            // Test a small room around the calibrated origin, not the full plan.
            System.Drawing.Point Project(int x,int y)=>new(
                gridOrigin.X+x*gridWidthStep.X+y*gridDepthStep.X,
                gridOrigin.Y+x*gridWidthStep.Y+y*gridDepthStep.Y);
            var corners=new[]{Project(0,0),Project(4,0),Project(4,4),Project(0,4)};
            var bounds=Screen.AllScreens.Select(x=>x.Bounds).ToArray();
            if(corners.Any(c=>!bounds.Any(b=>b.Contains(c)))) {
                buildProgress.Text="Room corners are outside the display. Recalibrate with the camera unchanged.";
                return;
            }
            if(MessageBox.Show(this,"Buildcraft CANNOT detect the active lot or verify the 4x4 footprint. The walls will be placed relative to your FIRST calibration click. Confirm that click was on the intended buildable lot, and all four corners are within its boundaries. Save first. Select the straight Wall Tool and switch to Sims within 12 seconds. ESC stops. Continue?","Confirm live wall placement",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            building=true;buildTest.Enabled=false;checkingMode=true;
            string cancelReason="Canceled by user or safety control.";
            int focusMisses=0;
            void CheckWallSafety() {
                constructionCancel.Token.ThrowIfCancellationRequested();
                if((GetAsyncKeyState(0x1B)&0x8000)!=0){cancelReason="ESC pressed.";throw new OperationCanceledException();}
                if(SimsForeground()){focusMisses=0;return;}
                if(++focusMisses>=10){cancelReason="Sims 4 lost foreground focus during wall placement.";throw new OperationCanceledException();}
            }
            buildProgress.Text="Waiting for Sims 4 to become foreground (12 seconds). ESC cancels.";
            try {
                bool ready=false;
                for(int i=0;i<60;i++){
                    constructionCancel.Token.ThrowIfCancellationRequested();
                    if((GetAsyncKeyState(0x1B)&0x8000)!=0)throw new OperationCanceledException();
                    if(SimsForeground()){ready=true;break;}
                    await System.Threading.Tasks.Task.Delay(200,constructionCancel.Token);
                }
                if(!ready)throw new InvalidOperationException("Sims 4 was not activated in time.");
                cancelReason="Wall placement interrupted; inspect focus and ESC state.";
                for(int side=0;side<4;side++){
                    constructionCancel.Token.ThrowIfCancellationRequested();
                    CheckWallSafety();
                    var from=corners[side];var to=corners[(side+1)%4];
                    SetCursorPos(from.X,from.Y);
                    await System.Threading.Tasks.Task.Delay(150,constructionCancel.Token);
                    CheckWallSafety();
                    mouse_event(0x0002,0,0,0,UIntPtr.Zero); // left down
                    try {
                        for(int step=1;step<=12;step++){
                            CheckWallSafety();
                            constructionCancel.Token.ThrowIfCancellationRequested();
                            SetCursorPos(from.X+(to.X-from.X)*step/12,from.Y+(to.Y-from.Y)*step/12);
                            await System.Threading.Tasks.Task.Delay(30,constructionCancel.Token);
                        }
                    }finally{mouse_event(0x0004,0,0,0,UIntPtr.Zero);} // always release
                    buildProgress.Text="Wall "+(side+1)+" of 4 dragged. Check the game for actual placement.";
                    await System.Threading.Tasks.Task.Delay(350,constructionCancel.Token);
                }
                buildProgress.Text="Four wall drags sent. Inspect Sims 4 to confirm the walls were placed correctly.";
            }catch(OperationCanceledException){buildProgress.Text="STOPPED: "+(constructionCancel.IsCancellationRequested?"Emergency Stop activated.":cancelReason);}
             catch(Exception ex){buildProgress.Text="Wall placement halted: "+ex.Message;}
            finally{building=false;armed=false;buildTest.Enabled=true;checkingMode=false;}
        };
        p.Controls.Add(wallToolReady);p.Controls.Add(buildTest);p.Controls.Add(buildProgress);
        // Assisted automatic construction: generates and executes a small house shell
        // with two interior partitions. The grid must be calibrated on the intended lot.
        var autoHouse=new Button { Text="AUTO BUILD HOUSE — from prompt (wall layout)",AutoSize=true };
        autoHouse.Click+=async (_,_)=>{
            if(building){buildProgress.Text="Another construction is running.";return;}
            if(!SimsRunning()||!gridReady||!cameraTopDown.Checked||!wallToolReady.Checked){
                buildProgress.Text="Auto Build needs Sims 4, a calibrated lot, top-down camera and selected straight Wall Tool.";
                return;
            }
            if(!allowControl.Checked){buildProgress.Text="Approve game control before Auto Build.";return;}
            if(string.IsNullOrWhiteSpace(prompt.Text)){buildProgress.Text="Describe the house first.";return;}
            // Translate supported prompt requirements into a variable wall plan.
            // Unsupported features must not silently become a generic 8x8 house.
            var request=prompt.Text.ToLowerInvariant();
            if(request.Contains("two-story")||request.Contains("two story")||request.Contains("2-story")||
               request.Contains("upstairs")||request.Contains("second floor")||
               request.Contains("roof")||request.Contains("furnish")||request.Contains("window")||
               request.Contains("door")||request.Contains("stairs")||request.Contains("garage")){
                buildProgress.Text="Prompt requests unsupported construction (upper floors, roof, furnishings, openings or garage). This version builds single-story wall layouts only; no construction started.";
                return;
            }
            int CountRoom(string word){
                var match=System.Text.RegularExpressions.Regex.Match(request,@"(\d+)\s*"+word);
                if(match.Success&&int.TryParse(match.Groups[1].Value,out int n))return Math.Clamp(n,1,8);
                if(request.Contains("three "+word))return 3;
                if(request.Contains("two "+word))return 2;
                if(request.Contains("four "+word))return 4;
                return request.Contains(word)?1:0;
            }
            int beds=CountRoom("bedroom"),baths=CountRoom("bathroom");
            bool kitchen=request.Contains("kitchen");
            int rooms=Math.Max(2,beds+baths+(kitchen?1:0)+1);
            int lotWidth=(int)width.Value,lotDepth=(int)depth.Value;
            int houseWidth=Math.Clamp(6+Math.Max(0,rooms-2)*2,8,Math.Min(20,lotWidth-4));
            int houseDepth=Math.Clamp(6+Math.Max(0,rooms-2),8,Math.Min(18,lotDepth-4));
            var wallSegments=new List<(int X1,int Y1,int X2,int Y2,string Name)>{
                (0,0,houseWidth,0,"North exterior"),
                (houseWidth,0,houseWidth,houseDepth,"East exterior"),
                (houseWidth,houseDepth,0,houseDepth,"South exterior"),
                (0,houseDepth,0,0,"West exterior")
            };
            // Divide the footprint into room bays based on requested room count.
            int bayCount=Math.Min(rooms,Math.Max(2,houseWidth/3));
            for(int bay=1;bay<bayCount;bay++){
                int x=houseWidth*bay/bayCount;
                wallSegments.Add((x,0,x,houseDepth/2,"Room divider "+bay));
            }
            wallSegments.Add((0,houseDepth/2,houseWidth,houseDepth/2,"Living-area divider"));
            var segments=wallSegments.ToArray();
            System.Drawing.Point Tile(int x,int y)=>new(
                gridOrigin.X+x*gridWidthStep.X+y*gridDepthStep.X,
                gridOrigin.Y+x*gridWidthStep.Y+y*gridDepthStep.Y);
            var allPoints=segments.SelectMany(w=>new[]{Tile(w.X1,w.Y1),Tile(w.X2,w.Y2)}).ToArray();
            var screens=Screen.AllScreens.Select(x=>x.Bounds).ToArray();
            // Edge scrolling can move the Sims camera and invalidate every calibrated point.
            // Keep every planned drag comfortably inside the game window, not merely on-screen.
            using var simsWindow=Process.GetProcesses().FirstOrDefault(p=>
                (p.ProcessName.Contains("TS4",StringComparison.OrdinalIgnoreCase)||
                 p.ProcessName.Contains("Sims4",StringComparison.OrdinalIgnoreCase))&&p.MainWindowHandle!=IntPtr.Zero);
            if(simsWindow==null||!GetWindowRect(simsWindow.MainWindowHandle,out var gameRect)){
                buildProgress.Text="Cannot verify Sims 4 window bounds. Construction stopped.";return;
            }
            var safeGameArea=System.Drawing.Rectangle.FromLTRB(
                gameRect.Left+140,gameRect.Top+140,gameRect.Right-140,gameRect.Bottom-190);
            if(allPoints.Any(pt=>!safeGameArea.Contains(pt))){
                buildProgress.Text="Camera locked: planned walls would enter an edge-scroll or UI zone. No input sent. Keep your camera fixed; adjust the blueprint footprint or recalibrate.";
                return;
            }
            if(allPoints.Any(pt=>!screens.Any(rect=>rect.Contains(pt)))){
                buildProgress.Text="Planned house extends outside the display. Reposition the camera and recalibrate.";
                return;
            }
            if(MessageBox.Show(this,
                "AUTO BUILD WALL PLAN: "+houseWidth+"x"+houseDepth+" tiles, "+segments.Length+" wall segments. Prompt: "+prompt.Text+Environment.NewLine+
                "This will modify your Sims 4 lot and spend in-game funds. The app cannot verify lot boundaries. "+
                "Confirm ALL projected corners are on the intended empty lot and save your game first. "+
                "Keep Sims 4 foreground and the straight Wall Tool selected. Continue?",
                "Approve automatic house construction",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            building=true;checkingMode=true;autoHouse.Enabled=false;buildTest.Enabled=false;
            buildProgress.Text="Auto Build: switch to Sims 4 within 12 seconds. ESC stops.";
            bool mouseHeld=false;
            try{
                bool focused=false;
                for(int attempt=0;attempt<60;attempt++){
                    constructionCancel.Token.ThrowIfCancellationRequested();
                    if((GetAsyncKeyState(0x1B)&0x8000)!=0)throw new OperationCanceledException("ESC pressed.");
                    if(SimsForeground()){focused=true;break;}
                    await System.Threading.Tasks.Task.Delay(200,constructionCancel.Token);
                }
                if(!focused)throw new InvalidOperationException("Sims 4 did not become foreground.");
                for(int i=0;i<segments.Length;i++){
                    // Camera remains user-controlled. Never send pan, zoom or rotate commands.
                    // Verify the mouse path remains within the central safe area.
                    constructionCancel.Token.ThrowIfCancellationRequested();
                    if((GetAsyncKeyState(0x1B)&0x8000)!=0)throw new OperationCanceledException("ESC pressed.");
                    if(!SimsForeground())throw new InvalidOperationException("Sims 4 lost focus; stopped before the next wall.");
                    var w=segments[i];
                    var from=Tile(w.X1,w.Y1);var to=Tile(w.X2,w.Y2);
                    if(!safeGameArea.Contains(from)||!safeGameArea.Contains(to))throw new InvalidOperationException("Camera lock: wall path approaches screen edge. No further placement attempted.");
                    SetCursorPos(from.X,from.Y);
                    await System.Threading.Tasks.Task.Delay(160,constructionCancel.Token);
                    if(!SimsForeground())throw new InvalidOperationException("Sims 4 lost focus before dragging.");
                    mouse_event(0x0002,0,0,0,UIntPtr.Zero);
                    mouseHeld=true;
                    try{
                        for(int step=1;step<=20;step++){
                            constructionCancel.Token.ThrowIfCancellationRequested();
                            if((GetAsyncKeyState(0x1B)&0x8000)!=0)throw new OperationCanceledException("ESC pressed.");
                            if(!SimsForeground())throw new InvalidOperationException("Sims 4 lost focus while dragging.");
                            SetCursorPos(from.X+(to.X-from.X)*step/20,from.Y+(to.Y-from.Y)*step/20);
                            await System.Threading.Tasks.Task.Delay(35,constructionCancel.Token);
                        }
                    }finally{
                        mouse_event(0x0004,0,0,0,UIntPtr.Zero);
                        mouseHeld=false;
                    }
                    buildProgress.Text="Auto Build: "+(i+1)+"/"+segments.Length+" wall segments sent — "+w.Name;
                    await System.Threading.Tasks.Task.Delay(450,constructionCancel.Token);
                }
                buildProgress.Text="Auto Build wall plan sent: "+segments.Length+" drags. Inspect the lot; doors, roof, floors and furniture are not built yet.";
            }catch(OperationCanceledException ex){
                buildProgress.Text="Auto Build stopped: "+(constructionCancel.IsCancellationRequested?"Emergency Stop.":ex.Message);
            }catch(Exception ex){buildProgress.Text="Auto Build halted: "+ex.Message;}
            finally{
                if(mouseHeld)mouse_event(0x0004,0,0,0,UIntPtr.Zero);
                building=false;checkingMode=false;armed=false;
                autoHouse.Enabled=true;buildTest.Enabled=true;
            }
        };
        p.Controls.Add(autoHouse);


        page.Disposed+=(_,_)=>gridTimer.Dispose();
        var output=new TextBox { Width=760,Height=230,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical };
        var plan=new Button { Text="Create starter build plan",AutoSize=true };
        plan.Click+=(_,_)=>{
            if(string.IsNullOrWhiteSpace(prompt.Text)){status.Text="Describe the building first.";return;}
            int lotW=(int)width.Value,lotD=(int)depth.Value;
            // Conservative starter footprint with a two-tile lot margin.
            int houseW=Math.Max(6,lotW-4),houseD=Math.Max(6,lotD-4);
            int startX=(lotW-houseW)/2,startY=(lotD-houseD)/2;
            var description=prompt.Text.Trim();
            var lower=description.ToLowerInvariant();
            bool garage=lower.Contains("garage");
            bool kitchen=lower.Contains("kitchen");
            bool bath=lower.Contains("bath");
            bool bedroom=lower.Contains("bedroom");
            var rooms=new List<string>{"Entry / living area"};
            if(kitchen)rooms.Add("Kitchen");
            if(bath)rooms.Add("Bathroom");
            if(bedroom)rooms.Add("Bedroom(s)");
            if(garage)rooms.Add("Garage (space allocation pending)");
            var map=new System.Text.StringBuilder();
            for(int y=0;y<lotD;y++){
                for(int x=0;x<lotW;x++){
                    bool wall=(x>=startX&&x<startX+houseW&&
                        (y==startY||y==startY+houseD-1))||
                        (y>=startY&&y<startY+houseD&&
                        (x==startX||x==startX+houseW-1));
                    // Entrance opening on south wall (visual plan only).
                    bool door=y==startY+houseD-1&&x==startX+houseW/2;
                    map.Append(door?'D':wall?'#':'.');
                }
                map.AppendLine();
            }
            output.Text="BUILDCRAFT — STARTER FOOTPRINT (PREVIEW ONLY)"+Environment.NewLine+
                "Lot: "+lotW+" x "+lotD+" tiles"+Environment.NewLine+
                "House: "+houseW+" x "+houseD+" tiles, origin ("+startX+", "+startY+")"+Environment.NewLine+
                "Requested rooms: "+string.Join(", ",rooms)+Environment.NewLine+
                "Description: "+description+Environment.NewLine+Environment.NewLine+
                "Top-down grid (# perimeter wall, D entry opening, . unoccupied):"+Environment.NewLine+
                map.ToString()+Environment.NewLine+
                "Room partitions, furniture, floor levels, and game placement are not generated yet. No game input sent.";
            if(gridReady) {
                int x0=startX,y0=startY,x1=startX+houseW-1,y1=startY+houseD-1;
                System.Drawing.Point Project(int x,int y)=>new(
                    gridOrigin.X+x*gridWidthStep.X+y*gridDepthStep.X,
                    gridOrigin.Y+x*gridWidthStep.Y+y*gridDepthStep.Y);
                var corners=new[]{Project(x0,y0),Project(x1,y0),Project(x1,y1),Project(x0,y1)};
                output.AppendText(Environment.NewLine+"DRY-RUN SCREEN CORNERS (no clicks): "+
                    string.Join(" → ",corners.Select(c=>"("+c.X+","+c.Y+")"))+
                    Environment.NewLine+"Grid mapping assumes origin tile is lot coordinate (0,0), with unchanged camera and zoom.");
            } else output.AppendText(Environment.NewLine+"Calibrate the lot grid to preview wall coordinates on screen.");
            status.Text="Starter footprint generated locally. Construction remains dry-run.";
        };
        p.Controls.Add(plan);p.Controls.Add(output);
        var export=new Button { Text="Export Buildcraft project",AutoSize=true };
        export.Click+=(_,_)=>{
            if(string.IsNullOrWhiteSpace(prompt.Text)){status.Text="Describe the building first.";return;}
            using var dialog=new SaveFileDialog { Filter="JSON files|*.json",FileName="buildcraft-project.json" };
            if(dialog.ShowDialog()!=DialogResult.OK)return;
            try {
                File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new {format="simcraft-buildcraft-project",schemaVersion=1,lotWidth=(int)width.Value,lotDepth=(int)depth.Value,description=prompt.Text,gridCalibrated=gridReady,gridOrigin=gridReady?new[]{gridOrigin.X,gridOrigin.Y}:null,gridWidthStep=gridReady?new[]{gridWidthStep.X,gridWidthStep.Y}:null,gridDepthStep=gridReady?new[]{gridDepthStep.X,gridDepthStep.Y}:null,constructionEnabled=false},new JsonSerializerOptions {WriteIndented=true}));
                status.Text="Buildcraft project exported.";
            }catch(Exception ex){status.Text="Export failed: "+ex.Message;}
        };
        p.Controls.Add(export);
        Caption(p,"Safety: Live wall test requires explicit confirmation and selected Wall Tool. ESC or EMERGENCY STOP aborts. Save the lot first.");
    }
    void BuildSettingsTab(TabPage page) {
        var p=Panel(page);Caption(p,"Simcraft Studio — native preview");
        Caption(p,"Export and import of mod and outfit plans are available in their respective tabs.");
        Caption(p,"Photo previews and manual F1–F19 / M1–M19 selection are available in Character Studio.");
        Caption(p,"The original HTML workspace is not used by this native interface.");
        Caption(p,"Installed version: v"+CurrentVersion+" | Windows EXE file version: "+(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()??"unknown"));
        var updateStatus=new Label { Text="Update status: not checked",AutoSize=true,ForeColor=Color.White };
        var check=new Button { Text="Check for updates",AutoSize=true };
        check.Click+=async (_,_)=>await CheckForUpdates(updateStatus);
        p.Controls.Add(check);p.Controls.Add(updateStatus);
    }
    const string CurrentVersion = "1.0.12";
    static readonly HttpClient UpdateClient = new() { Timeout=TimeSpan.FromSeconds(15) };
    async System.Threading.Tasks.Task CheckForUpdates(Label? status) {
        if(status!=null)status.Text="Checking GitHub releases...";
        try {
            using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.github.com/repos/Becksploder2/Simscraft-Windows/releases/latest");
            request.Headers.UserAgent.ParseAdd("SimcraftStudio/"+CurrentVersion);
            using var response=await UpdateClient.SendAsync(request);response.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag=json.RootElement.GetProperty("tag_name").GetString()??"";
            if(!Version.TryParse(tag.TrimStart('v','V'),out var latest)||latest<=new Version(CurrentVersion)){if(status!=null)status.Text="Up to date (v"+CurrentVersion+").";return;}
            if(status!=null)status.Text="Update available: "+tag;
            // Manual check only: do not interrupt testing with a release prompt.
        } catch(Exception ex){if(status!=null)status.Text="Update check failed: "+ex.Message;}
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
            try { File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true})); MessageBox.Show("Project saved. This is not an installable mod."); }
            catch(Exception ex){MessageBox.Show("Export failed: "+ex.Message);}
        };
        panel.Controls.Add(export);
    }
}