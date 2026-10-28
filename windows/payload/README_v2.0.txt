SimMorph Studio v2.0 - isolated photo dialog experiment

Changes: Photo browser runs on its own OS thread initialized for COM STA, outside the main UI callback. The main window remains responsive. No path entry and no drag-and-drop. JPEG, JPG, PNG supported.

Extract the entire archive and run SimMorphStudio.exe. If the photo dialog does not appear, check %LOCALAPPDATA%\SimMorphStudio\Logs\app.log.

This is a Windows-only experimental repair, compiled on Linux and NOT run-tested on Windows. Automatic CAS sculpting is not implemented.
