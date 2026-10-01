# VibePet

🌐 **English** | [Bahasa Indonesia](README_IDN.md)

> [!NOTE]
> **Vibecoding Notice:**
> This project was **written entirely through *vibecoding*** (built through conversations with, and instructions to, an AI agent). As the author, *I didn't have any idea about the code* at a deep technical level. That is why I include the context guide [**`AGENTS.md`**](AGENTS.md), so anyone who wants to contribute to, modify, or continue developing this project can share exactly the same *vibecoding* baseline and understanding with the AI assistant of their choice.

---

**VibePet** is a standalone Windows desktop application built with **C# WPF (.NET 8 LTS)** that shows an interactive animated pet on top of other applications (*always-on-top borderless overlay*). The pet (using the *Kawahime* sprite) reflects your activity in *real time*: when you type, move the mouse cursor, when the computer is working hard (high CPU/RAM), and it shows process notifications in an interactive comic speech bubble.

> [!NOTE]
> VibePet was previously called *Desktop Pet*. The executable, source folders, data folder (`%AppData%\DesktopPet`), and IPC pipe (`\\.\pipe\DesktopPetIpc`) still use the `DesktopPet` name, so existing settings, skins, and scripts keep working.

---

## Key Features

### 1. Smooth, Power-Efficient Sprite Animation
* **8×11 Sprite Atlas:** Contains 9 main animations (Idle, Running-Right, Running-Left, Waving, Jumping, Failed, Waiting, Running/Work, Review/Typing) and 16 gaze-direction poses (*gaze tracking*).
* **Efficient Rendering:** Uses an adaptive per-frame timer; rendering only happens when the frame changes, not in a continuous 60/144 Hz loop.
* **Perfect Transparency:** 32-bit lossless transparent borderless overlay without alpha-edge glitches.

### 2. User Behavior & Activity Detection (Antivirus-Friendly)
* **Non-Invasive Typing Detection:** Compares the Windows input timestamp (`GetLastInputInfo`) with mouse movement instead of an invasive global keyboard hook (`WH_KEYBOARD_LL`), so it is 100% safe and does not trigger antivirus false positives. Typing triggers the *Review* pose.
* **Gaze Tracking (16 Directions):** The pet's eyes naturally follow the mouse cursor while idle, with a neutral *deadzone* in the center to prevent jitter.
* **Auto Wander & Reduced Motion:** The pet occasionally strolls along the screen. A *Reduced Motion* option is available for users who prefer a still/static display.

### 3. Computer Resource Monitoring (CPU & RAM)
* **Separate Sampling:** CPU (`GetSystemTimes`) and RAM (`GlobalMemoryStatusEx`) monitoring runs on a separate thread without burdening the UI loop.
* **Sweat Effect & Load Badges:** A sweat drop and badges (`🔥 CPU`, `⚡ RAM`) appear when system load exceeds the thresholds.
* **Anti-Oscillation Hysteresis:** A *sustain counter* keeps the visual state from flickering when the load briefly rises and falls.
* **Very Light Footprint:** Working set RAM of only ~88–90 MB with average CPU usage < 0.4%.

### 4. Application Integration & IPC Named Pipe
* **Duplex Named Pipe (`\\.\pipe\DesktopPetIpc`):** Receives structured JSON messages from external scripts, terminals, or CI/CD without a Windows Firewall prompt.
* **Comic Notification Bubble (*Speech Bubble*):** Floats above the pet's head with themed colors (Success, Error, Needs Response, Info), an action button that runs a shell command, and a `✕` close button.
* **Automatic Process Watcher:** Monitors the lifecycle of target developer applications (`dotnet`, `node`, `pwsh`, `cargo`, `ffmpeg`, `code`, etc.). Plays a celebration animation (*Jumping*) when a process exits with code 0, or a sulking animation (*Failed*) for any other exit code.

### 5. Windows System Resilience
* **Per-Monitor V2 High-DPI:** The sprite stays sharp and proportional at every display scale (100%, 125%, 150%, 200%).
* **Multi-Monitor Resilience:** If a secondary monitor is unplugged or turned off, the pet automatically moves to the primary monitor's work area.
* **Smart Sleep / Resume:** Stops the animation loop and sampling when the laptop lid is closed or Windows sleeps, to save battery.
* **Windows Auto-Start:** Optional automatic startup at Windows login via Registry Run (`HKCU`).

---

## How to Run

### Option A: Standalone Executable (Instant)
Just run the executable in the project root:
```cmd
.\DesktopPet.exe
```

### Option B: Automatic Installation
Run the PowerShell install script to install into `%LocalAppData%\Programs\DesktopPet` and create Desktop and Start Menu shortcuts:
```powershell
.\install.ps1
```
> *Note: To enable auto-start during installation, use:* `.\install.ps1 -AutoStart`

To uninstall the application later:
```powershell
.\uninstall.ps1
```

### Option C: Portable Archive (ZIP)
A ready-to-use portable ZIP bundle is available at:
```
dist/DesktopPet-v1.0.0-win-x64.zip
```

---

## Interaction & Controls

| Action | How |
| :--- | :--- |
| **Move the Pet** | Left-click and hold (*drag*) the pet anywhere on screen. When released, the pet does a happy jump (*jumping*). |
| **Quick Context Menu** | Right-click the pet to open a menu with the 9 manual animations, 16 gaze directions, scale (0.75x–2.0x), behavior options, and position reset. |
| **Control Panel & Tester** | Right-click the pet &rarr; choose **"Control Panel & Tester..."** (or double-click the tray icon in the bottom-right corner of the taskbar). |
| **Close a Notification** | Click the `✕` on the comic bubble above the pet. |
| **Exit the Application** | Right-click the pet &rarr; choose **"Exit"**, or use the button in the Control Panel. |

---

## Creating Your Own Skin / Sprite (with AI Help)

You can replace the pet character with your own artwork. The flow: ask an AI to draw the character's frames, clean them up, then assemble them into an atlas with [`scripts/build-atlas.ps1`](scripts/build-atlas.ps1) and install it as a skin.

### 1. The atlas format the application reads

One transparent PNG image with **8 columns × 11 rows** of cells. The default cell size is 192 × 208 px, so the atlas is 1536 × 2288 px.

| Row | Animation | Frames | Content |
| :--- | :--- | :--- | :--- |
| 0 | idle | 6 | Standing relaxed, breathing/blinking |
| 1 | running-right | 8 | Running facing right |
| 2 | running-left | 8 | Running facing left (mirror of row 1) |
| 3 | waving | 4 | Waving a hand |
| 4 | jumping | 5 | Happy jump |
| 5 | failed | 8 | Sad / failed |
| 6 | waiting | 6 | Asking / waiting |
| 7 | running | 6 | Busy working in place |
| 8 | review | 6 | Typing / reading |
| 9–10 | gaze | 16 | Head turned to 0°, 22.5°, … 337.5° (0° = up, clockwise) |

Frame counts and durations may differ; configure them in `skin.json`. The row order cannot be changed.

### 2. Lock in the character design (character sheet)

AI tends to change character details from image to image, so lock the design first. Upload your image to a generator that accepts reference images (ChatGPT, Gemini, Midjourney `--cref`, Leonardo Character Reference, etc.):

> Create a character sheet of the character in this image: front, right side, left side, and back views. Chibi/mascot style, large head proportions, bold outlines, flat colors, no floor shadow. Solid green #00FF00 background. All poses at the same size and scale.

Keep the best result and **attach it again as the reference in every following prompt**.

### 3a. Generate one row at a time (recommended)

One prompt per animation, producing a horizontal *strip*. This is the most consistent approach and uses the least quota.

**Idle (row 0):**
> Using the character from the reference, create a horizontal sprite strip with 6 frames of an idle animation: standing facing front, breathing gently, blinking on frame 4. Every frame the same size, character centered, feet on the same baseline in every frame. Solid green #00FF00 background, no shadow, no text.

**Running right (row 1):**
> The same character, an 8-frame sprite strip of a run cycle facing right (contact, down, pass, up for each leg). Consistent size and foot position, the character does not move forward inside the frame (running in place). Green #00FF00 background.

**Waving (row 3):**
> The same character, 4 frames: right hand rises, waves to the left, to the right, then lowers. Facing front, smiling.

**Gaze (rows 9–10):**
> The same character, 16 head/eye poses turning clockwise, starting from looking up (0°), up-right (45°), right (90°), … to up-left (337.5°). The body stays still; only the head and eyes change.

Continue with the same pattern for jumping, failed, waiting, running (busy at a laptop), and review (typing).

Tips:
* If the generator cannot produce the exact frame count, generate frames one at a time ("frame 3 of an 8-frame run cycle, left leg forward …").
* **Running-left does not need to be generated**; `build-atlas.ps1` mirrors it from running-right.
* Ask for "running in place". Moving the pet across the screen is handled by the application, not by the images.

### 3b. Generate everything in a single prompt

> [!WARNING]
> Creating the whole atlas (75 frames) in a single prompt is much heavier than going row by row. On subscription/quota-based services (ChatGPT, Gemini, Midjourney, Leonardo, etc.) this can **use up your token limit, credits, or image quota faster**, especially because the result usually has to be regenerated several times before the grid and consistency are right. If your quota is limited, use the row-by-row approach.

Attach the character image as the reference:

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

An Indonesian version of this prompt is available in [README_IDN.md](README_IDN.md#3b-generate-sekaligus-dalam-satu-prompt).

Notes on the single-prompt approach:
* AI-generated grids are rarely exact (wrong number of columns/rows, cells of unequal size). Check first; `build-atlas.ps1 -SheetImage` splits the image evenly into 8 × 11 cells.
* Consistency usually drops in the last rows (gaze). Redo only the failed rows with row-by-row prompts.
* Budget strategy: generate everything once to lock in the style, then fix failed rows one at a time using that sheet as the reference.

### 4. Assemble the atlas with `build-atlas.ps1`

Save the AI output in one of these layouts (folder/file names = the animation names in the table above):

```
frames\                          frames\                       ai-sheet.png
├── idle\01.png 02.png …         ├── idle.png      (strip)     (one 8 x 11 sheet)
├── running-right\…              ├── running-right.png
├── waving\… jumping\…           ├── waving.png …
├── … review\                    └── gaze\ (16 files)
└── gaze\01.png … 16.png
```

Then run:

```powershell
# One folder or strip file per animation, green background removed, installed as a skin right away
powershell -File scripts\build-atlas.ps1 -InputDir .\frames -ChromaKey '#00FF00' -Name "My Character" -InstallAs MyCharacter

# A single sheet from the all-in-one prompt
powershell -File scripts\build-atlas.ps1 -SheetImage .\ai-sheet.png -ChromaKey '#00FF00' -InstallAs MyCharacter
```

What the script does:
* Removes the solid background color (`-ChromaKey`), including the green fringe around the character's edges. If the character uses **no** green at all, add `-ChromaSpill All` so green showing through gaps in the hair is cleaned too. If the character does use green, ask for a magenta `#FF00FF` background in the prompt and use `-ChromaKey '#FF00FF'`.
* Crops to the character's bounds, scales it, and centers it in the cell with the feet on a shared baseline. The jump height in `jumping` is preserved.
* If rows were generated separately at different scales, the script warns you; run it again with `-ScaleMode Animation` so the character is the same size in every animation.
* Creates `running-left` by mirroring `running-right` when it is not provided, and fills the gaze poses with the first idle frame when there is no `gaze` folder (the pet will not look around).
* Writes `spritesheet.png` + `skin.json` (frame counts that differ from the defaults are recorded automatically), validates the size and transparency, and with `-InstallAs` copies them to `%AppData%\DesktopPet\Skins\<name>\`.

Other options: `-StripFrames @{ idle = 4 }` (frame count of a strip file), `-PixelArt` (nearest-neighbor scaling), `-CellWidth`/`-CellHeight` (different cell size), `-Force` (overwrite an existing skin). See all options with `Get-Help .\scripts\build-atlas.ps1 -Detailed`.

> [!TIP]
> The pet is drawn with *nearest-neighbor* scaling. Pixel art or bold-outline styles stay sharp at 1×/2×; smooth, gradient-heavy art may look jagged at 1.25× or 1.5×.

### 5. Choose the skin in the application

Open **Control Panel → Skin / Sprite Pack → Reload List**, then pick your skin (or right-click the pet → **Skin**). No restart needed. Test every row with the **9 Main Animations** and **16 Gaze Directions** buttons in the Control Panel. If a skin cannot be used (atlas too small, PNG without transparency, etc.), the reason is shown below the dropdown.

Without the script, you can also assemble the atlas manually (Aseprite, Photoshop, GIMP with a 192 × 208 grid) and place `spritesheet.png` (+ optional `skin.json`) in `%AppData%\DesktopPet\Skins\<SkinName>\`. The `skin.json` format is described in `skin.example.json`, which the application creates when you press **Open Skins Folder**.

### Shortcut: pets from Codex

Codex pets (`~/.codex/pets/<name>/`) use the same atlas format but are stored as WebP. Convert and install one with:

```powershell
powershell -File scripts\import-codex-pet.ps1 -Name <pet-name>
```

This script needs ImageMagick, FFmpeg, or dwebp to convert WebP to PNG with transparency.

---

## Sending Events via IPC (Scripting / CLI)

You can send events directly from a terminal, build script, or local automation using [`send-event.bat`](send-event.bat) or the PowerShell script [`scripts/send-event.ps1`](scripts/send-event.ps1).

### Syntax
```cmd
send-event.bat <event> [title] [message] [actionLabel] [timeoutSeconds]
```

### Example Commands:

1. **Work / Build Started:**
   ```cmd
   send-event.bat start "Build Started" "Compiling the project..."
   ```

2. **Work Completed Successfully:**
   ```cmd
   send-event.bat success "Build Succeeded!" "All files compiled successfully" "Open Folder" 6
   ```

3. **Error / Failure:**
   ```cmd
   send-event.bat error "Compilation Failed" "2 files have syntax errors" "View Log"
   ```

4. **Waiting for Confirmation / User Action:**
   ```cmd
   send-event.bat needs_action "Git Confirmation" "Push the branch to main?" "Push Now"
   ```

5. **Plain Notification:**
   ```cmd
   send-event.bat notify "New Message" "A teammate mentioned you in PR #12"
   ```

6. **Clear Notifications & Reset the Animation:**
   ```cmd
   send-event.bat clear
   ```

---

## Building & Testing

### Prerequisites:
* Windows 10 or Windows 11 (64-bit)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running the Unit Tests:
The project includes 44 automated unit tests covering SpriteSheet, StateMachine, GazeTracker, ResourceMonitor, NamedPipe IPC, ProcessWatcher, AutoStart, and multi-monitor positioning:
```cmd
dotnet test
```

### Rebuilding the Release Package:
Run the release packaging script:
```powershell
pwsh -File .\scripts\package-release.ps1
```

---

## Directory Structure

```
pet-ag/
├── src/
│   └── DesktopPet/               # Main C# WPF application source
│       ├── Assets/               # Spritesheet PNG (32-bit RGBA) and .ico icon
│       ├── Models/               # Data models, IPC events, and animation definitions
│       ├── Services/             # System services (IPC, Monitor, Watcher, AutoStart, etc.)
│       ├── Views/                # Control Panel XAML & code-behind
│       ├── MainWindow.xaml       # Transparent pet overlay & comic bubble
│       └── app.manifest          # Per-Monitor V2 DPI & OS compatibility configuration
├── tests/
│   └── DesktopPet.Tests/         # 44 unit tests (xUnit)
├── scripts/
│   ├── build-atlas.ps1           # Assemble sprite frames (e.g. AI output) into a skin atlas
│   ├── import-codex-pet.ps1      # Convert a Codex pet (WebP) into a PNG skin
│   ├── package-release.ps1       # Build & zip release automation script
│   └── send-event.ps1            # PowerShell IPC Named Pipe client script
├── dist/                         # Release ZIP output and portable folder
├── AGENTS.md                     # Architecture guide & technical rules for AI agents
├── DesktopPet.exe                # Ready-to-use single-file executable
├── install.ps1                   # User installer script
├── uninstall.ps1                 # Uninstaller script
├── installer.iss                 # Inno Setup Compiler configuration
├── send-event.bat                # CLI shortcut for sending events
├── README.md                     # This file (English)
├── README_IDN.md                 # Indonesian version of this README
└── spritesheet.webp              # Source sprite atlas asset
```

---

## Additional Notes for Contributors / AI Agents

If you want to modify or extend VibePet, be sure to read the full guide in [**`AGENTS.md`**](AGENTS.md). It documents the 8×11 sprite atlas contract, the visual state priority hierarchy, the antivirus-safe technical decisions, and the benchmark targets that must be respected.
