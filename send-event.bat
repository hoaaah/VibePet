@echo off
rem =======================================================================
rem Desktop Pet - Kirim Event via IPC Named Pipe
rem Penggunaan:
rem   send-event.bat <event> [title] [message] [actionLabel] [timeoutSeconds]
rem
rem Contoh:
rem   send-event.bat success "Build Selesai" "Kompilasi sukses tanpa warning" "Lihat" 5
rem   send-event.bat error "Test Gagal" "2 dari 10 unit test gagal"
rem   send-event.bat needs_action "Persetujuan Git" "Push branch main butuh konfirmasi"
rem   send-event.bat notify "Pesan Masuk" "Ada pesan baru dari tim"
rem   send-event.bat start "Build Dimulai" "Membangun proyek..."
rem   send-event.bat clear
rem =======================================================================

where pwsh >nul 2>nul
if %ERRORLEVEL% equ 0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\send-event.ps1" %*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\send-event.ps1" %*
)
