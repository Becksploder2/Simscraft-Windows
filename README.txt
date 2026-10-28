SIMCRAFT INSTALLER PROJECT

WINDOWS
This bundle contains the Simcraft CAS Release Candidate Windows files and an Inno Setup installer recipe.
To build locally on Windows: install Inno Setup 6, open windows/Simcraft.iss and click Compile.
To build via GitHub: add windows/ and .github/workflows/windows-installer.yml to the Windows repository, then run the workflow in Actions. GitHub will provide Simcraft-Studio-Setup.exe inside its artifact ZIP.
IMPORTANT: The payload is based on the locally available CAS Release Candidate, not verified to be your newest GitHub version. The installer packages local files but does not automatically install Python, face-engine dependencies, external Sims 4 mods, or other third-party prerequisites. Verify what features need before release.

MAC
The macos/build-dmg.yml file is a complete DMG workflow intended for your existing Mac repository. Place it at .github/workflows/build-dmg.yml in that repository. It assumes your existing Build_on_Mac.command and Resources are present. It creates a drag-to-Applications DMG; no Xcode is needed on the recipient's Mac. The app is unsigned/unnotarized.

Both packages are build configurations, not tested installer binaries. Test on clean Windows and Mac machines before release.
