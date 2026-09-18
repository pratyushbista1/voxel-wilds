@echo off
setlocal
cd /d "%~dp0"
if exist "release\2.0.3\Windows\Voxel Wilds.exe" (
  start "" "release\2.0.3\Windows\Voxel Wilds.exe" -voxel-saves "%~dp0saves\unity"
  exit /b 0
)
if exist "release\2.0.2\Windows\Voxel Wilds.exe" (
  start "" "release\2.0.2\Windows\Voxel Wilds.exe" -voxel-saves "%~dp0saves\unity"
  exit /b 0
)
if exist "release\2.0.1\Windows\Voxel Wilds.exe" (
  start "" "release\2.0.1\Windows\Voxel Wilds.exe" -voxel-saves "%~dp0saves\unity"
  exit /b 0
)
if exist "release\2.0.0\Windows\Voxel Wilds.exe" (
  start "" "release\2.0.0\Windows\Voxel Wilds.exe" -voxel-saves "%~dp0saves\unity"
  exit /b 0
)
echo The Unity player has not been built yet.
echo Open this Game folder in Unity Hub, or run scripts\build-unity.ps1.
pause
exit /b 1
