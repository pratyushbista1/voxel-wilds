param([switch]$Check)

$ErrorActionPreference = 'Stop'
$voxelRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$voxelAssets = Join-Path $voxelRoot 'Assets'
if (-not (Test-Path -LiteralPath $voxelAssets -PathType Container)) { throw "Assets folder not found: $voxelAssets" }
$voxelHash = [Security.Cryptography.SHA256]::Create()
$voxelCreated = 0
$voxelMissing = [Collections.Generic.List[string]]::new()
try {
    foreach ($voxelEntry in Get-ChildItem -LiteralPath $voxelAssets -Recurse -Force | Sort-Object FullName) {
        if ($voxelEntry.Name.EndsWith('.meta') -or $voxelEntry.Name.StartsWith('.')) { continue }
        $voxelMetaPath = $voxelEntry.FullName + '.meta'
        if (Test-Path -LiteralPath $voxelMetaPath) { continue }
        $voxelRelative = $voxelEntry.FullName.Substring($voxelRoot.Length + 1).Replace('\', '/')
        $voxelMissing.Add($voxelRelative)
        if ($Check) { continue }
        $voxelDigest = $voxelHash.ComputeHash([Text.Encoding]::UTF8.GetBytes('voxel-wilds-unity:' + $voxelRelative))
        $voxelGuid = [BitConverter]::ToString($voxelDigest).Replace('-', '').ToLowerInvariant().Substring(0, 32)
        $voxelMeta = "fileFormatVersion: 2`nguid: $voxelGuid`n"
        if ($voxelEntry.PSIsContainer) {
            $voxelMeta += "folderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n"
        } elseif ($voxelEntry.Extension -eq '.cs') {
            $voxelMeta += "MonoImporter:`n  externalObjects: {}`n  serializedVersion: 2`n  defaultReferences: []`n  executionOrder: 0`n  icon: {fileID: 0}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n"
        } elseif ($voxelEntry.Extension -eq '.shader') {
            $voxelMeta += "ShaderImporter:`n  externalObjects: {}`n  defaultTextures: []`n  nonModifiableTextures: []`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n"
        } elseif ($voxelEntry.Extension -ne '.fbx') {
            $voxelMeta += "DefaultImporter:`n  externalObjects: {}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n"
        }
        [IO.File]::WriteAllText($voxelMetaPath, $voxelMeta, [Text.UTF8Encoding]::new($false))
        $voxelCreated++
    }
} finally {
    $voxelHash.Dispose()
}
if ($Check -and $voxelMissing.Count -gt 0) { throw ('Missing Unity metadata: ' + ($voxelMissing -join ', ')) }
$voxelGuids = @{}
foreach ($voxelMetaFile in Get-ChildItem -LiteralPath $voxelAssets -Recurse -File -Filter '*.meta') {
    $voxelMatches = @(Select-String -LiteralPath $voxelMetaFile.FullName -Pattern '^guid: ([0-9a-fA-F]{32})$')
    if ($voxelMatches.Count -ne 1) { throw "Missing or invalid Unity GUID: $($voxelMetaFile.FullName)" }
    $voxelGuid = $voxelMatches[0].Matches[0].Groups[1].Value.ToLowerInvariant()
    if ($voxelGuids.ContainsKey($voxelGuid)) { throw "Duplicate Unity GUID in $($voxelMetaFile.FullName) and $($voxelGuids[$voxelGuid])" }
    $voxelGuids[$voxelGuid] = $voxelMetaFile.FullName
}
if ($Check) { Write-Output "All Unity assets have metadata with $($voxelGuids.Count) valid, unique GUIDs." }
else { Write-Output "Created $voxelCreated missing Unity metadata files; existing metadata was preserved." }
