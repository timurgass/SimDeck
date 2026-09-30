# Generates the small, original SimDeck icon required by the FS25 mod selector.
param([string]$Output = (Join-Path $PSScriptRoot '../../mods/FS25_SimDeckStatus/icon.dds'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::new(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bmp)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(14, 23, 31))
$accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(242, 184, 75))
$muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(155, 225, 207))
$border = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(242, 184, 75), 8)
$font = [System.Drawing.Font]::new('Segoe UI', 77, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$small = [System.Drawing.Font]::new('Segoe UI', 24, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
try {
    $graphics.DrawRectangle($border, 18, 18, 220, 220)
    $graphics.DrawString('SD', $font, $accent, 39, 55)
    $graphics.DrawString('FARM', $small, $muted, 76, 176)
    $target = [System.IO.Path]::GetFullPath($Output)
    $stream = [System.IO.File]::Create($target)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('DDS '))
        foreach ($word in [uint32[]]@(124, 0x100F, 256, 256, 1024, 0, 0)) { $writer.Write($word) }
        for ($i = 0; $i -lt 11; $i++) { $writer.Write([uint32]0) }
        foreach ($word in [uint32[]]@(32, 0x41, 0, 32, 0x00FF0000, 0x0000FF00, 0x000000FF, 4278190080)) { $writer.Write($word) }
        foreach ($word in [uint32[]]@(0x1000, 0, 0, 0, 0)) { $writer.Write($word) }
        for ($y = 0; $y -lt 256; $y++) { for ($x = 0; $x -lt 256; $x++) {
            $pixel = $bmp.GetPixel($x, $y)
            $writer.Write([byte]$pixel.B); $writer.Write([byte]$pixel.G)
            $writer.Write([byte]$pixel.R); $writer.Write([byte]$pixel.A)
        }}
    } finally { $writer.Dispose() }
} finally {
    $graphics.Dispose(); $bmp.Dispose(); $accent.Dispose(); $muted.Dispose(); $border.Dispose(); $font.Dispose(); $small.Dispose()
}
