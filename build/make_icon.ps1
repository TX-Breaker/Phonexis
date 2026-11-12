# Generates the Phonexis app icon (MetaGroove-coordinated: purple #6B4092).
# Draws: purple rounded square, white play triangle, white eighth note.
# Output: logo.png (256px) + phonexis.ico (16..256 PNG-encoded entries).

Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "..\src\Phonexis\Resources\Images"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Draw-Logo([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $s = $size / 256.0
    $purple = [System.Drawing.Color]::FromArgb(255, 0x6B, 0x40, 0x92)
    $white  = [System.Drawing.Color]::White

    # Rounded square background
    $r = 56 * $s
    $rect = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = $size
    $rect.AddArc(0, 0, $r, $r, 180, 90)
    $rect.AddArc($w - $r, 0, $r, $r, 270, 90)
    $rect.AddArc($w - $r, $w - $r, $r, $r, 0, 90)
    $rect.AddArc(0, $w - $r, $r, $r, 90, 90)
    $rect.CloseFigure()
    $brushP = New-Object System.Drawing.SolidBrush($purple)
    $g.FillPath($brushP, $rect)

    $brushW = New-Object System.Drawing.SolidBrush($white)

    # Play triangle (right side)
    $tri = @(
        (New-Object System.Drawing.PointF((118 * $s), (62 * $s))),
        (New-Object System.Drawing.PointF((212 * $s), (118 * $s))),
        (New-Object System.Drawing.PointF((118 * $s), (174 * $s)))
    )
    $g.FillPolygon($brushW, $tri)

    # Eighth note: stem + head + flag (left side)
    $pen = New-Object System.Drawing.Pen($white, (14 * $s))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    # stem
    $g.DrawLine($pen, (96 * $s), (78 * $s), (96 * $s), (188 * $s))
    # note head (ellipse, slightly rotated look via wider ellipse)
    $g.FillEllipse($brushW, (52 * $s), (172 * $s), (52 * $s), (36 * $s))
    # flag (curve from stem top to the right)
    $flag = New-Object System.Drawing.Drawing2D.GraphicsPath
    $flag.AddBezier((96 * $s), (78 * $s), (120 * $s), (84 * $s), (128 * $s), (100 * $s), (122 * $s), (124 * $s))
    $g.DrawPath($pen, $flag)

    $pen.Dispose(); $brushW.Dispose(); $brushP.Dispose(); $rect.Dispose()
    $g.Dispose()
    return $bmp
}

# 256 logo.png
$logo = Draw-Logo 256
$logoPath = Join-Path $outDir "logo.png"
$logo.Save($logoPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Wrote $logoPath"

# ICO with PNG entries
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngBlobs = @()
foreach ($sz in $sizes) {
    $b = Draw-Logo $sz
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBlobs += , $ms.ToArray()
    $ms.Dispose(); $b.Dispose()
}
$logo.Dispose()

$icoPath = Join-Path $outDir "phonexis.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
# ICONDIR
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
# ICONDIRENTRYs
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([Byte]$dim)      # width
    $bw.Write([Byte]$dim)      # height
    $bw.Write([Byte]0)         # palette
    $bw.Write([Byte]0)         # reserved
    $bw.Write([UInt16]1)       # planes
    $bw.Write([UInt16]32)      # bpp
    $bw.Write([UInt32]$pngBlobs[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngBlobs[$i].Length
}
foreach ($blob in $pngBlobs) { $bw.Write($blob) }
$bw.Close(); $fs.Close()
Write-Host "Wrote $icoPath"
