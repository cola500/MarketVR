// Minimal WebSocket server side (RFC 6455) on top of node:http's 'upgrade' event, so the relay needs no npm package.
// Text messages only; binary frames are refused. Fragmented messages are reassembled up to maxBytes.
import { createHash } from 'node:crypto';
import { EventEmitter } from 'node:events';

const GUID = '258EAFA5-E914-47DA-95CA-C5AB0DC85B11';

export function acceptUpgrade(req, socket) {
  const key = req.headers['sec-websocket-key'];
  if (!key || (req.headers.upgrade || '').toLowerCase() !== 'websocket') {
    socket.end('HTTP/1.1 400 Bad Request\r\n\r\n');
    return false;
  }
  const accept = createHash('sha1').update(key + GUID).digest('base64');
  socket.write('HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n' +
    `Sec-WebSocket-Accept: ${accept}\r\n\r\n`);
  return true;
}

export class Peer extends EventEmitter {
  constructor(socket, { maxBytes = 128 * 1024 } = {}) {
    super();
    this.socket = socket;
    this.maxBytes = maxBytes;
    this.buffer = Buffer.alloc(0);
    this.parts = [];
    this.partsLength = 0;
    this.open = true;
    this.lastSeen = Date.now();
    socket.setNoDelay(true);
    socket.on('data', (d) => this.#onData(d));
    socket.on('close', () => this.#closed());
    socket.on('error', () => this.#closed());
  }

  send(text) {
    if (!this.open) return;
    const payload = Buffer.from(text, 'utf8');
    this.socket.write(Buffer.concat([header(0x1, payload.length), payload]));
  }

  ping() {
    if (this.open) this.socket.write(header(0x9, 0));
  }

  close(code = 1000, reason = '') {
    if (!this.open) return;
    const r = Buffer.from(reason, 'utf8');
    const body = Buffer.alloc(2 + r.length);
    body.writeUInt16BE(code, 0);
    r.copy(body, 2);
    this.socket.write(Buffer.concat([header(0x8, body.length), body]));
    this.socket.end();
    this.#closed();
  }

  #closed() {
    if (!this.open) return;
    this.open = false;
    this.socket.destroy();
    this.emit('close');
  }

  #onData(chunk) {
    this.lastSeen = Date.now();
    this.buffer = Buffer.concat([this.buffer, chunk]);
    while (this.open) {
      const b = this.buffer;
      if (b.length < 2) return;
      const fin = (b[0] & 0x80) !== 0;
      const opcode = b[0] & 0x0f;
      const masked = (b[1] & 0x80) !== 0;
      let len = b[1] & 0x7f;
      let at = 2;
      if (len === 126) {
        if (b.length < 4) return;
        len = b.readUInt16BE(2);
        at = 4;
      } else if (len === 127) {
        if (b.length < 10) return;
        const big = b.readBigUInt64BE(2);
        if (big > BigInt(this.maxBytes)) return this.close(1009, 'too big');
        len = Number(big);
        at = 10;
      }
      if (!masked) return this.close(1002, 'client frames must be masked');
      if (len > this.maxBytes) return this.close(1009, 'too big');
      if (b.length < at + 4 + len) return;
      const mask = b.subarray(at, at + 4);
      const data = Buffer.from(b.subarray(at + 4, at + 4 + len));
      for (let i = 0; i < data.length; i++) data[i] ^= mask[i & 3];
      this.buffer = b.subarray(at + 4 + len);

      if (opcode === 0x8) return this.close(1000);
      if (opcode === 0x9) { this.socket.write(Buffer.concat([header(0xa, data.length), data])); continue; }
      if (opcode === 0xa) continue;
      if (opcode === 0x2) return this.close(1003, 'text only');
      if (opcode !== 0x1 && opcode !== 0x0) return this.close(1002, 'bad opcode');
      this.parts.push(data);
      this.partsLength += data.length;
      if (this.partsLength > this.maxBytes) return this.close(1009, 'too big');
      if (!fin) continue;
      const text = Buffer.concat(this.parts).toString('utf8');
      this.parts = [];
      this.partsLength = 0;
      this.emit('message', text);
    }
  }
}

function header(opcode, length) {
  if (length < 126) return Buffer.from([0x80 | opcode, length]);
  if (length < 65536) {
    const h = Buffer.alloc(4);
    h[0] = 0x80 | opcode; h[1] = 126; h.writeUInt16BE(length, 2);
    return h;
  }
  const h = Buffer.alloc(10);
  h[0] = 0x80 | opcode; h[1] = 127; h.writeBigUInt64BE(BigInt(length), 2);
  return h;
}
