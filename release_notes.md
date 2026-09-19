## What's New in v3.0.0

### 5 Major New Features
- **📸 Photo Mode**: Pause gameplay and enter free camera mode. Drag to orbit, scroll/pinch to zoom, WASD to move. Capture screenshots directly from the pause menu.
- **📤 Screenshot Sharing**: Death screen share button now captures a screenshot with your run stats and shares via native Android share intent (text + image).
- **🎵 Gender-Specific Sound Effects**: Female characters (Nezuko, Hinata, Anya) get unique higher-pitched jump, slide, lane-change, and landing sounds. Male characters retain the original deeper tones.
- **🌆 Parallax Background**: Main menu now features 3-layer parallax jungle silhouettes scrolling at different speeds for depth illusion.
- **🏷️ Animated Title Logo**: Main menu title breathes with subtle scale pulse, gold glow animation, and shimmering filigree divider lines.

### Enhanced Gameplay
- **📹 Screen Shake Overhaul**: Added lateral wobble (side-to-side shake) and FOV kick on stumble/death for more impactful hit feedback.
- **🎓 Swipe Tutorial Overlay**: First-time players see a 3-step animated tutorial (jump, slide, lane change) with dot indicators and tap-to-dismiss.
- **📳 Haptic Feedback**: Light/medium/heavy vibration patterns for different events (stumble, death, powerup collection). Added cooldown to prevent rapid repeated vibrations.
- **🖥️ Dynamic Resolution Scaling**: Automatically reduces render resolution when FPS drops below 35, recovers when FPS exceeds 55. Resets on menu return.
- **🛣️ Road Surface Variety**: Chunks now cycle through 4 different surface materials (mossy stone, worn flagstone, cracked earth, vine-covered).
- **🃏 Animated Character Cards**: Selected hero card on main menu features pulsing glow border, bouncing icon, and shimmer highlight line.

### Performance
- Reduced active chunk count from 14 to 10
- Junction chunk spawning spread across frames (4 per frame) instead of 28 at once
- Capped active brazier point lights to maximum 4
- Reduced tree density from 3+3 to 2+2 per side
- More aggressive quality stepping thresholds (40 FPS critical, 1.5s check interval)

### Bug Fixes
- Spinning blades now properly re-animate when reused from object pool
- Pool reuse force-toggles SpinningBladeAnimator component
- Blade animation uses unscaledDeltaTime so it works during slow-mo death

### Download
- **APK Size**: ~145 MB
- **Min Android**: 8.0 (Oreo)
- **Architecture**: ARM64 + ARMv7 (IL2CPP)
