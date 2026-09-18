param(
    [string]$Player,
    [switch]$Visible,
    [ValidateRange(15, 600)][int]$TimeoutSeconds = 150
)
$ErrorActionPreference = 'Stop'
if (-not $Visible) { throw 'Native inventory screenshots need a visible game window. Run this test with -Visible on an unlocked desktop.' }
$voxelRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (-not $Player) { $Player = Join-Path $voxelRoot 'release/2.0.3/Windows/Voxel Wilds.exe' }
$voxelPlayer = [IO.Path]::GetFullPath($Player)
if (-not (Test-Path -LiteralPath $voxelPlayer -PathType Leaf)) { throw "Build the Windows player first: $voxelPlayer" }
$voxelRun = 'unity-inventory-' + [Guid]::NewGuid().ToString('N')
$voxelSaves = Join-Path $voxelRoot ".cache/$voxelRun"
$voxelArtifacts = Join-Path $voxelRoot "artifacts/$voxelRun"
New-Item -ItemType Directory -Path $voxelSaves, $voxelArtifacts | Out-Null
$voxelLog = Join-Path $voxelArtifacts 'player.log'
$voxelArguments = @('-voxel-inventory-check', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-voxel-saves', ('"{0}"' -f $voxelSaves), '-voxel-artifacts', ('"{0}"' -f $voxelArtifacts), '-logFile', ('"{0}"' -f $voxelLog))
Write-Output "Inventory test artifacts: $voxelArtifacts"
$voxelWindowStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
$voxelProcess = Start-Process -FilePath $voxelPlayer -ArgumentList $voxelArguments -WorkingDirectory ([IO.Path]::GetDirectoryName($voxelPlayer)) -WindowStyle $voxelWindowStyle -PassThru
if (-not $voxelProcess.WaitForExit($TimeoutSeconds * 1000)) {
    if (-not $voxelProcess.HasExited) { $voxelProcess.Kill(); $voxelProcess.WaitForExit() }
    throw "Inventory test timed out. See $voxelLog. Isolated test saves were retained."
}
$voxelProcess.Refresh()
if ($voxelProcess.ExitCode -ne 0) { throw "Inventory test failed. See $voxelLog." }
if (-not (Test-Path -LiteralPath $voxelLog) -or -not (Select-String -LiteralPath $voxelLog -SimpleMatch 'VOXEL_INVENTORY_PASS' -Quiet)) { throw "No passing inventory report. See $voxelLog." }
Write-Output "Native inventory and movement checks passed. Log: $voxelLog"
Write-Output 'Only fresh isolated saves were used; existing worlds were not touched.'
