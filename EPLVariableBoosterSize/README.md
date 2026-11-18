# EPL Variable Booster Size

![EPL Variable Booster Size](NexusMods/header_1400x400.jpg)

An extension for Enhanced Prefab Loader: card packs of custom sets can hold any number of cards, not only 7. Build
your boosters like the real ones, 11 cards for an old set or 10 for a modern one, with a rarity table for every slot,
and open them in the game as usual.

For the MyCurio Pokémon sets, **EPL Variable Booster Size - MyCurio Suite** gives every booster its real size and pull
rates.

> **AI-made.** This mod was written by an AI (Claude). I'm a developer, but I did not review its code: I made it
> simply to be able to play and enjoy the game. It is shared as is; use it at your own risk.

## Features

EPL builds a pack from slots, `Slot1` to `Slot7`, each with its rarity chances, and the game's opening is made for
exactly 7 cards. This mod lifts both limits for the packs that ask for it:

- **Any number of slots**: `Slot8`, `Slot9` and beyond, ranges such as `Slot1-5`, or fewer than 7. One card per slot,
  drawn like EPL's own packs.
- **The opening follows the pack**: the game's card by card reveal goes on to the last card.
- **Final screen**: two rows fitted to your screen for more than 7 cards, whatever its resolution or shape, and one
  row across the screen for fewer.
- **God packs** get the pack's size too.
- Card backs, foils and holo effects are untouched: EPL still makes every card, and the auto pack opener gets every
  card of the pack.
- The game's packs and every other EPL pack open exactly as before.

On its own, the mod changes no pack: a pack gets its size from a card set mod, from a pack file such as the MyCurio
Suite, or from a file of your own.

## Giving a pack another size

### In the set's EPL JSON

Write the slots in the pack's `SlotWeights`. The set then requires EPL Variable Booster Size.

```json
"SlotWeights": {
  "Slot1-7":  { "Common": 100 },
  "Slot8-10": { "Uncommon": 100 },
  "Slot11":   { "Rare": 70, "Holo Rare": 30 }
}
```

### In a pack file

Keep the set's EPL JSON at 7 slots and ship a `<anything>.packslots.json` file anywhere under `BepInEx/plugins`, one
entry per pack `ItemType`. Without EPL Variable Booster Size the file is simply ignored, so a card set mod stays
playable for everyone.

```jsonc
{
  "sviPack": {
    "SlotWeights": {                    // required: one card per slot
      "Slot1-4": { "Common ": 100 },
      "Slot5-7": { "Uncommon ": 100 },
      "Slot8":   { "Common Reverse Holofoil": 49.3, "Uncommon Reverse Holofoil": 28.2, "Rare Reverse Holofoil": 9.9 },
      "Slot9":   { "Common Reverse Holofoil": 49.3, "Illustration Rare": 5.6, "Special Illustration Rare": 2.2, "Hyper Rare": 1.9 },
      "Slot10":  { "Rare Holofoil": 79.7, "Double Rare": 13.8, "Ultra Rare": 6.6 }
    },
    "CanHaveDuplicates": false,         // optional: the same card twice in a pack
    "PriceMultiplier": 1.4,             // optional: price of the pack and its boxes, existing saves included
    "CanHaveGodPacks": true,            // optional: god packs on or off
    "GodPackPercentage": 0.0002,        // optional: chance of a god pack
    "GodWeights": {                     // optional: god pack rarities, every card is drawn from them
      "Special Illustration Rare": 70,
      "★_Special Illustration Rare": 30
    }
  }
}
```

- Slot keys are `SlotN`, ranges `SlotA-B` or lists `Slot1, Slot2`. Rarity names are the set's own, and weights are
  relative chances that do not need to add up to 100.
- Optional fields fall back to the pack's own settings.
- A pack file wins over the set's JSON. Keys starting with `_` and `//` comments are ignored.
- Files are read when the game starts. The BepInEx log lists every pack changed and why one was skipped.

## Installation

1. Install [BepInEx 5.4.23](https://github.com/BepInEx/BepInEx/releases) and Enhanced Prefab Loader 6.8.0 or newer.
2. Put `EPLVariableBoosterSize.dll` in `BepInEx/plugins/EPLVariableBoosterSize/`.

## Configuration

`BepInEx/config/gab.tcgshop.eplvariableboostersize.cfg`, also available in game through the Configuration Manager.
Changes apply to the next pack opened.

| Setting | Default | |
|---|---|---|
| `CardScale` | 0.75 | Card size on the final screen of a pack of more than 7 cards, 1 being the game's size |
| `RowSpacing` | 0.73 | Distance between the two rows, in card heights |

## Compatibility

- MyCurio Products keeps its own opening for its short box packs.
- A mod that rewrites the whole pack opening screen may conflict. The log warns if the opening could not be patched.

## Build

```bash
dotnet build -c Release
```

If the game is not in `D:\Games\TCG Card Shop Simulator`, add `-p:GameDir="C:\path\to\the\game"`. Requires the .NET SDK
8 or newer. `-p:Deploy=true` copies the DLL to `BepInEx/scripts/EPLVariableBoosterSize/` for
[ScriptEngine](https://github.com/BepInEx/BepInEx.Debug) hot reload. Slots written in a set's EPL JSON are only read at
game start.

## Changelog

### 1.2.0

- Works with the latest game version: packs of more than 7 cards no longer freeze the opening.

### 1.1.0

- God packs have the pack's size, drawn from the set's god pack weights.
- New optional pack file fields: `CanHaveGodPacks`, `GodPackPercentage`, `GodWeights` and `PriceMultiplier`.
- `//` comments are allowed in pack files.

### 1.0.0

- Packs smaller than 7 cards, on one row across the screen.
- The opening follows god packs.
- The final screen stays centred when MyCurio Products moved the game's places.

### 0.1.0

- First release: packs of more than 7 cards, final screen on two rows fitted to the screen.
