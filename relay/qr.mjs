// Small QR code encoder (byte mode, error correction M, versions 1–10), so the relay can show the phone address as a
// QR code without any npm package or internet. Follows ISO/IEC 18004 the same way as Project Nayuki's reference
// encoder (MIT). Returns a square boolean matrix (true = dark), without the quiet zone.

const ECC_PER_BLOCK_M = [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26];
const BLOCKS_M = [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5];
const FORMAT_M = 0; // format bits for level M

export function encode(text) {
  const bytes = [...Buffer.from(text, 'utf8')];
  let version = 1;
  for (; version <= 10; version++) {
    const countBits = version <= 9 ? 8 : 16;
    if (4 + countBits + bytes.length * 8 <= dataCodewords(version) * 8) break;
  }
  if (version > 10) throw new Error('text too long for this QR encoder');

  const capacity = dataCodewords(version) * 8;
  const bits = [];
  const put = (value, n) => { for (let i = n - 1; i >= 0; i--) bits.push((value >>> i) & 1); };
  put(0b0100, 4);
  put(bytes.length, version <= 9 ? 8 : 16);
  for (const b of bytes) put(b, 8);
  put(0, Math.min(4, capacity - bits.length));
  put(0, (8 - (bits.length % 8)) % 8);
  for (let pad = 0xec; bits.length < capacity; pad ^= 0xec ^ 0x11) put(pad, 8);
  const data = [];
  for (let i = 0; i < bits.length; i += 8) data.push(bits.slice(i, i + 8).reduce((a, b) => (a << 1) | b, 0));

  const qr = new Matrix(version);
  qr.drawFunctionPatterns();
  qr.drawCodewords(interleave(version, data));
  let best = null;
  for (let mask = 0; mask < 8; mask++) {
    qr.applyMask(mask);
    qr.drawFormat(mask);
    const p = qr.penalty();
    if (best === null || p < best.p) best = { p, mask };
    qr.applyMask(mask); // XOR again = undo
  }
  qr.applyMask(best.mask);
  qr.drawFormat(best.mask);
  return qr.modules;
}

function rawModules(ver) {
  let r = (16 * ver + 128) * ver + 64;
  if (ver >= 2) {
    const n = Math.floor(ver / 7) + 2;
    r -= (25 * n - 10) * n - 55;
    if (ver >= 7) r -= 36;
  }
  return r;
}

function dataCodewords(ver) {
  return Math.floor(rawModules(ver) / 8) - ECC_PER_BLOCK_M[ver] * BLOCKS_M[ver];
}

function interleave(ver, data) {
  const numBlocks = BLOCKS_M[ver];
  const eccLen = ECC_PER_BLOCK_M[ver];
  const raw = Math.floor(rawModules(ver) / 8);
  const numShort = numBlocks - (raw % numBlocks);
  const shortLen = Math.floor(raw / numBlocks);
  const divisor = rsDivisor(eccLen);
  const blocks = [];
  for (let i = 0, k = 0; i < numBlocks; i++) {
    const dat = data.slice(k, k + shortLen - eccLen + (i < numShort ? 0 : 1));
    k += dat.length;
    const ecc = rsRemainder(dat, divisor);
    if (i < numShort) dat.push(0);
    blocks.push(dat.concat(ecc));
  }
  const out = [];
  for (let i = 0; i < blocks[0].length; i++)
    blocks.forEach((blk, j) => { if (i !== shortLen - eccLen || j >= numShort) out.push(blk[i]); });
  return out;
}

function gfMul(x, y) {
  let z = 0;
  for (let i = 7; i >= 0; i--) {
    z = (z << 1) ^ ((z >>> 7) * 0x11d);
    z ^= ((y >>> i) & 1) * x;
  }
  return z;
}

function rsDivisor(degree) {
  const r = new Array(degree).fill(0);
  r[degree - 1] = 1;
  let root = 1;
  for (let i = 0; i < degree; i++) {
    for (let j = 0; j < r.length; j++) {
      r[j] = gfMul(r[j], root);
      if (j + 1 < r.length) r[j] ^= r[j + 1];
    }
    root = gfMul(root, 0x02);
  }
  return r;
}

function rsRemainder(data, divisor) {
  const r = divisor.map(() => 0);
  for (const b of data) {
    const factor = b ^ r.shift();
    r.push(0);
    divisor.forEach((c, i) => { r[i] ^= gfMul(c, factor); });
  }
  return r;
}

class Matrix {
  constructor(version) {
    this.version = version;
    this.size = version * 4 + 17;
    this.modules = Array.from({ length: this.size }, () => new Array(this.size).fill(false));
    this.fn = Array.from({ length: this.size }, () => new Array(this.size).fill(false));
  }

  set(x, y, dark) { this.modules[y][x] = dark; this.fn[y][x] = true; }

  drawFunctionPatterns() {
    const s = this.size;
    for (let i = 0; i < s; i++) { this.set(6, i, i % 2 === 0); this.set(i, 6, i % 2 === 0); }
    for (const [x, y] of [[3, 3], [s - 4, 3], [3, s - 4]])
      for (let dy = -4; dy <= 4; dy++)
        for (let dx = -4; dx <= 4; dx++) {
          const d = Math.max(Math.abs(dx), Math.abs(dy));
          const xx = x + dx, yy = y + dy;
          if (xx >= 0 && xx < s && yy >= 0 && yy < s) this.set(xx, yy, d !== 2 && d !== 4);
        }
    const pos = this.alignmentPositions();
    const n = pos.length;
    for (let i = 0; i < n; i++)
      for (let j = 0; j < n; j++) {
        if ((i === 0 && j === 0) || (i === 0 && j === n - 1) || (i === n - 1 && j === 0)) continue;
        for (let dy = -2; dy <= 2; dy++)
          for (let dx = -2; dx <= 2; dx++) this.set(pos[i] + dx, pos[j] + dy, Math.max(Math.abs(dx), Math.abs(dy)) !== 1);
      }
    this.drawFormat(0);
    if (this.version >= 7) {
      let rem = this.version;
      for (let i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >>> 11) * 0x1f25);
      const bits = (this.version << 12) | rem;
      for (let i = 0; i < 18; i++) {
        const bit = ((bits >>> i) & 1) !== 0;
        const a = s - 11 + (i % 3), b = Math.floor(i / 3);
        this.set(a, b, bit);
        this.set(b, a, bit);
      }
    }
  }

  alignmentPositions() {
    if (this.version === 1) return [];
    const n = Math.floor(this.version / 7) + 2;
    const step = Math.floor((this.version * 8 + n * 3 + 5) / (n * 4 - 4)) * 2;
    const r = [6];
    for (let p = this.size - 7; r.length < n; p -= step) r.splice(1, 0, p);
    return r;
  }

  drawFormat(mask) {
    const data = (FORMAT_M << 3) | mask;
    let rem = data;
    for (let i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >>> 9) * 0x537);
    const bits = ((data << 10) | rem) ^ 0x5412;
    const bit = (i) => ((bits >>> i) & 1) !== 0;
    const s = this.size;
    for (let i = 0; i <= 5; i++) this.set(8, i, bit(i));
    this.set(8, 7, bit(6));
    this.set(8, 8, bit(7));
    this.set(7, 8, bit(8));
    for (let i = 9; i < 15; i++) this.set(14 - i, 8, bit(i));
    for (let i = 0; i < 8; i++) this.set(s - 1 - i, 8, bit(i));
    for (let i = 8; i < 15; i++) this.set(8, s - 15 + i, bit(i));
    this.set(8, s - 8, true);
  }

  drawCodewords(data) {
    const s = this.size;
    let i = 0;
    for (let right = s - 1; right >= 1; right -= 2) {
      if (right === 6) right = 5;
      for (let vert = 0; vert < s; vert++)
        for (let j = 0; j < 2; j++) {
          const x = right - j;
          const upward = ((right + 1) & 2) === 0;
          const y = upward ? s - 1 - vert : vert;
          if (!this.fn[y][x] && i < data.length * 8) {
            this.modules[y][x] = ((data[i >>> 3] >>> (7 - (i & 7))) & 1) !== 0;
            i++;
          }
        }
    }
  }

  applyMask(mask) {
    const f = [
      (x, y) => (x + y) % 2 === 0,
      (x, y) => y % 2 === 0,
      (x) => x % 3 === 0,
      (x, y) => (x + y) % 3 === 0,
      (x, y) => (Math.floor(x / 3) + Math.floor(y / 2)) % 2 === 0,
      (x, y) => ((x * y) % 2) + ((x * y) % 3) === 0,
      (x, y) => (((x * y) % 2) + ((x * y) % 3)) % 2 === 0,
      (x, y) => (((x + y) % 2) + ((x * y) % 3)) % 2 === 0,
    ][mask];
    for (let y = 0; y < this.size; y++)
      for (let x = 0; x < this.size; x++) if (!this.fn[y][x] && f(x, y)) this.modules[y][x] = !this.modules[y][x];
  }

  // Simplified ISO penalty (runs, 2x2 blocks, finder-like patterns, balance): any mask decodes, this only picks a
  // good-looking one.
  penalty() {
    const m = this.modules, s = this.size;
    let p = 0;
    const lines = [];
    for (let i = 0; i < s; i++) { lines.push(m[i]); lines.push(m.map((row) => row[i])); }
    for (const line of lines) {
      let run = 1;
      for (let i = 1; i <= s; i++) {
        if (i < s && line[i] === line[i - 1]) run++;
        else { if (run >= 5) p += run - 2; run = 1; }
      }
      const str = line.map((d) => (d ? '1' : '0')).join('');
      for (const pat of ['10111010000', '00001011101']) for (let k = str.indexOf(pat); k >= 0; k = str.indexOf(pat, k + 1)) p += 40;
    }
    for (let y = 0; y < s - 1; y++)
      for (let x = 0; x < s - 1; x++) {
        const c = m[y][x];
        if (c === m[y][x + 1] && c === m[y + 1][x] && c === m[y + 1][x + 1]) p += 3;
      }
    const dark = m.flat().filter(Boolean).length;
    p += Math.floor(Math.abs(dark * 20 - s * s * 10) / (s * s)) * 10;
    return p;
  }
}

// Terminal rendering with half blocks (two module rows per text line), quiet zone included.
export function toTerminal(modules) {
  const q = 2, s = modules.length;
  const at = (x, y) => x >= 0 && y >= 0 && x < s && y < s && modules[y][x];
  const lines = [];
  for (let y = -q; y < s + q; y += 2) {
    let line = '';
    for (let x = -q; x < s + q; x++) {
      const top = at(x, y), bottom = at(x, y + 1);
      line += top && bottom ? ' ' : top ? '▄' : bottom ? '▀' : '█'; // inverted: light terminal blocks = light modules
    }
    lines.push(line);
  }
  return lines.join('\n');
}

export function toSvg(modules, scale = 8) {
  const q = 4, s = modules.length, size = (s + q * 2) * scale;
  let path = '';
  modules.forEach((row, y) => row.forEach((d, x) => { if (d) path += `M${(x + q) * scale},${(y + q) * scale}h${scale}v${scale}h-${scale}z`; }));
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" shape-rendering="crispEdges">` +
    `<rect width="100%" height="100%" fill="#fff"/><path d="${path}" fill="#000"/></svg>`;
}
