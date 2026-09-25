param(
    [string]$Version = '2.0.4',
    [string]$Compiler
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$gameRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must have three numeric components.' }
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $gameRoot "release\$Version"))
if (-not $releaseRoot.StartsWith((Join-Path $gameRoot 'release') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid release directory.' }
$sourceRoot = Join-Path $releaseRoot 'Windows'
$cacheRoot = Join-Path $gameRoot '.cache'
if (-not $Compiler) {
    $availableCompiler = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($availableCompiler) { $Compiler = $availableCompiler.Source }
    else {
        $candidates = @()
        $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
        $programFiles = [Environment]::GetFolderPath('ProgramFiles')
        if ($programFilesX86) { $candidates += Join-Path $programFilesX86 'NSIS\makensis.exe' }
        if ($programFiles) { $candidates += Join-Path $programFiles 'NSIS\makensis.exe' }
        $candidates += Join-Path $cacheRoot 'electron-builder\nsis-3.0.4.1\nsis-3.0.4.1-1mx3n\Bin\makensis.exe'
        $Compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}
if (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler -PathType Leaf)) { throw 'NSIS compiler is missing. Pass -Compiler with a makensis.exe path.' }
foreach ($name in @('Voxel Wilds.exe', 'UnityPlayer.dll', 'Voxel Wilds_Data', 'MonoBleedingEdge')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $name))) { throw "Native Unity build is incomplete: $name" }
}
foreach ($directory in @($sourceRoot, $releaseRoot, $cacheRoot)) {
    if (((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Package directory is a link: $directory" }
}
function Get-Checksum([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $hash.Dispose(); $stream.Dispose() }
}
function Escape-Nsis([string]$Value) { $Value.Replace('$', '$$').Replace('"', '$\"') }
$packageId = 'unity-package-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$stageRoot = [IO.Path]::GetFullPath((Join-Path $cacheRoot $packageId))
if (-not $stageRoot.StartsWith($cacheRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging directory.' }
$payloadRoot = Join-Path $stageRoot 'payload'
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
$files = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
foreach ($name in @('Voxel Wilds.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', 'WinPixEventRuntime.dll')) {
    $path = Join-Path $sourceRoot $name
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $entry = Get-Item -LiteralPath $path
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Payload file is a link: $path" }
        $files.Add($entry)
    }
}
foreach ($name in @('Voxel Wilds_Data', 'MonoBleedingEdge', 'D3D12')) {
    $path = Join-Path $sourceRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Container)) { continue }
    if (((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Payload directory is a link: $path" }
    foreach ($entry in (Get-ChildItem -LiteralPath $path -Recurse -Force)) {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Payload entry is a link: $($entry.FullName)" }
        if (-not $entry.PSIsContainer) { $files.Add($entry) }
    }
}
$records = New-Object 'System.Collections.Generic.List[object]'
$originalPaths = @{}
foreach ($file in ($files | Sort-Object FullName)) {
    $relative = $file.FullName.Substring($sourceRoot.Length + 1)
    if ($relative -match '(?i)(^|[\\/])(saves|userdata|artifacts|logs|smoke|BackUpThisFolder_ButDontShipItWithYourGame)([\\/]|$)' -or $relative -match '(?i)\.(pdb|log|vws|dmp)$' -or $file.Name -eq '.DS_Store') { continue }
    $destination = Join-Path $payloadRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    $checksum = Get-Checksum $file.FullName
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    if ((Get-Checksum $destination) -ne $checksum) { throw "Copied payload does not match: $relative" }
    $records.Add([ordered]@{ Path = $relative; Bytes = $file.Length; Sha256 = $checksum })
    $originalPaths[$relative] = $file.FullName
}
foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md', 'Legal\Unity-Player-Windows-Mono-6000.3.23f1.pdf')) {
    $document = Get-Item -LiteralPath (Join-Path $gameRoot $name)
    if ($document.PSIsContainer -or ($document.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Release document is not a regular file: $name" }
    $destination = Join-Path $payloadRoot $name
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    $checksum = Get-Checksum $document.FullName
    Copy-Item -LiteralPath $document.FullName -Destination $destination
    if ((Get-Checksum $destination) -ne $checksum) { throw "Copied document does not match: $name" }
    $records.Add([ordered]@{ Path = $name; Bytes = $document.Length; Sha256 = $checksum })
    $originalPaths[$name] = $document.FullName
}
if ($records.Count -lt 10) { throw 'The Unity payload contains unexpectedly few files.' }
$directories = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($record in $records) {
    $directory = Split-Path -Parent $record.Path
    while ($directory) { [void]$directories.Add($directory); $directory = Split-Path -Parent $directory }
}
$include = New-Object Text.StringBuilder
[void]$include.AppendLine('!macro InstallPayload')
foreach ($directory in ($directories | Sort-Object { $_.Length })) { [void]$include.AppendLine('  !insertmacro CheckDirectory "$INSTDIR\' + (Escape-Nsis $directory) + '"') }
foreach ($record in $records) {
    $relativeDirectory = Split-Path -Parent $record.Path
    $outputPath = if ($relativeDirectory) { '$INSTDIR\' + (Escape-Nsis $relativeDirectory) } else { '$INSTDIR' }
    [void]$include.AppendLine('  SetOutPath "' + $outputPath + '"')
    [void]$include.AppendLine('  File "' + (Escape-Nsis (Join-Path $payloadRoot $record.Path)) + '"')
}
[void]$include.AppendLine('!macroend')
[void]$include.AppendLine('!macro UninstallPayload')
foreach ($directory in ($directories | Sort-Object { $_.Length })) { [void]$include.AppendLine('  !insertmacro CheckDirectory "$INSTDIR\' + (Escape-Nsis $directory) + '"') }
foreach ($record in $records) {
    [void]$include.AppendLine('  Delete "$INSTDIR\' + (Escape-Nsis $record.Path) + '"')
    [void]$include.AppendLine('  IfFileExists "$INSTDIR\' + (Escape-Nsis $record.Path) + '" uninstall_busy')
}
foreach ($directory in ($directories | Sort-Object { $_.Length } -Descending)) { [void]$include.AppendLine('  RMDir "$INSTDIR\' + (Escape-Nsis $directory) + '"') }
[void]$include.AppendLine('!macroend')
$includePath = Join-Path $stageRoot 'payload.nsh'
[IO.File]::WriteAllText($includePath, $include.ToString(), (New-Object Text.UTF8Encoding $true))
$setupPath = Join-Path $releaseRoot "Voxel-Wilds-Unity-Setup-$Version.exe"
$temporarySetup = Join-Path $stageRoot 'unity-package.exe'
$compilerInfo = New-Object Diagnostics.ProcessStartInfo
$compilerInfo.FileName = [IO.Path]::GetFullPath($Compiler)
$compilerInfo.WorkingDirectory = $gameRoot
$compilerInfo.UseShellExecute = $false
$compilerInfo.CreateNoWindow = $true
$compilerInfo.RedirectStandardOutput = $true
$compilerInfo.RedirectStandardError = $true
$compilerDirectory = Split-Path -Parent $compilerInfo.FileName
$nsisRoot = if ((Split-Path -Leaf $compilerDirectory) -eq 'Bin') { Split-Path -Parent $compilerDirectory } else { $compilerDirectory }
$compilerInfo.EnvironmentVariables['NSISDIR'] = $nsisRoot
$totalBytes = [long]0
foreach ($record in $records) { $totalBytes += [long]$record.Bytes }
$installedKb = [Math]::Ceiling($totalBytes / 1024)
$compilerInfo.Arguments = '/V3 /NOCD "/DVERSION=' + $Version + '" "/DPROJECT_DIR=' + $gameRoot + '" "/DPAYLOAD_INCLUDE=' + $includePath + '" "/DOUTPUT=' + $temporarySetup + '" "/DINSTALLED_KB=' + $installedKb + '" "' + (Join-Path $gameRoot 'build\unity-installer.nsi') + '"'
$process = New-Object Diagnostics.Process
$process.StartInfo = $compilerInfo
[void]$process.Start()
$stdout = $process.StandardOutput.ReadToEndAsync()
$stderr = $process.StandardError.ReadToEndAsync()
$process.WaitForExit()
Write-Output $stdout.GetAwaiter().GetResult()
Write-Output $stderr.GetAwaiter().GetResult()
if ($process.ExitCode -ne 0) { throw "NSIS compiler failed with exit code $($process.ExitCode). Staging remains at $stageRoot" }
$process.Dispose()
foreach ($record in $records) {
    if ((Get-Checksum $originalPaths[$record.Path]) -ne $record.Sha256) { throw 'The native build or release documents changed during packaging. Finish changes and package again.' }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = Join-Path $releaseRoot "Voxel-Wilds-Unity-Portable-$Version.zip"
$temporaryZip = Join-Path $stageRoot 'portable.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($payloadRoot, $temporaryZip, [IO.Compression.CompressionLevel]::Optimal, $false)
Move-Item -LiteralPath $temporarySetup -Destination $setupPath -Force
Move-Item -LiteralPath $temporaryZip -Destination $zipPath -Force
$manifest = [ordered]@{ Version = $Version; FileCount = $records.Count; Files = @($records.ToArray()); SetupSha256 = (Get-Checksum $setupPath); ZipSha256 = (Get-Checksum $zipPath) }
[IO.File]::WriteAllText((Join-Path $releaseRoot 'unity-package-manifest.json'), ($manifest | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
$sums = $manifest.SetupSha256 + '  ' + [IO.Path]::GetFileName($setupPath) + "`r`n" + $manifest.ZipSha256 + '  ' + [IO.Path]::GetFileName($zipPath) + "`r`n"
[IO.File]::WriteAllText((Join-Path $releaseRoot 'UNITY-PACKAGES-SHA256.txt'), $sums)
Write-Output "Created $setupPath"
Write-Output "Created $zipPath"
Write-Output "Packaged $($records.Count) verified files. Staging preserved at $stageRoot"
