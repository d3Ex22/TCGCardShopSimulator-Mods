// Checks the installed MyCurio card sets against the suite after a MyCurio update. Not shipped.
// - Every booster of the pack files must still exist, and every rarity they name must still be in its set.
// - Against the snapshot of the sets the suite was last built from: sets added or removed, boosters renamed, rarities
//   added or removed, card counts and MyCurio's own slot weights changed.
// Usage: node check-mycurio.js            report
//        node check-mycurio.js --snapshot  record the installed sets as the new reference, after rebuilding the suite
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const SETS = process.env.MYCURIO_SETS || 'D:/Games/TCG Card Shop Simulator/BepInEx/plugins/MyCurioCardSets_prefabLoader/';
const PACKS = path.join(__dirname, '..', 'EPLVariableBoosterSize_MyCurioSuite');
const SNAPSHOT = path.join(__dirname, 'mycurio-snapshot.json');

const hash = o => crypto.createHash('sha1').update(JSON.stringify(o)).digest('hex').slice(0, 12);

function readSets() {
  const sets = {};
  for (const file of fs.readdirSync(SETS).filter(f => f.endsWith('.json')).sort()) {
    const j = JSON.parse(fs.readFileSync(SETS + file, 'utf8'));
    const cards = (j.CardExpansions || []).flatMap(e => e.Cards || []);
    const rarities = {};
    for (const c of cards) rarities[c.Rarity] = (rarities[c.Rarity] || 0) + 1;
    // A set can sell several boosters (promo sets); they all draw from the set's cards
    const boosters = {};
    for (const p of (j.Items || []).filter(i => i.IsCardPack)) boosters[p.ItemType] = p.SlotWeights ? hash(p.SlotWeights) : null;
    sets[file] = { boosters, cards: cards.length, rarities };
  }
  return sets;
}

const sets = readSets();
if (process.argv.includes('--snapshot')) {
  fs.writeFileSync(SNAPSHOT, JSON.stringify(sets, null, 1));
  console.log(`Snapshot written: ${Object.keys(sets).length} sets.`);
  process.exit(0);
}

const problems = [];
const byBooster = {};
for (const s of Object.values(sets)) for (const b of Object.keys(s.boosters)) byBooster[b.toLowerCase()] = s;

for (const file of fs.readdirSync(PACKS).filter(f => f.endsWith('.packslots.json')).sort()) {
  const packs = JSON.parse(fs.readFileSync(path.join(PACKS, file), 'utf8'));
  for (const [booster, def] of Object.entries(packs)) {
    if (booster.startsWith('_')) continue;
    const set = byBooster[booster.toLowerCase()];
    if (!set) { problems.push(`${file}: booster ${booster} no longer exists`); continue; }
    const named = new Set();
    for (const w of Object.values(def.SlotWeights || {})) for (const r of Object.keys(w)) named.add(r);
    for (const r of Object.keys(def.GodWeights || {})) named.add(r);
    const gone = [...named].filter(r => !(r in set.rarities));
    if (gone.length) problems.push(`${file}: ${booster} names rarities its set no longer has: ${gone.map(r => `'${r}'`).join(', ')}`);
  }
}

const changes = [];
if (fs.existsSync(SNAPSHOT)) {
  const old = JSON.parse(fs.readFileSync(SNAPSHOT, 'utf8'));
  for (const f of Object.keys(sets)) if (!old[f]) changes.push(`${f}: new set`);
  for (const f of Object.keys(old)) if (!sets[f]) changes.push(`${f}: set removed`);
  for (const [f, s] of Object.entries(sets)) {
    const o = old[f];
    if (!o) continue;
    for (const b of Object.keys(s.boosters)) if (!(b in o.boosters)) changes.push(`${f}: new booster ${b}`);
    for (const b of Object.keys(o.boosters)) if (!(b in s.boosters)) changes.push(`${f}: booster ${b} removed`);
    for (const b of Object.keys(s.boosters)) if (b in o.boosters && o.boosters[b] !== s.boosters[b]) changes.push(`${f}: ${b} has new MyCurio slot weights`);
    const added = Object.keys(s.rarities).filter(r => !(r in o.rarities));
    const removed = Object.keys(o.rarities).filter(r => !(r in s.rarities));
    if (added.length) changes.push(`${f}: new rarities ${added.map(r => `'${r}'`).join(', ')}`);
    if (removed.length) changes.push(`${f}: rarities removed ${removed.map(r => `'${r}'`).join(', ')}`);
    if (o.cards !== s.cards) changes.push(`${f}: ${o.cards} cards, now ${s.cards}`);
  }
} else {
  changes.push('No snapshot yet: run with --snapshot once the suite matches the installed sets.');
}

console.log(`${Object.keys(sets).length} MyCurio sets installed.`);
console.log(problems.length ? `\nBreaks the suite (${problems.length}):\n  ` + problems.join('\n  ') : '\nThe suite matches the installed sets.');
console.log(changes.length ? `\nChanged since the snapshot (${changes.length}):\n  ` + changes.join('\n  ') : '\nNothing changed since the snapshot.');
