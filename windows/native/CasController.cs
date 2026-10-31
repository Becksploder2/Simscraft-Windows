using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Simcraft.Cas;

/// <summary>Explicitly calibrated, foreground-guarded CAS drag controls. No guessed screen coordinates.</summary>
public sealed class CasController {
    readonly Dictionary<string, Point> points = new(StringComparer.OrdinalIgnoreCase);
    readonly string calibrationPath;
    public CasController(string calibrationPath) { this.calibrationPath=calibrationPath; Load(); }
    public IReadOnlyDictionary<string,Point> Points => points;
    public void Calibrate(string feature, Point screenPoint) {
        if (string.IsNullOrWhiteSpace(feature)) throw new ArgumentException("Feature is required",nameof(feature));
        points[feature]=screenPoint; Save();
    }
    public bool IsCalibrated(string feature) => points.ContainsKey(feature);
    public bool TryGetPoint(string feature, out Point point) => points.TryGetValue(feature,out point);
    public void Load() {
        if (!File.Exists(calibrationPath)) return;
        try {
            var saved=JsonSerializer.Deserialize<Dictionary<string,Point>>(File.ReadAllText(calibrationPath));
            if(saved is null)return;
            points.Clear(); foreach(var pair in saved)points[pair.Key]=pair.Value;
        } catch(JsonException) { /* Invalid calibration must never trigger a click. */ }
    }
    public void Save() {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(calibrationPath))!);
        File.WriteAllText(calibrationPath,JsonSerializer.Serialize(points,new JsonSerializerOptions{WriteIndented=true}));
    }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point p);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extraInfo);
    const uint LEFTDOWN=0x0002, LEFTUP=0x0004;
    public async Task DragAsync(string feature, int dx, int dy, Func<IntPtr,bool> isSimsWindow, CancellationToken cancellationToken=default) {
        if (!points.TryGetValue(feature,out var p)) throw new InvalidOperationException("Calibrate "+feature+" before applying CAS edits.");
        if (Math.Abs(dx)>180 || Math.Abs(dy)>180) throw new ArgumentOutOfRangeException(nameof(dx),"Single drag exceeds safety limit.");
        var target=GetForegroundWindow();
        if(target==IntPtr.Zero || !isSimsWindow(target)) throw new InvalidOperationException("Focus The Sims 4 before applying a morph.");
        GetCursorPos(out var original);
        bool pressed=false;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            SetCursorPos(p.X,p.Y);
            await Task.Delay(90,cancellationToken);
            if(GetForegroundWindow()!=target) throw new InvalidOperationException("CAS focus changed.");
            mouse_event(LEFTDOWN,0,0,0,UIntPtr.Zero); pressed=true;
            for(int i=1;i<=12;i++) {
                cancellationToken.ThrowIfCancellationRequested();
                if(GetForegroundWindow()!=target) throw new InvalidOperationException("CAS focus changed.");
                SetCursorPos(p.X+(int)Math.Round(dx*i/12.0),p.Y+(int)Math.Round(dy*i/12.0));
                await Task.Delay(18,cancellationToken);
            }
        } finally {
            if(pressed) mouse_event(LEFTUP,0,0,0,UIntPtr.Zero);
            SetCursorPos(original.X,original.Y);
        }
    }
}
