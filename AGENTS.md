# Windows Desktop Pet — Panduan AI Agent

## Tujuan project

Membangun aplikasi desktop Windows mandiri yang menampilkan pet animasi di atas aplikasi lain. Pet menggambarkan aktivitas user, pekerjaan komputer, beban resource, error, dan notifikasi yang membutuhkan respons. User dapat memindahkan dan berinteraksi dengan pet.

Aplikasi berjalan tanpa ketergantungan pada Codex. Format sprite diadaptasi dari pet Codex yang telah disalin ke project.

## Status dan batas pekerjaan

- **Tahap 1 (Pet dasar) selesai dan terverifikasi:** Proyek C# WPF (.NET 8 LTS) telah di-scaffold di `src/DesktopPet`, aset `spritesheet.png` (RGBA 32-bit lossless) berhasil dibuat dan dipotong ke cache memori, sprite player dengan adaptif timer aktif, overlay transparan borderless dengan drag & drop dan respon jumping selesai dibuat, menu kontrol interaktif serta context menu 9 animasi & 16 arah pandang selesai, tray icon aktif, serta persistensi posisi multi-monitor (`settings.json`) berfungsi. 14 unit tests di `tests/DesktopPet.Tests` lulus 100%.
- Aset berasal dari pet terpilih `Kawahime`.
- Tahap berikutnya yang menunggu eksekusi: Tahap 2 (Perilaku dan aktivitas user / State machine).
- Belum ada benchmark RAM/CPU resmi atau daftar aplikasi pihak ketiga yang diintegrasikan via named pipes.

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
| Pet sedang diseret | Tahan satu pose selama drag; respons jumping setelah dilepas |
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

### 4. Integrasi aplikasi

- Tentukan aplikasi/proses pertama yang perlu dipantau bersama user.
- Tambahkan event dimulai, selesai, error, dan membutuhkan respons.
- Tambahkan balon informasi serta tindakan yang relevan.

### 5. Distribusi dan penyempurnaan

- Uji DPI, multi-monitor, sleep/resume, dan pergantian monitor.
- Tentukan installer/paket distribusi dan opsi startup bersama Windows.
- Tambahkan GPU atau integrasi lain hanya bila diperlukan.

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
