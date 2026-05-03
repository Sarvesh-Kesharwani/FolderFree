$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$assets = Join-Path $root "assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null

Add-Type -AssemblyName System.Drawing

function New-LogoBitmap([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap $size, $size
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)

  $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
    ([System.Drawing.Color]::FromArgb(255, 242, 250, 255)),
    ([System.Drawing.Color]::FromArgb(255, 222, 246, 238)),
    45
  $g.FillEllipse($bg, 7, 7, $size - 14, $size - 14)

  $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(35, 20, 55, 72))
  $folderBody = [System.Drawing.RectangleF]::new($size * 0.18, $size * 0.35, $size * 0.64, $size * 0.35)
  $folderTab = [System.Drawing.RectangleF]::new($size * 0.22, $size * 0.28, $size * 0.25, $size * 0.15)
  [GraphicsExtensions]::FillRoundedRectangle($g, $shadow, $folderBody.X + 2, $folderBody.Y + 4, $folderBody.Width, $folderBody.Height, $size * 0.08)
  [GraphicsExtensions]::FillRoundedRectangle($g, $shadow, $folderTab.X + 2, $folderTab.Y + 4, $folderTab.Width, $folderTab.Height, $size * 0.05)

  $folderTop = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
    ([System.Drawing.Color]::FromArgb(255, 38, 166, 154)),
    ([System.Drawing.Color]::FromArgb(255, 45, 122, 235)),
    20
  [GraphicsExtensions]::FillRoundedRectangle($g, $folderTop, $folderTab.X, $folderTab.Y, $folderTab.Width, $folderTab.Height, $size * 0.05)
  [GraphicsExtensions]::FillRoundedRectangle($g, $folderTop, $folderBody.X, $folderBody.Y, $folderBody.Width, $folderBody.Height, $size * 0.08)

  $inner = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 255, 255, 255)), ([Math]::Max(1, $size * 0.02))
  $g.DrawArc($inner, $size * 0.31, $size * 0.40, $size * 0.38, $size * 0.38, 205, 250)

  $arrowPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(3, $size * 0.055))
  $arrowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $arrowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawLine($arrowPen, $size * 0.49, $size * 0.52, $size * 0.64, $size * 0.52)
  $g.DrawLine($arrowPen, $size * 0.64, $size * 0.52, $size * 0.58, $size * 0.45)
  $g.DrawLine($arrowPen, $size * 0.64, $size * 0.52, $size * 0.58, $size * 0.59)

  $lockPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 31, 41, 55)), ([Math]::Max(2, $size * 0.035))
  $g.DrawArc($lockPen, $size * 0.31, $size * 0.45, $size * 0.20, $size * 0.18, 190, 170)
  $lockBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 31, 41, 55))
  [GraphicsExtensions]::FillRoundedRectangle($g, $lockBrush, $size * 0.28, $size * 0.53, $size * 0.26, $size * 0.16, $size * 0.035)

  $cutPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 255, 255)), ([Math]::Max(2, $size * 0.035))
  $g.DrawLine($cutPen, $size * 0.30, $size * 0.67, $size * 0.51, $size * 0.47)

  $g.Dispose()
  return $bmp
}

Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

public static class GraphicsExtensions {
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, float x, float y, float width, float height, float radius) {
        using (GraphicsPath path = RoundedRect(x, y, width, height, radius)) {
            graphics.FillPath(brush, path);
        }
    }

    private static GraphicsPath RoundedRect(float x, float y, float width, float height, float radius) {
        float diameter = radius * 2f;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
"@ -ReferencedAssemblies System.Drawing

$pngPath = Join-Path $assets "FolderFree.png"
$icoPath = Join-Path $assets "FolderFree.ico"
$bitmap = New-LogoBitmap 512
$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($s in $sizes) {
  $img = New-Object System.Drawing.Bitmap $bitmap, $s, $s
  $ms = New-Object System.IO.MemoryStream
  $img.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $images += [PSCustomObject]@{ Size = $s; Bytes = $ms.ToArray() }
  $img.Dispose()
  $ms.Dispose()
}

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$images.Count)
$offset = 6 + (16 * $images.Count)
foreach ($image in $images) {
  $widthByte = if ($image.Size -eq 256) { 0 } else { $image.Size }
  $bw.Write([Byte]$widthByte)
  $bw.Write([Byte]$widthByte)
  $bw.Write([Byte]0)
  $bw.Write([Byte]0)
  $bw.Write([UInt16]1)
  $bw.Write([UInt16]32)
  $bw.Write([UInt32]$image.Bytes.Length)
  $bw.Write([UInt32]$offset)
  $offset += $image.Bytes.Length
}
foreach ($image in $images) {
  $bw.Write($image.Bytes)
}
$bw.Dispose()
$fs.Dispose()
$bitmap.Dispose()

Write-Host "Generated $pngPath and $icoPath"
