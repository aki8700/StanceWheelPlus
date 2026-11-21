# StanceWheelPlus

An addon for **Fontaine's Stance Overhaul** (`com.fontaine.spt-stance-overhaul`) that replaces the 2-way `key + scroll` toggle (High/Low Ready) with stepping through a configurable stance list, and displays the current stance in the bottom-right HUD (`AmmoCountPanel`, same as ammo check).

## Features
- **Step Wheel**: Scroll up/down while holding your Stance Overhaul modifier key to step through stances in order (default: `PatrolStance,LowReady,None,HighReady,ShortStock`).
- **HUD Indicator**: Shows the current stance name and a one-line modifier summary (firing, move speed, hipfire spread, arm stamina, tactical sprint) in the bottom-right panel.
- **Hotkey Support**: Optionally shows the HUD indicator when switching stances via Stance Overhaul hotkeys.
- **Multi-language & Localizable**: Built-in support for English (`en`, default), Japanese (`ja`), Russian (`ru`), Spanish (`es`), and Simplified Chinese (`zh`), plus external `lang/<code>.json` overrides/additions.

## Requirements
- SPT 4.1.x
- [Stance Overhaul](https://sp-mod.com) (`com.fontaine.spt-stance-overhaul`)

## Installation
Extract the archive into your SPT root folder so that the files are placed at:
```
BepInEx/plugins/StanceWheelPlus/
  StanceWheelPlus.dll
  lang/
    en.json
    ja.json
    ru.json
    es.json
    zh.json
```

## Configuration (F12)
- **1. Wheel**
  - `Enable Step Wheel`: Enable/disable the step wheel override.
  - `Stance Order (Down to Up)`: Comma-separated stance list (`PatrolStance`, `LowReady`, `None`, `HighReady`, `ShortStock`, `ActiveAiming`).
  - `Wrap Around`: Wrap from top to bottom and vice versa.
  - `Input Cooldown (sec)`: Minimum interval between wheel steps.
- **2. Indicator**
  - `Show Stance Indicator`: Toggle the bottom-right HUD indicator.
  - `Show Stance Details`: Toggle the one-line modifier summary.
  - `Show On Keybind Change`: Show indicator when using Stance Overhaul hotkeys.
  - `Show For Active Aim`: Show indicator when entering/exiting Active Aim.
- **3. Localization**
  - `Language`: Language code (`en`, `ja`, `ru`, `es`, `zh`, or custom).

## Adding a Custom Translation
1. Copy `BepInEx/plugins/StanceWheelPlus/lang/en.json` to `lang/<code>.json` (for example, `lang/fr.json` or `lang/de.json`).
2. Translate the values (keep `{0}` placeholders intact).
3. Set `Language` in the F12 menu (or `BepInEx/config/com.aki8700.stancewheelplus.cfg`) to `<code>`. Changes apply immediately without restarting the game.
