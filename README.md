# TeamCraft Test Beta (unnumbered)

Windows desktop Electron test with embedded EA TeamBuilder website.

## No PowerShell required for end users
Upload the contents of this folder to the root of a GitHub repository, **including `.github/workflows/windows-installer.yml`**. In GitHub, open Actions → Build TeamCraft Windows Installer → Run workflow. Download the Windows Installer artifact and extract it, then double-click `TeamCraft-Test-Beta-Setup.exe`. The installer bundles Electron and does not require Node.js or PowerShell on the user's computer.

## Current functionality
Offline team identity, 2D uniforms, field, roster, local sound assignments, save/load project. EA website loads in an embedded isolated browser view with persistent session storage. EA may restrict embedded authentication: the external-browser fallback remains available. No automatic transfer to EA or custom music upload is implemented. Publishing requires EA's website and internet.

The GitHub workflow builds the Windows EXE; the source ZIP itself is **not** an installer.
