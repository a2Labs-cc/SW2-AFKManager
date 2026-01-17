<div align="center">

# [SwiftlyS2] AFKManager

<a href="https://github.com/a2Labs-cc/SW2-AFKManager/releases/latest">
  <img src="https://img.shields.io/github/v/release/a2Labs-cc/SW2-AFKManager?label=release&color=07f223&style=for-the-badge">
</a>
<a href="https://github.com/a2Labs-cc/SW2-AFKManager/issues">
  <img src="https://img.shields.io/github/issues/a2Labs-cc/SW2-AFKManager?label=issues&color=E63946&style=for-the-badge">
</a>
<a href="https://github.com/a2Labs-cc/SW2-AFKManager/releases">
  <img src="https://img.shields.io/github/downloads/a2Labs-cc/SW2-AFKManager/total?label=downloads&color=3A86FF&style=for-the-badge">
</a>
<a href="https://github.com/a2Labs-cc/SW2-AFKManager/stargazers">
  <img src="https://img.shields.io/github/stars/a2Labs-cc/SW2-AFKManager?label=stars&color=e3d322&style=for-the-badge">
</a>

<br/>
<sub>Made by <a href="https://github.com/agasking1337" target="_blank" rel="noopener noreferrer">aga</a></sub>

</div>


## Overview

**SW2-AFKManager** is a SwiftlyS2 plugin that helps keep your server active by warning and punishing:

- AFK players
- Players camping within a radius
- Players staying in spectator too long

It also supports optional C4 transfer from AFK players, localized messages, chat prefix + color, and optional center HTML warnings.

## Support

Need help or have questions? Join our Discord server:

<p align="center">
  <a href="https://discord.gg/d853jMW2gh" target="_blank">
    <img src="https://img.shields.io/badge/Join%20Discord-5865F2?logo=discord&logoColor=white&style=for-the-badge" alt="Discord">
  </a>
</p>


## Download Shortcuts
<ul>
  <li>
    <code>📦</code>
    <strong>&nbsp;Download Latest Plugin Version</strong> &rarr;
    <a href="https://github.com/a2Labs-cc/SW2-AFKManager/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
  <li>
    <code>⚙️</code>
    <strong>&nbsp;Download Latest SwiftlyS2 Version</strong> &rarr;
    <a href="https://github.com/swiftly-solution/swiftlys2/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
</ul>

## Installation

1. Download/build the plugin (publish output lands in `build/publish/AFKManager/`).
2. Copy the published plugin folder to your server:

```
.../game/csgo/addons/swiftlys2/plugins/AFKManager/
```
3. Ensure the `resources/` folder (translations, gamedata) is alongside the DLL.
4. Start/restart the server.

## Configuration

The plugin uses SwiftlyS2's JSON config system.

- **File name**: `config.jsonc`
- **Section**: `swiftlys2/configs/plugins/AFKManager/`

On first run the config is created automatically. The resolved path is logged on startup.

### Key Configuration Options

- `Timer`: How often the plugin checks players (seconds)
- `SkipWarmup`: Skip checks during warmup
- `WarningSound`: Sound emitted on warnings

- `ChatPrefix` / `ChatPrefixColor`: Prefix applied to chat messages
- `CenterHtmlAlerts`: Show center HTML warning messages (auto clears when player becomes active)

- `AfkWarnInterval` / `AfkPunishAfterWarnings` / `AfkPunishment`: AFK detection + punishment
- `AfkTransferC4AfterWarnings` / `AfkTransferC4OnlyFromBuyZone`: Optional C4 transfer behavior
- `AfkSkipFlag`: Permissions to skip AFK checks

- `SpecWarnInterval` / `SpecKickAfterWarnings` / `SpecKickMinPlayers` / `SpecKickOnlyMovedByPlugin`: Spectator handling
- `SpecSkipFlag`: Permissions to skip spectator checks

- `AntiCampRadius` / `AntiCampWarnInterval` / `AntiCampPunishAfterWarnings`: Camping detection
- `AntiCampPunishment` / `AntiCampSlapDamage`: Punishment configuration
- `AntiCampSkipBombPlanted` / `AntiCampSkipTeam` / `AntiCampSkipFlag`: Skip rules

### Commands

- None.

## Building

```bash
dotnet build
```

## Credits
- Original plugin [NiGHT757/AFKManager](https://github.com/NiGHT757/AFKManager)
- Readme template by [criskkky](https://github.com/criskkky)
- Release workflow based on [K4ryuu/K4-Guilds-SwiftlyS2 release workflow](https://github.com/K4ryuu/K4-Guilds-SwiftlyS2/blob/main/.github/workflows/release.yml)
