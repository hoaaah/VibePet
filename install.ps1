<#
.SYNOPSIS
    Script Penginstal Windows Desktop Pet (Kawahime)
.DESCRIPTION
    Menginstal Desktop Pet ke direktori aplikasi user ($env:LOCALAPPDATA\Programs\DesktopPet),
    membuat shortcut di Desktop dan Start Menu, serta opsional mengaktifkan auto-start Windows.
.PARAMETER AutoStart
    Mengaktifkan Desktop Pet otomatis saat Windows login.
#>
param(
    [switch]$AutoStart = $false
)

$ErrorActionPreference = "Stop"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Pemasangan Windows Desktop Pet (Kawahime) v1.0.0" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Hentikan instance berjalan jika ada
$running = Get-Process -Name DesktopPet -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[1/5] Menghentikan proses DesktopPet yang sedang berjalan..." -ForegroundColor Yellow
    Stop-Process -Name DesktopPet -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
} else {
    Write-Host "[1/5] Tidak ada proses DesktopPet berjalan sebelumnya." -ForegroundColor Green
}

# 2. Siapkan direktori instalasi
$installDir = Join-Path $env:LOCALAPPDATA "Programs\DesktopPet"
Write-Host "[2/5] Menyiapkan folder instalasi: $installDir" -ForegroundColor Cyan
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# Tentukan sumber binary DesktopPet.exe
$sourceExe = Join-Path $PSScriptRoot "DesktopPet.exe"
if (-not (Test-Path $sourceExe)) {
    $sourceExe = Join-Path $PSScriptRoot "publish\DesktopPet.exe"
}
if (-not (Test-Path $sourceExe)) {
    $sourceExe = Join-Path $PSScriptRoot "src\DesktopPet\bin\Release\net8.0-windows\win-x64\publish\DesktopPet.exe"
}
if (-not (Test-Path $sourceExe)) {
    Write-Host "[Error] File DesktopPet.exe tidak ditemukan di folder sumber!" -ForegroundColor Red
    exit 1
}

# 3. Salin file aplikasi
Write-Host "[3/5] Menyalin berkas aplikasi dan skrip integrasi..." -ForegroundColor Cyan
Copy-Item -Path $sourceExe -Destination (Join-Path $installDir "DesktopPet.exe") -Force

# Salin folder scripts dan send-event.bat jika ada
$scriptsFolder = Join-Path $PSScriptRoot "scripts"
if (Test-Path $scriptsFolder) {
    Copy-Item -Path $scriptsFolder -Destination $installDir -Recurse -Force
}
$sendEventBat = Join-Path $PSScriptRoot "send-event.bat"
if (Test-Path $sendEventBat) {
    Copy-Item -Path $sendEventBat -Destination $installDir -Force
}
$uninstallScript = Join-Path $PSScriptRoot "uninstall.ps1"
if (Test-Path $uninstallScript) {
    Copy-Item -Path $uninstallScript -Destination $installDir -Force
}

$targetExe = Join-Path $installDir "DesktopPet.exe"

# 4. Buat shortcut Desktop & Start Menu via WScript.Shell
Write-Host "[4/5] Membuat pintasan (Shortcut) Desktop & Menu Start..." -ForegroundColor Cyan
$wsh = New-Object -ComObject WScript.Shell

# Shortcut Desktop
$desktopPath = [Environment]::GetFolderPath("Desktop")
$desktopLnk = Join-Path $desktopPath "Desktop Pet.lnk"
$scDesktop = $wsh.CreateShortcut($desktopLnk)
$scDesktop.TargetPath = $targetExe
$scDesktop.WorkingDirectory = $installDir
$scDesktop.Description = "Windows Desktop Pet (Kawahime)"
$scDesktop.Save()

# Shortcut Start Menu
$startMenuPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$startMenuLnk = Join-Path $startMenuPath "Desktop Pet.lnk"
$scStart = $wsh.CreateShortcut($startMenuLnk)
$scStart.TargetPath = $targetExe
$scStart.WorkingDirectory = $installDir
$scStart.Description = "Windows Desktop Pet (Kawahime)"
$scStart.Save()

# 5. Konfigurasi Auto-Start (opsional / parameter)
if ($AutoStart) {
    Write-Host "[5/5] Mendaftarkan Desktop Pet ke Auto-Start Windows (Registry Run)..." -ForegroundColor Yellow
    $runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    Set-ItemProperty -Path $runKey -Name "DesktopPet" -Value "`"$targetExe`""
} else {
    Write-Host "[5/5] Auto-start tidak diaktifkan (dapat diubah nanti di Panel Kontrol)." -ForegroundColor Gray
}

Write-Host "`nInstalasi Berhasil!" -ForegroundColor Green
Write-Host "Pintasan telah dibuat di Desktop dan Menu Start." -ForegroundColor White
Write-Host "Untuk menjalankan, buka 'Desktop Pet' dari Desktop atau jalankan:" -ForegroundColor White
Write-Host "  & `"$targetExe`"" -ForegroundColor Cyan
