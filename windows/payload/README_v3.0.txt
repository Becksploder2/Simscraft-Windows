SimMorph Studio v3.0 — CAS Preview + Native Capture

- Capture Current CAS Face: after a 3-second countdown, captures the primary Windows display using native GDI, no PowerShell or CMD.
- Refresh Preview: displays the most recent Captures/current_cas.png within the application.
- Preview displays the whole screen, not a face crop. MediaPipe facial landmarks and automatic sculpting remain unimplemented.
- Importer remains the isolated native dialog from v2.0.
- Automatic Sims 4 process detection and all 38 CAS template images retained.

How to test:
1. Extract the entire ZIP to a new folder.
2. Launch Sims 4 in CAS on your primary display.
3. Launch SimMorph Studio, click Capture Current CAS Face, then switch to Sims 4 within 3 seconds.
4. The screenshot should be saved to Captures/current_cas.png and shown in the preview panel.
5. Click Refresh Preview to reload an existing screenshot.

This build was cross-compiled for Windows, not runtime-tested on Windows. If it freezes, check %LOCALAPPDATA%\SimMorphStudio\Logs\app.log.
