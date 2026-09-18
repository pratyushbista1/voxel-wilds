param(
    [string]$Player,
    [ValidateRange(15, 600)][int]$TimeoutSeconds = 150
)

$ErrorActionPreference = 'Stop'
$voxelRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (-not $Player) { $Player = Join-Path $voxelRoot 'release/2.0.3/Windows/Voxel Wilds.exe' }
$voxelPlayer = [IO.Path]::GetFullPath($Player)
if (-not (Test-Path -LiteralPath $voxelPlayer -PathType Leaf)) { throw "Windows player not found: $voxelPlayer. Run scripts/build-unity.ps1 first." }
$voxelRun = 'unity-interactions-' + [Guid]::NewGuid().ToString('N')
$voxelSaves = Join-Path $voxelRoot ".cache/$voxelRun"
$voxelArtifacts = Join-Path $voxelRoot "artifacts/$voxelRun"
New-Item -ItemType Directory -Path $voxelSaves, $voxelArtifacts | Out-Null
$voxelLog = Join-Path $voxelArtifacts 'player.log'
$voxelArguments = @('-voxel-interaction-check', '-voxel-saves', ('"{0}"' -f $voxelSaves), '-voxel-artifacts', ('"{0}"' -f $voxelArtifacts), '-logFile', ('"{0}"' -f $voxelLog))
Write-Output "Interaction test saves: $voxelSaves"
Write-Output "Interaction test artifacts: $voxelArtifacts"
$voxelProcess = Start-Process -FilePath $voxelPlayer -ArgumentList $voxelArguments -WorkingDirectory ([IO.Path]::GetDirectoryName($voxelPlayer)) -WindowStyle Hidden -PassThru
if (-not $voxelProcess.WaitForExit($TimeoutSeconds * 1000)) {
    if (-not $voxelProcess.HasExited) { $voxelProcess.Kill(); $voxelProcess.WaitForExit() }
    throw "Player interaction test exceeded $TimeoutSeconds seconds and its test process was stopped. See $voxelLog. Test saves were retained at $voxelSaves."
}
$voxelProcess.Refresh()
if ($voxelProcess.ExitCode -ne 0) { throw "Player interaction test failed with exit code $($voxelProcess.ExitCode). See $voxelLog. Test saves were retained at $voxelSaves." }
if (-not (Test-Path -LiteralPath $voxelLog) -or -not (Select-String -LiteralPath $voxelLog -SimpleMatch 'VOXEL_INTERACTION_PASS' -Quiet)) {
    throw "The player exited without a passing interaction test marker. See $voxelLog. Test saves were retained at $voxelSaves."
}
Write-Output "Native player interaction test passed. Log: $voxelLog"
Write-Output 'Only this fresh test save directory was used; existing worlds were not touched.'
