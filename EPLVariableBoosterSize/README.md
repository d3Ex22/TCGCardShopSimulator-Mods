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
- **God packs** (EPL's rare packs drawn from the set's god pack weights) get the pack's size too.
- The auto pack opener goes through EPL's pack generation, so it gets all the cards of the pack too.

**On its own, this mod changes no pack.** A pack gets another size from a card set mod, from a pack file such as the
MyCurio Suite, or from your own small file (see below).

## Giving a pack another size

### 1. In the set's EPL JSON

Write the slots in the pack's `SlotWeights`:

```json
"SlotWeights": {
  "Slot1-7":  { "Common": 100 },
  "Slot8-10": { "Uncommon": 100 },
  "Slot11":   { "Rare": 70, "Holo Rare": 30 }
}
```

- Slot keys: `SlotN`, ranges `SlotA-B`, or lists `Slot1, Slot2`. Rarity names are the set's own (as in its
  `Rarities` list).
- Weights are relative chances inside a slot (they do not need to add up to 100).

The set then requires EPL Variable Booster Size.

### 2. In a pack file, for compatibility

Keep the set's EPL JSON at 7 slots and ship a `<anything>.packslots.json` file anywhere under `BepInEx/plugins`.
Without EPL Variable Booster Size the file is ignored and the packs stay at 7 cards.

Each entry is a pack's `ItemType`:

```jsonc
{
  "_doc": "Keys starting with _ are comments.",
  "sviPack": {                          // the pack's ItemType, from its set's EPL JSON
    "SlotWeights": {                    // required: one card per slot
      "Slot1-4": { "Common ": 100 },
      "Slot5-7": { "Uncommon ": 100 },
      "Slot8":   { "Common Reverse Holofoil": 49.3, "Uncommon Reverse Holofoil": 28.2, "Rare Reverse Holofoil": 9.9 },
      "Slot9":   { "Common Reverse Holofoil": 49.3, "Illustration Rare": 5.6, "Special Illustration Rare": 2.2, "Hyper Rare": 1.9 },
      "Slot10":  { "Rare Holofoil": 79.7, "Double Rare": 13.8, "Ultra Rare": 6.6 }
    },
    "CanHaveDuplicates": false,         // optional: the same card twice in a pack (default: the pack's own setting)
    "PriceMultiplier": 1.4,             // optional: price of the pack and of its boxes, existing saves too (default: 1)
    "CanHaveGodPacks": true,            // optional: god packs on or off (default: the pack's own setting)
    "GodPackPercentage": 0.0002,        // optional: chance of a god pack (default: the pack's own)
    "GodWeights": {                     // optional: god pack rarities, every card drawn here (default: the set's own)
      "Special Illustration Rare": 70,
      "★_Special Illustration Rare": 30
    }
  }
}
```

- Slot keys: `SlotN`, ranges `SlotA-B`, or lists `Slot1, Slot2`. Rarity names are the set's own (as in its
  `Rarities` list). Weights are relative chances (they do not need to add up to 100).
- A god pack has as many cards as the pack, all drawn from `GodWeights`.
- A pack file wins over the set's own JSON. Comments (`//`) are allowed.
- The files are read when the game starts. The log (`BepInEx/LogOutput.log`) lists every pack it changed, and says
  why when it skips one (pack not installed, unknown rarity, missing slot).

## Limits

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
