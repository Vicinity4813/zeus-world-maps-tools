# Generates the converter icon (original drawing): a golden globe with "enlarge" arrows, on a navy background.
# Genere l'icone du convertisseur (dessin original) : globe dore et fleches d'agrandissement, fond bleu nuit.
# Multi-size .ico: 256 px as PNG, 48 / 32 / 16 px as 32-bit BMP (Windows Vista and later).
param([string]$Out = (Join-Path $PSScriptRoot '..\src\world-maps.ico'))
Add-Type -AssemblyName System.Drawing

function Draw([int]$s) {
    $b = New-Object Drawing.Bitmap $s, $s
    $g = [Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([Drawing.Color]::Transparent)
    $k = $s / 256.0
    # background: navy rounded square
    $r = 44 * $k; $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc(8*$k, 8*$k, 2*$r, 2*$r, 180, 90); $path.AddArc(248*$k - 2*$r, 8*$k, 2*$r, 2*$r, 270, 90)
    $path.AddArc(248*$k - 2*$r, 248*$k - 2*$r, 2*$r, 2*$r, 0, 90); $path.AddArc(8*$k, 248*$k - 2*$r, 2*$r, 2*$r, 90, 90); $path.CloseFigure()
    $bg = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.PointF 0, 0), (New-Object Drawing.PointF 0, $s), [Drawing.Color]::FromArgb(28, 58, 110), [Drawing.Color]::FromArgb(12, 28, 60))
    $g.FillPath($bg, $path)
    # globe: golden circle with meridians and parallels
    $gold = [Drawing.Color]::FromArgb(236, 186, 74)
    $pw = [Math]::Max(1.2, 9 * $k); $pen = New-Object Drawing.Pen $gold, $pw
    $cx = 128 * $k; $cy = 128 * $k; $rad = 62 * $k
    $g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 236, 186, 74))), $cx - $rad, $cy - $rad, 2 * $rad, 2 * $rad)
    $g.DrawEllipse($pen, $cx - $rad, $cy - $rad, 2 * $rad, 2 * $rad)
    if ($s -ge 32) {
        $g.DrawEllipse($pen, $cx - $rad * 0.45, $cy - $rad, $rad * 0.9, 2 * $rad)
        $g.DrawLine($pen, $cx, $cy - $rad, $cx, $cy + $rad)
        $g.DrawLine($pen, $cx - $rad, $cy, $cx + $rad, $cy)
        $h = $rad * 0.55; $w = [Math]::Sqrt($rad * $rad - $h * $h)
        $g.DrawLine($pen, $cx - $w, $cy - $h, $cx + $w, $cy - $h); $g.DrawLine($pen, $cx - $w, $cy + $h, $cx + $w, $cy + $h)
    } else { $g.DrawLine($pen, $cx - $rad, $cy, $cx + $rad, $cy); $g.DrawLine($pen, $cx, $cy - $rad, $cx, $cy + $rad) }
    # white "enlarge" arrows in the four corners
    $aw = [Math]::Max(1.5, 13 * $k); $ap = New-Object Drawing.Pen ([Drawing.Color]::White), $aw
    $ap.StartCap = 'Round'; $ap.EndCap = 'Round'
    $l = 34 * $k
    foreach ($c in @(@(40, 40, 1, 1), @(216, 40, -1, 1), @(40, 216, 1, -1), @(216, 216, -1, -1))) {
        $x = $c[0] * $k; $y = $c[1] * $k
        $g.DrawLine($ap, $x, $y, $x + $c[2] * $l, $y); $g.DrawLine($ap, $x, $y, $x, $y + $c[3] * $l)
        if ($s -ge 32) { $g.DrawLine($ap, $x, $y, $x + $c[2] * $l * 0.8, $y + $c[3] * $l * 0.8) }
    }
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    if ($s -ge 256) { $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png) }
    else {
        $bw = New-Object IO.BinaryWriter $ms
        $bw.Write([uint32]40); $bw.Write([int32]$s); $bw.Write([int32]($s * 2)); $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]0); $bw.Write([uint32]($s * $s * 4)); $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
        for ($y = $s - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $s; $x++) { $c = $b.GetPixel($x, $y); $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A) } }
        $stride = [int]([Math]::Ceiling($s / 32.0) * 4)
        for ($y = $s - 1; $y -ge 0; $y--) { $row = New-Object byte[] $stride; for ($x = 0; $x -lt $s; $x++) { if ($b.GetPixel($x, $y).A -lt 128) { $row[$x -shr 3] = $row[$x -shr 3] -bor (0x80 -shr ($x -band 7)) } }; $bw.Write($row) }
        $bw.Flush()
    }
    $b.Dispose()
    return , $ms.ToArray()
}

$sizes = 256, 48, 32, 16
$imgs = @(); foreach ($s in $sizes) { $imgs += , (Draw $s) }
$fs = [IO.File]::Create($Out); $w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$off = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$d); $w.Write([byte]$d); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$imgs[$i].Length); $w.Write([uint32]$off)
    $off += $imgs[$i].Length
}
foreach ($img in $imgs) { $w.Write($img) }
$w.Close()
"icon: $Out"
