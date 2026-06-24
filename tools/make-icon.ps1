# Генератор иконки приложения: «перевёрнутый монитор» (тема — эффекты над экраном).
# Рисует монитор (экран + ножка + подставка) и переворачивает по вертикали.
# Собирает многоразмерный .ico (PNG-кадры 16/32/48/64/128/256) для окна, трея и .exe.

Add-Type -AssemblyName System.Drawing

function New-MonitorBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [double]$size

    # Цвета темы.
    $frame  = [System.Drawing.Color]::FromArgb(255, 40, 30, 60)
    $screenA = [System.Drawing.Color]::FromArgb(255, 138, 99, 255)
    $screenB = [System.Drawing.Color]::FromArgb(255, 96, 60, 200)
    $accent = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)

    # Корпус монитора (экран).
    $mx = $s * 0.10
    $my = $s * 0.12
    $mw = $s * 0.80
    $mh = $s * 0.56
    $bodyRect = New-Object System.Drawing.RectangleF($mx, $my, $mw, $mh)

    $frameBrush = New-Object System.Drawing.SolidBrush($frame)
    $g.FillRectangle($frameBrush, $bodyRect)

    # Экран (внутренняя область) с градиентом.
    $pad = $s * 0.06
    $scrRect = New-Object System.Drawing.RectangleF(($mx + $pad), ($my + $pad), ($mw - 2*$pad), ($mh - 2*$pad))
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($scrRect, $screenA, $screenB, 45.0)
    $g.FillRectangle($lg, $scrRect)

    # Значок «эффекта» на экране — стрелка переворота (полукруг со стрелкой).
    $pen = New-Object System.Drawing.Pen($accent, [single]($s * 0.05))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $cx = $mx + $mw/2
    $cy = $my + $mh/2
    $r = $mh * 0.22
    $arcRect = New-Object System.Drawing.RectangleF(($cx - $r), ($cy - $r), (2*$r), (2*$r))
    $g.DrawArc($pen, $arcRect, 40, 260)

    # Ножка + подставка.
    $neckW = $s * 0.10
    $neckRect = New-Object System.Drawing.RectangleF(($cx - $neckW/2), ($my + $mh), $neckW, ($s * 0.12))
    $g.FillRectangle($frameBrush, $neckRect)
    $baseW = $s * 0.40
    $baseRect = New-Object System.Drawing.RectangleF(($cx - $baseW/2), ($my + $mh + $s*0.10), $baseW, ($s * 0.08))
    $g.FillRectangle($frameBrush, $baseRect)

    $g.Dispose()

    # Переворот по вертикали — «перевёрнутый монитор».
    $bmp.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipY)
    return $bmp
}

$sizes = @(16, 32, 48, 64, 128, 256)
$pngs = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($sz in $sizes) {
    $bmp = New-MonitorBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs.Add($ms.ToArray())
    $bmp.Dispose()
    $ms.Dispose()
}

# Сборка .ico (формат: ICONDIR + ICONDIRENTRY[] + PNG-данные).
$outDir = Join-Path $PSScriptRoot '..\ScreenApp\Resources'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outPath = Join-Path $outDir 'app.ico'

$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$count = $sizes.Count
$bw.Write([uint16]0)      # reserved
$bw.Write([uint16]1)      # type = icon
$bw.Write([uint16]$count) # image count

$offset = 6 + (16 * $count)
for ($i = 0; $i -lt $count; $i++) {
    $sz = $sizes[$i]
    $data = $pngs[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim)      # width
    $bw.Write([byte]$dim)      # height
    $bw.Write([byte]0)         # palette
    $bw.Write([byte]0)         # reserved
    $bw.Write([uint16]1)       # planes
    $bw.Write([uint16]32)      # bpp
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($data in $pngs) {
    $bw.Write($data, 0, $data.Length)
}

$bw.Flush()
$bw.Dispose()
$fs.Dispose()

Write-Output "Иконка создана: $outPath"
