<#
.SYNOPSIS
    Rakit frame-frame sprite (misalnya hasil generate AI) menjadi atlas Desktop Pet (spritesheet.png + skin.json).
.DESCRIPTION
    Atlas mengikuti kontrak Desktop Pet / Codex v2: 8 kolom x 11 baris sel (bawaan 192 x 208 px).
      Baris 0-8 : idle, running-right, running-left, waving, jumping, failed, waiting, running, review
      Baris 9-10: 16 arah pandang (gaze), 0 derajat = atas, searah jarum jam

    Sumber frame (pilih salah satu):
      1. -InputDir dengan subfolder per animasi, berisi satu file per frame (diurutkan menurut nama):
           input\idle\01.png, 02.png, ...
           input\running-right\...   input\waving\...   input\gaze\ (16 frame)
      2. -InputDir dengan satu file strip horizontal per animasi, dipotong sama rata:
           input\idle.png  (6 frame)   input\running-right.png  (8 frame) ...
         Jumlah frame strip = jumlah bawaan, atau atur dengan -StripFrames @{ idle = 4 }.
      3. -SheetImage: satu gambar sheet 8 x 11 (misalnya hasil prompt "sekaligus"), dipotong rata per sel.

    Untuk setiap frame skrip ini:
      - membuang latar polos bila -ChromaKey diisi (misalnya '#00FF00' dari prompt AI), termasuk
        pinggiran hijau di tepi karakter;
      - memotong ke batas karakter, menskalakan (-ScaleMode Global: satu skala untuk semua frame;
        Animation: tinggi karakter disamakan antar animasi bila tiap baris di-generate terpisah),
        lalu menaruhnya di tengah sel dengan kaki di garis dasar yang sama;
      - animasi di -KeepPosition (bawaan: jumping) mempertahankan perbedaan tinggi antar frame;
      - running-left dibuat otomatis dari cermin running-right bila tidak disediakan;
      - arah pandang yang tidak disediakan diisi frame idle pertama (pet tidak menoleh).

    Hasil: spritesheet.png (RGBA) + skin.json di -OutputDir, divalidasi ukuran dan alpha-nya.
    Dengan -InstallAs, hasil langsung disalin ke %AppData%\DesktopPet\Skins\<nama>\.
.EXAMPLE
    powershell -File scripts\build-atlas.ps1 -InputDir .\frames -ChromaKey '#00FF00' -Name "Karakter Ku" -InstallAs KarakterKu
.EXAMPLE
    powershell -File scripts\build-atlas.ps1 -SheetImage .\ai-sheet.png -ChromaKey '#00FF00' -InstallAs KarakterKu
.EXAMPLE
    powershell -File scripts\build-atlas.ps1 -InputDir .\strips -ChromaKey '#00FF00' -ScaleMode Animation -StripFrames @{ idle = 4 } -PixelArt
#>
[CmdletBinding(DefaultParameterSetName = 'Folder')]
param(
    [Parameter(ParameterSetName = 'Folder', Mandatory = $true, Position = 0)]
    [string]$InputDir,

    [Parameter(ParameterSetName = 'Sheet', Mandatory = $true)]
    [string]$SheetImage,

    [string]$OutputDir,
    [string]$Name,
    [string]$Author,

    [int]$CellWidth = 192,
    [int]$CellHeight = 208,
    [int]$Padding = 6,

    # Warna latar yang dibuang, misalnya '#00FF00'. Kosong = frame harus sudah transparan.
    [string]$ChromaKey,
    [int]$ChromaTolerance = 60,
    [int]$ChromaFeather = 40,
    # Pembersihan sisa warna latar (spill) yang tembus di tepi/sela rambut:
    #   Edge = hanya pita tepi selebar -ChromaEdge px (aman bila karakter punya warna yang mirip latar)
    #   All  = seluruh gambar (hasil terbaik bila karakter TIDAK memakai warna latar sama sekali)
    #   Off  = tidak dibersihkan
    [ValidateSet('Edge', 'All', 'Off')]
    [string]$ChromaSpill = 'Edge',
    [int]$ChromaEdge = 3,

    # Global    = satu skala untuk semua frame (ukuran asli antar animasi dipertahankan).
    # Animation = tiap animasi disamakan tinggi karakternya (untuk baris hasil prompt terpisah yang skalanya beda).
    # Frame     = tiap frame dipaskan ke sel sendiri-sendiri.
    [ValidateSet('Global', 'Animation', 'Frame')]
    [string]$ScaleMode = 'Global',
    # Penskalaan nearest-neighbor, untuk pixel art.
    [switch]$PixelArt,
    # Animasi yang mempertahankan posisi vertikal relatif antar frame (tinggi lompatan).
    [string[]]$KeepPosition = @('jumping'),
    # Jumlah frame untuk file strip, misalnya @{ idle = 4 }.
    [hashtable]$StripFrames = @{},

    # Salin hasil ke %AppData%\DesktopPet\Skins\<InstallAs>\
    [string]$InstallAs,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw "Skrip ini butuh thread STA (WPF). Jalankan dengan: powershell -STA -File scripts\build-atlas.ps1 ..."
}

# --- Kontrak atlas -------------------------------------------------------------------------

$Columns = 8
$Rows = 11
$Animations = @(
    @{ Key = 'idle';          Row = 0; Frames = 6 }
    @{ Key = 'running-right'; Row = 1; Frames = 8 }
    @{ Key = 'running-left';  Row = 2; Frames = 8 }
    @{ Key = 'waving';        Row = 3; Frames = 4 }
    @{ Key = 'jumping';       Row = 4; Frames = 5 }
    @{ Key = 'failed';        Row = 5; Frames = 8 }
    @{ Key = 'waiting';       Row = 6; Frames = 6 }
    @{ Key = 'running';       Row = 7; Frames = 6 }
    @{ Key = 'review';        Row = 8; Frames = 6 }
    @{ Key = 'gaze';          Row = 9; Frames = 16 }   # baris 9 dan 10
)

# --- Operasi per piksel (C# agar cepat) ---------------------------------------------------

if (-not ('AtlasPixels' -as [type])) {
    Add-Type -TypeDefinition @'
using System;

public static class AtlasPixels
{
    // BGRA (non-premultiplied). Pixels close to the key become transparent; a soft band keeps edges smooth.
    // Anti-aliased edges that were blended with the background keep a key-colored fringe, so pixels within
    // edgeRadius of the background lose transparency in proportion to their key-color excess and are despilled.
    public static void ChromaKey(byte[] px, int width, int height, int keyR, int keyG, int keyB,
                                 int tolerance, int feather, int edgeRadius)
    {
        for (int i = 0; i < px.Length; i += 4)
        {
            int db = px[i] - keyB, dg = px[i + 1] - keyG, dr = px[i + 2] - keyR;
            double d = Math.Sqrt(db * db + dg * dg + dr * dr);
            if (d <= tolerance)
            {
                px[i] = 0; px[i + 1] = 0; px[i + 2] = 0; px[i + 3] = 0;
            }
            else if (feather > 0 && d < tolerance + feather)
            {
                int a = (int)((d - tolerance) / feather * 255.0);
                if (a < px[i + 3]) px[i + 3] = (byte)a;
            }
        }

        // Which channel the key color is made of (B=0, G=1, R=2); -1 = not a primary color, skip spill removal
        int channel = -1;
        if (keyG > 200 && keyR < 100 && keyB < 100) channel = 1;
        else if (keyB > 200 && keyR < 100 && keyG < 100) channel = 0;
        else if (keyR > 200 && keyG < 100 && keyB < 100) channel = 2;
        if (channel < 0 || edgeRadius == 0) return;
        bool everywhere = edgeRadius < 0;   // -1: clean spill on every pixel (character has no key color)

        // Mostly transparent pixels (including the soft band) count as background for the edge test
        bool[] background = new bool[width * height];
        for (int p = 0; p < background.Length; p++) background[p] = px[p * 4 + 3] < 200;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int p = y * width + x;
                if (px[p * 4 + 3] == 0) continue;

                bool nearBackground = everywhere || background[p];
                for (int ny = Math.Max(0, y - edgeRadius); ny <= Math.Min(height - 1, y + edgeRadius) && !nearBackground; ny++)
                    for (int nx = Math.Max(0, x - edgeRadius); nx <= Math.Min(width - 1, x + edgeRadius); nx++)
                        if (background[ny * width + nx]) { nearBackground = true; break; }
                if (!nearBackground) continue;

                int i = p * 4;
                int other = Math.Max(px[i + (channel + 1) % 3], px[i + (channel + 2) % 3]);
                int excess = px[i + channel] - other;
                if (excess <= 0) continue;

                px[i + channel] = (byte)other;
                px[i + 3] = (byte)(px[i + 3] * Math.Max(0.0, 1.0 - excess / 160.0));
            }
        }
    }

    // Returns {x, y, width, height} of pixels with alpha > threshold, or null when the frame is empty.
    public static int[] Bounds(byte[] px, int width, int height, int threshold)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            int row = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                if (px[row + x * 4 + 3] > threshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX < 0) return null;
        return new int[] { minX, minY, maxX - minX + 1, maxY - minY + 1 };
    }

    public static bool HasTransparency(byte[] px)
    {
        for (int i = 3; i < px.Length; i += 4)
        {
            if (px[i] < 250) return true;
        }
        return false;
    }
}
'@
}

# --- Helper --------------------------------------------------------------------------------

function Get-NormalizedKey([string]$text) {
    return ($text -replace '[-_\s]', '').ToLowerInvariant()
}

function Get-NaturalSortKey([string]$text) {
    return [regex]::Replace($text.ToLowerInvariant(), '\d+', { param($m) $m.Value.PadLeft(10, '0') })
}

function ConvertFrom-HexColor([string]$hex) {
    $h = $hex.Trim().TrimStart('#')
    if ($h.Length -ne 6) { throw "Warna -ChromaKey harus format #RRGGBB, bukan '$hex'." }
    return @(
        [Convert]::ToInt32($h.Substring(0, 2), 16),
        [Convert]::ToInt32($h.Substring(2, 2), 16),
        [Convert]::ToInt32($h.Substring(4, 2), 16)
    )
}

# Decode any image (PNG/JPG/BMP/WebP) into BGRA bytes.
function Read-Bgra([string]$path) {
    $full = (Resolve-Path -LiteralPath $path).Path
    $stream = [IO.File]::OpenRead($full)
    try {
        $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(
            $stream,
            [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
            [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
        $frame = $decoder.Frames[0]
    } finally {
        $stream.Dispose()
    }
    $bgra = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($frame, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $stride = $bgra.PixelWidth * 4
    $pixels = New-Object byte[] ($stride * $bgra.PixelHeight)
    $bgra.CopyPixels($pixels, $stride, 0)
    return @{ W = $bgra.PixelWidth; H = $bgra.PixelHeight; Pixels = $pixels; Path = $full }
}

function Get-Region($image, [int]$x, [int]$y, [int]$w, [int]$h) {
    $src = [System.Windows.Media.Imaging.BitmapSource]::Create($image.W, $image.H, 96, 96,
        [System.Windows.Media.PixelFormats]::Bgra32, $null, $image.Pixels, $image.W * 4)
    $crop = New-Object System.Windows.Media.Imaging.CroppedBitmap($src, (New-Object System.Windows.Int32Rect($x, $y, $w, $h)))
    $pixels = New-Object byte[] ($w * 4 * $h)
    $crop.CopyPixels($pixels, $w * 4, 0)
    return @{ W = $w; H = $h; Pixels = $pixels; Path = $image.Path }
}

# Split an image into equal cells (strip or sheet).
function Split-Image($image, [int]$cols, [int]$rowsCount, [int]$row, [int]$count) {
    $cellW = [Math]::Floor($image.W / $cols)
    $cellH = [Math]::Floor($image.H / $rowsCount)
    $result = @()
    for ($i = 0; $i -lt $count; $i++) {
        $result += , (Get-Region $image ($i * $cellW) ($row * $cellH) $cellW $cellH)
    }
    return $result
}

$script:ChromaRgb = $null
if ($ChromaKey) { $script:ChromaRgb = ConvertFrom-HexColor $ChromaKey }

# Remove background, find the character bounds, keep only that part.
function ConvertTo-Frame($image, [string]$label) {
    $px = $image.Pixels
    if ($script:ChromaRgb) {
        $spillRadius = switch ($ChromaSpill) { 'All' { -1 } 'Off' { 0 } default { $ChromaEdge } }
        [AtlasPixels]::ChromaKey($px, $image.W, $image.H, $script:ChromaRgb[0], $script:ChromaRgb[1], $script:ChromaRgb[2],
            $ChromaTolerance, $ChromaFeather, $spillRadius)
    }

    if (-not [AtlasPixels]::HasTransparency($px)) {
        $c = '#{0:X2}{1:X2}{2:X2}' -f $px[2], $px[1], $px[0]
        throw "$label tidak punya transparansi (sudut kiri atas berwarna $c). Hapus latarnya dulu, atau jalankan dengan -ChromaKey '$c'."
    }

    $bounds = [AtlasPixels]::Bounds($px, $image.W, $image.H, 8)
    if ($null -eq $bounds) { throw "$label kosong (semua piksel transparan). Periksa -ChromaTolerance atau file sumbernya." }

    $src = [System.Windows.Media.Imaging.BitmapSource]::Create($image.W, $image.H, 96, 96,
        [System.Windows.Media.PixelFormats]::Bgra32, $null, $px, $image.W * 4)
    $crop = New-Object System.Windows.Media.Imaging.CroppedBitmap($src,
        (New-Object System.Windows.Int32Rect($bounds[0], $bounds[1], $bounds[2], $bounds[3])))
    $crop.Freeze()

    return [pscustomobject]@{
        Label  = $label
        Bitmap = $crop
        W      = $bounds[2]
        H      = $bounds[3]
        Bottom = $bounds[1] + $bounds[3]   # feet position inside the source frame
        Mirror = $false
    }
}

# --- 1. Kumpulkan frame --------------------------------------------------------------------

$imageExtensions = @('.png', '.jpg', '.jpeg', '.bmp', '.webp')
$frames = [ordered]@{}   # key -> list of frame objects

if ($PSCmdlet.ParameterSetName -eq 'Sheet') {
    Write-Host "Membaca sheet $SheetImage ..." -ForegroundColor Cyan
    $sheet = Read-Bgra $SheetImage
    foreach ($anim in $Animations) {
        $list = @()
        if ($anim.Key -eq 'gaze') {
            $parts = @(Split-Image $sheet $Columns $Rows 9 8) + @(Split-Image $sheet $Columns $Rows 10 8)
        } else {
            $parts = @(Split-Image $sheet $Columns $Rows $anim.Row $anim.Frames)
        }
        for ($i = 0; $i -lt $parts.Count; $i++) {
            $list += ConvertTo-Frame $parts[$i] "$($anim.Key) frame $($i + 1)"
        }
        $frames[$anim.Key] = $list
    }
    if (-not $OutputDir) { $OutputDir = Join-Path (Split-Path -Parent (Resolve-Path $SheetImage).Path) 'atlas' }
} else {
    if (-not (Test-Path -LiteralPath $InputDir -PathType Container)) { throw "Folder input tidak ditemukan: $InputDir" }
    $entries = Get-ChildItem -LiteralPath $InputDir

    foreach ($anim in $Animations) {
        $wanted = Get-NormalizedKey $anim.Key
        $folder = $entries | Where-Object { $_.PSIsContainer -and (Get-NormalizedKey $_.Name) -eq $wanted } | Select-Object -First 1
        $strip = $entries | Where-Object {
            -not $_.PSIsContainer -and $imageExtensions -contains $_.Extension.ToLowerInvariant() -and
            (Get-NormalizedKey $_.BaseName) -eq $wanted
        } | Select-Object -First 1

        $list = @()
        if ($folder) {
            $files = Get-ChildItem -LiteralPath $folder.FullName -File |
                Where-Object { $imageExtensions -contains $_.Extension.ToLowerInvariant() } |
                Sort-Object { Get-NaturalSortKey $_.Name }
            foreach ($file in $files) {
                $list += ConvertTo-Frame (Read-Bgra $file.FullName) "$($anim.Key)\$($file.Name)"
            }
        } elseif ($strip) {
            $count = $anim.Frames
            foreach ($k in $StripFrames.Keys) { if ((Get-NormalizedKey $k) -eq $wanted) { $count = [int]$StripFrames[$k] } }
            $image = Read-Bgra $strip.FullName
            $parts = @(Split-Image $image $count 1 0 $count)
            for ($i = 0; $i -lt $parts.Count; $i++) {
                $list += ConvertTo-Frame $parts[$i] "$($strip.Name) frame $($i + 1)"
            }
        }

        if ($list.Count -gt 0) {
            $frames[$anim.Key] = $list
            Write-Host ("  {0,-14} {1} frame" -f $anim.Key, $list.Count)
        }
    }
    if (-not $OutputDir) { $OutputDir = Join-Path $InputDir 'atlas' }
}

# --- 2. Lengkapi & validasi ----------------------------------------------------------------

$warnings = @()

if (-not $frames.Contains('running-left') -and $frames.Contains('running-right')) {
    $frames['running-left'] = @($frames['running-right'] | ForEach-Object {
        [pscustomobject]@{ Label = "$($_.Label) (cermin)"; Bitmap = $_.Bitmap; W = $_.W; H = $_.H; Bottom = $_.Bottom; Mirror = $true }
    })
    $warnings += 'running-left dibuat dari cermin running-right.'
}

if (-not $frames.Contains('gaze') -and $frames.Contains('idle')) {
    $first = $frames['idle'][0]
    $frames['gaze'] = @(1..16 | ForEach-Object { $first })
    $warnings += 'Arah pandang (gaze) tidak disediakan; diisi frame idle pertama, jadi pet tidak menoleh.'
}

$missing = @($Animations | Where-Object { -not $frames.Contains($_.Key) } | ForEach-Object { $_.Key })
if ($missing.Count -gt 0) {
    throw "Animasi berikut belum ada frame-nya: $($missing -join ', '). Buat subfolder atau file strip dengan nama tersebut di $InputDir."
}

foreach ($anim in $Animations) {
    $count = $frames[$anim.Key].Count
    if ($anim.Key -eq 'gaze') {
        if ($count -ne 16) { throw "gaze harus tepat 16 frame (0 derajat = atas, searah jarum jam), ditemukan $count." }
    } elseif ($count -lt 1 -or $count -gt $Columns) {
        throw "$($anim.Key) punya $count frame; harus 1-$Columns."
    }
}

# --- 3. Skala & tata letak -----------------------------------------------------------------

$availableW = $CellWidth - 2 * $Padding
$availableH = $CellHeight - 2 * $Padding
$allFrames = @($frames.Values | ForEach-Object { $_ })
$maxW = ($allFrames | Measure-Object -Property W -Maximum).Maximum
$maxH = ($allFrames | Measure-Object -Property H -Maximum).Maximum
$globalScale = [Math]::Min($availableW / $maxW, $availableH / $maxH)

function Get-Median($values) {
    $sorted = @($values | Sort-Object)
    $n = $sorted.Count
    if ($n % 2 -eq 1) { return $sorted[($n - 1) / 2] }
    return ($sorted[$n / 2 - 1] + $sorted[$n / 2]) / 2
}

# Typical character height per animation (median of its frames)
$medianH = @{}
foreach ($anim in $Animations) { $medianH[$anim.Key] = Get-Median ($frames[$anim.Key] | ForEach-Object { $_.H }) }

# Animation mode: every animation gets the same typical height k, as large as still fits every frame
$animScale = @{}
if ($ScaleMode -eq 'Animation') {
    $k = [double]::MaxValue
    foreach ($anim in $Animations) {
        foreach ($f in $frames[$anim.Key]) {
            $k = [Math]::Min($k, [Math]::Min($availableW * $medianH[$anim.Key] / $f.W, $availableH * $medianH[$anim.Key] / $f.H))
        }
    }
    foreach ($anim in $Animations) { $animScale[$anim.Key] = $k / $medianH[$anim.Key] }
} elseif ($ScaleMode -eq 'Global') {
    $heights = @($medianH.Values)
    $ratio = ($heights | Measure-Object -Maximum).Maximum / ($heights | Measure-Object -Minimum).Minimum
    if ($ratio -gt 1.35) {
        $tallest = ($medianH.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key
        $shortest = ($medianH.GetEnumerator() | Sort-Object Value | Select-Object -First 1).Key
        $warnings += ("Tinggi karakter antar animasi berbeda {0:0.0}x ({1} vs {2}); kemungkinan skala sumbernya berbeda. " +
            "Coba -ScaleMode Animation.") -f $ratio, $tallest, $shortest
    }
}

$atlasW = $CellWidth * $Columns
$atlasH = $CellHeight * $Rows
$visual = New-Object System.Windows.Media.DrawingVisual
$scalingMode = if ($PixelArt) { [System.Windows.Media.BitmapScalingMode]::NearestNeighbor } else { [System.Windows.Media.BitmapScalingMode]::HighQuality }
[System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, $scalingMode)
$dc = $visual.RenderOpen()
$keepKeys = @($KeepPosition | ForEach-Object { Get-NormalizedKey $_ })

foreach ($anim in $Animations) {
    $list = $frames[$anim.Key]
    $keep = $keepKeys -contains (Get-NormalizedKey $anim.Key)
    $lowestBottom = ($list | Measure-Object -Property Bottom -Maximum).Maximum

    for ($i = 0; $i -lt $list.Count; $i++) {
        $f = $list[$i]
        if ($anim.Key -eq 'gaze') {
            $row = 9 + [Math]::Floor($i / 8); $col = $i % 8
        } else {
            $row = $anim.Row; $col = $i
        }

        $scale = switch ($ScaleMode) {
            'Frame'     { [Math]::Min($availableW / $f.W, $availableH / $f.H) }
            'Animation' { $animScale[$anim.Key] }
            default     { $globalScale }
        }
        $w = [Math]::Round($f.W * $scale)
        $h = [Math]::Round($f.H * $scale)

        # Feet on the shared baseline; KeepPosition rows keep their height relative to the lowest frame
        $lift = if ($keep) { ($lowestBottom - $f.Bottom) * $scale } else { 0 }
        $x = $col * $CellWidth + [Math]::Round(($CellWidth - $w) / 2)
        $y = $row * $CellHeight + $CellHeight - $Padding - $h - [Math]::Round($lift)
        if ($y -lt $row * $CellHeight) {
            $warnings += "$($f.Label) terlalu tinggi untuk sel; posisinya dipotong ke atas sel."
            $y = $row * $CellHeight
        }

        $rect = New-Object System.Windows.Rect($x, $y, $w, $h)
        if ($f.Mirror) {
            $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform(-1, 1, ($x + $w / 2), ($y + $h / 2))))
            $dc.DrawImage($f.Bitmap, $rect)
            $dc.Pop()
        } else {
            $dc.DrawImage($f.Bitmap, $rect)
        }
    }
}
$dc.Close()

$target = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($atlasW, $atlasH, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$target.Render($visual)

# --- 4. Simpan & validasi ------------------------------------------------------------------

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$pngPath = Join-Path $OutputDir 'spritesheet.png'
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($target))
$out = [IO.File]::Create($pngPath)
try { $encoder.Save($out) } finally { $out.Dispose() }

$check = Read-Bgra $pngPath
if ($check.W -ne $atlasW -or $check.H -ne $atlasH) { throw "Validasi gagal: atlas $($check.W)x$($check.H), seharusnya ${atlasW}x${atlasH}." }
if (-not [AtlasPixels]::HasTransparency($check.Pixels)) { throw 'Validasi gagal: atlas tidak punya transparansi.' }

# skin.json: only values that differ from the defaults
$manifest = [ordered]@{}
$manifest.name = if ($Name) { $Name } elseif ($InstallAs) { $InstallAs } else { Split-Path -Leaf (Split-Path -Parent $pngPath) }
if ($Author) { $manifest.author = $Author }
$manifest.spritesheet = 'spritesheet.png'
if ($CellWidth -ne 192 -or $CellHeight -ne 208) {
    $manifest.cellWidth = $CellWidth
    $manifest.cellHeight = $CellHeight
}
$overrides = [ordered]@{}
foreach ($anim in $Animations) {
    if ($anim.Key -eq 'gaze') { continue }
    $count = $frames[$anim.Key].Count
    if ($count -ne $anim.Frames) { $overrides[$anim.Key] = [ordered]@{ frames = $count } }
}
if ($overrides.Count -gt 0) { $manifest.animations = $overrides }

$jsonPath = Join-Path $OutputDir 'skin.json'
[IO.File]::WriteAllText($jsonPath, ($manifest | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))

Write-Host "`nAtlas berhasil dibuat:" -ForegroundColor Green
Write-Host "  $pngPath (${atlasW}x${atlasH}, sel ${CellWidth}x${CellHeight})"
Write-Host "  $jsonPath"
Write-Host "  mode skala: $ScaleMode"
foreach ($w in ($warnings | Select-Object -Unique)) { Write-Host "  Catatan: $w" -ForegroundColor Yellow }

# --- 5. Pasang sebagai skin (opsional) -----------------------------------------------------

if ($InstallAs) {
    $skinDir = Join-Path $env:APPDATA "DesktopPet\Skins\$InstallAs"
    if ((Test-Path -LiteralPath $skinDir) -and -not $Force) {
        throw "Skin '$InstallAs' sudah ada di $skinDir. Gunakan -Force untuk menimpa."
    }
    New-Item -ItemType Directory -Path $skinDir -Force | Out-Null
    Copy-Item -LiteralPath $pngPath, $jsonPath -Destination $skinDir -Force
    Write-Host "`nTerpasang sebagai skin: $skinDir" -ForegroundColor Green
    Write-Host 'Pilih di Panel Kontrol > Skin / Paket Sprite (Muat Ulang Daftar) atau klik kanan pet > Skin.'
}
