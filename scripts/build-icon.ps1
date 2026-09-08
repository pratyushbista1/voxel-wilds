Add-Type -AssemblyName System.Drawing
$gameRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $gameRoot 'build'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$bitmap = New-Object System.Drawing.Bitmap 256, 256
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$brushes = @{}
foreach ($entry in @{ background = '#18261F'; top = '#A5C65C'; left = '#887348'; right = '#5E5036'; grassLeft = '#739243'; grassRight = '#587237' }.GetEnumerator()) {
    $brushes[$entry.Key] = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($entry.Value))
}
$graphics.FillRectangle($brushes.background, 8, 8, 240, 240)
$top = [System.Drawing.Point[]]@([System.Drawing.Point]::new(128, 38), [System.Drawing.Point]::new(219, 88), [System.Drawing.Point]::new(128, 139), [System.Drawing.Point]::new(37, 88))
$left = [System.Drawing.Point[]]@([System.Drawing.Point]::new(37, 88), [System.Drawing.Point]::new(128, 139), [System.Drawing.Point]::new(128, 224), [System.Drawing.Point]::new(37, 173))
$right = [System.Drawing.Point[]]@([System.Drawing.Point]::new(128, 139), [System.Drawing.Point]::new(219, 88), [System.Drawing.Point]::new(219, 173), [System.Drawing.Point]::new(128, 224))
$graphics.FillPolygon($brushes.top, $top)
$graphics.FillPolygon($brushes.left, $left)
$graphics.FillPolygon($brushes.right, $right)
$graphics.FillPolygon($brushes.grassLeft, [System.Drawing.Point[]]@([System.Drawing.Point]::new(37, 88), [System.Drawing.Point]::new(128, 139), [System.Drawing.Point]::new(128, 166), [System.Drawing.Point]::new(37, 115)))
$graphics.FillPolygon($brushes.grassRight, [System.Drawing.Point[]]@([System.Drawing.Point]::new(128, 139), [System.Drawing.Point]::new(219, 88), [System.Drawing.Point]::new(219, 115), [System.Drawing.Point]::new(128, 166)))
$bitmap.Save((Join-Path $outputDir 'icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$stream = New-Object System.IO.MemoryStream
$bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
$bytes = $stream.ToArray()
$fileStream = [System.IO.File]::Create((Join-Path $outputDir 'icon.ico'))
$writer = New-Object System.IO.BinaryWriter $fileStream
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]1)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([uint16]1)
$writer.Write([uint16]32)
$writer.Write([uint32]$bytes.Length)
$writer.Write([uint32]22)
$writer.Write($bytes)
$writer.Dispose()
$stream.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
foreach ($brush in $brushes.Values) { $brush.Dispose() }
Write-Output 'Created build/icon.png and build/icon.ico'
