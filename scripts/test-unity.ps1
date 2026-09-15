param(
    [string]$UnityEditor,
    [switch]$CompileOnly
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
if (-not $voxelEditor) { throw "Unity $voxelVersion is required for engine API compilation. Set UNITY_EDITOR_PATH or pass -UnityEditor." }
$voxelEditor = [IO.Path]::GetFullPath($voxelEditor)
Push-Location -LiteralPath $voxelRoot
try {
    & dotnet run --project Tests/CoreTests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Core gameplay tests failed.' }
    & dotnet build Tests/UnityCompile.csproj "-p:UnityEditorPath=$voxelEditor" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Compilation against the installed Unity assemblies failed.' }
    & (Join-Path $PSScriptRoot 'update-unity-meta.ps1') -Check
    if ($CompileOnly) {
        Write-Output 'Core tests and real Unity API compilation passed. Editor asset checks and player smoke testing were not run.'
        return
    }
    $voxelLogs = Join-Path $voxelRoot 'artifacts'
    New-Item -ItemType Directory -Path $voxelLogs -Force | Out-Null
    $voxelStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $voxelLog = Join-Path $voxelLogs "unity-editor-checks-$voxelStamp.log"
    $voxelReport = Join-Path $voxelLogs "unity-editor-checks-$voxelStamp.json"
    $voxelArguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"{0}"' -f $voxelRoot), '-executeMethod', 'VoxelWilds.Editor.BuildGame.RunChecks', '-voxelTestReport', ('"{0}"' -f $voxelReport), '-logFile', ('"{0}"' -f $voxelLog))
    $voxelProcess = Start-Process -FilePath $voxelEditor -ArgumentList $voxelArguments -WindowStyle Hidden -PassThru
    $voxelProcess.WaitForExit()
    $voxelProcess.Refresh()
    if ($voxelProcess.ExitCode -ne 0) { throw "Unity editor checks failed with exit code $($voxelProcess.ExitCode). See $voxelLog. Unity Hub activation is required for editor execution." }
    if (-not (Test-Path -LiteralPath $voxelReport)) { throw "Unity produced no test report. See $voxelLog." }
    $voxelResult = Get-Content -LiteralPath $voxelReport -Raw | ConvertFrom-Json
    if (-not $voxelResult.success -or $voxelResult.checks -lt 1) { throw "Editor validation did not pass. See $voxelReport." }
    Write-Output "$($voxelResult.checks) editor checks passed. Report: $voxelReport"
    Write-Output 'Packaged player smoke testing is a separate step.'
} finally {
    Pop-Location
}
