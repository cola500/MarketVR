// Relay tests (EPIC-004): routing, session code, what each side may send, photo caching and limits.
// Run: node --test relay/test/   (no npm packages; Node's built-in WebSocket client plays phone and Quest)
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { startRelay } from '../server.mjs';
import { encode } from '../qr.mjs';

let relay;
const base = () => `ws://127.0.0.1:${relay.port}/ws`;

before(async () => { relay = await startRelay({ port: 0, code: '4821', beacon: false, log: () => {} }); });
after(async () => { await relay.close(); });

// A client that records every message and can wait for one matching a predicate.
function client(url) {
  const ws = new WebSocket(url);
  const got = [];
  const waiters = [];
  ws.onmessage = (e) => {
    const m = JSON.parse(e.data);
    if (m.t === 'ping') return;
    got.push(m);
    for (const w of [...waiters]) if (w.pred(m)) { waiters.splice(waiters.indexOf(w), 1); w.resolve(m); }
  };
  const opened = new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });
  const closed = new Promise((res) => { ws.onclose = (e) => res(e); });
  return {
    ws, got, opened, closed,
    send: (o) => ws.send(typeof o === 'string' ? o : JSON.stringify(o)),
    next: (pred = () => true, ms = 2000) => {
      const hit = got.find(pred);
      if (hit) { got.splice(got.indexOf(hit), 1); return Promise.resolve(hit); }
      return new Promise((resolve, reject) => {
        const w = { pred, resolve: (m) => { clearTimeout(t); got.splice(got.indexOf(m), 1); resolve(m); } };
        const t = setTimeout(() => { waiters.splice(waiters.indexOf(w), 1); reject(new Error('timeout waiting for message')); }, ms);
        waiters.push(w);
      });
    },
    close: () => ws.close(),
  };
}

const quiet = (ms = 150) => new Promise((r) => setTimeout(r, ms));
const phone = (id = 'phone-aaaaaaaa', code = '4821') => client(`${base()}?role=phone&code=${code}&client=${id}`);
const quest = () => client(`${base()}?role=quest`);

test('Relay_WrongSessionCodeIsRefused', async () => {
  const p = phone('phone-wrongcode', '0000');
  const err = await p.next((m) => m.t === 'error');
  assert.equal(err.reason, 'code');
  const e = await p.closed;
  assert.equal(e.code, 4001);
});

test('Relay_OrderReachesQuestWithOnlyAllowedFields', async () => {
  const q = quest(); await q.opened;
  q.send({ t: 'hello', role: 'quest', build: 'test', photos: [] });
  const p = phone(); await p.opened;
  await p.next((m) => m.t === 'welcome');
  p.send({ t: 'order', orderId: 'order-0001abcd', items: ['apple'], photoId: '', wallet: 99, sum: 50, state: 'done' });
  const o = await q.next((m) => m.t === 'order');
  assert.deepEqual(o, { t: 'order', orderId: 'order-0001abcd', items: ['apple'], photoId: '' }, 'wallet/sum/state never reach the Quest');
  p.send({ t: 'pay', orderId: 'order-0001abcd' });
  p.send({ t: 'status', orderId: 'order-0001abcd', state: 'done' });
  p.send({ t: 'wallet', balance: 1000 });
  p.send('not json');
  await quiet();
  assert.equal(q.got.filter((m) => m.t !== 'photo').length, 0, 'a phone can only send order, photo and sync');
  p.close(); q.close(); await q.closed;
});

test('Relay_StatusFromQuestReachesPhones', async () => {
  const q = quest(); await q.opened;
  q.send({ t: 'hello', photos: [] });
  const p = phone('phone-bbbbbbbb'); await p.opened;
  await p.next((m) => m.t === 'welcome');
  q.send({ t: 'status', orderId: 'order-0002abcd', state: 'waiting', reason: '', sum: 1, duplicate: false });
  const s = await p.next((m) => m.t === 'status');
  assert.equal(s.state, 'waiting');
  q.send({ t: 'order', orderId: 'x' }); // a Quest may not send orders to phones
  await quiet();
  assert.equal(p.got.filter((m) => m.t === 'order').length, 0);
  p.close(); q.close(); await q.closed;
});

test('Relay_OrderWhileQuestOfflineTellsThePhone', async () => {
  const p = phone('phone-cccccccc'); await p.opened;
  const w = await p.next((m) => m.t === 'welcome');
  assert.equal(w.questOnline, false);
  p.send({ t: 'order', orderId: 'order-0003abcd', items: ['apple'] });
  const m = await p.next((x) => x.t === 'quest');
  assert.equal(m.online, false);
  const q = quest(); await q.opened;
  q.send({ t: 'hello', photos: [] });
  assert.equal((await p.next((x) => x.t === 'quest')).online, true, 'phones hear when the Quest comes (back)');
  q.close(); await q.closed;
  assert.equal((await p.next((x) => x.t === 'quest')).online, false, 'and when it goes');
  p.close();
});

test('Relay_PhotoIsCachedAndSentToQuestOnlyWhenMissing', async () => {
  const rgba = 'A'.repeat(4 * Math.ceil((128 * 128 * 4) / 3));
  const p = phone('phone-dddddddd'); await p.opened;
  await p.next((m) => m.t === 'welcome');
  p.send({ t: 'photo', photoId: 'photo-0001abcd', w: 128, h: 128, rgba, extra: 'x' });
  await quiet();

  const q1 = quest(); await q1.opened;
  q1.send({ t: 'hello', photos: [] });
  const ph = await q1.next((m) => m.t === 'photo');
  assert.equal(ph.photoId, 'photo-0001abcd');
  assert.equal(ph.extra, undefined);
  p.send({ t: 'order', orderId: 'order-0004abcd', items: ['apple'], photoId: 'photo-0001abcd' });
  await q1.next((m) => m.t === 'order');
  assert.equal(q1.got.filter((m) => m.t === 'photo').length, 0, 'not sent again with the order');
  q1.close(); await q1.closed;

  const q2 = quest(); await q2.opened;
  q2.send({ t: 'hello', photos: ['photo-0001abcd'] }); // reconnected and still has it
  await quiet();
  assert.equal(q2.got.filter((m) => m.t === 'photo').length, 0, 'a Quest that has the photo does not get it again');
  q2.close(); await q2.closed;

  const q3 = quest(); await q3.opened;
  q3.send({ t: 'hello', photos: [] }); // app restarted: photo gone
  assert.equal((await q3.next((m) => m.t === 'photo')).photoId, 'photo-0001abcd');
  q3.close(); await q3.closed;

  const p2 = phone('phone-dddddddd'); await p2.opened; // same phone reconnects
  assert.equal((await p2.next((m) => m.t === 'welcome')).photoId, 'photo-0001abcd', 'the phone need not upload it again');
  p.close(); p2.close();
});

test('Relay_OversizePhotoAndStatusWithImageDataAreDropped', async () => {
  const q = quest(); await q.opened;
  q.send({ t: 'hello', photos: [] });
  const p = phone('phone-eeeeeeee'); await p.opened;
  await p.next((m) => m.t === 'welcome');
  p.send({ t: 'photo', photoId: 'photo-0002abcd', w: 512, h: 512, rgba: 'A'.repeat(100 * 1024) });
  await quiet();
  assert.equal(q.got.filter((m) => m.photoId === 'photo-0002abcd').length, 0, 'over the size limit');
  p.send({ t: 'photo', photoId: 'photo-0003abcd', w: 128, h: 64, rgba: 'A'.repeat(4 * Math.ceil((128 * 64 * 4) / 3)) });
  p.send({ t: 'photo', photoId: 'photo-0004abcd', w: 64, h: 64, rgba: 'A'.repeat(100) });
  await quiet();
  assert.equal(q.got.filter((m) => m.photoId === 'photo-0003abcd' || m.photoId === 'photo-0004abcd').length, 0, 'not square / wrong length');
  q.send({ t: 'status', orderId: 'order-0005abcd', state: 'done', rgba: 'A'.repeat(8 * 1024) });
  await quiet();
  assert.equal(p.got.filter((m) => m.t === 'status').length, 0, 'statuses are small; image data never rides along');
  p.close(); q.close(); await q.closed;
});

test('Relay_NewerQuestReplacesOlder', async () => {
  const a = quest(); await a.opened;
  const b = quest(); await b.opened;
  const e = await a.closed;
  assert.equal(e.code, 4000);
  assert.equal(relay.questOnline, true);
  b.close(); await b.closed;
});

test('Relay_ServesPhonePageAndLocalStatus', async () => {
  const page = await fetch(`http://127.0.0.1:${relay.port}/`);
  assert.equal(page.status, 200);
  assert.match(await page.text(), /Handla på marknaden/);
  const s = await (await fetch(`http://127.0.0.1:${relay.port}/api/status`)).json();
  assert.equal(s.code, '4821');
  const host = await fetch(`http://127.0.0.1:${relay.port}/host`);
  assert.match(await host.text(), /<svg/);
});

test('Relay_ParentCommandsOnlyKnownFromAuthorisedPhone', async () => {
  const q = quest(); await q.opened;
  q.send({ t: 'hello', photos: [] });
  const p = phone('phone-ffffffff'); await p.opened;
  await p.next((m) => m.t === 'welcome');
  p.send({ t: 'parent', cmd: 'rest', cmdId: 'cmd-00001abc', wallet: 100, state: 'playing' });
  const c = await q.next((m) => m.t === 'parent');
  assert.deepEqual(c, { t: 'parent', cmd: 'rest', cmdId: 'cmd-00001abc' }, 'only the command and its id reach the Quest');
  for (const bad of [{ t: 'parent', cmd: 'newGame', cmdId: 'cmd-00002abc' }, { t: 'parent', cmd: 'wallet', cmdId: 'cmd-00003abc' },
    { t: 'parent', cmd: 'rest', cmdId: 'x' }, { t: 'parent', cmd: 'rest' }])
    p.send(bad);
  await quiet();
  assert.equal(q.got.filter((m) => m.t === 'parent').length, 0, 'only rest/nextRound with a valid id');
  const stranger = phone('phone-gggggggg', '9999');
  await stranger.closed; // refused: wrong session code
  assert.equal(q.got.filter((m) => m.t === 'parent').length, 0, 'nothing from a phone without the session code');
  q.send({ t: 'parentAck', cmdId: 'cmd-00001abc', cmd: 'rest', accepted: true, duplicate: false });
  assert.equal((await p.next((m) => m.t === 'parentAck')).accepted, true, 'the acknowledgement reaches the phone');
  p.close(); q.close(); await q.closed;
});

test('Relay_MarketStatusReachesPhones', async () => {
  const q = quest(); await q.opened;
  q.send({ t: 'hello', photos: [] });
  const p = phone('phone-hhhhhhhh'); await p.opened;
  await p.next((m) => m.t === 'welcome');
  q.send({ t: 'market', state: 'resting', rgba: 'A'.repeat(100) });
  const m = await p.next((x) => x.t === 'market');
  assert.deepEqual(m, { t: 'market', state: 'resting' }, 'only the state, never image data');
  const late = phone('phone-iiiiiiii'); await late.opened;
  const w = await late.next((x) => x.t === 'welcome');
  assert.equal(w.market, 'resting', 'a phone that connects later learns it at once');
  q.send({ t: 'market', state: 'hacked' });
  await quiet();
  assert.equal(p.got.filter((x) => x.t === 'market').length, 0, 'unknown states are dropped');
  q.close(); await q.closed;
  p.send({ t: 'parent', cmd: 'rest', cmdId: 'cmd-00009abc' });
  assert.equal((await p.next((x) => x.t === 'quest')).online, false, 'no Quest: the phone is told, nothing is confirmed');
  p.close(); late.close();
});

// EPIC-009 D4: the fruit on sale travel with the market status; only three known fruit keys, kept for new phones.
test('Relay_MarketFruitKeptForNewPhones', async () => {
  const q = quest(); await q.opened;
  q.send({ t: 'hello', photos: [] });
  const p = phone('phone-jjjjjjjj'); await p.opened;
  await p.next((m) => m.t === 'welcome');
  q.send({ t: 'market', state: 'playing', fruit: ['pear', 'grapes', 'apple'] });
  const m = await p.next((x) => x.t === 'market');
  assert.deepEqual(m, { t: 'market', state: 'playing', fruit: ['pear', 'grapes', 'apple'] });
  const late = phone('phone-kkkkkkkk'); await late.opened;
  const w = await late.next((x) => x.t === 'welcome');
  assert.deepEqual(w.fruit, ['pear', 'grapes', 'apple'], 'a phone that connects later gets the fruit at once');
  q.send({ t: 'market', state: 'playing', fruit: ['pear', 'lollipop', 'x'.repeat(500)] });
  const odd = await p.next((x) => x.t === 'market');
  assert.equal(odd.fruit, undefined, 'unknown or non-fruit keys: no fruit list is forwarded');
  q.close(); await q.closed; p.close(); late.close();
});

// EPIC-010 D1: the candy on sale travel with the market status too; only three known candy keys.
test('Relay_MarketCandyKeptForNewPhones', async () => {
  const q = quest(); await q.opened;
  q.send({ t: 'hello', photos: [] });
  const p = phone('phone-llllllll'); await p.opened;
  await p.next((m) => m.t === 'welcome');
  q.send({ t: 'market', state: 'playing', fruit: ['pear', 'grapes', 'apple'], candy: ['gummybear', 'chocolate', 'lollipop'] });
  const m = await p.next((x) => x.t === 'market');
  assert.deepEqual(m.candy, ['gummybear', 'chocolate', 'lollipop']);
  const late = phone('phone-mmmmmmmm'); await late.opened;
  const w = await late.next((x) => x.t === 'welcome');
  assert.deepEqual(w.candy, ['gummybear', 'chocolate', 'lollipop'], 'a phone that connects later gets the candy at once');
  q.send({ t: 'market', state: 'playing', fruit: ['pear', 'grapes', 'apple'], candy: ['gummybear', 'ball', 'apple'] });
  const odd = await p.next((x) => x.t === 'market');
  assert.equal(odd.candy, undefined, 'toys or fruit as candy: no candy list is forwarded');
  q.close(); await q.closed; p.close(); late.close();
});

// EPIC-012: the parent can continue a pause ("resume"), and the phones learn who paused (restBy).
test('Relay_ParentResumeAndRestBy', async () => {
  const q = quest(); await q.opened;
  q.send({ t: 'hello', photos: [] });
  const p = phone('phone-nnnnnnnn'); await p.opened;
  await p.next((m) => m.t === 'welcome');
  p.send({ t: 'parent', cmd: 'resume', cmdId: 'cmd-00077abc' });
  const cmd = await q.next((m) => m.t === 'parent');
  assert.deepEqual(cmd, { t: 'parent', cmd: 'resume', cmdId: 'cmd-00077abc' }, 'resume reaches the Quest');
  q.send({ t: 'market', state: 'resting', restBy: 'parent' });
  const m = await p.next((x) => x.t === 'market');
  assert.equal(m.restBy, 'parent');
  const late = phone('phone-oooooooo'); await late.opened;
  assert.equal((await late.next((x) => x.t === 'welcome')).restBy, 'parent', 'kept for phones that connect later');
  q.send({ t: 'market', state: 'resting', restBy: 'hacker' });
  assert.equal((await p.next((x) => x.t === 'market')).restBy, undefined, 'unknown causes are dropped');
  q.close(); await q.closed; p.close(); late.close();
});

test('Qr_EncodesUrlSizes', () => {
  assert.equal(encode('HELLO').length, 21);
  assert.equal(encode('http://192.168.1.10:8787/?k=4821').length, 29);
});
