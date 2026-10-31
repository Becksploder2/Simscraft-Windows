# Simscraft Studio

**Simscraft Studio** is a desktop companion application for creating and refining characters in **The Sims 4 Create-a-Sim (CAS)**. It combines reference-photo face analysis, face-template suggestions, CAS assistance, and tools for working with Sims custom content.

> Development status: active. Some tabs and automation features are experimental or incomplete. Simscraft Studio is an independent fan-made tool, not affiliated with Electronic Arts or Maxis.

## Current Windows application

The Windows application is a native **.NET 8 Windows Forms** program located in `windows/native/`. Its portable build bundles the application, a MediaPipe-based face analyzer, and supporting assets. End users of a completed portable build should not need to install Python or .NET separately.

### Features and development status

| Feature | Status |
| --- | --- |
| Import a reference face photograph | Implemented |
| Detect facial landmarks with MediaPipe | Implemented in bundled analyzer |
| Suggest a starting CAS face template (F1–F19 / M1–M19) | Implemented as image-based estimates; verify in CAS |
| Preview template images | Implemented when template assets are available |
| Detect The Sims 4 process and capture/check CAS screenshots | Basic functionality available; CAS detection is heuristic |
| Set and test a CAS click point | Implemented |
| Calibrate per-feature CAS drag positions | Added; requires testing |
| Compare reference and CAS face regions to plan morph drags | Added; requires testing |
| Apply assisted CAS face-morph drags | Added to Character Studio; **not yet verified in-game** |
| Automatic iterative recapture and correction | Not yet implemented |
| Other studios, CC tools, and mod-building tabs | Varying levels of implementation |

**Important:** CAS editing relies on calibrated mouse movements, not an official Sims 4 facial-slider API. Use it only with The Sims 4 focused in CAS Detail Edit mode, check the results carefully, and expect to adjust calibration for different resolutions or layouts. Exact likeness is not guaranteed.

## Building and downloading

To get a Windows build, open **Actions** in this repository and select the **Build Simcraft Portable Windows** workflow (`.github/workflows/windows-installer.yml`). Run the workflow if manual dispatch is available, then download the **Simcraft-Studio-Windows-Portable** artifact from a successful run.

Extract the ZIP and launch `SimcraftStudio.exe`. The source-code ZIP downloaded from GitHub is **not** a ready-to-run Windows application.

The workflow downloads the MediaPipe model during the build and currently retrieves face-template images from `Becksploder2/simcraft-studio-mac`. Consolidating these assets into this repository and providing unified Windows/macOS releases are planned; the Mac migration is not complete.

## Project layout

- `windows/native/` — native Windows app, CAS controls, and morph-planning code
- `windows/payload/FaceEngine/` — face-landmark analysis engine
- `.github/workflows/windows-installer.yml` — Windows portable-build automation

## Planned improvements

- Calibrated, repeatable CAS control profiles and improved hover calibration
- Closed-loop auto morphing: capture, compare, adjust, recapture, and refine
- Better safeguards, diagnostics, and validation for CAS automation
- A shared code and asset workflow for Windows and macOS, with separate platform-specific builds and updaters

## License and game content

The Sims 4 and its trademarks belong to their respective owners. Users are responsible for complying with applicable game and custom-content terms. No affiliation or endorsement is implied.
