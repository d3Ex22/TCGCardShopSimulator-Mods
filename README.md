# TCG Card Shop Simulator – Mods

BepInEx mods for **TCG Card Shop Simulator**. Each mod lives in its own folder and gets one GitHub release per version.

| Mod | What it does |
|---|---|
| [Smart Worker Restock](SmartWorkerRestock/) | Workers order the stock the shelves need, counting what is really in the warehouse, use up opened boxes first and restock the emptiest shelves first. Replaces Auto Worker Order |

## Install

Download the mod's zip from [Releases](https://github.com/d3Ex22/TCGCardShopSimulator-Mods/releases) and extract it
into the game folder (or install it with Vortex). Requires [BepInEx 5.4.23](https://github.com/BepInEx/BepInEx/releases).

## Build

Requires the game (Unity 6, Mono) with BepInEx installed, and the .NET SDK 8 or newer. In a mod's folder:

```bash
dotnet build -c Release -p:Deploy=true
```

`Deploy=true` copies the DLL into the game's `BepInEx/plugins` folder. If the game is not in
`D:\Games\TCG Card Shop Simulator`, add `-p:GameDir="C:\path\to\the\game"`.

## Rules

- No code copied from other mods; ideas are reimplemented.
- The game's code is never redistributed.
- A mod never depends on another mod; compatibility fixes find other mods by reflection and do nothing if they are absent.
