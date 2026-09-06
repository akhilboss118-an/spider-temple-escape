# 🕷️ Spider Temple Escape (3D Endless Runner)

[![Platform](https://img.shields.io/badge/Platform-Android-green.svg)](https://github.com/akhilboss118-an/spider-temple-escape/releases)
[![Unity](https://img.shields.io/badge/Unity-6000.6.0f1%20LTS-blue.svg)](https://unity.com/)
[![Architecture](https://img.shields.io/badge/Architecture-ARM64%20%7C%20ARMv7-orange.svg)](https://github.com/akhilboss118-an/spider-temple-escape/releases)
[![Release](https://img.shields.io/badge/Release-v1.0.0-brightgreen.svg)](https://github.com/akhilboss118-an/spider-temple-escape/releases/tag/v1.0.0)

A high-fidelity, high-octane 3D Temple Run-style endless runner built natively in **Unity 6** for **Android** (portrait, 60fps). 

Escape from the ancient beast through a ruined jungle temple filled with crumbling pathways, leaping over ancient fallen logs, sliding beneath low-hanging tree branches, cornering sharp 90° turns, and gathering glowing relics!

---

## 📥 Download the Game (Android APK)

You can download and play the latest standalone Android `.apk` directly:

👉 **[Download SpiderTempleEscape.apk (v1.0.0)](https://github.com/akhilboss118-an/spider-temple-escape/releases/download/v1.0.0/SpiderTempleEscape.apk)** *(~68.3 MB)*

### 📱 How to Install on Android:
1. Download the `.apk` file above on your Android phone (or transfer it via USB / Google Drive).
2. Tap the downloaded `.apk` file to install.
3. If prompted, enable **"Install from unknown sources"** for your browser / file manager.
4. Launch **Spider Temple Escape** and run for your life!

---

## 🎮 Core Features & Gameplay

### 1. Hardened Game Loop & Speed Progression
- **Dynamic Speed Progression**: Starts at $8.0\text{ m/s}$ and smoothly scales up to a capped $20.0\text{ m/s}$ over $3\text{ minutes}$ ($180\text{s}$).
- **Score System**: Distance score ($10\times \text{meters}$) + Coin bonuses ($50\text{ pts}$ per coin).
- **Relic Bank & Hearts**: Bank relics and extra lives that survive run to run.
- **High Score Persistence**: Automatically saved via `PlayerPrefs`.

### 2. Multi-Platform Controls (Touch, Tilt, Keyboard)
- **Fluid Touch Gestures**:
  - **Swipe Up**: High ballistic leap over fallen tree logs and gap chasms.
  - **Swipe Down**: Fast crouch slide underneath low overhead tree branches.
  - **Swipe Left / Right**: Instant lane switching (3 lanes: Left, Center, Right) or cornering 90° temple turns.
- **Desktop / Editor Testing**:
  - `W` / `Up` / `Space` = Leap
  - `S` / `Down` = Slide
  - `A` / `D` or `Left` / `Right` = Shift lanes / Take 90° corners
  - `Mouse Drag` = Emulates touch swipes with identical vector math.

### 3. Tree Branch Obstacle System
- **Jump Tree Logs**: Natural fallen ancient jungle trunks spawned across lanes requiring timed leaps.
- **Slide Tree Branch Arches**: Low-hanging arched jungle branches that require sliding to pass beneath safely.
- **Randomized Dynamic Spawning**: Procedural distribution preventing duplicate obstacle overlaps.

### 4. Beast Guardian & 2-Strike Danger System
- An ancient beast stalks the runner closely from behind.
- **1st Stumble**: Player speed drops temporarily, the beast rushes closer to $3.8\text{m}$, and danger pulses on-screen with a 5-second recovery window.
- **2nd Stumble**: If another obstacle is hit before recovery, the beast strikes!

### 5. Clean Loading & UI Transitions
- **Startup Loading Screen**: Clean obsidian dark theme with smooth progress bar easing, pulsating status dots, and rotating gameplay pro-tips.
- **Seamless Death Replay**: Die in the jungle and tap **Main Menu** or **Play Again** for instant silky loading transitions back to camp.
- **Input Guard**: Full touch and mouse absorption while loading to ensure zero accidental clicks.

---

## 🛠️ Technical Specifications

| Parameter | Specification |
| :--- | :--- |
| **Engine** | Unity 6 (6000.6.0f1) |
| **Scripting Backend** | IL2CPP (C# compiled to native C++) |
| **Architectures** | `ARM64-v8a` (64-bit) & `armeabi-v7a` (32-bit) |
| **Graphics API** | OpenGL ES 3.0 |
| **Target Frame Rate** | 60 FPS capped |
| **Orientation** | Portrait Locked (9:16 aspect ratio) |
| **Memory Footprint** | ~500 MB runtime RAM |

---

## 💻 Source Code & Project Structure

```
├── Assets/
│   ├── Animations/        # Character run, jump, slide, and death animations
│   ├── Materials/         # Shaders and textures for environment, player, beast, and obstacles
│   ├── Models/            # 3D models for runner, beast, and jungle obstacles
│   ├── Resources/         # Dynamically loaded UI textures and prefabs
│   ├── Scenes/            # Main.unity - complete game scene
│   └── Scripts/
│       ├── Core/          # GameManager, SoundManager, State machines
│       ├── Editor/        # AndroidBuildHelper, scene generation tools
│       ├── Obstacles/     # Jump logs, slide arches, obstacle logic
│       ├── Player/        # PlayerController, lane switching, physics
│       ├── Track/         # TrackManager, procedural chunk recycling
│       └── UI/            # UIManager, HUD, Loading screen, GameOver modal
├── Builds/Android/        # Standalone compiled SpiderTempleEscape.apk
└── ProjectSettings/       # Android player settings, input configurations, tags, and layers
```

---

## 📜 License & Credits

Developed with ❤️ using Unity 3D.  
All gameplay systems, shaders, procedural track recycling, and UI managers are customized for maximum performance.
