// Downloads the English booster pack pull rates published by cardcodex.com into pullrates.json
// (rarity -> expected cards per pack). Used once to build the MyCurio Suite pack files; not shipped.
const fs = require('fs');
const UA = { headers: { 'User-Agent': 'Mozilla/5.0' } };
const get = u => fetch(u, UA).then(r => r.text());
(async () => {
  let urls = [];
  for (const s of ['sealed_product-sitemap.xml', 'sealed_product-sitemap2.xml', 'sealed_product-sitemap3.xml'])
    urls.push(...[...(await get('https://cardcodex.com/' + s)).matchAll(/<loc>([^<]+)<\/loc>/g)].map(m => m[1]));
  urls = urls.filter(u => /booster-pack/.test(u) && !/blister|bundle|sleeved|box|case|tin|collection|japanese|korean|chinese|display|_ja\/|\/pop\/|miscp|special-promo/i.test(u));
  const out = {};
  for (const u of urls) {
    const h = await get(u);
    const t = h.split(/<table/i).find(x => /pull-rates-table/.test(x.slice(0, 80)));
    if (!t) continue;
    const key = u.match(/-([a-z0-9]+)\/$/)[1];
    out[key] = { url: u };
    for (const row of t.split(/<tr/i).slice(2)) {
      const cells = [...row.matchAll(/<t[dh][^>]*>([\s\S]*?)<\/t[dh]>/gi)].map(m => m[1].replace(/<[^>]+>/g, '').replace(/&amp;/g, '&').replace(/&#8217;/g, "'").trim());
      if (cells.length < 2) continue;
      const v = cells[1].replace(/,/g, '');
      const n = v.match(/^([\d.]+) cards? per pack/), m = v.match(/1 in ([\d.]+)/);
      out[key][cells[0].replace(/Pok[^ ]* Ball/, 'Poke Ball')] = n ? +n[1] : m ? +(1 / +m[1]).toFixed(6) : null;
    }
  }
  fs.writeFileSync(require('path').join(__dirname, 'pullrates.json'), JSON.stringify(out, null, 1));
  console.log(Object.keys(out).length + ' packs');
})();
