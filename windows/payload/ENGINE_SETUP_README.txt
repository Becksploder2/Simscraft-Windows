SimMorph Studio v4.1 - OPTIONAL ONE-TIME FACIAL ENGINE SETUP

The Windows GUI is precompiled. The facial engine is NOT included in this ZIP.
To acquire the runtime on a Windows PC with internet access:
1. Extract the entire ZIP to a writable folder.
2. Double-click INSTALL_FACE_ENGINE.cmd. This ONE-TIME installer opens a console and downloads Python 3.11.9 embedded + MediaPipe to FaceEngine/runtime. It may take time and requires internet access. This installer has not been tested on Windows.
3. Put your real face_landmarker.task in Models/face_landmarker.task.
4. Launch SimMorphStudio.exe, import a photo, capture CAS, click Analyze Faces.

The installer may fail because of network restrictions, incompatible wheels, or native DLL requirements. If so, see FaceEngine/runtime_install.log. Do not mistake a successful EXE compilation for a tested MediaPipe runtime.

Analyze Faces writes FaceAnalysis/latest.json. It does NOT automatically sculpt the Sim.
