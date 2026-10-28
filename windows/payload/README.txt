SimMorph Studio v1.4 — Native Photo Picker Repair

Extract the whole folder, then run SimMorphStudio.exe.
Click Browse for Photo to choose a JPG or PNG. Drag-and-drop is disabled.
This version replaces the PowerShell file picker with the Windows common file dialog on a dedicated OS thread.
The application keeps its CAS background and 38 face template PNGs.

Logs: %LOCALAPPDATA%\SimMorphStudio\Logs\app.log

This build was cross-compiled for Windows x64 but could not be run-tested on Windows here.
It does NOT implement automatic facial landmarks, face matching, or live CAS sculpting.
No Python, GitHub, or extra runtime is required to run the executable.
