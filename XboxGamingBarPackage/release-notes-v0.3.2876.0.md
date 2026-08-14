## GoTweaks S - first public release

A Legion Go-focused fork of [GoTweaks](https://github.com/corando98/GoTweaks) with Steam-friendly controller behavior, desktop controls, and widget/desktop coexistence.

### Install

1. Download **`GoTweaksS-0.3.2876.0.zip`** below (the full install package — do **not** download the `.cmd` / `.ps1` / `.msixbundle` separately)
2. Extract the zip to a folder (e.g. Desktop\GoTweaksS)
3. Double-click **`Install GoTweaks.cmd`** inside that folder (not the `.ps1` directly) and click **Yes** on the UAC prompt
4. Open **Xbox Game Bar** (`Win+G`) and pin the GoTweaks widget
5. Optional: launch **GoTweaks** from Start for the full desktop app (same settings, stays in sync with the widget)

> **Note:** The installer must stay in the same folder as the `.msixbundle`, `.cer`, and `Dependencies` folder — that is why everything is bundled in one zip. Do not use `Install.exe` (ps2exe); it often triggers Windows Defender false positives.

---

### Highlights

#### Steam + Legion buttons (no emulation required)
- **Helper no longer kills Steam Input** when controller emulation is off - physical XInput stays alive while GoTweaks reads vendor HID for Legion L/R and paddles
- Remap **Y1/Y2/Y3, M1/M2/M3, Legion L/R, Desktop, Page** without replacing the whole controller
- **Full keyboard picker** for Legion L/R shortcuts (not just preset keys)
- **Steam BPM shortcuts** in the Legion L/R action list

#### SteamOS-style desktop controls
- **Desktop Controls** preset: right-stick mouse, LT/RT as mouse buttons, tab navigation with LT/RT
- **Steam-style RT priority** - tab nav at rest, left-click when you are actively moving the cursor
- **Hold Legion L for mouse** (experimental)
- **Auto-disable Desktop Controls in-game** option
- Dedicated **Desktop profile** overlay so desktop remaps do not overwrite your game profiles

#### Widget + desktop app
- **Separate desktop app** and Game Bar widget can run together (desktop minimizes to tray instead of killing the widget)
- **Remap settings stay in sync** between widget and desktop app
- **Panel brightness slider** on supported handheld displays (Quick tab + Display settings)

#### Quality of life
- LT/RT **tab navigation** in the widget and desktop UI when Desktop Controls are active
- Improved **mouse sensitivity scaling** (slider 50 ~ previous 70 speed)
- **GoTweaks S** installer (`.cmd` + `.ps1` — avoids Defender false positives on ps2exe)

---

### Requirements
- Windows 10/11 handheld PC (built and tested on **Legion Go**)
- Xbox Game Bar
- Administrator rights for first install

### Notes
- Keep **Controller Emulation off** if you want Steam to own face buttons/triggers/sticks
- Map paddles to **keyboard or mouse** actions for cleanest Steam coexistence (mapping paddles to gamepad buttons injects before Steam sees them)

---

Based on upstream GoTweaks. See `LICENSING.md` for MIT/GPL distribution terms.
