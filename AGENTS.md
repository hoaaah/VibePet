# Windows Desktop Pet — Panduan AI Agent

## Tujuan project

Membangun aplikasi desktop Windows mandiri yang menampilkan pet animasi di atas aplikasi lain. Pet menggambarkan aktivitas user, pekerjaan komputer, beban resource, error, dan notifikasi yang membutuhkan respons. User dapat memindahkan dan berinteraksi dengan pet.

Aplikasi berjalan tanpa ketergantungan pada Codex. Format sprite diadaptasi dari pet Codex yang telah disalin ke project.

## Status dan batas pekerjaan

- **Tahap 1 (Pet dasar) selesai dan terverifikasi:** Proyek C# WPF (.NET 8 LTS) telah di-scaffold di `src/DesktopPet`, aset `spritesheet.png` (RGBA 32-bit lossless) berhasil dibuat dan dipotong ke cache memori, sprite player dengan adaptif timer aktif, overlay transparan borderless dengan drag & drop dan respon jumping selesai dibuat, menu kontrol interaktif serta context menu 9 animasi & 16 arah pandang selesai, tray icon aktif, serta persistensi posisi multi-monitor (`settings.json`) berfungsi. 14 unit tests di `tests/DesktopPet.Tests` lulus 100%.
- **Tahap 2 (Perilaku dan aktivitas user) selesai dan terverifikasi:** Hierarki prioritas `PetStateMachine` aktif dengan pemulihan otomatis dari animasi one-shot (`Waving`, `Jumping`), deteksi mengetik ramah antivirus non-invasif (`GetLastInputInfo` + delta kursor) memicu animasi `Review`, penjejakan 16 arah pandang (`GazeTracker`) dengan deadzone netral mengikuti kursor mouse saat idle, sistem perpindahan pet mandiri (`PetMovementManager` - Auto Wander dengan animasi `RunningRight`/`RunningLeft`), opsi aksesibilitas `ReducedMotion` serta toggle preferensi tersimpan di `settings.json`, kontrol simulasi event di Panel Kontrol selesai diuji, dan 28 unit tests lulus 100%.
- **Tahap 3 (Resource komputer & Benchmark) selesai dan terverifikasi:** Monitoring CPU (`GetSystemTimes`) dan RAM (`GlobalMemoryStatusEx`) berjalan di thread terpisah tanpa membebani UI loop. Sistem hysteresis dengan *sustain counter* mencegah osilasi pergantian state. Efek visual keringat di kepala pet dan badge beban sistem (`🔥 CPU`, `⚡ RAM`) aktif saat beban tinggi dan otomatis hilang saat beban kembali normal. 32 unit tests lulus 100%.
- **Hasil Benchmark Overhead Resmi (.NET 8 Windows x64):**
  - *Skenario 1 (Idle + Monitoring 1s):* Working Set RAM 88.7 MB (Puncak 90.0 MB), CPU 0.69%, GC 0.
  - *Skenario 2 (Animasi Aktif Loop):* Working Set RAM 90.5 MB (Puncak 90.6 MB), CPU 0.32%, GC 0.
  - *Skenario 3 (Monitoring Agresif 250ms):* Working Set RAM 90.7 MB (Puncak 90.7 MB), CPU 0.37%, GC 0.
- **Tahap 4 (Integrasi aplikasi & Balon Notifikasi) selesai dan terverifikasi:**
  - *IPC Server Named Pipe (`\\.\pipe\DesktopPetIpc`):* Komunikasi duplex full-speed tanpa izin firewall untuk menerima payload terstruktur JSON (`event`, `title`, `message`, `actionLabel`, `actionCommand`, `timeoutSeconds`).
  - *Event yang didukung:* `start` / `work_started`, `success` / `work_completed`, `error` / `work_failed`, `needs_action` / `waiting`, `notify` / `waving`, dan `clear`.
  - *Balon Notifikasi Komik Interaktif (Speech Bubble):* Terletak mengambang presisi di atas pet dengan ekor penunjuk, skema warna dinamis sesuai jenis pesan (hijau sukses, merah error, persik aksi, biru info), tombol tindakan (menjalankan URL/perintah shell), dan tombol dismiss `✕`.
  - *Watcher Proses Otomatis (`ProcessWatcherService`):* Memantau lifecycle proses target pengembang (`dotnet`, `node`, `pwsh`, `cargo`, `ffmpeg`, `code`), memicu animasi komputer bekerja saat proses mulai dan animasi sukses/error berdasarkan exit code (0 = success, != 0 = error).
  - *Integrasi UI & Scripting CLI:* Panel Kontrol menyertakan kontrol IPC & penambahan/penghapusan proses watcher secara visual. Disediakan juga `send-event.bat` dan `scripts/send-event.ps1` untuk pemanggilan instan dari build scripts, CI/CD lokal, atau terminal.
  - *37 Unit Tests* di `tests/DesktopPet.Tests` lulus 100%.
- **Tahap 5 (Distribusi & Penyempurnaan Sistem) selesai dan terverifikasi:**
  - *Per-Monitor V2 DPI Awareness:* Dikonfigurasi melalui `app.manifest` dan `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` pada .NET 8. Penanganan event `DpiChanged` memastikan sprite tetap tajam pada semua skala layar (100%, 125%, 150%, 200%).
  - *Multi-Monitor Resilience & Pemulihan Posisi:* Menggunakan `Screen.AllScreens` untuk validasi area kerja. Jika monitor sekunder dicabut/mati, pet otomatis dipulihkan ke monitor utama tanpa terlempar keluar layar. Responsif terhadap `SystemEvents.DisplaySettingsChanged`.
  - *Manajemen Daya & Sleep/Resume:* Menangani `SystemEvents.PowerModeChanged` (`Suspend` / `Resume`) untuk menghentikan timer animasi dan monitoring saat komputer tidur guna menghemat daya baterai.
  - *Auto-Start Windows:* Diimplementasikan via `AutoStartService` pada registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Tersedia toggle di Panel Kontrol dan Context Menu pet.
  - *Distribusi Lengkap:* Skrip penginstal PowerShell (`install.ps1`) dan uninstaller (`uninstall.ps1`), skrip installer Inno Setup (`installer.iss`), serta pemaket rilis otomatis (`scripts/package-release.ps1`) yang menghasilkan paket portable `dist/DesktopPet-v1.0.0-win-x64.zip` dan binary root `DesktopPet.exe` (2.66 MB).
  - *44 Unit Tests* di `tests/DesktopPet.Tests` lulus 100%.
- Aset berasal dari pet terpilih `Kawahime`.
- Seluruh 5 tahapan pengembangan awal telah rampung 100%.
- **Tahap 6 (Pengembangan Lanjutan):**
  1. *Nama Proses & Dokumen Aktif (Selesai, unit test + uji proses nyata):* PID diganti identitas `ProcessIdentity` = nama aplikasi + project/dokumen yang dibuka (misal `Visual Studio Code — pet-ag (AGENTS.md)`, `Microsoft Word — Laporan.docx`, `Node.js — server.js`, `.NET CLI — build`) pada balon notifikasi dan tooltip tray. Proses pembantu (anak dari proses bernama sama atau ber-argumen `--type=`) tidak memicu balon maupun animasi gagal; proses yang terdeteksi dalam satu scan (termasuk saat pet dibuka) digabung menjadi satu balon ringkasan; tooltip tray menampilkan satu aplikasi per baris dalam batas 63 karakter. Daftar default watcher diperluas dengan `gitkraken`, `claude`, `codex`, `agy`, `winword`, `msedge`. 93 unit tests lulus 100%. Verifikasi visual balon dan tooltip di aplikasi berjalan belum dilakukan.
  2. *Interactive Drag Movement (Diimplementasikan, unit test lulus; belum diverifikasi visual):* Animasi berlari ke kiri (`RunningLeft`) atau kanan (`RunningRight`) mengikuti arah seretan, pose ditahan saat kursor diam 180 ms, menghormati `ReducedMotion`, disusul `Jumping` setelah dilepas. 104 unit tests lulus 100%.
  3. *Animasi Kerja Dinamis (Diimplementasikan, unit test lulus; belum diverifikasi visual):* Saat ComputerWork, pet berlari bolak-balik ±70 DIP di sekitar posisinya (toggle *Statis* / *Bolak-balik* di Panel Kontrol & context menu, default bolak-balik). Status kerja kini gabungan sumber terpisah (CPU, IPC, proses, simulasi); proses hanya dihitung bekerja bila tool CLI/build/agent yang dipantau sedang memakai CPU. 125 unit tests lulus 100%.
  4. *Multi-Skin / Sprite Packs System:* Penyimpanan dan pemilihan paket sprite kustom dari `%AppData%\DesktopPet\Skins\` dengan *hot-swap* langsung dari Panel Kontrol & Context Menu tanpa restart.

## Stack yang direncanakan

| Komponen | Pilihan | Tujuan |
| --- | --- | --- |
| Bahasa dan runtime | C# + .NET LTS yang masih didukung saat implementasi | Logika aplikasi dan integrasi Windows |
| UI desktop | WPF + XAML | Overlay transparan, tanpa bingkai, selalu di atas, drag, dan pengaturan |
| Sprite player | WPF Image dengan frame yang di-cache | Memutar animasi dengan durasi per frame |
| Pengatur perilaku | State machine sederhana di C# | Prioritas event, transisi, dan kembali ke aktivitas yang masih berjalan |
| Aktivitas user | Win32 API (GetLastInputInfo + GetCursorPos / Raw Input) | Aktivitas input, posisi kursor, dan aplikasi aktif |
| Resource komputer | Windows Performance Counters / PDH dan API Windows yang sesuai | CPU, RAM, serta counter tambahan yang tersedia |
| Integrasi aplikasi | Named pipes (prioritas utama) atau HTTP loopback | Event pekerjaan dimulai, selesai, gagal, dan membutuhkan respons |
| System tray | NotifyIcon | Menu pengaturan, tampil/sembunyikan, dan keluar |
| Preferensi | JSON dalam direktori data user | Posisi, ukuran pet, dan pengaturan |

WPF dipilih karena target khusus Windows dan kebutuhan integrasi OS. Tauri + TypeScript + Rust adalah alternatif bila kebutuhan UI web menjadi dominan; Electron bukan pilihan dasar rencana ini. Jangan mengganti stack tanpa alasan konkret yang dibahas dengan user. Jangan mengklaim angka konsumsi resource tanpa pengukuran.

## Aset dan kontrak sprite

- Sumber: `spritesheet.webp` di root project. Pertahankan file sumber.
- Format v2: atlas 8 kolom × 11 baris, ukuran total 1536 × 2288 piksel.
- Ukuran setiap sel: 192 × 208 piksel.
- Baris 0–8 berisi 9 animasi utama; baris 9–10 berisi 16 pose arah pandang, bukan dua state animasi tambahan.
- Siapkan PNG transparan turunan untuk runtime WPF agar tidak bergantung pada decoder WebP tambahan. Konversi harus mempertahankan alpha dan ukuran atlas.
- Decode dan cache frame sekali saat pemuatan. Jangan membaca file atau memotong ulang gambar pada setiap tick.
- Validasi dimensi dan alpha aset pada awal implementasi; jangan mengubah atlas agar cocok dengan asumsi renderer yang keliru.

### Pemetaan animasi ke perilaku aplikasi

Nomor baris dan kolom dihitung mulai dari nol. Pemetaan aktivitas berikut adalah rancangan aplikasi ini, bukan klaim tentang perilaku internal Codex.

| Baris | State | Frame terpakai | Durasi frame | Aktivitas dalam aplikasi |
| --- | --- | --- | --- | --- |
| 0 | idle | 0–5 | 280, 110, 110, 140, 140, 320 ms | User tidak aktif dan tidak ada pekerjaan yang dipantau |
| 1 | running-right | 0–7 | 120 ms; frame terakhir 220 ms | Pet bergerak menuju posisi di kanan |
| 2 | running-left | 0–7 | 120 ms; frame terakhir 220 ms | Pet bergerak menuju posisi di kiri |
| 3 | waving | 0–3 | 140 ms; frame terakhir 280 ms | Notifikasi baru menarik perhatian |
| 4 | jumping | 0–4 | 140 ms; frame terakhir 280 ms | Pekerjaan berhasil selesai, klik pet, atau respons setelah drag |
| 5 | failed | 0–7 | 140 ms; frame terakhir 240 ms | Error dari proses atau aplikasi yang dipantau |
| 6 | waiting | 0–5 | 150 ms; frame terakhir 260 ms | Menunggu respons, persetujuan, atau tindakan user |
| 7 | running | 0–5 | 120 ms; frame terakhir 220 ms | Pekerjaan komputer sedang berjalan; bukan perpindahan pet |
| 8 | review | 0–5 | 150 ms; frame terakhir 280 ms | User sedang mengetik atau bekerja aktif |

Sel yang tidak digunakan pada baris animasi harus diabaikan renderer.

### Arah pandang

- Baris 9: 0°, 22,5°, 45°, 67,5°, 90°, 112,5°, 135°, 157,5°.
- Baris 10: 180°, 202,5°, 225°, 247,5°, 270°, 292,5°, 315°, 337,5°.
- 0° menunjuk ke atas; urutan searah jarum jam.
- Pilih pose berdasarkan posisi kursor relatif terhadap pet. Gunakan deadzone dekat pusat agar arah tidak bergetar; kembali ke idle saat netral.
- Pose arah pandang tidak boleh terus-menerus menimpa animasi error atau kebutuhan respons.

### Variasi yang membutuhkan elemen tambahan

| Kondisi | Rancangan |
| --- | --- |
| CPU/GPU tinggi secara berkelanjutan | running dengan indikator kerja berat; efek keringat opsional |
| Tekanan RAM tinggi | Indikator RAM; tidak otomatis dianggap error |
| Beban kembali normal | Hilangkan indikator; pertahankan running bila pekerjaan belum selesai |
| Pet sedang diseret | Berlari ke arah seretan; tahan pose saat kursor diam atau bila reduced motion aktif; respons jumping setelah dilepas |
| Detail error/notifikasi | Balon teks atau panel ringkas yang dapat ditindaklanjuti |

Efek keringat, indikator resource, dan balon teks belum tersedia di sprite sumber dan harus dibuat terpisah bila diimplementasikan.

## Aturan perilaku

1. Prioritas awal: interaksi langsung dengan pet → error → membutuhkan respons → notifikasi baru → pekerjaan berjalan → user mengetik → idle.
2. Animasi singkat seperti waving dan jumping berakhir setelah satu siklus, lalu kembali ke kondisi aktif dengan prioritas tertinggi.
3. Event penyelesaian pekerjaan dapat memicu jumping; error dan kebutuhan respons tetap lebih tinggi prioritasnya.
4. Gunakan debounce, durasi minimum, dan hysteresis untuk mencegah perubahan state berulang saat input atau resource naik-turun. Nilai ambang ditentukan dan diuji saat implementasi.
5. Pisahkan status pekerjaan, tingkat beban resource, dan state visual. CPU rendah tidak membuktikan pekerjaan selesai; CPU tinggi tidak membuktikan terjadi error.
6. Sampling resource harus berjalan terpisah dari loop animasi dan tidak memblokir UI.
7. Jangan merebut fokus keyboard ketika pet memperlihatkan animasi atau notifikasi.
8. Pertimbangkan multi-monitor, skala DPI, batas area kerja, dan pemulihan posisi ketika monitor terlepas.
9. Sediakan reduced motion atau opsi animasi diam.

## Deteksi aktivitas dan integrasi

- Deteksi mengetik hanya membutuhkan informasi adanya aktivitas keyboard, bukan isi ketikan. Jangan menyimpan karakter yang diketik.
- GetLastInputInfo berguna untuk waktu idle sesi, tetapi tidak membedakan keyboard dan mouse. Gunakan mekanisme input Windows yang sesuai bila deteksi mengetik harus spesifik.
- Jangan mengasumsikan semua proses Windows yang hidup sedang melakukan pekerjaan bermakna. Tentukan proses yang dipantau atau terima event eksplisit.
- Error, progres, dan permintaan respons paling akurat berasal dari integrasi aplikasi, kode keluar proses yang dipantau, atau event eksplisit.
- Deteksi universal semua dialog error dan semua notifikasi Windows bukan cakupan MVP.
- Mulai dengan CPU/RAM. GPU merupakan perluasan yang bergantung pada counter dan perangkat yang tersedia.
- Jika memakai HTTP localhost, batasi ke loopback dan validasi pengirim/payload. Jika memakai named pipes, batasi akses sesuai user aplikasi.

## Rekomendasi dan keputusan teknis penyempurnaan

1. **Aset (WebP ke PNG Lossless Sekali Jalan):**
   - Lakukan konversi satu kali dari `spritesheet.webp` ke `spritesheet.png` (32-bit RGBA lossless) sebelum/saat build sebagai embedded resource.
   - WPF me-load PNG sekali saat startup dan memotong frame ke dalam memori menggunakan `CroppedBitmap` + `Int32Rect`, menghindari dependensi decoder WebP eksternal pada runtime.

2. **Deteksi Mengetik Ramah Antivirus (Anti False-Positive):**
   - Hindari hook global invasif (`SetWindowsHookEx` dengan `WH_KEYBOARD_LL`) karena sering terdeteksi sebagai pola keylogger oleh Windows Defender / antivirus.
   - Prioritaskan pendekatan non-invasif: bandingkan selisih timestamp `GetLastInputInfo()` dengan perpindahan koordinat `GetCursorPos()`. Jika ada input baru namun posisi kursor diam, simpulkan sebagai aktivitas mengetik. Alternatif berikutnya jika membutuhkan presisi lebih tinggi adalah *Raw Input API* (`RegisterRawInputDevices`).

3. **Komunikasi Antar-Proses (Named Pipes > HTTP Localhost):**
   - Prioritaskan *Named Pipes* (`NamedPipeServerStream`) untuk integrasi event aplikasi.
   - Keuntungan: tidak memicu dialog izin Windows Firewall (berbeda dengan pembukaan port HTTP lokal), latensi/footprint memori lebih rendah, dan akses dapat dibatasi secara ketat ke sesi user yang sama via `PipeSecurity`.

4. **Loop Animasi Hemat Daya:**
   - Hindari continuous rendering loop (`CompositionTarget.Rendering` pada 60/144 Hz) karena membuang siklus CPU/GPU untuk animasi sprite ber-fps rendah.
   - Gunakan timer berbasis durasi frame adaptif (misalnya `DispatcherTimer` dengan interval per-frame dinamis sesuai tabel animasi). UI hanya di-update/re-render ketika frame benar-benar berganti.

## Tahapan implementasi

### 1. Pet dasar

- Scaffold C# + WPF dengan SDK LTS yang didukung.
- Siapkan aset PNG turunan dan definisi metadata animasi.
- Implementasikan sprite player serta kontrol manual untuk mencoba seluruh 9 animasi dan 16 arah pandang.
- Buat overlay transparan, drag, tray, keluar aplikasi, dan penyimpanan posisi.

### 2. Perilaku dan aktivitas user

- Implementasikan state machine, prioritas, dan event simulasi.
- Tambahkan idle, aktivitas mengetik, arah pandang, serta perpindahan pet.
- Tambahkan opsi reduced motion dan pengaturan dasar.

### 3. Resource komputer

- Tambahkan sampling CPU/RAM berkala.
- Tambahkan indikator beban tinggi dengan ambang dan hysteresis yang dapat dikonfigurasi.
- Ukur overhead aplikasi saat idle, animasi aktif, dan monitoring aktif.

### 4. Integrasi aplikasi (Selesai)

- Implementasi server duplex Named Pipe (`\\.\pipe\DesktopPetIpc`) berbasis JSON tanpa firewall prompt.
- Tambahkan watcher proses otomatis (`ProcessWatcherService`) untuk mendeteksi event proses mulai/selesai/gagal berdasarkan exit code.
- Tambahkan balon dialog komik (Speech Bubble) di atas pet dengan tema warna dinamis, tombol tindakan shell, dan tombol close.
- Integrasi ke Panel Kontrol (tab/grupbox IPC dan Process Watcher) serta penyediaan skrip CLI `send-event.bat` dan `scripts/send-event.ps1`.
- Seluruh 37 unit tests lulus 100%.

### 5. Distribusi dan penyempurnaan (Selesai)

- Implementasi Per-Monitor V2 High-DPI awareness di `app.manifest` dan `.csproj`, serta event `DpiChanged`.
- Penanganan multi-monitor, layout monitor berubah (`DisplaySettingsChanged`), dan pemulihan posisi otomatis jika monitor sekunder dicabut (`GetValidatedPosition`).
- Manajemen daya cerdas (`PowerModeChanged`) untuk pause/resume animasi dan monitoring saat sleep/wake.
- Layanan Auto-Start Windows (`AutoStartService`) via Registry Run `HKCU` dengan kontrol di UI.
- Skrip penginstal PowerShell (`install.ps1`) dan pembersih (`uninstall.ps1`), konfigurasi Inno Setup (`installer.iss`), serta pemaket rilis otomatis (`scripts/package-release.ps1`).
- Seluruh 44 unit tests lulus 100%.

### 6. Interaktivitas lanjutan & paket sprite kustom (Roadmap Berikutnya)

1. **Nama Proses & Dokumen Aktif (*Friendly Process Identity* — Selesai):**
   - `ProcessNameFormatter` (logika murni, teruji): kamus nama aplikasi (`dotnet` &rarr; `.NET CLI`, `node` &rarr; `Node.js`, `code` &rarr; `Visual Studio Code`, `winword` &rarr; `Microsoft Word`, `excel`, `powerpnt`, `devenv`, `notepad`, dsb.) dan fallback `my_tool-name.exe` &rarr; `My Tool Name`.
   - Sumber konteks dengan prioritas: (1) judul jendela proses itu sendiri, (2) argumen command line (nama file/project/skrip atau subcommand CLI), (3) judul jendela proses bernama sama (untuk helper Electron VS Code yang tidak punya jendela).
   - Parser judul membuang akhiran nama aplikasi, penanda belum disimpan (`●`, `*`), dan status seperti `Compatibility Mode`; pola VS Code `<file> - <folder> - Visual Studio Code` dipecah menjadi project + file aktif.
   - `ProcessIdentityResolver` (Win32): satu kali `EnumWindows` per tick watcher untuk peta PID &rarr; judul, dan command line via `NtQueryInformationProcess` dengan hak `PROCESS_QUERY_LIMITED_INFORMATION`. Command line utuh tidak disimpan; hanya nama file/subcommand yang diambil.
   - Identitas diperbarui tiap tick (1,5 s) sehingga notifikasi selesai/gagal memakai project/dokumen terakhir yang diketahui. Tooltip tray dikelompokkan per identitas dan dipotong ke 63 karakter.
   - Proses pembantu ditandai saat ditemukan: parent PID (via `NtQueryInformationProcess` `ProcessBasicInformation`) adalah proses ter-pantau dengan nama sama, atau command line memuat `--type=` (Chromium/Electron). Start/exit proses pembantu diabaikan, sehingga renderer VS Code/Edge yang keluar dengan kode non-nol tidak memicu animasi gagal.
   - Event `ProcessesStarted` dikirim per scan dengan identitas unik; scan pertama setelah `Start()` ditandai `isInitialScan` dan ditampilkan sebagai satu balon "Proses Terdeteksi".
   - Daftar default watcher: `dotnet`, `node`, `pwsh`, `cargo`, `ffmpeg`, `code`, `gitkraken`, `claude`, `codex`, `agy` (CLI Antigravity), `winword`, `msedge`. `git` sengaja tidak dimasukkan karena VS Code/GitKraken menjalankannya terus-menerus. `settings.json` yang sudah ada tidak dimigrasi otomatis.
   - Catatan: `excel`/`powerpnt` perlu ditambahkan manual bila dibutuhkan.

2. **Animasi Berlari Interaktif Saat Drag (*Interactive Drag Running*):**
   - Mengganti penahanan satu frame statis saat drag menjadi animasi dinamis mengikuti arah seretan kursor pengguna.
   - Membandingkan pergerakan koordinat horizontal kursor mouse (`deltaX`):
     - `deltaX > 0` &rarr; Animasi `RunningRight` (Baris 1).
     - `deltaX < 0` &rarr; Animasi `RunningLeft` (Baris 2).
     - `deltaX == 0` (kursor berhenti saat ditahan) &rarr; Menahan pose berjalan.
   - Saat mouse dilepas (mouse up), memicu animasi perayaan `Jumping` (Baris 4).
   - Implementasi: drag tetap memakai `DragMove()` (loop pemindahan bawaan Windows, menangani DPI dan multi-monitor). Pergeseran dibaca dari `Window.LocationChanged`; timer 60 ms memeriksa kursor diam.
   - `DragMotionTracker` (logika murni, teruji) memutuskan pose: arah pertama dari `deltaX` (ambang 0,5 DIP), arah baru hanya berlaku setelah pergeseran berlawanan terkumpul ≥ 6 DIP (anti-kedip), pose ditahan setelah 180 ms tanpa gerak, dan gerak vertikal melanjutkan arah terakhir.
   - Selama drag `PetStateMachine` tetap di `DirectInteraction`, jadi gaze, wander, dan event lain tidak menimpa animasi drag.
   - Belum diverifikasi visual: apakah `LocationChanged` dan timer berjalan mulus selama loop `DragMove()` di mesin pengguna.

3. **Animasi Komputer Bekerja Berganti ke Berlari Kiri / Kanan (*Running Process Animation*):**
   - Menghubungkan state `ComputerWork` / `Running` dengan animasi berlari bolak-balik (*pacing / patrolling*) alih-alih berlari statis di tempat.
   - Pet berlari ke kanan selama durasi tertentu, lalu berbalik ke kiri, menciptakan ilusi bekerja mondar-mandir yang lebih hidup.
   - Sediakan toggle mode gaya animasi kerja di Panel Kontrol: *Statis di tempat* vs *Berlari bolak-balik*.
   - Implementasi:
     - `WorkAnimationStyle` (`Static`/`Pacing`) tersimpan di `settings.json` sebagai string; diatur lewat radio button Panel Kontrol dan item context menu "Kerja: Berlari Bolak-balik".
     - `PetMovementManager` memulai/menghentikan pacing lewat `SyncPacing()` (dipanggil tertunda dari `StateChanged`, tidak re-entrant). Pacing hanya berjalan bila prioritas teratas `ComputerWork`, tidak sedang drag/manual test, dan `ReducedMotion` mati. Rentang ±70 DIP dari posisi awal, dibatasi area kerja monitor tempat pet berada (`Screen.FromHandle` + transform DPI); bila ruang < 24 DIP, tetap animasi statis. Kecepatan 2,5 DIP per 30 ms.
     - `PetStateMachine.SetWorkPacingDirection` memilih Row 1/2 untuk state ComputerWork; prioritas lebih tinggi (error, notifikasi, dll.) tetap menang.
   - Perbaikan terkait status kerja (AGENTS.md aturan 5):
     - Sebelumnya monitor CPU memanggil `SetComputerWork(false)` tiap sampel sehingga status kerja dari proses/IPC hilang dalam ±1 detik. Kini `WorkSource` (`CpuLoad`, `Ipc`, `Process`, `Simulation`) disimpan terpisah; ComputerWork aktif bila salah satu aktif. `ClearAllSimulations` tidak menghapus sumber hasil pengukuran (CPU, proses).
     - IPC `start` mengaktifkan sumber `Ipc`; `success`/`error` mematikannya.
     - `ProcessWatcherService.IsWorkActive`: tool CLI/build/agent yang dipantau (termasuk proses pembantunya) dianggap bekerja bila memakai ≥ 5% satu core CPU dalam satu tick, dipertahankan 4 detik setelah sampel sibuk terakhir. Aplikasi GUI (`code`, `winword`, `msedge`, `gitkraken`, dsb.) tidak pernah dihitung; event mulai proses kini hanya memunculkan balon. Diuji pada mesin pengguna: proses `node`/`claude`/`codex`/`agy` yang diam terukur 0% (tidak memicu), `dotnet build` terdeteksi sibuk selama build.

4. **Sistem Paket Sprite Kustom (*Custom Sprite Packs / Skins*):**
   - Struktur direktori skin lokal: `%AppData%\DesktopPet\Skins\<NamaSkin>\` yang memuat `spritesheet.png` dan berkas opsional `skin.json` (metadata author, dimensi sel atlas, durasi frame).
   - Layanan manajer skin (`SkinManagerService`) untuk memindai skin bawaan dan folder skin kustom pengguna.
   - Fitur *hot-swap* instan: pemotongan ulang frame ke memori secara dinamis saat skin dipilih dari dropdown Panel Kontrol atau Context Menu tanpa me-restart aplikasi.
   - Tombol *"Buka Folder Skins"* di Panel Kontrol untuk memudahkan pengguna menambahkan atlas sprite baru.

## Verifikasi dan kriteria keberhasilan

- Semua animasi menggunakan frame, urutan, dan timing yang benar; tidak ada sel kosong yang ikut diputar.
- Latar pet transparan tanpa kotak latar atau tepi alpha rusak.
- Drag, tray, keluar aplikasi, dan pemulihan posisi bekerja.
- Animasi dan notifikasi tidak mencuri fokus dari aplikasi tempat user mengetik.
- Transisi prioritas diuji dengan event tumpang tindih; pet kembali ke aktivitas yang masih berlangsung setelah animasi sementara selesai.
- CPU/RAM yang ditampilkan berasal dari pengukuran nyata; kondisi counter tidak tersedia ditangani tanpa crash.
- Laporkan penggunaan RAM/CPU berdasarkan benchmark beserta skenario dan interval sampling, bukan estimasi atau batas konfigurasi.
- Lakukan inspeksi visual sprite dan overlay; build sukses saja tidak membuktikan hasil visual benar.

## Referensi

- WPF windows: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/
- Windows performance counters: https://learn.microsoft.com/en-us/windows/win32/perfctrs/consuming-counter-data
- GetLastInputInfo: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlastinputinfo

Dokumen ini menjadi titik masuk konteks project bagi AI agent. Perbarui status dan keputusan berdasarkan implementasi yang benar-benar sudah dilakukan; bedakan rencana dari hasil yang telah diverifikasi.
