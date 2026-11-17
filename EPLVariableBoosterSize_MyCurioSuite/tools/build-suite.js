// Builds the EPL Variable Booster Size pack files for the MyCurio card sets from the real English booster pull rates
// (pullrates.json, from cardcodex.com) and MyCurio's own set files. Not shipped.
//
// Per pack:
// - common and uncommon slots: the real count, each a copy of MyCurio's own common / uncommon slot;
// - every other MyCurio slot (rare slot, reverse slot...) is a "group" of MyCurio rarities. Each rarity is matched to
//   the real rarity it stands for; the real expected cards per pack of the group gives its number of slots, and the
//   real rates give the weights. MyCurio's star tiers of one rarity share its real rate in MyCurio's own proportions.
//   A MyCurio rarity with no real counterpart keeps MyCurio's own chance. Basic Energy cards are left out.
// Usage: node build-suite.js [--write <outDir>]
const fs = require('fs');
const path = require('path');
const SETS = 'D:/Games/TCG Card Shop Simulator/BepInEx/plugins/MyCurioCardSets_prefabLoader/';
const rates = require('./pullrates.json');
// Legendary Collection (no pull rate data): 11 cards, 6 commons, 3 uncommons, 1 reverse holo, 1 rare (PSA); the rare
// slot's holo share as in the Wizards sets (1 in 3); the reverse slot keeps MyCurio's chances.
rates.lc = { Common: 6, Uncommon: 3, Rare: 0.666667, 'Rare Holo': 0.333333 };

// MyCurio set file -> CardCodex pack key; "~key" = no data for this set, the same era's pack is used (estimate).
const KEY = {
  base01: 'base1', base02: 'base2', base03: 'base3', base04: 'base4', base05: 'base5', gym01: 'gym1', gym02: 'gym2',
  neo01: 'neo1', neo02: 'neo2', neo03: 'neo3', neo04: '~neo3', neo05: '~lc',
  ecard01: 'ecard1', ecard02: 'ecard2', ecard03: 'ecard3',
  ex01: 'ex1', ex02: 'ex2', ex03: 'ex3', ex04: 'ex4', ex05: 'ex5', ex06: 'ex6', ex07: 'ex7', ex08: 'ex8', ex09: 'ex9',
  ex10: 'ex10', ex11: 'ex11', ex12: 'ex12', ex13: 'ex13', ex14: 'ex14', ex15: 'ex15', ex16: 'ex16',
  dp01: 'dp1', dp02: 'dp2', dp03: 'dp3', dp04: 'dp4', dp05: 'dp5', dp06: 'dp6', dp07: 'dp7',
  pl01: '~dp7', pl02: '~dp7', pl03: '~dp7', pl04: '~dp7',
  hgss01: 'hgss1', hgss02: 'hgss2', hgss03: 'hgss3', hgss04: 'hgss4', hgss05: 'col1',
  bw01: 'bw1', bw02: 'bw2', bw03: 'bw3', bw04: '~bw5', bw05: 'bw5', bw06: 'bw6', bw07: 'bw7', bw08: 'bw8', bw09: 'bw9',
  bw10: 'bw10', bw11: 'bw11',
  xy01: 'xy1', xy02: 'xy2', xy03: 'xy3', xy04: 'xy4', xy05: 'xy5', xy06: 'xy6', xy07: 'xy7', xy08: 'xy8', xy09: 'xy9',
  xy10: 'xy10', xy11: 'xy11', xy12: 'xy12', xy095: 'g1', xy055: 'dc1',
  sm01: 'sm1', sm02: 'sm2', sm03: 'sm3', sm035: 'sm35', sm04: 'sm4', sm05: 'sm5', sm06: 'sm6', sm07: 'sm7', sm075: 'sm75',
  sm08: 'sm8', sm09: 'sm9', sm10: 'sm10', sm11: 'sm11', sm115: 'sm115', sm12: 'sm12',
  swsh01: 'swsh1', swsh02: 'swsh2', swsh03: 'swsh3', swsh035: 'swsh35', swsh04: 'swsh4', swsh045: 'swsh45', swsh05: 'swsh5',
  swsh06: 'swsh6', swsh07: 'swsh7', swsh08: 'swsh8', swsh09: 'swsh9', swsh10: 'swsh10', swsh105: 'pgo', swsh11: 'swsh11',
  swsh12: 'swsh12', swsh125: 'swsh12pt5',
  svi: 'sv1', pal: 'sv2', obf: 'sv3', mew: 'sv3pt5', par: 'sv4', paf: 'sv4pt5', tef: 'sv5', twm: 'sv6', sfa: 'sv6pt5',
  scr: 'sv7', ssp: 'sv8', pre: 'sv8pt5', jtg: 'sv9', dri: '~sv9', blk: 'zsv10pt5', wht: 'rsv10pt5',
  me01: 'me1', me02: 'me2', me025: 'me2pt5', me03: '~me2', me04: '~me2', me05: '~me2', me06: '~me2',
};

// MyCurio rarity (star tier removed) -> real rarity, for a pack's real rarities `r`.
function real(name, r, set) {
  const has = k => r[k] !== undefined;
  const tg = /^swsh(09|10|11|12)$/.test(set), gg = set === 'swsh125', sf = set === 'swsh045';
  if (gg) {
    const g = { 'Trainer Gallery Rare Holo': 'GG Rare Holo', 'Rare Holo V': 'GG Rare Holo V', 'Rare Holo VMAX': 'GG Rare Holo VMAX',
      'Rare Ultra': 'GG Ultra Rare', 'Rare Secret': 'GG Secret Rare', 'Rare Holo VSTAR': 'GG Rare Holo VSTAR' }[name];
    if (g) return g;
  }
  if (tg) {
    const t = { 'Trainer Gallery Rare Holo': 'TG Rare Holo', 'Rare Holo V': 'TG Rare Holo V', 'Rare Holo VMAX': 'TG Rare Holo VMAX',
      'Rare Ultra': 'TG Ultra Rare', 'Rare Secret': 'TG Secret Rare' }[name];
    if (t) return t;
  }
  if (sf) {
    const s = { 'Rare Holo V': 'Shiny Rare V', 'Rare Holo VMAX': 'Shiny Rare VMAX', 'Rare Secret': 'SV Secret Rare', 'Rare Shiny': 'Shiny Rare' }[name];
    if (s) return s;
  }
  const m = {
    'Common': 'Common', 'Uncommon': 'Uncommon', 'Rare': 'Rare',
    'Holo Rare Holofoil': has('Rare Holo') ? 'Rare Holo' : 'Rare',
    'Rare Holofoil': has('Rare Holo') ? 'Rare Holo' : 'Rare',
    'Common Reverse Holofoil': 'Reverse Common', 'Uncommon Reverse Holofoil': 'Reverse Uncommon',
    'Rare Reverse Holofoil': 'Reverse Rare', 'Holo Rare Reverse Holofoil': has('Reverse Rare Holo') ? 'Reverse Rare Holo' : 'Reverse Rare',
    'Holo Rare EX': 'Rare Holo EX', 'Holo Rare GX': 'Rare Holo GX', 'Holo Rare V': 'Rare Holo V', 'Holo Rare VMAX': 'Rare Holo VMAX',
    'Holo Rare VSTAR': 'Rare Holo VSTAR', 'Rare Holo VSTAR': 'Rare Holo VSTAR', 'Holo Rare LVX': 'Rare Holo Lv.X',
    'Ultra Rare': 'Ultra Rare', 'Secret Rare': 'Secret Rare', 'Rainbow Rare': 'Rainbow Rare', 'Gold Star Rare': 'Gold Star',
    'Prism Star Rare': 'Rare Prism Star', 'LEGEND Rare': 'Rare Legend', 'Prime Rare': 'Rare Prime', 'BREAK Rare': 'Rare BREAK',
    'Shining Rare': 'Shining Rare', 'Amazing Rare': 'Amazing Rare', 'Radiant Rare': 'Radiant Rare',
    'Double Rare': 'Double Rare', 'Illustration Rare': 'Illustration Rare', 'Special Illustration Rare': 'Special Illustration Rare',
    'Hyper Rare': 'Hyper Rare', 'ACE SPEC Rare': 'ACE SPEC Rare', 'Shiny Rare': 'Shiny Rare', 'Shiny Ultra Rare': 'Shiny Ultra Rare',
    'Shiny Rare GX': 'Shiny Rare GX', 'Mega Hyper Rare': 'Mega Hyper Rare', 'Black White Rare': 'Black White Rare',
    'Common Poke Ball Holofoil': 'Poke Ball Foil', 'Uncommon Poke Ball Holofoil': 'Poke Ball Foil', 'Rare Poke Ball Holofoil': 'Poke Ball Foil',
    'Common Master Ball Holofoil': 'Master Ball Foil', 'Uncommon Master Ball Holofoil': 'Master Ball Foil', 'Rare Master Ball Holofoil': 'Master Ball Foil',
    'Radiant Collection Holofoil': 'RC',
  }[name];
  if (m === 'RC') return Object.keys(r).filter(k => k.startsWith('RC '));
  // MyCurio has no separate rarity for some real ones: they go with the closest MyCurio rarity of the set.
  const alt = {
    'Secret Rare': ['Secret Rare', ...(myBases.has('Rainbow Rare') ? [] : ['Rainbow Rare']), 'SV Secret Rare', 'Reverse Secret Rare'],
    'Ultra Rare': ['Ultra Rare', 'SV Ultra Rare'],
    'ACE SPEC Rare': ['ACE SPEC Rare', 'Rare ACE'],
    'Shiny Rare': ['Shiny Rare', 'Reverse Shiny Rare', 'Shiny Legendary'],
    'Common Holofoil': ['Energy (Holo)'],
  }[name];
  if (alt) { const a = alt.filter(has); return a.length ? a : null; }
  return m && has(m) ? m : null;
}
let myBases = new Set();

const base = n => n.replace(/^★+_/, '').trim();
const isRev = k => /Reverse|Ball Foil|^RC Common/.test(k);
const slotA = k => /^(ACE SPEC Rare|Shiny Rare|Shiny Ultra Rare)$/.test(k);   // the first reverse slot's hits (Bulbapedia)

// Small special packs, laid out by hand from their real contents (MyCurio's slots reused, real counts).
function special(id, j, item, r) {
  const inSet = new Set(j.CardExpansions[0].Cards.map(c => c.Rarity));
  const my = Object.values(item.SlotWeights).map(w => Object.fromEntries(Object.entries(w).filter(([n, v]) => v > 0 && inSet.has(n))));
  const norm = w => { const t = Object.values(w).reduce((a, b) => a + b, 0); return Object.fromEntries(Object.entries(w).map(([n, v]) => [n, v / t])); };
  const avg = ws => { const s = {}; for (const w of ws.map(norm)) for (const [n, v] of Object.entries(w)) s[n] = (s[n] || 0) + v / ws.length; return s; };
  const rep = (w, n) => Array.from({ length: n }, () => norm(w));
  if (/^gem0\d$/.test(id))
    // Simplified Chinese Gem Packs: 4 holofoil cards, three of the base rarities and one star rarity (no pull rate data:
    // MyCurio's own chances inside each).
    return { slots: [...rep(avg(my.slice(0, 6)), 3), norm(my[6])], note: '4 cards (real), MyCurio chances' };
  if (id === 'sm095') return { slots: [...rep(my[0], 3), norm(my[6])], note: '4 cards: 3 holo commons, 1 holo rare (real)' };
  if (id === 'bw065') return { slots: rep(my[0], 5), note: '5 holo rares (real)' };
  if (id === 'me30th') {
    // 30th Celebration (tcgtalk.com, 652 packs): 5 cards (+ a Basic Energy, left out), one guaranteed holo Pikachu, one
    // slot with a chance at a hit: ex 21.9%, IR 18.4%, gold border reprint 9.8%, SIR 5.5%, FUR 1.2%, RGB Mew about
    // 1 in 20,000; otherwise a plain rare (43.1%). Each rarity's star tiers share its rate in MyCurio's proportions.
    const real = { 'Rare': 0.431, 'Double Rare': 0.219, 'Illustration Rare': 0.184, 'Classic Collection': 0.098,
      'Special Illustration Rare': 0.055, 'Futuristic Rare': 0.012, 'RGB Rare': 0.00005 };
    const hit = {};
    for (const [b, p] of Object.entries(real)) {
      const tiers = Object.entries(my[6]).filter(([n]) => base(n) === b);
      const t = tiers.reduce((a, [, w]) => a + w, 0);
      for (const [n, w] of tiers) hit[n] = p * w / t;
    }
    return { slots: [...rep(my[0], 3), norm(my[5]), hit], note: '5 cards: 3 commons, 1 holo Pikachu, 1 hit slot (real rates)' };
  }
  if (id === 'swsh075' && r) {
    // Celebrations: 4 cards, 3 holo rares and one slot where the hits appear (real rates).
    const holo = norm(Object.fromEntries(Object.entries(my[0]).filter(([n]) => /^(Rare Holofoil|Holo Rare Holofoil)$/.test(base(n))))), last = {};
    const hits = { 'Classic Collection': r['Classic Collection'], 'Holo Rare V': r['Rare Holo V'], 'Holo Rare VMAX': r['Rare Holo VMAX'],
      'Ultra Rare': r['Ultra Rare'], 'Secret Rare': r['Secret Rare'] };
    let hp = 0;
    for (const [n, w] of Object.entries(my[5] || {}).concat(Object.entries(my[6] || {}))) {
      const b = base(n), p = hits[b];
      if (p === undefined) continue;
      const same = [...my[5] ? Object.keys(my[5]) : [], ...Object.keys(my[6] || {})].filter(x => base(x) === b);
      last[n] = (last[n] || 0) + p / same.length;
    }
    for (const v of Object.values(last)) hp += v;
    for (const [n, v] of Object.entries(holo)) last[n] = (last[n] || 0) + (1 - hp) * v;
    return { slots: [holo, holo, holo, last], note: '4 cards: 3 holo rares, 1 hit slot (real rates)' };
  }
  return null;
}

function build(file) {
  const id = file.replace('mycurio-pokemon-', '').replace('.json', '');
  const sp = { gem01: 1, gem02: 1, gem03: 1, gem04: 1, gem05: 1, gem06: 1, gem07: 1, sm095: 1, bw065: 1, me30th: 1, swsh075: 'cel25' }[id];
  if (sp) {
    const j = JSON.parse(fs.readFileSync(SETS + file, 'utf8'));
    const item = j.Items.find(i => i.IsCardPack);
    const s = special(id, j, item, typeof sp === 'string' ? rates[sp] : null);
    const inSet = new Set(j.CardExpansions[0].Cards.map(c => c.Rarity));
    const inSlots = new Set(s.slots.flatMap(x => Object.keys(x)));
    return { id, item: item.ItemType, key: 'special', estimate: false, size: s.slots.length, nC: 0, nU: 0, groups: s.slots.length,
      slots: s.slots, dropped: [], missing: [...inSet].filter(n => !inSlots.has(n)), notes: [s.note] };
  }
  const keyRaw = KEY[id];
  if (!keyRaw) return null;
  const estimate = keyRaw.startsWith('~'), key = keyRaw.replace('~', '');
  const r = rates[key];
  if (!r) return { id, error: 'no rates for ' + key };
  const j = JSON.parse(fs.readFileSync(SETS + file, 'utf8'));
  const item = j.Items.find(i => i.IsCardPack);
  const cards = j.CardExpansions[0].Cards;
  const inSet = new Set(cards.map(c => c.Rarity));
  myBases = new Set([...inSet].map(base));
  // Basic Energy cards: real packs have one, MyCurio sets mostly have none (one card less). Where MyCurio has them
  // among its commons, the Energy slot becomes one more common slot.
  const energyCommons = cards.some(c => /^(Grass|Fire|Water|Lightning|Psychic|Fighting|Darkness|Metal|Fairy) Energy$/.test(c.Name) && base(c.Rarity) === 'Common');
  const mySlots = Object.values(item.SlotWeights).map(w => Object.fromEntries(Object.entries(w).filter(([n, v]) => v > 0 && inSet.has(n))));
  const dominant = w => { const e = Object.entries(w).sort((a, b) => b[1] - a[1])[0]; return e ? base(e[0]) : ''; };
  const norm = w => { const t = Object.values(w).reduce((a, b) => a + b, 0); return Object.fromEntries(Object.entries(w).map(([n, v]) => [n, v / t])); };
  const cSlot = norm(mySlots.find(w => dominant(w) === 'Common')), uSlot = norm(mySlots.find(w => dominant(w) === 'Uncommon'));
  const groups = mySlots.filter(w => w !== cSlot && w !== uSlot && !['Common', 'Uncommon'].includes(dominant(w)));
  const nC = Math.round((r.Common || 0) + (energyCommons ? r.Energy || 0 : 0)), nU = Math.round(r.Uncommon || 0);
  const slots = [];
  for (let i = 0; i < nC; i++) slots.push(cSlot);
  for (let i = 0; i < nU; i++) slots.push(uSlot);
  const used = new Set(), notes = [];
  used.add('Energy');   // dropped (no MyCurio card) or counted as a common above: not reported
  for (const g of groups) {
    // Real rarities of the group and MyCurio rarities standing for each.
    const byReal = {}, fallback = {};
    const myTotal = Object.values(g).reduce((a, b) => a + b, 0);
    for (const [n, w] of Object.entries(g)) {
      const k = real(base(n), r, id);
      const ks = Array.isArray(k) ? k : k ? [k] : [];
      if (ks.length === 0) { fallback[n] = w / myTotal; continue; }
      for (const kk of ks) { (byReal[kk] = byReal[kk] || []).push([n, w / ks.length]); used.add(kk); }
    }
    const realKeys = Object.keys(byReal);
    const mass = realKeys.reduce((a, k) => a + r[k], 0);
    const k = Math.max(1, Math.round(mass));
    const fb = Object.values(fallback).reduce((a, b) => a + b, 0);
    if (fb > 0) notes.push('kept MyCurio chance: ' + Object.keys(fallback).join(', '));
    // A real rarity's chance shared by its MyCurio star tiers in MyCurio's proportions.
    const spread = (kk, p, into) => { const t = byReal[kk].reduce((a, x) => a + x[1], 0); for (const [n, w] of byReal[kk]) into[n] = (into[n] || 0) + p * w / t; };
    if (k === 1) {
      const s = {};
      for (const kk of realKeys) spread(kk, (r[kk] / mass) * (1 - fb), s);
      Object.assign(s, fallback);
      slots.push(s);
    } else {
      const rev = realKeys.filter(isRev), hits = realKeys.filter(x => !isRev(x));
      const revMass = rev.reduce((a, kk) => a + r[kk], 0);
      for (let i = 0; i < k; i++) {
        const last = i === k - 1, s = {};
        const mine = hits.filter(h => (slotA(h) ? !last || k === 1 : last));
        let hp = 0;
        for (const h of mine) { spread(h, r[h], s); hp += r[h]; }
        for (const kk of rev) spread(kk, (1 - hp) * r[kk] / revMass, s);
        if (last) Object.assign(s, fallback);
        slots.push(s);
      }
    }
  }
  const dropped = Object.entries(r).filter(([kk, v]) => kk !== 'url' && typeof v === 'number' && !used.has(kk) && !['Common', 'Uncommon'].includes(kk) && v >= 0.005)
    .map(([kk, v]) => kk + ' ' + (v * 100).toFixed(1) + '%');
  // Coverage: every rarity of the set must be in a slot.
  const inSlots = new Set(slots.flatMap(s => Object.keys(s).filter(n => s[n] > 0)));
  const missing = [...inSet].filter(n => !inSlots.has(n));
  return { id, item: item.ItemType, key: keyRaw, estimate, size: slots.length, nC, nU, groups: slots.length - nC - nU, slots, dropped, missing, notes };
}

const out = [];
for (const f of fs.readdirSync(SETS).filter(f => f.endsWith('.json')).sort()) {
  const p = build(f);
  if (p) out.push(p);
}
const writeAt = process.argv.indexOf('--write');
if (writeAt > 0) {
  const dir = process.argv[writeAt + 1];
  const eras = {};
  for (const p of out.filter(p => !p.error)) {
    const era = p.id.replace(/\d.*$/, '') || p.id;
    const name = { gem: 'gem', svi: 'sv', pal: 'sv', obf: 'sv', mew: 'sv', par: 'sv', paf: 'sv', tef: 'sv', twm: 'sv', sfa: 'sv', scr: 'sv', ssp: 'sv', pre: 'sv', jtg: 'sv', dri: 'sv', blk: 'sv', wht: 'sv', gym: 'base', neo: 'base', pl: 'dp', ecard: 'ex', col: 'hgss' }[era] || era;
    const e = eras[name] = eras[name] || { _doc: 'EPL Variable Booster Size: MyCurio boosters with the real English booster sizes and rates (cardcodex.com), in MyCurio\'s rarities. Generated by build-suite.js.' };
    const sw = {};
    p.slots.forEach((s, i) => { sw['Slot' + (i + 1)] = Object.fromEntries(Object.entries(s).map(([n, w]) => [n, +(w * 100).toFixed(4)])); });
    e[p.item] = { SlotWeights: sw };
  }
  fs.mkdirSync(dir, { recursive: true });
  for (const [n, e] of Object.entries(eras)) fs.writeFileSync(path.join(dir, 'mycurio-' + n + '.packslots.json'), JSON.stringify(e, null, 1));
  console.log('written: ' + Object.keys(eras).map(n => 'mycurio-' + n + '.packslots.json').join(', '));
} else {
  for (const p of out) {
    if (p.error) { console.log(p.id + ' ERROR ' + p.error); continue; }
    console.log(`${p.id.padEnd(8)} ${String(p.size).padStart(2)} cards = ${p.nC}C ${p.nU}U +${p.groups} other${p.estimate ? '  [estimate from ' + p.key.slice(1) + ']' : ''}` +
      (p.missing.length ? '  MISSING: ' + p.missing.join(', ') : '') + (p.dropped.length ? '  no MyCurio card: ' + p.dropped.join(', ') : '') +
      (p.notes.length ? '  ' + p.notes.join('; ') : ''));
  }
}
