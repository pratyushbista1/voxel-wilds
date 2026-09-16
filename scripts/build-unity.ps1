param(
    [string]$UnityEditor,
    [switch]$SkipPackage
)

$ErrorActionPreference = 'Stop'
$voxelRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$voxelVersion = '6000.3.23f1'
$voxelCandidates = @(
    $UnityEditor,
    $env:UNITY_EDITOR_PATH,
    (Join-Path $voxelRoot "tools/Unity-$voxelVersion/Editor/Unity.exe"),
    (Join-Path $env:ProgramFiles "Unity/Hub/Editor/$voxelVersion/Editor/Unity.exe")
)
$voxelEditor = $voxelCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
if (-not $voxelEditor) { throw "Unity $voxelVersion is required. Set UNITY_EDITOR_PATH to Editor/Unity.exe or pass -UnityEditor." }
$voxelEditor = [IO.Path]::GetFullPath($voxelEditor)
$voxelRelease = Join-Path $voxelRoot 'release/2.0.2'
$voxelOutput = Join-Path $voxelRelease 'Windows'
$voxelLogs = Join-Path $voxelRoot 'artifacts'
New-Item -ItemType Directory -Path $voxelOutput, $voxelLogs -Force | Out-Null
& (Join-Path $PSScriptRoot 'update-unity-meta.ps1')
$voxelLog = Join-Path $voxelLogs ('unity-build-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$voxelArguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"{0}"' -f $voxelRoot), '-buildTarget', 'Win64', '-executeMethod', 'VoxelWilds.Editor.BuildGame.BuildWindows', '-voxelBuildPath', ('"{0}"' -f $voxelOutput), '-logFile', ('"{0}"' -f $voxelLog))
Write-Output "Building native Windows player with $voxelEditor"
Write-Output "Log: $voxelLog"
$voxelProcess = Start-Process -FilePath $voxelEditor -ArgumentList $voxelArguments -WindowStyle Hidden -PassThru
$voxelProcess.WaitForExit()
$voxelProcess.Refresh()
if ($voxelProcess.ExitCode -ne 0) { throw "Unity build failed with exit code $($voxelProcess.ExitCode). Check $voxelLog. If licensing failed, activate Unity Personal in Unity Hub first." }
if (-not (Select-String -LiteralPath $voxelLog -SimpleMatch 'VOXEL_BUILD_SUCCEEDED' -Quiet)) { throw "Unity did not report a successful build. Check $voxelLog." }
foreach ($voxelRequired in @('Voxel Wilds.exe', 'UnityPlayer.dll', 'Voxel Wilds_Data')) {
    if (-not (Test-Path -LiteralPath (Join-Path $voxelOutput $voxelRequired))) { throw "Build file missing: $voxelRequired" }
}
if (-not $SkipPackage) {
    & (Join-Path $PSScriptRoot 'package-unity.ps1')
}
Write-Output "Windows player: $(Join-Path $voxelOutput 'Voxel Wilds.exe')"
