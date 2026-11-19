# Smart Worker Restock

![Smart Worker Restock](NexusMods/header_1400x400.jpg)

Your workers order the stock your shelves need, put loose boxes away, finish opened boxes before opening new ones and
refill the emptiest shelves first. Built for the latest version of TCG Card Shop Simulator and made to sit alongside
other worker mods.

> **AI-made.** This mod was written by an AI (Claude). I'm a developer, but I did not review its code: I made it
> simply to be able to play and enjoy the game. It is shared as is; use it at your own risk.

## Features

### Automatic orders

Every 45 seconds, the mod looks at every shop compartment filled at 80 % or less, subtracts what you already have
(boxes stored in the warehouse, boxes lying around, boxes on their way) and orders the difference in whole boxes,
emptiest shelves first.

- Your money reserve is never touched, 500 by default.
- Only items whose license you bought are ordered.
- Orders are paid and booked like the game's own restock checkout: delivery fee, transactions, daily report and
  shop experience.
- An item is not ordered again for 3 minutes, so boxes still on their way are never ordered twice.

### Opened boxes first

When a worker is about to open a full box, he takes the emptiest opened box of the same item and size instead, on the
floor or on a warehouse shelf. A box another worker is already going for is left alone.

### Emptiest shelves first

The game picks the item to restock and the compartment to fill at random. With Smart Worker Restock, a worker restocks
the item whose compartment is the emptiest, then fills the emptiest compartment of what he carries, the nearest one on
a tie. Workers going for the same item spread over its compartments. `RestockPriority` sets what "emptiest" means:
fewest items, lowest fill, or the game's random choice.

### Loose boxes put away first

Workers put the boxes left on the floor away on warehouse shelves before restocking the shop shelves. Boxes with no
room on a warehouse shelf stay where they are. Turn `StoreLooseBoxesFirst` off to restock first, as the game does.

### Worker AI Overhaul

Worker AI Overhaul also decides which box and shelf your workers go for. When it is installed, a window lets you
choose once a save is loaded:

- **Compatibility mode**: Worker AI Overhaul makes those choices and Smart Worker Restock only orders stock.
- **Keep Smart Worker Restock's choices**: opened boxes first and emptiest shelves first apply as usual.

You can change it later with `WorkerAIOverhaulMode`.

Worker AI Overhaul 1.4.6 does not work on game version 1.07: workers lose track of the boxes stored on warehouse
shelves, which then show no stock. Remove it until it is updated.

## Installation

1. Install [BepInEx 5.4.23](https://github.com/BepInEx/BepInEx/releases).
2. Remove Auto Worker Order if you have it: Smart Worker Restock replaces it and stays idle while it is installed.
3. Put `SmartWorkerRestock.dll` in `BepInEx/plugins/SmartWorkerRestock/`.

## Configuration

`BepInEx/config/gab.tcgshop.smartworkerrestock.cfg`, also available in game through the Configuration Manager.

| Setting | Default | |
|---|---|---|
| `Enabled` | true | Turns the whole mod on or off |
| `RequireHiredWorker` | true | Only order while a worker is on the clock |
| `CheckIntervalSeconds` | 45 | Time between two stock checks |
| `OrderBelowFillPercent` | 80 % | A compartment needs stock at or below this fill |
| `ExtraBoxesInReserve` | 0 | Extra boxes to keep for each item that needs stock |
| `MoneyReserve` | 500 | Money automatic orders never spend |
| `MaxItemTypesPerOrder` | 20 | Different items in one order |
| `ReorderCooldownSeconds` | 180 | Wait before ordering the same item again |
| `PreferBigBox` | true | Order big boxes when both sizes are unlocked |
| `UseOpenedBoxesFirst` | true | Opened boxes before full ones |
| `StoreLooseBoxesFirst` | true | Put loose boxes away before restocking |
| `RestockPriority` | LowestCount | `LowestCount`, `LowestFillPercent` or `GameDefault` |
| `WorkerAIOverhaulMode` | Ask | `Ask`, `Compatibility` or `Override` |

Every order and every opened box taken instead of a full one is written to the BepInEx log.

## Credits

Inspired by **Auto Worker Order** by y46056s. Smart Worker Restock is a rewrite from scratch, no code copied.

## Build

```bash
dotnet build -c Release -p:Deploy=true
```

Copies the DLL to `BepInEx/plugins/SmartWorkerRestock/`. If the game is not in `D:\Games\TCG Card Shop Simulator`, add
`-p:GameDir="C:\path\to\the\game"`. Requires the .NET SDK 8 or newer. `-p:DeployScripts=true` copies it to
`BepInEx/scripts/SmartWorkerRestock/` instead, for [ScriptEngine](https://github.com/BepInEx/BepInEx.Debug) hot reload.
