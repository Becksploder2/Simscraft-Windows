SimMorph Studio v4.3 — Face Analysis Repair

What is new:
- Analyze Faces launches the bundled local Python runtime and records useful errors.
- Face landmarks and region metrics for eyes, eyebrows, nose, cheeks, mouth/lips, jaw/chin, forehead.
- Reference and CAS results are saved to FaceAnalysis/latest.json.
- CAS face detection retries a center crop for screenshots.
- Missing runtime and missing model are clearly distinguished.

SETUP (ONE TIME, INTERNET REQUIRED):
1. Extract the whole ZIP.
2. Place genuine face_landmarker.task in Models/.
3. Double-click INSTALL_FACE_ENGINE.cmd and let it finish.
4. Launch SimMorphStudio.exe, import a reference photo, capture CAS, Analyze Faces.
5. If an error occurs, send FaceEngine/runtime_install.log or FaceEngine/analysis_error.log.

The Python runtime and trained model are NOT included in this ZIP. The setup script downloads runtime dependencies into FaceEngine/runtime on your PC. No system-wide Python installation is required.
This build measures facial features but DOES NOT automatically choose or sculpt facial templates in CAS. CAS click bridge and other existing features are retained.
Not tested on Windows with Sims 4.
