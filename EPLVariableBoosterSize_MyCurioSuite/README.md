# EPL Variable Booster Size - MyCurio Suite

Pack files for [EPL Variable Booster Size](../EPLVariableBoosterSize/) that give the boosters of the **MyCurio Pokémon
card sets** their **real size** and their **real pull rates**, written with MyCurio's own rarities.

> **AI-made.** This mod was written by an AI (Claude). I'm a developer, but I did not review its data: I made it
> simply to be able to play and enjoy the game. It is shared as is; use it at your own risk.

## What it does

The MyCurio sets open 7 card boosters, the size Enhanced Prefab Loader allows. With this suite, each booster gets the
size of the real English booster and the real chance of every rarity in every slot:

| Era | Cards | Content |
|---|---|---|
| Base Set, Jungle, Fossil, Base Set 2, Team Rocket, Gym, Neo | 11 | 7 commons, 3 uncommons, 1 rare (holo 1 in 3) |
| Legendary Collection | 11 | 6 commons, 3 uncommons, 1 reverse holo, 1 rare |
| e-Card, EX | 9 | 5 commons, 2 uncommons, 1 reverse holo, 1 rare |
| Diamond & Pearl to Sword & Shield | 10 | 5 commons, 3 uncommons, 1 reverse holo, 1 rare |
| Scarlet & Violet, Mega Evolution | 10 | 4 commons, 3 uncommons, 2 reverse holos (the second can be an IR, SIR or Hyper Rare), 1 rare |
| Celebrations, Detective Pikachu, Simplified Chinese Gem Packs | 4 | 3 cards and 1 hit (Celebrations, Gems) or 1 holo rare (Detective Pikachu) |
| 30th Celebration | 5 | 3 commons, 1 holo Pikachu, 1 hit slot |
| Dragon Vault | 5 | 5 holo rares |

Special sets (Generations, Hidden Fates, Shining Fates, Crown Zenith, Paldean Fates, Prismatic Evolutions, 151...)
follow their own real contents (Radiant Collection, Shiny Vault, Galarian Gallery, Poké Ball and Master Ball foils...).

- **Basic Energy cards are left out** (most MyCurio sets have none); modern boosters keep their 10 other cards. Where
  a MyCurio set has Basic Energy among its commons (Base Set, Base Set 2, Gym, Neo Genesis, Sun & Moon), the Energy
  card counts as one more common.
- **Star tiers** (MyCurio's ★ rarities for the most wanted cards) share their rarity's real rate in MyCurio's own
  proportions. A MyCurio rarity with no real counterpart keeps MyCurio's own chance, so every card of a set can still
  be pulled.
- Promo packs (box contents), Southern Islands and the Kalos Starter Set (never sold as plain boosters) keep MyCurio's
  own packs.

## Sources

- Booster sizes: [Bulbapedia, Booster pack (TCG)](https://bulbapedia.bulbagarden.net/wiki/Booster_pack_(TCG)).
- Pull rates: the community collected rates published by [Card Codex](https://cardcodex.com) for every English
  booster; 30th Celebration from [TCGTalk](https://tcgtalk.com/guides/30th-anniversary-pull-rates) (652 packs);
  Legendary Collection's contents from PSA.
- No rate data (estimated from a set of the same era): Neo Destiny (Neo Revelation), Platinum (Stormfront), Next
  Destinies (Dark Explorers), Destined Rivals (Journey Together), Mega Evolution 3 to 6 (Phantasmal Flames). The Gem
  Packs use MyCurio's own chances in their 4 real slots.

Pull rates are not published by The Pokémon Company: these are estimates from opened packs.

## Installation

1. Install [EPL Variable Booster Size](../EPLVariableBoosterSize/) **1.0.0 or newer**.
2. Extract the zip into the game folder: it contains `BepInEx/plugins/EPLVariableBoosterSize_MyCurioSuite/` with one
   `.packslots.json` file per era.

Nothing else is needed: a booster of a MyCurio set you do not have is skipped (one line in `BepInEx/LogOutput.log`).
To go back to MyCurio's own boosters, remove the folder.

The pack files follow MyCurio's rarities. Keep the suite and your MyCurio sets on the versions listed in the
changelog: with an older or newer set, a rarity the other side does not have is left out of that booster.

## Rebuilding the files

`tools/` holds the scripts that made them (Node.js 18 or newer):

```bash
node tools/fetch-pullrates.js
node tools/build-suite.js
node tools/build-suite.js --write EPLVariableBoosterSize_MyCurioSuite
```

The first downloads the pull rates into `pullrates.json`, the second prints each booster (size, rarities kept or left
out, coverage check), the third writes the pack files. The MyCurio set files are read from
`D:\Games\TCG Card Shop Simulator\BepInEx\plugins\MyCurioCardSets_prefabLoader`, or from the folder in the
`MYCURIO_SETS` environment variable.

After a MyCurio update, `node tools/check-mycurio.js` lists the boosters and rarities of the suite that the installed
sets no longer have, and what changed since `tools/mycurio-snapshot.json`, the sets the suite was last built from.
`--snapshot` records the installed sets as the new reference.

## Changelog

**1.0.1**
- Gem Pack Vol. 7 updated for MyCurio's Chinese Gem Packs All-in-One HD 1.2.1: the new ★ Triple Rare tier is pulled
  at MyCurio's own rate. Needs that version of Gem Pack Vol. 7.
- Checked against Sun and Moon Era All-in-One HD 1.1.1 and XY Era All-in-One HD 1.1.1: no change needed.

**1.0.0**
- First release: 137 boosters with their real size and pull rates.
