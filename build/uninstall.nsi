Unicode true
Name "Uninstall Voxel Wilds"
OutFile "${OUTPUT}"
Icon "${PROJECT_DIR}\build\icon.ico"
RequestExecutionLevel user
AutoCloseWindow true
ShowInstDetails nevershow
BrandingText "Voxel Wilds"

Section
  SetRegView 64
  ReadRegStr $0 HKCU "Software\34e646bf-ca86-5f39-b459-84e60fb13190" "InstallLocation"
  StrCmp $0 "" not_installed
  IfFileExists "$0\Uninstall Voxel Wilds.exe" 0 not_installed
  IfSilent silent interactive
  silent:
    ExecWait '"$0\Uninstall Voxel Wilds.exe" /S' $1
    SetErrorLevel $1
    Quit
  interactive:
    ExecWait '"$0\Uninstall Voxel Wilds.exe"' $1
    SetErrorLevel $1
    Quit
  not_installed:
    MessageBox MB_OK|MB_ICONINFORMATION "Voxel Wilds is not installed for this Windows user.$\r$\n$\r$\nThe portable version does not need an uninstaller. Remove its EXE to stop using it, and keep its userdata folder to preserve worlds."
    SetErrorLevel 0
SectionEnd
