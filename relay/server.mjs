#!/usr/bin/env node
// MarketVR – relay for the phone customer (EPIC-004). Runs on the MacBook, on the local network only.
//
//   phone (browser) ──WebSocket──► relay ◄──WebSocket── Quest
//
// - Serves the phone page (GET /) and a host page for the MacBook (GET /host: QR code, session code, status; only
//   from this computer).
// - WebSocket at /ws. Phones must give the session code (?role=phone&code=1234&client=<id>); one Quest (?role=quest),
//   a newer Quest connection replaces the older one.
// - Forwards only what each side may send: phone -> Quest: order, photo, sync, and (EPIC-008) parent with cmd
//   rest|resume|nextRound only. Quest -> phones: status, parentAck, market (playing|party|resting + the three fruit (EPIC-009)
//   and candy (EPIC-010) keys on sale; the last one is kept and
//   given to phones that connect later). The session code is all the authorisation there is: a practical parent
//   control on the local network, not a secure lock.
//   No game logic here; the Quest decides everything. Nothing is written to disk.
// - Keeps each phone's latest photo in memory for this session and gives it to the Quest when the Quest
//   (re)connects without it, so photos are never repeated in ordinary messages.
// - UDP beacon to the LAN broadcast address (port 47811) once a second so the Quest finds the relay.
//
// Usage: node relay/server.mjs [--port 8787] [--code 1234] [--no-beacon] [--quiet]
import { createServer } from 'node:http';
import { createSocket } from 'node:dgram';
import { networkInterfaces } from 'node:os';
import { readFileSync } from 'node:fs';
import { randomInt } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { acceptUpgrade, Peer } from './ws.mjs';
import { encode, toSvg, toTerminal } from './qr.mjs';

export const BEACON_PORT = 47811;
const LIMITS = { photoChars: 90 * 1024, messageChars: 4 * 1024, frameBytes: 128 * 1024 };
const PHONE_TYPES = new Set(['order', 'photo', 'sync', 'parent']);
const QUEST_TYPES = new Set(['hello', 'status', 'parentAck', 'market']);
const PARENT_COMMANDS = new Set(['rest', 'resume', 'nextRound']); // EPIC-012: resume ends a pause in the same round
const REST_CAUSES = new Set(['child', 'parent', 'party']);
const MARKET_STATES = new Set(['playing', 'party', 'resting']);
// EPIC-009: the fruit/veg keys the Quest may say are on sale (three at a time).
const FRUIT_KEYS = new Set(['apple', 'banana', 'carrot', 'pear', 'orange', 'strawberry', 'grapes', 'watermelon']);
// EPIC-010: the candy keys likewise (toys can't be ordered from the phone, so they are never sent).
const CANDY_KEYS = new Set(['lollipop', 'polkagris', 'candybag', 'gummybear', 'chocolate', 'wrappedcandy', 'rainbowstrip', 'candyring']);
const threeOf = (keys) => (f) => Array.isArray(f) && f.length === 3 && new Set(f).size === 3 && f.every((k) => keys.has(k));
const validFruit = threeOf(FRUIT_KEYS);
const validCandy = threeOf(CANDY_KEYS);
const ID = /^[A-Za-z0-9_-]{8,40}$/;

export function lanAddresses() {
  const out = [];
  for (const [name, list] of Object.entries(networkInterfaces()))
    for (const a of list || [])
      if (a.family === 'IPv4' && !a.internal) out.push({ name, address: a.address, broadcast: broadcastOf(a.address, a.netmask) });
  // en0 (wifi on a MacBook) first
  return out.sort((a, b) => (a.name === 'en0' ? -1 : b.name === 'en0' ? 1 : 0));
}

function broadcastOf(address, netmask) {
  const a = address.split('.').map(Number), m = netmask.split('.').map(Number);
  return a.map((v, i) => (v | (~m[i] & 255))).join('.');
}

export function startRelay({ port = 8787, code = String(randomInt(0, 10000)).padStart(4, '0'), beacon = true, log = console.log } = {}) {
  const phonePage = readFileSync(new URL('./public/phone.html', import.meta.url), 'utf8');
  const phones = new Set();
  const photos = new Map(); // client id -> { photoId, json } (latest photo per phone, memory only)
  let quest = null;
  let questPhotos = new Set();
  let market = null; // the last market state from the Quest
  let fruit = null;  // EPIC-009: the last fruit on sale from the Quest (three keys)
  let candy = null;  // EPIC-010: the last candy on sale
  let restBy = null; // EPIC-012: who rests the market (child | parent | party), null while playing
  const stats = { orders: 0, statuses: 0, photos: 0, dropped: 0, questConnects: 0 };

  const toPhones = (obj) => { const s = JSON.stringify(obj); for (const p of phones) p.send(s); };

  const server = createServer((req, res) => {
    const url = new URL(req.url, 'http://x');
    const local = ['127.0.0.1', '::1', '::ffff:127.0.0.1'].includes(req.socket.remoteAddress);
    if (req.method !== 'GET') { res.writeHead(405).end(); return; }
    if (url.pathname === '/') {
      res.writeHead(200, { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store' }).end(phonePage);
    } else if (url.pathname === '/host' && local) {
      res.writeHead(200, { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store' }).end(hostPage());
    } else if (url.pathname === '/api/status' && local) {
      res.writeHead(200, { 'content-type': 'application/json', 'cache-control': 'no-store' })
        .end(JSON.stringify({ code, questOnline: !!quest, phones: phones.size, ...stats, urls: phoneUrls() }));
    } else {
      res.writeHead(404, { 'content-type': 'text/plain' }).end('Hittades inte');
    }
  });

  const phoneUrls = () => lanAddresses().map((a) => `http://${a.address}:${server.address()?.port ?? port}/?k=${code}`);

  server.on('upgrade', (req, socket) => {
    const url = new URL(req.url, 'http://x');
    if (url.pathname !== '/ws') { socket.end('HTTP/1.1 404 Not Found\r\n\r\n'); return; }
    if (!acceptUpgrade(req, socket)) return;
    const peer = new Peer(socket, { maxBytes: LIMITS.frameBytes });
    const role = url.searchParams.get('role');
    const from = req.socket.remoteAddress;

    if (role === 'quest') {
      if (quest) quest.close(4000, 'replaced');
      quest = peer;
      questPhotos = new Set();
      stats.questConnects++;
      log(`[relay] Quest connected from ${from}`);
      peer.on('message', (text) => onQuest(peer, text));
      peer.on('close', () => {
        if (quest !== peer) return;
        quest = null;
        log('[relay] Quest disconnected');
        toPhones({ t: 'quest', online: false });
      });
      return;
    }

    if (role !== 'phone' || url.searchParams.get('code') !== code) {
      peer.send(JSON.stringify({ t: 'error', reason: 'code' }));
      peer.close(4001, 'wrong session code');
      log(`[relay] refused a phone from ${from} (wrong session code)`);
      return;
    }
    const client = url.searchParams.get('client');
    if (!ID.test(client || '')) { peer.close(4002, 'bad client id'); return; }
    peer.client = client;
    phones.add(peer);
    log(`[relay] phone connected from ${from} (${phones.size} phone${phones.size === 1 ? '' : 's'})`);
    peer.send(JSON.stringify({ t: 'welcome', questOnline: !!quest, photoId: photos.get(client)?.photoId ?? null, market, fruit, candy, restBy }));
    peer.on('message', (text) => onPhone(peer, text));
    peer.on('close', () => { phones.delete(peer); log(`[relay] phone disconnected (${phones.size} left)`); });
  });

  function onPhone(peer, text) {
    const msg = parse(text, LIMITS.photoChars + 512);
    if (!msg || !PHONE_TYPES.has(msg.t)) { stats.dropped++; return; }
    if (msg.t !== 'photo' && text.length > LIMITS.messageChars) { stats.dropped++; return; }
    if (msg.t === 'photo') {
      const side = msg.w | 0;
      const okShape = side === (msg.h | 0) && side >= 16 && side <= 128 && typeof msg.rgba === 'string'
        && msg.rgba.length === 4 * Math.ceil((side * side * 4) / 3);
      if (!ID.test(msg.photoId || '') || !okShape || msg.rgba.length > LIMITS.photoChars) { stats.dropped++; return; }
      // Only the fields the Quest reads, so nothing else rides along.
      const json = JSON.stringify({ t: 'photo', photoId: msg.photoId, w: msg.w | 0, h: msg.h | 0, rgba: msg.rgba });
      photos.set(peer.client, { photoId: msg.photoId, json });
      stats.photos++;
      log(`[relay] photo ${msg.photoId.slice(0, 8)} (${Math.round(msg.rgba.length / 1024)} kB) from a phone`);
      sendPhotoToQuest(peer.client);
      return;
    }
    if (msg.t === 'order') {
      stats.orders++;
      sendPhotoToQuest(peer.client); // the Quest has the picture before the customer appears
      log(`[relay] order ${String(msg.orderId).slice(0, 8)}: ${(msg.items || []).join(', ')}`);
    }
    if (msg.t === 'parent') {
      if (!PARENT_COMMANDS.has(msg.cmd) || !ID.test(msg.cmdId || '')) { stats.dropped++; return; }
      log(`[relay] parent command ${msg.cmd} (${msg.cmdId.slice(0, 8)})`);
      if (!quest) { peer.send(JSON.stringify({ t: 'quest', online: false })); return; }
      quest.send(JSON.stringify({ t: 'parent', cmd: msg.cmd, cmdId: msg.cmdId }));
      return;
    }
    if (!quest) { peer.send(JSON.stringify({ t: 'quest', online: false })); return; }
    const clean = msg.t === 'order'
      ? { t: 'order', orderId: msg.orderId, items: Array.isArray(msg.items) ? msg.items.slice(0, 3).map(String) : [], photoId: msg.photoId ?? '' }
      : { t: 'sync', orderId: msg.orderId };
    quest.send(JSON.stringify(clean));
  }

  function sendPhotoToQuest(client) {
    const p = photos.get(client);
    if (!quest || !p || questPhotos.has(p.photoId)) return;
    quest.send(p.json);
    questPhotos.add(p.photoId);
  }

  function onQuest(peer, text) {
    if (peer !== quest) return;
    const msg = parse(text, LIMITS.messageChars);
    if (!msg || !QUEST_TYPES.has(msg.t)) { stats.dropped++; return; }
    if (msg.t === 'hello') {
      questPhotos = new Set(Array.isArray(msg.photos) ? msg.photos : []);
      log(`[relay] Quest says hello (build ${msg.build || '?'})`);
      for (const client of photos.keys()) sendPhotoToQuest(client); // only those it doesn't have yet
      toPhones({ t: 'quest', online: true });
      return;
    }
    if (msg.t === 'market') {
      if (!MARKET_STATES.has(msg.state)) { stats.dropped++; return; }
      market = msg.state;
      fruit = validFruit(msg.fruit) ? msg.fruit.slice() : null;
      candy = validCandy(msg.candy) ? msg.candy.slice() : null;
      restBy = REST_CAUSES.has(msg.restBy) ? msg.restBy : null;
      log(`[relay] market: ${market}${fruit ? ` (${fruit.join(', ')})` : ''}${candy ? ` (${candy.join(', ')})` : ''}`);
      const out = { t: 'market', state: market };
      if (fruit) out.fruit = fruit;
      if (candy) out.candy = candy;
      if (restBy) out.restBy = restBy;
      toPhones(out);
      return;
    }
    if (msg.t === 'parentAck') {
      log(`[relay] parent ${msg.cmd} ${msg.accepted ? 'accepted' : 'not accepted'}${msg.duplicate ? ' (duplicate)' : ''}`);
      toPhones({ t: 'parentAck', cmdId: String(msg.cmdId), cmd: String(msg.cmd), accepted: !!msg.accepted, duplicate: !!msg.duplicate });
      return;
    }
    stats.statuses++;
    log(`[relay] status ${String(msg.orderId).slice(0, 8)}: ${msg.state}${msg.duplicate ? ' (duplicate)' : ''}`);
    toPhones(msg);
  }

  // Liveness: an app-level ping (the Quest treats 15 s of silence as a lost link) and a protocol ping; a peer that
  // has sent nothing at all for 20 s is dropped (browsers and .NET answer pings automatically).
  const pinger = setInterval(() => {
    const now = Date.now();
    for (const p of [...phones, ...(quest ? [quest] : [])]) {
      if (now - p.lastSeen > 20000) { p.close(1001, 'silent'); continue; }
      p.ping();
      p.send('{"t":"ping"}');
    }
  }, 5000);

  let udp = null, beaconTimer = null;
  const startBeacon = () => {
    udp = createSocket({ type: 'udp4', reuseAddr: true });
    udp.on('error', (e) => log(`[relay] beacon error: ${e.message}`));
    udp.bind(0, () => {
      udp.setBroadcast(true);
      const send = () => {
        const msg = Buffer.from(JSON.stringify({ fruktstand: 'relay', port: server.address()?.port ?? port }));
        const targets = new Set(['255.255.255.255', ...lanAddresses().map((a) => a.broadcast)]);
        for (const t of targets) udp.send(msg, BEACON_PORT, t, () => {});
      };
      send();
      beaconTimer = setInterval(send, 1000);
    });
  };

  function hostPage() {
    const urls = phoneUrls();
    const qr = urls.length ? toSvg(encode(urls[0]), 10) : '';
    return `<!doctype html><html lang="sv"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Kund från mobilen</title><style>
:root{--bg:#fffaf0;--fg:#2b2b2b;--muted:#6b6b6b;--ok:#2e7d32;--bad:#b23b3b}
@media (prefers-color-scheme:dark){:root{--bg:#1d1b18;--fg:#f3efe6;--muted:#aaa39a;--ok:#7bd389;--bad:#ff8a80}}
body{margin:0;padding:24px 16px;background:var(--bg);color:var(--fg);font:18px/1.45 system-ui,-apple-system,sans-serif;text-align:center}
h1{font-size:26px;margin:0 0 8px}.qr{background:#fff;display:inline-block;padding:8px;border-radius:12px;margin:12px 0}
.qr svg{width:min(320px,80vw);height:auto;display:block}.code{font-size:44px;letter-spacing:.2em;font-weight:700}
.muted{color:var(--muted)}.ok{color:var(--ok)}.bad{color:var(--bad)}code{word-break:break-all}</style></head><body>
<h1>🍎 Kund från mobilen</h1>
<p class="muted">Skanna med mobilen (samma wifi som Quest och den här datorn).</p>
<div class="qr">${qr}</div>
<p>eller öppna <code>${urls[0] || '(ingen nätverksadress hittades)'}</code></p>
<p class="muted">Sessionskod</p><div class="code">${code}</div>
<p id="s" class="muted">…</p>
<script>
async function tick(){try{const r=await fetch('/api/status');const s=await r.json();
document.getElementById('s').innerHTML=(s.questOnline?'<span class="ok">Quest ansluten</span>':'<span class="bad">Quest inte ansluten än</span>')+' · mobiler: '+s.phones+' · beställningar: '+s.orders;}
catch(e){document.getElementById('s').textContent='Relän svarar inte';}}
tick();setInterval(tick,1000);</script></body></html>`;
  }

  return new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(port, '0.0.0.0', () => {
      const actual = server.address().port;
      if (beacon) startBeacon(); // only once the port is known
      resolve({
        port: actual,
        code,
        server,
        stats,
        get questOnline() { return !!quest; },
        get phoneCount() { return phones.size; },
        phoneUrls,
        close() {
          clearInterval(pinger);
          if (beaconTimer) clearInterval(beaconTimer);
          if (udp) udp.close();
          for (const p of phones) p.close(1001, 'relay stopped');
          if (quest) quest.close(1001, 'relay stopped');
          photos.clear();
          return new Promise((r) => server.close(() => r()));
        },
      });
    });
  });
}

function parse(text, maxChars) {
  if (typeof text !== 'string' || text.length > maxChars) return null;
  try {
    const v = JSON.parse(text);
    return v && typeof v === 'object' && !Array.isArray(v) ? v : null;
  } catch { return null; }
}

// Command line
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const arg = (name, fallback) => { const i = process.argv.indexOf(name); return i > 0 ? process.argv[i + 1] : fallback; };
  const quiet = process.argv.includes('--quiet');
  const relay = await startRelay({
    port: Number(arg('--port', 8787)),
    code: arg('--code', undefined),
    beacon: !process.argv.includes('--no-beacon'),
    log: quiet ? () => {} : (s) => console.log(`${new Date().toLocaleTimeString('sv-SE')} ${s}`),
  }).catch((e) => {
    console.error(e.code === 'EADDRINUSE' ? `Porten är upptagen. Körs relän redan? (${e.message})` : e.message);
    process.exit(2);
  });
  const urls = relay.phoneUrls();
  console.log('\nMarketVR – kund från mobilen\n');
  if (!urls.length) console.log('Hittade ingen nätverksadress. Är datorn ansluten till wifi?');
  else {
    console.log(toTerminal(encode(urls[0])));
    console.log(`\nMobilen: ${urls[0]}`);
    for (const u of urls.slice(1)) console.log(`         ${u}`);
  }
  console.log(`Sessionskod: ${relay.code}`);
  console.log(`Värdsida (QR i stort format): http://localhost:${relay.port}/host`);
  console.log('Stoppa med Ctrl+C. Ingenting sparas.\n');
  const stop = async () => { await relay.close(); process.exit(0); };
  process.on('SIGINT', stop);
  process.on('SIGTERM', stop);
}
