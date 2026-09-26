<#
.SYNOPSIS
    Skrip Pemaketan Rilis Windows Desktop Pet
.DESCRIPTION
    1. Membangun single-file executable DesktopPet.exe (Release, win-x64)
    2. Membuat folder rilis mandiri di dist/DesktopPet-v1.0.0-win-x64/
    3. Mengompresi ke format ZIP dist/DesktopPet-v1.0.0-win-x64.zip
    4. Meng-update DesktopPet.exe di root direktori project
#>

$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $PSScriptRoot
$dotnetExe = "C:\Users\hoaaa\AppData\Local\Microsoft\dotnet\dotnet.exe"
if (-not (Test-Path $dotnetExe)) {
    $dotnetExe = "dotnet"
}

$version = "1.0.0"
$distDir = Join-Path $rootDir "dist"
$bundleDir = Join-Path $distDir "DesktopPet-v$version-win-x64"
$zipFile = Join-Path $distDir "DesktopPet-v$version-win-x64.zip"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Pemaketan Rilis Desktop Pet v$version (Windows x64)" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Bersihkan folder dist lama
Write-Host "[1/4] Menyiapkan direktori rilis..." -ForegroundColor Cyan
if (Test-Path $bundleDir) { Remove-Item $bundleDir -Recurse -Force }
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }
New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null

# 2. Hentikan DesktopPet jika aktif sebelum publish
Stop-Process -Name DesktopPet -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 3. Compile & Publish Release binary
Write-Host "[2/4] Melakukan build dan publish single-file executable..." -ForegroundColor Cyan
$projectPath = Join-Path $rootDir "src\DesktopPet\DesktopPet.csproj"
& $dotnetExe publish $projectPath -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -o (Join-Path $distDir "publish_temp")

if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] Kompilasi dotnet publish gagal!" -ForegroundColor Red
    exit 1
}

$publishedExe = Join-Path $distDir "publish_temp\DesktopPet.exe"

# 4. Salin file ke bundle
Write-Host "[3/4] Mengumpulkan berkas distribusi..." -ForegroundColor Cyan
Copy-Item $publishedExe (Join-Path $bundleDir "DesktopPet.exe") -Force
Copy-Item (Join-Path $rootDir "send-event.bat") (Join-Path $bundleDir "send-event.bat") -Force
Copy-Item (Join-Path $rootDir "install.ps1") (Join-Path $bundleDir "install.ps1") -Force
Copy-Item (Join-Path $rootDir "uninstall.ps1") (Join-Path $bundleDir "uninstall.ps1") -Force
Copy-Item (Join-Path $rootDir "installer.iss") (Join-Path $bundleDir "installer.iss") -Force
Copy-Item (Join-Path $rootDir "scripts") (Join-Path $bundleDir "scripts") -Recurse -Force

# Salin juga DesktopPet.exe ke root folder project
Copy-Item $publishedExe (Join-Path $rootDir "DesktopPet.exe") -Force

# Hapus folder publish_temp
Remove-Item (Join-Path $distDir "publish_temp") -Recurse -Force

# 5. Kompresi ke ZIP
Write-Host "[4/4] Membuat arsip portable ZIP..." -ForegroundColor Cyan
Compress-Archive -Path "$bundleDir\*" -DestinationPath $zipFile -Force

$zipSize = (Get-Item $zipFile).Length / 1MB
$exeSize = (Get-Item (Join-Path $rootDir "DesktopPet.exe")).Length / 1MB

Write-Host "`nPaket Rilis Berhasil Dibuat!" -ForegroundColor Green
Write-Host ("  Binary Root  : DesktopPet.exe ({0:N2} MB)" -f $exeSize) -ForegroundColor White
Write-Host "  Folder Bundle: dist\DesktopPet-v$version-win-x64\" -ForegroundColor White
Write-Host ("  Portable ZIP : dist\DesktopPet-v$version-win-x64.zip ({0:N2} MB)" -f $zipSize) -ForegroundColor White
