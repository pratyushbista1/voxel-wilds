Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

!ifndef VERSION
  !define VERSION "2.0.3"
!endif
!define PRODUCT "Voxel Wilds - Unity Edition"
!define REGISTRY "Software\Microsoft\Windows\CurrentVersion\Uninstall\VoxelWildsUnity"
!define MARKER "voxel-wilds-unity.install"
!include "${PAYLOAD_INCLUDE}"

Name "${PRODUCT}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\VoxelWildsUnity"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
CRCCheck on
ShowInstDetails show
ShowUninstDetails show
BrandingText "Voxel Wilds"
Icon "${PROJECT_DIR}\build\icon.ico"
UninstallIcon "${PROJECT_DIR}\build\icon.ico"
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "${PRODUCT}"
VIAddVersionKey /LANG=1033 "FileDescription" "Voxel Wilds Unity installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright (c) 2026 Voxel Wilds contributors"

!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "Install the Unity edition of Voxel Wilds for your Windows account.$\r$\n$\r$\nThis is separate from older Electron releases. The shortcuts save worlds in your application data folder.$\r$\n$\r$\nUninstalling removes installed game files and keeps your worlds and other personal files."
!define MUI_FINISHPAGE_TEXT "Voxel Wilds is installed.$\r$\n$\r$\nUse the Unity Edition desktop or Start menu shortcut to play. Your worlds are kept when the game is uninstalled."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Var TestMode
Var MarkerHandle
Var MarkerMagic
Var MarkerPath
Var MarkerMode
Var DirectoryHandle
Var DirectoryEntry

!macro Refuse MESSAGE CODE
  IfSilent +2
    MessageBox MB_OK|MB_ICONSTOP "${MESSAGE}"
  SetErrorLevel ${CODE}
  Quit
!macroend

!macro CheckDirectory DIRECTORY
  StrCpy $9 "${DIRECTORY}"
  System::Call 'kernel32::GetFileAttributesW(w r9) i.r0'
  IntOp $1 $0 & 0x400
  ${If} $0 != -1
  ${AndIf} $1 != 0
    !insertmacro Refuse "A game directory is a junction or symbolic link. No files can safely be changed there." 72
  ${EndIf}
!macroend

!macro ResolveInstallDirectory
  StrCpy $9 "$INSTDIR"
  System::Call 'kernel32::GetFullPathNameW(w r9, i ${NSIS_MAX_STRLEN}, w .r8, p 0) i.r0'
  ${If} $0 == 0
  ${OrIf} $0 >= ${NSIS_MAX_STRLEN}
    !insertmacro Refuse "The installation path could not be resolved. No files were changed." 79
  ${EndIf}
  StrCpy $1 "$8" 1 -1
  StrLen $0 "$8"
  ${If} $1 == "\"
  ${AndIf} $0 > 3
    StrCpy $8 "$8" -1
  ${EndIf}
  StrCpy $INSTDIR "$8"
!macroend

Function .onInit
  SetShellVarContext current
  SetRegView 64
  StrCpy $TestMode 0
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/TESTMODE" $1
  ${IfNot} ${Errors}
    StrCpy $TestMode 1
  ${EndIf}
  ${IfNot} ${RunningX64}
    !insertmacro Refuse "This build needs 64-bit Windows." 71
  ${EndIf}
FunctionEnd

Function ValidateInstallDirectory
  !insertmacro ResolveInstallDirectory
  !insertmacro CheckDirectory "$INSTDIR"
  ${GetRoot} "$INSTDIR" $0
  StrCpy $1 "$0\"
  ${If} $INSTDIR == $0
  ${OrIf} $INSTDIR == $1
  ${OrIf} $INSTDIR == "$PROFILE"
  ${OrIf} $INSTDIR == "$LOCALAPPDATA"
  ${OrIf} $INSTDIR == "$APPDATA"
  ${OrIf} $INSTDIR == "$PROGRAMFILES"
  ${OrIf} $INSTDIR == "$PROGRAMFILES64"
  ${OrIf} $INSTDIR == "$WINDIR"
  ${OrIf} $INSTDIR == "$SYSDIR"
  ${OrIf} $INSTDIR == "$DESKTOP"
  ${OrIf} $INSTDIR == "$DOCUMENTS"
    !insertmacro Refuse "Choose a dedicated folder for the game." 73
  ${EndIf}
  IfFileExists "$INSTDIR\${MARKER}" existing_install check_empty
  existing_install:
    FileOpen $MarkerHandle "$INSTDIR\${MARKER}" r
    FileRead $MarkerHandle $MarkerMagic
    FileRead $MarkerHandle $MarkerPath
    FileRead $MarkerHandle $MarkerMode
    FileClose $MarkerHandle
    ${If} $MarkerMagic != "VoxelWilds.Unity.Install.v1$\r$\n"
    ${OrIf} $MarkerPath != "$INSTDIR$\r$\n"
    ${OrIf} $MarkerMode != "$TestMode$\r$\n"
      !insertmacro Refuse "This folder does not contain a matching Unity installation. Choose a new empty folder." 74
    ${EndIf}
    Return
  check_empty:
    ClearErrors
    FindFirst $DirectoryHandle $DirectoryEntry "$INSTDIR\*.*"
    ${IfNot} ${Errors}
      directory_loop:
        ${If} $DirectoryEntry != ""
        ${AndIf} $DirectoryEntry != "."
        ${AndIf} $DirectoryEntry != ".."
          FindClose $DirectoryHandle
          !insertmacro Refuse "The selected folder contains other files. Choose a new empty folder." 75
        ${EndIf}
        ClearErrors
        FindNext $DirectoryHandle $DirectoryEntry
        IfErrors directory_done directory_loop
      directory_done:
      FindClose $DirectoryHandle
    ${EndIf}
FunctionEnd

Section "Game" SEC_GAME
  Call ValidateInstallDirectory
  ClearErrors
  SetOutPath "$INSTDIR"
  FileOpen $MarkerHandle "$INSTDIR\${MARKER}" w
  IfErrors install_failed
  FileWrite $MarkerHandle "VoxelWilds.Unity.Install.v1$\r$\n$INSTDIR$\r$\n$TestMode$\r$\n${VERSION}$\r$\n"
  FileClose $MarkerHandle
  IfErrors install_failed
  WriteUninstaller "$INSTDIR\Uninstall Voxel Wilds.exe"
  IfErrors install_failed
  ClearErrors
  !insertmacro InstallPayload
  IfErrors install_failed
  ${If} $TestMode == 0
    CreateDirectory "$SMPROGRAMS\Voxel Wilds Unity Edition"
    CreateShortcut "$DESKTOP\Voxel Wilds Unity Edition.lnk" "$INSTDIR\Voxel Wilds.exe" '-voxel-saves "$APPDATA\VoxelWildsUnity\saves"' "$INSTDIR\Voxel Wilds.exe"
    CreateShortcut "$SMPROGRAMS\Voxel Wilds Unity Edition\Voxel Wilds Unity Edition.lnk" "$INSTDIR\Voxel Wilds.exe" '-voxel-saves "$APPDATA\VoxelWildsUnity\saves"' "$INSTDIR\Voxel Wilds.exe"
    CreateShortcut "$SMPROGRAMS\Voxel Wilds Unity Edition\Uninstall.lnk" "$INSTDIR\Uninstall Voxel Wilds.exe"
    WriteRegStr HKCU "${REGISTRY}" "DisplayName" "${PRODUCT}"
    WriteRegStr HKCU "${REGISTRY}" "DisplayVersion" "${VERSION}"
    WriteRegStr HKCU "${REGISTRY}" "Publisher" "Voxel Wilds"
    WriteRegStr HKCU "${REGISTRY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${REGISTRY}" "DisplayIcon" "$INSTDIR\Voxel Wilds.exe"
    WriteRegStr HKCU "${REGISTRY}" "UninstallString" '$\"$INSTDIR\Uninstall Voxel Wilds.exe$\"'
    WriteRegStr HKCU "${REGISTRY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall Voxel Wilds.exe$\" /S'
    WriteRegDWORD HKCU "${REGISTRY}" "NoModify" 1
    WriteRegDWORD HKCU "${REGISTRY}" "NoRepair" 1
    WriteRegDWORD HKCU "${REGISTRY}" "EstimatedSize" ${INSTALLED_KB}
  ${EndIf}
  SetErrorLevel 0
  Goto install_done
  install_failed:
    !insertmacro Refuse "Installation could not finish. Close any running copy of this game and try again." 76
  install_done:
SectionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 64
  !insertmacro ResolveInstallDirectory
  !insertmacro CheckDirectory "$INSTDIR"
  IfFileExists "$INSTDIR\${MARKER}" 0 invalid_install
  FileOpen $MarkerHandle "$INSTDIR\${MARKER}" r
  IfErrors invalid_install
  FileRead $MarkerHandle $MarkerMagic
  FileRead $MarkerHandle $MarkerPath
  FileRead $MarkerHandle $MarkerMode
  FileClose $MarkerHandle
  ${If} $MarkerMagic != "VoxelWilds.Unity.Install.v1$\r$\n"
  ${OrIf} $MarkerPath != "$INSTDIR$\r$\n"
    Goto invalid_install
  ${EndIf}
  ${If} $MarkerMode == "1$\r$\n"
    StrCpy $TestMode 1
  ${ElseIf} $MarkerMode == "0$\r$\n"
    StrCpy $TestMode 0
  ${Else}
    Goto invalid_install
  ${EndIf}
  Return
  invalid_install:
    !insertmacro Refuse "The installation marker is missing or does not match this directory. No files were removed." 77
FunctionEnd

Section "Uninstall"
  !insertmacro UninstallPayload
  IfFileExists "$INSTDIR\Voxel Wilds.exe" uninstall_busy
  Delete "$INSTDIR\${MARKER}"
  Delete "$INSTDIR\Uninstall Voxel Wilds.exe"
  ${If} $TestMode == 0
    ReadRegStr $0 HKCU "${REGISTRY}" "InstallLocation"
    ${If} $0 == $INSTDIR
      Delete "$DESKTOP\Voxel Wilds Unity Edition.lnk"
      Delete "$SMPROGRAMS\Voxel Wilds Unity Edition\Voxel Wilds Unity Edition.lnk"
      Delete "$SMPROGRAMS\Voxel Wilds Unity Edition\Uninstall.lnk"
      RMDir "$SMPROGRAMS\Voxel Wilds Unity Edition"
      DeleteRegKey HKCU "${REGISTRY}"
    ${EndIf}
  ${EndIf}
  RMDir "$INSTDIR"
  SetErrorLevel 0
  Goto uninstall_done
  uninstall_busy:
    !insertmacro Refuse "The game is still running or its files are in use. Close it and run this uninstaller again. Your saves were kept." 78
  uninstall_done:
SectionEnd
