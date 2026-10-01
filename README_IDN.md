# VibePet

🌐 [English](README.md) | **Bahasa Indonesia**

> [!NOTE]
> **Vibecoding Notice:**
> Project ini **sepenuhnya ditulis menggunakan metode *vibecoding*** (dibangun melalui percakapan dan instruksi AI agent). Sebagai author, *I didn't have any idea about the code* secara teknis mendalam. Oleh karena itu, saya menyertakan file panduan konteks [**`AGENTS.md`**](AGENTS.md) agar siapa pun yang ingin berkontribusi, memodifikasi, atau melanjutkan pengembangan project ini dapat memiliki basis dan pemahaman *vibecoding* yang sama persis dengan AI assistant pilihan Anda.

---

**VibePet** adalah aplikasi desktop Windows mandiri berbasis **C# WPF (.NET 8 LTS)** yang menampilkan karakter pet animasi interaktif di atas aplikasi lain (*always-on-top borderless overlay*). Karakter pet (menggunakan sprite *Kawahime*) merefleksikan aktivitas pengguna secara *real-time*: saat Anda mengetik, menggerakkan kursor mouse, saat komputer sedang bekerja keras (CPU/RAM tinggi), hingga menampilkan notifikasi proses melalui balon dialog komik interaktif.

> [!NOTE]
> VibePet sebelumnya bernama *Desktop Pet*. Nama teknisnya masih `DesktopPet`, yaitu file exe, folder kode, folder data (`%AppData%\DesktopPet`), dan pipe IPC (`\\.\pipe\DesktopPetIpc`). Karena itu, settings, skin, dan skrip yang sudah ada tetap berfungsi.

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

## Membuat Skin / Sprite Sendiri (dengan Bantuan AI)

Anda bisa mengganti karakter pet dengan gambar sendiri. Alurnya: minta AI menggambar frame-frame karakter, rapikan, lalu rakit menjadi atlas dengan [`scripts/build-atlas.ps1`](scripts/build-atlas.ps1) dan pasang sebagai skin.

### 1. Format atlas yang dibaca aplikasi

Satu gambar PNG transparan berisi **8 kolom × 11 baris** sel. Ukuran bawaan sel 192 × 208 px, jadi atlasnya 1536 × 2288 px.

| Baris | Animasi | Frame | Isi |
| :--- | :--- | :--- | :--- |
| 0 | idle | 6 | Berdiri santai, bernapas/berkedip |
| 1 | running-right | 8 | Berlari menghadap kanan |
| 2 | running-left | 8 | Berlari menghadap kiri (cermin baris 1) |
| 3 | waving | 4 | Melambaikan tangan |
| 4 | jumping | 5 | Melompat gembira |
| 5 | failed | 8 | Sedih / gagal |
| 6 | waiting | 6 | Bertanya / menunggu |
| 7 | running | 6 | Sibuk bekerja di tempat |
| 8 | review | 6 | Mengetik / membaca |
| 9–10 | arah pandang | 16 | Kepala menoleh 0°, 22,5°, … 337,5° (0° = atas, searah jarum jam) |

Jumlah frame dan durasi boleh berbeda; atur lewat `skin.json`. Urutan baris tidak bisa diubah.

### 2. Kunci desain karakter (character sheet)

AI cenderung mengubah detail karakter di setiap gambar, jadi kunci desainnya dulu. Upload gambar Anda ke generator yang menerima gambar referensi (ChatGPT, Gemini, Midjourney `--cref`, Leonardo Character Reference, dll.):

> Buat character sheet dari karakter di gambar ini: tampak depan, samping kanan, samping kiri, dan belakang. Gaya chibi/mascot, proporsi kepala besar, garis tegas, warna flat, tanpa bayangan di lantai. Latar polos hijau #00FF00. Semua pose dalam ukuran dan skala yang sama.

Simpan hasil terbaik dan **lampirkan lagi sebagai referensi di setiap prompt berikutnya**.

### 3a. Generate per baris (direkomendasikan)

Satu prompt per animasi, hasilnya *strip* horizontal. Cara ini paling konsisten dan paling hemat kuota.

**Idle (baris 0):**
> Menggunakan karakter dari referensi, buat sprite strip horizontal berisi 6 frame animasi idle: berdiri menghadap depan, bernapas pelan, frame ke-4 berkedip. Setiap frame sama ukurannya, karakter di tengah, kaki berada di garis dasar yang sama di semua frame. Latar polos hijau #00FF00, tanpa bayangan, tanpa teks.

**Running right (baris 1):**
> Karakter yang sama, sprite strip 8 frame siklus lari menghadap kanan (contact, down, pass, up untuk tiap kaki). Ukuran dan posisi kaki konsisten, karakter tidak bergeser maju di dalam frame (berlari di tempat). Latar hijau #00FF00.

**Waving (baris 3):**
> Karakter yang sama, 4 frame: tangan kanan naik, melambai ke kiri, ke kanan, lalu turun. Menghadap depan, tersenyum.

**Arah pandang (baris 9–10):**
> Karakter yang sama, 16 pose kepala/mata menoleh mengikuti arah jarum jam, mulai dari melihat ke atas (0°), atas-kanan (45°), kanan (90°), … sampai atas-kiri (337,5°). Tubuh tetap diam, hanya kepala dan mata yang berubah.

Lanjutkan dengan pola yang sama untuk jumping, failed, waiting, running (sibuk di depan laptop), dan review (mengetik).

Tips:
* Bila generator tidak bisa memberi jumlah frame yang tepat, generate frame satu per satu ("frame ke-3 dari 8 siklus lari, kaki kiri di depan …").
* **Running-left tidak perlu di-generate**; `build-atlas.ps1` mencerminkannya dari running-right.
* Minta "berlari di tempat". Perpindahan posisi pet ditangani aplikasi, bukan oleh gambarnya.

### 3b. Generate sekaligus dalam satu prompt

> [!WARNING]
> Membuat seluruh atlas (75 frame) dalam satu prompt jauh lebih berat daripada per baris. Di layanan berbasis langganan/kuota (ChatGPT, Gemini, Midjourney, Leonardo, dll.) cara ini dapat **menghabiskan limit token, kredit, atau kuota gambar lebih cepat**, terutama karena hasilnya biasanya perlu di-generate ulang beberapa kali sampai grid dan konsistensinya benar. Bila kuota terbatas, gunakan cara per baris.

Lampirkan gambar karakter sebagai referensi:

```
Gunakan karakter pada gambar referensi. Buat SATU sprite sheet animasi untuk desktop pet
dengan aturan berikut:

FORMAT GRID
- Grid tepat 8 kolom x 11 baris, semua sel berukuran sama (rasio sel 192:208, sedikit lebih tinggi
  daripada lebar). Ukuran gambar total 1536 x 2288 px bila memungkinkan.
- Satu pose per sel. Karakter di tengah sel, ukuran karakter sama di semua sel,
  kaki selalu di garis dasar yang sama, sisakan ruang kosong di sekeliling karakter.
- Sel yang tidak dipakai dibiarkan kosong.
- Latar seluruh gambar polos hijau #00FF00 (tanpa gradasi, tanpa bayangan lantai, tanpa garis grid,
  tanpa teks, tanpa nomor).

ISI SETIAP BARIS (kiri ke kanan)
- Baris 1 (6 frame): idle, berdiri menghadap depan, bernapas pelan, frame ke-4 berkedip.
- Baris 2 (8 frame): siklus lari menghadap KANAN, berlari di tempat.
- Baris 3 (8 frame): siklus lari menghadap KIRI, cermin dari baris 2.
- Baris 4 (4 frame): melambaikan tangan kanan sambil tersenyum.
- Baris 5 (5 frame): melompat gembira (jongkok, naik, puncak, turun, mendarat).
- Baris 6 (8 frame): sedih/gagal (bahu turun, kepala menunduk, sedikit gemetar).
- Baris 7 (6 frame): menunggu/bertanya (kepala miring, tanda tanya kecil di dekat kepala boleh).
- Baris 8 (6 frame): sibuk bekerja di depan laptop kecil, bersemangat, di tempat.
- Baris 9 (6 frame): mengetik/membaca dengan fokus.
- Baris 10 (8 frame): tubuh diam, hanya kepala dan mata menoleh ke arah
  atas, atas-kanan-atas, atas-kanan, kanan-atas, kanan, kanan-bawah, bawah-kanan, bawah-kanan-bawah.
- Baris 11 (8 frame): lanjutan menoleh ke arah
  bawah, bawah-kiri-bawah, bawah-kiri, kiri-bawah, kiri, kiri-atas, atas-kiri, atas-kiri-atas.

GAYA
- Karakter identik di semua frame (wajah, rambut, pakaian, warna, proporsi).
- Gaya mascot/chibi, garis tegas, warna flat, cocok untuk ukuran kecil di desktop.
- Pergerakan antar frame halus dan berurutan sehingga bisa diputar sebagai animasi.
```

<details>
<summary>Versi bahasa Inggris (banyak generator gambar lebih patuh pada prompt berbahasa Inggris)</summary>

```
Using the character from the reference image, create ONE sprite sheet for a desktop pet.

GRID: exactly 8 columns x 11 rows of equal cells (cell ratio 192:208), ideally 1536 x 2288 px total.
One pose per cell, character centered, same scale in every cell, feet on the same baseline,
padding around the character, unused cells left empty. Solid flat #00FF00 green background,
no floor shadow, no grid lines, no text, no numbers.

ROWS (left to right):
1 (6 frames) idle, facing front, gentle breathing, blink on frame 4
2 (8 frames) run cycle facing RIGHT, running in place
3 (8 frames) run cycle facing LEFT, mirror of row 2
4 (4 frames) waving right hand, smiling
5 (5 frames) happy jump: crouch, rise, peak, fall, land
6 (8 frames) sad/failed: slumped shoulders, head down, slight shake
7 (6 frames) waiting/asking: head tilt, small question mark allowed
8 (6 frames) busily working on a small laptop, energetic, in place
9 (6 frames) focused typing/reading
10 (8 frames) body still, only head/eyes look: up, up-up-right, up-right, right-up, right,
   right-down, down-right, down-down-right
11 (8 frames) continue: down, down-down-left, down-left, left-down, left, left-up, up-left, up-up-left

STYLE: identical character in every frame (face, hair, outfit, colors, proportions),
mascot/chibi style, bold outlines, flat colors, readable at small size,
smooth sequential motion so frames play as an animation.
```
</details>

Catatan untuk cara sekaligus:
* Grid hasil AI jarang tepat (jumlah kolom/baris salah, sel tidak sama besar). Periksa dulu; `build-atlas.ps1 -SheetImage` memotong gambar rata menjadi 8 × 11 sel.
* Konsistensi biasanya turun di baris akhir (arah pandang). Ulangi baris yang gagal saja dengan prompt per baris.
* Strategi hemat: generate sekaligus sekali untuk mengunci gaya, lalu perbaiki baris yang gagal per baris dengan sheet itu sebagai referensi.

### 4. Rakit atlas dengan `build-atlas.ps1`

Simpan hasil AI dalam salah satu bentuk berikut (nama folder/file = nama animasi di tabel atas):

```
frames\                          frames\                       ai-sheet.png
├── idle\01.png 02.png …         ├── idle.png      (strip)     (satu sheet 8 x 11)
├── running-right\…              ├── running-right.png
├── waving\… jumping\…           ├── waving.png …
├── … review\                    └── gaze\ (16 file)
└── gaze\01.png … 16.png
```

Lalu jalankan:

```powershell
# Folder per animasi atau file strip, latar hijau dibuang, langsung dipasang sebagai skin
powershell -File scripts\build-atlas.ps1 -InputDir .\frames -ChromaKey '#00FF00' -Name "Karakter Ku" -InstallAs KarakterKu

# Satu sheet hasil prompt sekaligus
powershell -File scripts\build-atlas.ps1 -SheetImage .\ai-sheet.png -ChromaKey '#00FF00' -InstallAs KarakterKu
```

Yang dilakukan skrip:
* Membuang latar warna polos (`-ChromaKey`), termasuk pinggiran hijau di tepi karakter. Bila karakter **tidak** memakai warna hijau sama sekali, tambahkan `-ChromaSpill All` agar sisa hijau di sela rambut ikut bersih. Bila karakter memakai hijau, minta latar magenta `#FF00FF` di prompt dan pakai `-ChromaKey '#FF00FF'`.
* Memotong ke batas karakter, menskalakan, lalu menaruhnya di tengah sel dengan kaki di garis dasar yang sama. Tinggi lompatan pada `jumping` dipertahankan.
* Bila tiap baris di-generate terpisah dengan skala berbeda, skrip memberi peringatan; jalankan ulang dengan `-ScaleMode Animation` agar ukuran karakter sama di semua animasi.
* Membuat `running-left` dari cermin `running-right` bila tidak disediakan, dan mengisi arah pandang dengan frame idle bila folder `gaze` tidak ada (pet tidak menoleh).
* Menulis `spritesheet.png` + `skin.json` (jumlah frame yang berbeda dari bawaan dicatat otomatis), memvalidasi ukuran dan transparansi, lalu dengan `-InstallAs` menyalinnya ke `%AppData%\DesktopPet\Skins\<nama>\`.

Opsi lain: `-StripFrames @{ idle = 4 }` (jumlah frame file strip), `-PixelArt` (penskalaan nearest-neighbor), `-CellWidth`/`-CellHeight` (ukuran sel lain), `-Force` (menimpa skin yang sudah ada). Lihat semua opsi dengan `Get-Help .\scripts\build-atlas.ps1 -Detailed`.

> [!TIP]
> Pet digambar dengan penskalaan *nearest-neighbor*. Gaya pixel art atau garis tegas tetap tajam di skala 1×/2×; gambar halus bergradasi bisa tampak bergerigi di skala 1,25× atau 1,5×.

### 5. Pilih skin di aplikasi

Buka **Panel Kontrol → Skin / Paket Sprite → Muat Ulang Daftar**, lalu pilih skin Anda (atau klik kanan pet → **Skin**). Tidak perlu restart. Uji setiap baris dengan tombol **9 Animasi Utama** dan **16 Arah Pandang** di Panel Kontrol. Bila skin tidak bisa dipakai (atlas terlalu kecil, PNG tanpa transparansi, dll.), alasannya tampil di bawah dropdown.

Tanpa skrip, Anda juga bisa menyusun atlas manual (Aseprite, Photoshop, GIMP dengan grid 192 × 208) dan meletakkan `spritesheet.png` (+ `skin.json` opsional) di folder `%AppData%\DesktopPet\Skins\<NamaSkin>\`. Format `skin.json` dijelaskan di `skin.example.json` yang dibuat aplikasi saat tombol **Buka Folder Skins** ditekan.

### Jalan pintas: pet dari Codex

Pet Codex (`~/.codex/pets/<nama>/`) memakai format atlas yang sama tetapi berupa WebP. Konversi dan pasang dengan:

```powershell
powershell -File scripts\import-codex-pet.ps1 -Name <nama-pet>
```

Skrip ini membutuhkan salah satu dari ImageMagick, FFmpeg, atau dwebp untuk mengubah WebP ke PNG dengan transparansi.

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
│   ├── build-atlas.ps1           # Rakit frame sprite (mis. hasil AI) menjadi atlas skin
│   ├── import-codex-pet.ps1      # Konversi pet Codex (WebP) menjadi skin PNG
│   ├── package-release.ps1       # Skrip otomatisasi build & zip rilis
│   └── send-event.ps1            # Skrip PowerShell client IPC Named Pipe
├── dist/                         # Output paket rilis ZIP dan folder portabel
├── AGENTS.md                     # Panduan arsitektur & aturan teknis bagi AI Agent
├── DesktopPet.exe                # Single-file executable siap pakai
├── install.ps1                   # Skrip installer user
├── uninstall.ps1                 # Skrip uninstaller
├── installer.iss                 # Konfigurasi Inno Setup Compiler
├── send-event.bat                # Shortcut CLI pengiriman event
├── README.md                     # README bahasa Inggris
├── README_IDN.md                 # README ini (bahasa Indonesia)
└── spritesheet.webp              # Aset atlas sprite sumber
```

---

## Catatan Tambahan bagi Kontributor / AI Agent

Jika Anda ingin memodifikasi atau memperluas fitur VibePet, pastikan membaca panduan lengkap di [**`AGENTS.md`**](AGENTS.md). File tersebut mencatat kontrak sprite atlas 8×11, aturan prioritas hierarki state visual, keputusan teknis anti-antivirus, dan tolok ukur benchmark yang harus dipatuhi.
