@echo off
cd /d "%~dp0"
set /p MODS=Mods folder path (press Enter for default): 
if "%MODS%"=="" (py -3 CCScanner.py --hash) else (py -3 CCScanner.py "%MODS%" --hash)
if errorlevel 1 echo Scanner failed. Install Python 3 or check the folder path.
pause
