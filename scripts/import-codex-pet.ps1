<#
.SYNOPSIS
    Impor pet Codex (spritesheet.webp) menjadi skin Desktop Pet (spritesheet.png RGBA).
.DESCRIPTION
    Decoder WebP bawaan Windows (WIC) membuang kanal alpha, sehingga Desktop Pet hanya menerima PNG.
    Skrip ini mengonversi spritesheet.webp memakai tool pertama yang tersedia (dwebp, ImageMagick, ffmpeg),
    menulis skin.json dari pet.json, lalu memvalidasi hasilnya (ukuran atlas + alpha).

    Tanpa -Name: menampilkan daftar pet Codex yang tersedia.
.EXAMPLE
    powershell -File scripts\import-codex-pet.ps1
    powershell -File scripts\import-codex-pet.ps1 -Name kawahime
    powershell -File scripts\import-codex-pet.ps1 -Name kawahime -Force
#>
param(
    [string]$Name,
    [string]$Source = (Join-Path $env:USERPROFILE ".codex\pets"),
    [string]$Destination = (Join-Path $env:APPDATA "DesktopPet\Skins"),
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$CellWidth = 192
$CellHeight = 208
$Columns = 8
$Rows = 11

if (-not (Test-Path $Source)) {
    Write-Host "Folder pet Codex tidak ditemukan: $Source" -ForegroundColor Red
    exit 1
}

if (-not $Name) {
    Write-Host "Pet Codex yang tersedia di ${Source}:" -ForegroundColor Cyan
    Get-ChildItem $Source -Directory | ForEach-Object {
        $petJson = Join-Path $_.FullName "pet.json"
        $label = $_.Name
        if (Test-Path $petJson) {
            try { $label = "{0}  ({1})" -f $_.Name, (Get-Content $petJson -Raw | ConvertFrom-Json).displayName } catch { }
        }
        Write-Host "  $label"
    }
    Write-Host "`nJalankan ulang dengan -Name <nama> untuk mengimpor." -ForegroundColor Cyan
    exit 0
}

$petDir = Join-Path $Source $Name
if (-not (Test-Path $petDir)) {
    Write-Host "Pet '$Name' tidak ditemukan di $Source" -ForegroundColor Red
    exit 1
}

# 1. Read pet.json (optional)
$pet = $null
$petJsonPath = Join-Path $petDir "pet.json"
if (Test-Path $petJsonPath) {
    $pet = Get-Content $petJsonPath -Raw | ConvertFrom-Json
}
$sheetName = if ($pet -and $pet.spritesheetPath) { $pet.spritesheetPath } else { "spritesheet.webp" }
$webp = Join-Path $petDir $sheetName
if (-not (Test-Path $webp)) {
    Write-Host "Spritesheet tidak ditemukan: $webp" -ForegroundColor Red
    exit 1
}
if ($pet -and $pet.spriteVersionNumber -and $pet.spriteVersionNumber -ne 2) {
    Write-Host "Peringatan: spriteVersionNumber $($pet.spriteVersionNumber); Desktop Pet mengikuti format v2 (8x11 sel)." -ForegroundColor Yellow
}

# 2. Pick a converter that keeps alpha
$converter = @("dwebp", "magick", "ffmpeg") | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
if (-not $converter) {
    Write-Host "Tidak ada tool konversi WebP -> PNG yang menjaga transparansi." -ForegroundColor Red
    Write-Host "Pasang salah satu, lalu jalankan ulang skrip ini:" -ForegroundColor Yellow
    Write-Host "  winget install ImageMagick.ImageMagick   (magick)"
    Write-Host "  winget install Gyan.FFmpeg               (ffmpeg)"
    Write-Host "  dwebp (libwebp): https://developers.google.com/speed/webp/download"
    Write-Host "(Buka terminal baru setelah instalasi agar PATH diperbarui.)"
    exit 1
}

$skinDir = Join-Path $Destination $Name
if ((Test-Path $skinDir) -and -not $Force) {
    Write-Host "Skin '$Name' sudah ada di $skinDir. Gunakan -Force untuk menimpa." -ForegroundColor Red
    exit 1
}
New-Item -ItemType Directory -Path $skinDir -Force | Out-Null
$png = Join-Path $skinDir "spritesheet.png"
if (Test-Path $png) { Remove-Item $png -Force }

Write-Host "Mengonversi dengan $converter..." -ForegroundColor Cyan
switch ($converter) {
    "dwebp"  { & dwebp $webp -o $png | Out-Null }
    "magick" { & magick $webp "PNG32:$png" }
    "ffmpeg" { & ffmpeg -y -loglevel error -i $webp -pix_fmt rgba $png }
}
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $png)) {
    Write-Host "Konversi gagal ($converter, exit $LASTEXITCODE)." -ForegroundColor Red
    exit 1
}

# 3. Validate with the same decoder the app uses
Add-Type -AssemblyName PresentationCore
$stream = [IO.File]::OpenRead($png)
try {
    $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create($stream, 'PreservePixelFormat', 'OnLoad')
    $frame = $decoder.Frames[0]
    $format = $frame.Format.ToString()
    $width = $frame.PixelWidth
    $height = $frame.PixelHeight
} finally {
    $stream.Dispose()
}

$alphaFormats = @("Bgra32", "Pbgra32", "Rgba64", "Prgba64", "Rgba128Float", "Prgba128Float")
$problems = @()
if ($width -lt $CellWidth * $Columns -or $height -lt $CellHeight * $Rows) {
    $problems += "ukuran ${width}x${height}, butuh minimal $($CellWidth * $Columns)x$($CellHeight * $Rows)"
}
if ($alphaFormats -notcontains $format) {
    $problems += "format $format tanpa alpha"
}
if ($problems.Count -gt 0) {
    Write-Host ("Hasil konversi tidak valid: " + ($problems -join "; ")) -ForegroundColor Red
    exit 1
}

# 4. skin.json from pet.json
$manifest = [ordered]@{
    name        = if ($pet -and $pet.displayName) { $pet.displayName } else { $Name }
    author      = "Codex pet"
    description = if ($pet -and $pet.description) { $pet.description } else { "Diimpor dari $petDir" }
    spritesheet = "spritesheet.png"
    cellWidth   = $CellWidth
    cellHeight  = $CellHeight
}
$manifest | ConvertTo-Json | Set-Content (Join-Path $skinDir "skin.json") -Encoding UTF8

Write-Host "`nSkin berhasil diimpor:" -ForegroundColor Green
Write-Host "  $skinDir"
Write-Host "  spritesheet.png ${width}x${height} ($format)"
Write-Host "Pilih di Panel Kontrol > Skin (Muat Ulang Daftar) atau klik kanan pet > Skin." -ForegroundColor White
