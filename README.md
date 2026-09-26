# Desktop Pet

> [!NOTE]
> **Vibecoding Notice:**
> Project ini **sepenuhnya ditulis menggunakan metode *vibecoding*** (dibangun melalui percakapan dan instruksi AI agent). Sebagai author, *I didn't have any idea about the code* secara teknis mendalam. Oleh karena itu, saya menyertakan file panduan konteks [**`AGENTS.md`**](AGENTS.md) agar siapa pun yang ingin berkontribusi, memodifikasi, atau melanjutkan pengembangan project ini dapat memiliki basis dan pemahaman *vibecoding* yang sama persis dengan AI assistant pilihan Anda.

---

**Desktop Pet** adalah aplikasi desktop Windows mandiri berbasis **C# WPF (.NET 8 LTS)** yang menampilkan karakter pet animasi interaktif di atas aplikasi lain (*always-on-top borderless overlay*). Karakter pet (menggunakan sprite *Kawahime*) merefleksikan aktivitas pengguna secara *real-time*: saat Anda mengetik, menggerakkan kursor mouse, saat komputer sedang bekerja keras (CPU/RAM tinggi), hingga menampilkan notifikasi proses melalui balon dialog komik interaktif.

---

## Fitur Utama

### 1. Animasi Sprite Halus & Hemat Daya
* **Atlas Sprite 8×11:** Memuat 9 animasi utama (Idle, Running-Right, Running-Left, Waving, Jumping, Failed, Waiting, Running/Work, Review/Typing) dan 16 pose arah pandang (*gaze tracking*).
* **Render Efisien:** Menggunakan timer per-frame adaptif dinamis; rendering hanya terjadi saat pergantian frame, bukan loop 60/144 Hz terus-menerus.
* **Transparansi Sempurna:** Overlay borderless transparan 32-bit lossless tanpa glitch alpha edge.

### 2. Deteksi Perilaku & Aktivitas Pengguna (Ramah Antivirus)
* **Deteksi Mengetik Non-Invasif:** Membandingkan timestamp input Windows (`GetLastInputInfo`) dengan perpindahan mouse tanpa hook keyboard global invasif (`WH_KEYBOARD_LL`), sehingga 100% aman dan tidak memicu deteksi false-positive antivirus. Mengetik akan memicu pose *Review*.
* **Penjejakan Arah Pandang (16 Arah):** Mata pet secara alami mengikuti posisi kursor mouse saat idle dengan *deadzone* netral di tengah agar tidak bergetar.
* **Auto Wander & Reduced Motion:** Pet sesekali akan berjalan-jalan santai di sepanjang layar. Tersedia opsi *Reduced Motion* bagi pengguna yang menyukai tampilan diam/statis.

### 3. Pemantauan Resource Komputer (CPU & RAM)
* **Sampling Terpisah:** Pemantauan CPU (`GetSystemTimes`) dan RAM (`GlobalMemoryStatusEx`) berjalan di thread terpisah tanpa membebani UI loop.
* **Efek Keringat & Badge Beban:** Muncul tetesan keringat visual dan badge (`🔥 CPU`, `⚡ RAM`) saat beban sistem melebihi ambang batas.
* **Hysteresis Anti-Osilasi:** *Sustain counter* memastikan transisi state visual tidak berkedip saat beban naik-turun sesaat.
* **Footprint Super Ringan:** Working set RAM hanya ~88–90 MB dengan konsumsi CPU rata-rata < 0.4%.

### 4. Integrasi Aplikasi & IPC Named Pipe
* **Named Pipe Duplex (`\\.\pipe\DesktopPetIpc`):** Menerima pesan terstruktur JSON dari skrip eksternal, terminal, atau CI/CD tanpa izin Windows Firewall.
* **Balon Notifikasi Komik (*Speech Bubble*):** Mengambang di atas kepala pet dengan warna tematik (Sukses, Error, Butuh Respon, Info), tombol tindakan eksekusi shell, dan tombol tutup `✕`.
* **Watcher Proses Otomatis:** Memantau siklus hidup aplikasi target pengembang (`dotnet`, `node`, `pwsh`, `cargo`, `ffmpeg`, `code`, dll.). Memberikan animasi perayaan (*Jumping*) jika proses selesai dengan exit code 0, atau animasi cemberut (*Failed*) jika exit code selain 0.

### 5. Ketahanan Sistem Windows
* **Per-Monitor V2 High-DPI:** Tampilan sprite tetap tajam dan proporsional pada semua skala layar (100%, 125%, 150%, 200%).
* **Multi-Monitor Resilience:** Jika monitor sekunder dicabut atau dimatikan, pet otomatis dipindahkan ke area kerja monitor utama.
* **Smart Sleep / Resume:** Menghentikan loop animasi dan sampling saat laptop ditutup atau Windows tidur guna menghemat daya baterai.
* **Auto-Start Windows:** Opsi startup otomatis saat login Windows via Registry Run (`HKCU`).

---

## Cara Menjalankan

### Opsi A: Menggunakan Executable Mandiri (Instan)
Cukup jalankan file executable di root project:
```cmd
.\DesktopPet.exe
```

### Opsi B: Pemasangan Otomatis ke Sistem
Jalankan skrip instalasi PowerShell untuk memasang ke `%LocalAppData%\Programs\DesktopPet` dan membuat shortcut di Desktop serta Start Menu:
```powershell
.\install.ps1
```
> *Catatan: Untuk mengaktifkan auto-start saat instalasi, gunakan:* `.\install.ps1 -AutoStart`

Untuk mencopot aplikasi di masa mendatang:
```powershell
.\uninstall.ps1
```

### Opsi C: Paket Arsip Portabel (ZIP)
File bundel ZIP portabel siap pakai tersedia di:
```
dist/DesktopPet-v1.0.0-win-x64.zip
```

---

## Interaksi & Kontrol

| Aksi | Cara Melakukan |
| :--- | :--- |
| **Pindahkan Pet** | Klik kiri dan tahan (*drag*) sprite pet ke posisi mana pun di layar. Setelah dilepas, pet akan melompat gembira (*jumping*). |
| **Menu Konteks Cepat** | Klik kanan pada pet untuk membuka menu pilihan 9 animasi manual, 16 arah pandang, ukuran skala (0.75x–2.0x), opsi perilaku, dan reset posisi. |
| **Panel Kontrol & Tester** | Klik kanan pet &rarr; pilih **"Panel Kontrol & Tester..."** (atau klik ganda ikon tray di pojok kanan bawah taskbar). |
| **Tutup Notifikasi** | Klik tanda `✕` pada balon komik di atas pet. |
| **Keluar dari Aplikasi** | Klik kanan pet &rarr; pilih **"Keluar"**, atau melalui tombol di Panel Kontrol. |

---

## Mengirim Event via IPC (Scripting / CLI)

Anda dapat mengirim event langsung dari terminal, skrip build, atau automasi lokal menggunakan [`send-event.bat`](send-event.bat) atau PowerShell [`scripts/send-event.ps1`](scripts/send-event.ps1).

### Sintaks
```cmd
send-event.bat <event> [title] [message] [actionLabel] [timeoutSeconds]
```

### Contoh Perintah:

1. **Pekerjaan / Build Dimulai:**
   ```cmd
   send-event.bat start "Build Dimulai" "Sedang mengompilasi project..."
   ```

2. **Pekerjaan Selesai dengan Sukses:**
   ```cmd
   send-event.bat success "Build Sukses!" "Semua file berhasil dikompilasi" "Buka Folder" 6
   ```

3. **Terjadi Error / Kegagalan:**
   ```cmd
   send-event.bat error "Kompilasi Gagal" "2 file mengalami syntax error" "Periksa Log"
   ```

4. **Menunggu Konfirmasi / Aksi Pengguna:**
   ```cmd
   send-event.bat needs_action "Konfirmasi Git" "Apakah ingin push branch ke main?" "Push Sekarang"
   ```

5. **Notifikasi Biasa:**
   ```cmd
   send-event.bat notify "Pesan Masuk" "Rekan tim menandai Anda di PR #12"
   ```

6. **Bersihkan Notifikasi & Reset Animasi:**
   ```cmd
   send-event.bat clear
   ```

---

## Kompilasi & Pengujian

### Prasyarat:
* Windows 10 atau Windows 11 (64-bit)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Menjalankan Unit Tests:
Project dilengkapi dengan 44 unit tests otomatis yang mencakup SpriteSheet, StateMachine, GazeTracker, ResourceMonitor, NamedPipe IPC, ProcessWatcher, AutoStart, dan Multi-Monitor positioning:
```cmd
dotnet test
```

### Membangun Ulang Paket Rilis:
Jalankan skrip pemaketan rilis:
```powershell
pwsh -File .\scripts\package-release.ps1
```

---

## Struktur Direktori

```
pet-ag/
├── src/
│   └── DesktopPet/               # Kode sumber aplikasi utama C# WPF
│       ├── Assets/               # Spritesheet PNG (RGBA 32-bit) dan ikon .ico
│       ├── Models/               # Model data, event IPC, dan definisi animasi
│       ├── Services/             # Layanan sistem (IPC, Monitor, Watcher, AutoStart, dll.)
│       ├── Views/                # XAML & Code-behind Panel Kontrol
│       ├── MainWindow.xaml       # Overlay visual pet transparan & Balon Komik
│       └── app.manifest          # Konfigurasi Per-Monitor V2 DPI & OS Compatibility
├── tests/
│   └── DesktopPet.Tests/         # 44 Unit tests (xUnit)
├── scripts/
│   ├── package-release.ps1       # Skrip otomatisasi build & zip rilis
│   └── send-event.ps1            # Skrip PowerShell client IPC Named Pipe
├── dist/                         # Output paket rilis ZIP dan folder portabel
├── AGENTS.md                     # Panduan arsitektur & aturan teknis bagi AI Agent
├── DesktopPet.exe                # Single-file executable siap pakai
├── install.ps1                   # Skrip installer user
├── uninstall.ps1                 # Skrip uninstaller
├── installer.iss                 # Konfigurasi Inno Setup Compiler
├── send-event.bat                # Shortcut CLI pengiriman event
└── spritesheet.webp              # Aset atlas sprite sumber
```

---

## Catatan Tambahan bagi Kontributor / AI Agent

Jika Anda ingin memodifikasi atau memperluas fitur Desktop Pet, pastikan membaca panduan lengkap di [**`AGENTS.md`**](AGENTS.md). File tersebut mencatat kontrak sprite atlas 8×11, aturan prioritas hierarki state visual, keputusan teknis anti-antivirus, dan tolok ukur benchmark yang harus dipatuhi.
