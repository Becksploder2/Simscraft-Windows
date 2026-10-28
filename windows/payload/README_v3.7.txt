SimMorph Studio v3.7 — Engine Readiness

This is a Windows diagnostic/stability build, NOT a complete facial-recognition release.
It preserves the working v3.4 UI-thread fix, CAS preview, custom bedroom background, and image importer.
Analyze Faces now clearly reports when the genuine trained model or self-contained runtime is missing, rather than trying an arbitrary system Python and producing exit status 1.

REQUIRED FOR REAL FACE ANALYSIS:
1. Models/face_landmarker.task (official MediaPipe model).
2. FaceEngine/runtime/pythonw.exe plus compatible installed MediaPipe dependencies.
3. Windows runtime verification.
4. Face matching and Sims 4 sculpting are NOT implemented.

Official model: https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task
No Python installation is necessary to launch the GUI, but analysis remains disabled until its dependencies are bundled.
