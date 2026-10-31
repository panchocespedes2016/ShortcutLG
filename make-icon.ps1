$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

$bmp = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::Transparent)

$bgPath = New-RoundedRect 4 4 248 248 46
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Rectangle(4, 4, 248, 248)),
    [System.Drawing.Color]::FromArgb(255, 8, 17, 30),
    [System.Drawing.Color]::FromArgb(255, 22, 49, 78),
    [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$g.FillPath($bgBrush, $bgPath)
$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 0, 190, 255), 5)
$g.DrawPath($borderPen, $bgPath)

$tvPath = New-RoundedRect 30 47 196 122 16
$screenBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 2, 9, 17))
$g.FillPath($screenBrush, $tvPath)
$tvPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 218, 242, 255), 6)
$g.DrawPath($tvPen, $tvPath)

$font = New-Object System.Drawing.Font("Segoe UI", 43, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$format = New-Object System.Drawing.StringFormat
$format.Alignment = [System.Drawing.StringAlignment]::Center
$format.LineAlignment = [System.Drawing.StringAlignment]::Center
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0, 200, 255))
$g.DrawString("HDMI", $font, $textBrush, (New-Object System.Drawing.RectangleF(30, 47, 196, 122)), $format)

$standBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 218, 242, 255))
$g.FillRectangle($standBrush, 113, 169, 30, 16)
$basePath = New-RoundedRect 79 184 98 13 6
$g.FillPath($standBrush, $basePath)

$ledBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 80, 235, 120))
$g.FillEllipse($ledBrush, 206, 56, 9, 9)

$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]1)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]32)
$bw.Write([UInt32]$png.Length)
$bw.Write([UInt32]22)
$bw.Write($png)
$bw.Flush()

$iconPath = Join-Path $PSScriptRoot 'app.ico'
[System.IO.File]::WriteAllBytes($iconPath, $out.ToArray())

$bw.Dispose()
$out.Dispose()
$ms.Dispose()
$g.Dispose()
$bmp.Dispose()
Write-Output ("Icono creado: " + $iconPath + " (" + (Get-Item $iconPath).Length + " bytes)")
