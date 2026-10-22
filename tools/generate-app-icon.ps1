$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\WinDesk.App\Assets'
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
foreach ($entry in @(@{Name='Square150x150Logo.png';Size=150},@{Name='Square44x44Logo.png';Size=44},@{Name='StoreLogo.png';Size=50},@{Name='WinDesk.png';Size=256})) {
    $bitmap = [Drawing.Bitmap]::new($entry.Size, $entry.Size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.ColorTranslator]::FromHtml('#1A2230'))
    $graphics.ScaleTransform($entry.Size / 150.0, $entry.Size / 150.0)
    $mint = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#73D9C3'))
    $purple = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#AFA4F4'))
    $graphics.FillRectangle($mint, 26, 30, 98, 20)
    $graphics.FillRectangle($purple, 26, 65, 44, 55)
    $graphics.FillRectangle($mint, 81, 65, 43, 24)
    $graphics.FillRectangle($mint, 81, 99, 43, 21)
    $bitmap.Save((Join-Path $assetRoot $entry.Name), [Drawing.Imaging.ImageFormat]::Png)
    $mint.Dispose(); $purple.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$pngBytes = [IO.File]::ReadAllBytes((Join-Path $assetRoot 'WinDesk.png'))
$stream = [IO.File]::Create((Join-Path $assetRoot 'WinDesk.ico'))
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
    $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$pngBytes.Length); $writer.Write([uint32]22)
    $writer.Write($pngBytes)
} finally { $writer.Dispose() }
