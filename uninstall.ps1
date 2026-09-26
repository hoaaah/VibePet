<#
.SYNOPSIS
    Script Penghapus Windows Desktop Pet (Kawahime)
.DESCRIPTION
    Menghapus Desktop Pet dari sistem, membersihkan shortcut dan registri startup.
#>
param(
    [switch]$KeepSettings = $false
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "======================================================" -ForegroundColor Yellow
Write-Host "  Pencopotan Windows Desktop Pet (Kawahime)" -ForegroundColor Yellow
Write-Host "======================================================" -ForegroundColor Yellow

# 1. Hentikan instance jika sedang berjalan
Write-Host "[1/4] Menghentikan proses DesktopPet..." -ForegroundColor Cyan
Stop-Process -Name DesktopPet -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800

# 2. Hapus Auto-start dari Registry
Write-Host "[2/4] Menghapus Auto-Start dari Registry..." -ForegroundColor Cyan
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "DesktopPet" -ErrorAction SilentlyContinue

# 3. Hapus Shortcuts
Write-Host "[3/4] Menghapus pintasan Desktop & Menu Start..." -ForegroundColor Cyan
$desktopLnk = Join-Path ([Environment]::GetFolderPath("Desktop")) "Desktop Pet.lnk"
if (Test-Path $desktopLnk) { Remove-Item $desktopLnk -Force }

$startMenuLnk = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Desktop Pet.lnk"
if (Test-Path $startMenuLnk) { Remove-Item $startMenuLnk -Force }

# 4. Hapus direktori aplikasi
Write-Host "[4/4] Menghapus berkas aplikasi..." -ForegroundColor Cyan
$installDir = Join-Path $env:LOCALAPPDATA "Programs\DesktopPet"
if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}

# Pengaturan pengguna (%AppData%\DesktopPet)
$settingsDir = Join-Path $env:APPDATA "DesktopPet"
if (-not $KeepSettings -and (Test-Path $settingsDir)) {
    Remove-Item $settingsDir -Recurse -Force
    Write-Host "Folder data dan preferensi ($settingsDir) telah dibersihkan." -ForegroundColor Gray
}

Write-Host "`nDesktop Pet berhasil dihapus dari sistem." -ForegroundColor Green
