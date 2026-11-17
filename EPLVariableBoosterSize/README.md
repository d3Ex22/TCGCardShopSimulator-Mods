# EPL Variable Booster Size

![EPL Variable Booster Size](NexusMods/header_1400x400.jpg)

An extension for Enhanced Prefab Loader (EPL): card packs of custom card sets can hold **any number of cards**, not
only 7. Pack your boosters like the real ones (11 cards for an old set, 10 for a modern one, 4 for a mini set) with a
rarity table for every card slot, and open them in the game as usual.

For the MyCurio Pokémon card sets, **EPL Variable Booster Size - MyCurio Suite** gives every booster its real size and
real pull rates.

> **AI-made.** This mod was written by an AI (Claude). I'm a developer, but I did not review its code: I made it
> simply to be able to play and enjoy the game. It is shared as is; use it at your own risk.

## What it does

EPL builds a pack from "slots": `Slot1` to `Slot7`, each with the chances of every rarity. Seven is its limit, and the
game's pack opening is made for exactly 7 cards. This mod lifts both limits, for the packs that ask for it:

- **Any number of slots**: `Slot8`, `Slot9` and so on, or ranges such as `Slot1-5`, or fewer than 7. One card is drawn
  per slot, with the slot's rarity chances, like EPL's own packs (no duplicate in a pack unless the pack allows it).
- **The pack opening follows the pack**: the same card by card reveal as the game, ending on the pack's last card.
- **Final screen**:
  - more than 7 cards: **two rows** (the top row gets the extra card of an odd count), fitted to your screen with your
    camera, whatever the resolution or the screen shape (16:9, 16:10, 21:9...); the cards get smaller only when the
    rows would not fit;
  - fewer than 7 cards: **one row**, spread between the two ends of the game's own row.
- **Only those packs change**: the game's packs and every other EPL pack open exactly as before.
- The card sets' own card backs, foils and holo effects are kept: EPL still makes every card.
- The auto pack opener goes through EPL's pack generation, so it gets all the cards of the pack too.

**On its own, this mod changes no pack.** A pack gets another size from a card set mod, from a pack file such as the
MyCurio Suite, or from your own small file (see below).

## Giving a pack another size

### A pack file (the intended way)

A file named `<anything>.packslots.json`, anywhere under `BepInEx/plugins`, gives packs their slots. It sits next to the
set's EPL JSON and does not change it:

- **Set authors**: ship this file with your set and keep your EPL JSON as it is (7 slots). Without EPL Variable
  Booster Size, the file is ignored and your packs open as 7 card packs; with it, they get their real size. Your set
  works in every mod list, with or without this mod.
- **Players and patches**: change the packs of a set you did not make, without editing its files (as the MyCurio Suite
  does for the MyCurio sets).

Each entry is the pack's `ItemType` (as written in its set's EPL JSON) and its slots:

```json
{
  "_doc": "Scarlet & Violet booster of the MyCurio sets as 10 cards.",
  "sviPack": {
    "SlotWeights": {
      "Slot1-4": { "Common ": 100 },
      "Slot5-7": { "Uncommon ": 100 },
      "Slot8":   { "Common Reverse Holofoil": 49.3, "Uncommon Reverse Holofoil": 28.2, "Rare Reverse Holofoil": 9.9 },
      "Slot9":   { "Common Reverse Holofoil": 49.3, "Illustration Rare": 5.6, "Special Illustration Rare": 2.2, "Hyper Rare": 1.9 },
      "Slot10":  { "Rare Holofoil": 79.7, "Double Rare": 13.8, "Ultra Rare": 6.6 }
    }
  }
}
```

- Slot keys: `SlotN`, ranges `SlotA-B`, or lists `Slot1, Slot2`. Rarity names are the set's own (as in its
  `Rarities` list).
- Weights are relative chances inside a slot (they do not need to add up to 100).
- `"CanHaveDuplicates": true` next to `SlotWeights` allows the same card twice in a pack (default: the pack's own
  setting).
- A pack file wins over the slots written in the set's own JSON. Keys starting with `_` are comments.
- The files are read when the game starts. The log (`BepInEx/LogOutput.log`) lists every pack it changed, and says
  why when it skips one (pack not installed, unknown rarity, missing slot).

### In the set's EPL JSON

The extra slots can also go straight into the pack's `SlotWeights`, in the set's EPL JSON:

```json
"SlotWeights": {
  "Slot1-7":  { "Common": 100 },
  "Slot8-10": { "Uncommon": 100 },
  "Slot11":   { "Rare": 70, "Holo Rare": 30 }
}
```

The set then needs EPL Variable Booster Size: EPL refuses a pack with slots it does not know. To keep the set working
without it, use a pack file instead.

## Limits

- **God packs** keep EPL's own generation (7 cards); the opening follows them.
- More cards per pack is more value per pack: the pack's price is still the one set by its mod.

## Installation

1. Install [BepInEx 5.4.23](https://github.com/BepInEx/BepInEx/releases) and Enhanced Prefab Loader (version 6.8.0).
2. Put `EPLVariableBoosterSize.dll` in `BepInEx/plugins/EPLVariableBoosterSize/`.

## Configuration

`BepInEx/config/gab.tcgshop.eplvariableboostersize.cfg` (also editable in game with the Configuration Manager; the
next pack opened uses the new values).

| Section | Setting | Default | |
|---|---|---|---|
| Pack opening | `CardScale` | 0.75 | Size of the cards on the final screen of a pack of more than 7 cards (1 = the game's size). Smaller on its own if the rows do not fit the screen. |
| | `RowSpacing` | 0.73 | Distance between the middles of the two rows, in card heights. Under 1, the bottom row covers the foot of the top one. |

## Compatibility

- Another mod that rewrites the game's pack opening screen for every pack may conflict; the log warns if the
  opening could not be patched.
- MyCurio Products' own short packs (box contents) keep its opening; the final screen of our packs is laid out after
  it, from the game's own places.

## Build

```bash
dotnet build -c Release
```

If the game is not in `D:\Games\TCG Card Shop Simulator`, add `-p:GameDir="C:\path\to\the\game"`. Requires the .NET SDK
8 or newer. `-p:Deploy=true` copies the DLL and its symbols to `BepInEx/scripts/EPLVariableBoosterSize/` for
[ScriptEngine](https://github.com/BepInEx/BepInEx.Debug) hot reload (the mod removes its patches when unloaded). Packs
written in a set's own EPL JSON are only read at game start: test those with a full restart.
