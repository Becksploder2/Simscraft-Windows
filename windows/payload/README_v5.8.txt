SIMCRAFT v5.8 — RELEASE & RELIABILITY PREVIEW
GitHub: https://github.com/Becksploder2/Simcraft

WHAT CHANGED
- Windows GUI app includes File > Check for Updates and File > Run Diagnostics.
- Update checker configured to look for GitHub Releases in Becksploder2/Simcraft.
- Diagnostic report checks required files and installation-folder write access.
- Signed manifest, verified HTTPS downloads, staging and separate installer code retained.
- Blue-and-Vegas-gold icon asset retained for window use.

IMPORTANT: LIVE UPDATES ARE NOT ENABLED.
The publisher's Ed25519 PUBLIC key is not embedded yet. The private signing key must be created and kept by the repository owner. Do not upload it to GitHub.
Without a public key, the updater deliberately skips downloads. GitHub releases are not created automatically by this ZIP.

INSTALLATION/TESTING
- Extract the ZIP to a writable folder, e.g. Documents/Simcraft (not Program Files).
- Launch SimcraftStudio.exe. Use File > Run Diagnostics.
- Use File > Check for Updates (no updates until signing and publishing are configured).
- Native Windows install/update/rollback testing remains required; this was cross-compiled on Linux.
- The EXE window icon loads Simcraft-Icon.ico, but Explorer icon resource embedding is still pending.
- This does not fix initial ChatGPT attachment download failures.

PRODUCTION RELEASE CHECKLIST
1. On the publisher's trusted computer, run: python Developer/github_publish.py --new-key
   (requires Python package cryptography). Back up publisher-private.pem securely.
2. Rebuild SimcraftUpdater.exe with embedded public key using -ldflags "-X main.publicKey=BASE64_PUBLIC_KEY".
3. Build and test a newer version. Upload the versioned ZIP to GitHub Releases.
4. Sign and upload simcraft-release.json to the same GitHub Release.
5. Test update, rollback, declined updates, invalid signature, and interrupted download on Windows.
6. Code-sign Windows executables for commercial distribution.
7. Implement and test native macOS updater separately.
