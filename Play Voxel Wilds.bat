@echo off
setlocal
cd /d "%~dp0"
if exist "release\1.2.0\win-unpacked\Voxel Wilds.exe" (
  set "VOXEL_DATA_DIR=%~dp0userdata"
  set "VOXEL_SAVE_DIR=%~dp0saves"
  start "" "release\1.2.0\win-unpacked\Voxel Wilds.exe"
  exit /b 0
)
if exist "release\win-unpacked\Voxel Wilds.exe" (
  set "VOXEL_DATA_DIR=%~dp0userdata"
  set "VOXEL_SAVE_DIR=%~dp0saves"
  start "" "release\win-unpacked\Voxel Wilds.exe"
  exit /b 0
)
where node >nul 2>nul
if errorlevel 1 (
  echo Node.js is not installed. Use the portable EXE, or install Node.js 22.12 or newer.
  pause
  exit /b 1
)
node scripts\launch.mjs
if errorlevel 1 pause
