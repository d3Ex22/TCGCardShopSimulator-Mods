# Smart Worker Restock

![Smart Worker Restock](NexusMods/header_1400x400.jpg)

Your workers order the stock your shop shelves need, and use up opened boxes before opening new ones.

**Smart Worker Restock is a rewrite of [Auto Worker Order](#credits)**, built for the **latest version of TCG Card Shop
Simulator** and to **work alongside the other worker mods** (Worker AI Overhaul, Staff Overhaul, QOL Events & Weather,
Pallet Delivery...). It replaces Auto Worker Order: remove that mod (Smart Worker Restock stays idle while it is
installed, so the two never order at the same time).

> **AI-made.** This mod was written by an AI (Claude). I'm a developer, but I did not review its code: I made it
> simply to be able to play and enjoy the game. It is shared as is; use it at your own risk.

## Why a rewrite

A recent game update changed how boxes are stored: a box put on a warehouse shelf is now kept as data, and the box
object only comes back when someone takes it. Auto Worker Order predates that change and only counts box objects, so:

- **it reordered all the time**: it saw an empty warehouse even when it was full (checked in game: it ordered an item
  while 16 of it were stored);
- **opened boxes piled up**: with new boxes always arriving on the floor, workers opened those first and never went back
  to the half-used ones on the warehouse shelves.

It also decided which items you could order from your shop level instead of the licenses you actually bought.

Smart Worker Restock was written from scratch against the current game, and fixes all of that.

## What it does

### Automatic orders

Every 45 seconds:

1. **What the shelves need**: every shop shelf compartment filled at 80 % or less counts the items missing to fill it.
2. **What you already have**: boxes stored on warehouse shelves, every box lying around (floor, carried, taken out of a
   shelf) and boxes already ordered and on their way.
3. **What it orders**: only the difference, in whole boxes, emptiest shelves first, without spending your money reserve
   (500 by default), and only items whose license you bought.

The order is paid and booked exactly like when you check out yourself on the restock screen: item cost, delivery fee,
transactions, daily report and shop experience. An item that was just ordered is not ordered again for 3 minutes, so
boxes still on their way (for example on a Pallet Delivery truck) are never ordered twice.

### Opened boxes first

When a worker is about to open a full box (lying on the floor, or just taken off a warehouse shelf, which then goes back
there), he takes the emptiest opened box of the same item and size instead, whether it is on the floor or stored on a
warehouse shelf, and never one another worker is already using. This
runs after the game and the other worker mods have made their choice, so it does not get in their way.

## Installation

1. Install [BepInEx 5.4.23](https://github.com/BepInEx/BepInEx/releases).
2. Remove `AutoWorkerOrder.dll` from `BepInEx/plugins`.
3. Put `SmartWorkerRestock.dll` in `BepInEx/plugins/SmartWorkerRestock/`.

## Configuration

`BepInEx/config/gab.tcgshop.smartworkerrestock.cfg` (also editable in game with the Configuration Manager).

| Setting | Default | |
|---|---|---|
| `Enabled` | true | Master switch: automatic orders and opened boxes first |
| `RequireHiredWorker` | true | Only while a worker is on the clock |
| `CheckIntervalSeconds` | 45 | Time between two stock checks |
| `OrderBelowFillPercent` | 80 % | A compartment needs stock at or below this fill level |
| `ExtraBoxesInReserve` | 0 | Extra boxes to keep in reserve for each item that needs stock |
| `MoneyReserve` | 500 | Money automatic orders never spend |
| `MaxItemTypesPerOrder` | 20 | Different items per order (emptiest shelves first) |
| `ReorderCooldownSeconds` | 180 | Wait before ordering the same item again |
| `PreferBigBox` | true | Big boxes when both sizes are unlocked |
| `UseOpenedBoxesFirst` | true | Opened boxes before full ones |

The BepInEx log shows every order ("Workers ordered ...") and every time a worker takes an opened box instead of a
full one.

## Credits

Based on the idea of **Auto Worker Order** (`com.karo.tcgshop.autoworkerorder`), rewritten from scratch: no code copied.

## Build

```bash
dotnet build -c Release -p:Deploy=true
```

Copies the DLL into the game's `BepInEx/plugins/SmartWorkerRestock/`. If the game is not in
`D:\Games\TCG Card Shop Simulator`, add `-p:GameDir="C:\path\to\the\game"`. Requires the .NET SDK 8 or newer.
