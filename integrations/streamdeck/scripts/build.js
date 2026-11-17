import { build } from 'esbuild';
import { mkdir, writeFile, readFile, cp } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { deflateSync } from 'node:zlib';
import { actions } from '../src/state.js';

const root = resolve(import.meta.dirname, '..');
const output = join(root, 'com.cultostudio.control.sdPlugin');
await mkdir(join(output, 'bin'), { recursive: true });
await mkdir(join(output, 'imgs'), { recursive: true });
await cp(join(root, 'ui'), join(output, 'ui'), { recursive: true });
await writeFile(join(output, 'package.json'), JSON.stringify({ type: 'module' }));
const licenses = [];
for (const dependency of ['@elgato/streamdeck', '@elgato/schemas', '@elgato/utils', 'ws', 'zod'])
  licenses.push(`${dependency}\n${await readFile(join(root, 'node_modules', dependency, 'LICENSE'), 'utf8')}`);
await writeFile(join(output, 'THIRD-PARTY-NOTICES.txt'), licenses.join('\n\n---\n\n'));
await build({ entryPoints: [join(root, 'src/plugin.js')], outfile: join(output, 'bin/plugin.js'), bundle: true, platform: 'node', target: 'node24', format: 'esm', banner: { js: "import { createRequire } from 'node:module'; const require = createRequire(import.meta.url);" } });
const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144"><rect x="6" y="6" width="132" height="132" rx="24" fill="#193b60"/><path d="M58 38h28v22h22v26H86v24H58V86H36V60h22z" fill="#ffffff"/></svg>';
await writeFile(join(output, 'imgs/action.svg'), svg);
const paths = {
  project: 'M52 28v72l56-36z M28 112h88',
  previous: 'M100 28v80L44 68z M28 28v80', next: 'M44 28v80l56-40z M116 28v80',
  background: 'M28 100l28-40 24 24 18-20 22 36z M94 36h1',
  blackout: 'M30 30h84v84H30z', pause: 'M48 28v84 M96 28v84',
  restart: 'M38 52a42 42 0 1 1-4 42 M38 24v28h28',
  'volume-down': 'M60 36L36 56H20v32h16l24 20z M84 72h40',
  'volume-up': 'M60 36L36 56H20v32h16l24 20z M84 72h40 M104 52v40',
  'return-media': 'M54 28L26 56l28 28 M26 56h60a26 26 0 0 1 0 52',
  scene: 'M26 38h68v68H26z M50 18h68v68',
  'order-item': 'M24 36h4 M48 36h72 M24 72h4 M48 72h72 M24 108h4 M48 108h72'
};
for (const [kind, path] of Object.entries(paths)) {
  await writeFile(join(output, `imgs/${kind}.svg`), `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144"><path d="${path}" fill="none" stroke="#ffffff" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/></svg>`);
}
// A deterministic PNG category/plugin icon requires no external image conversion runtime.
function crc(buffer) { let value = 0xffffffff; for (const byte of buffer) { value ^= byte; for (let i = 0; i < 8; i++) value = (value >>> 1) ^ (value & 1 ? 0xedb88320 : 0); } return (value ^ 0xffffffff) >>> 0; }
function chunk(type, data) { const name = Buffer.from(type), length = Buffer.alloc(4), checksum = Buffer.alloc(4); length.writeUInt32BE(data.length); checksum.writeUInt32BE(crc(Buffer.concat([name, data]))); return Buffer.concat([length, name, data, checksum]); }
function png(size, category = false) {
  const rows = Buffer.alloc((size * 4 + 1) * size);
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) { const index = y * (size * 4 + 1) + 1 + x * 4; const cross = (x > size * .4 && x < size * .6 && y > size * .22 && y < size * .78) || (x > size * .22 && x < size * .78 && y > size * .4 && y < size * .58); rows.set(cross ? [255, 255, 255, 255] : category ? [0, 0, 0, 0] : [25, 59, 96, 255], index); }
  const header = Buffer.alloc(13); header.writeUInt32BE(size, 0); header.writeUInt32BE(size, 4); header[8] = 8; header[9] = 6;
  return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk('IHDR', header), chunk('IDAT', deflateSync(rows)), chunk('IEND', Buffer.alloc(0))]);
}
await writeFile(join(output, 'imgs/plugin.png'), png(256));
await writeFile(join(output, 'imgs/plugin@2x.png'), png(512));
await writeFile(join(output, 'imgs/category.png'), png(28, true));
await writeFile(join(output, 'imgs/category@2x.png'), png(56, true));
const manifest = {
  $schema: 'https://schemas.elgato.com/streamdeck/plugins/manifest.json',
  Actions: Object.entries(actions).map(([kind, [name]]) => ({ UUID: `com.cultostudio.control.${kind}`, Name: name, Icon: `imgs/${kind}`, Controllers: ['Keypad'], SupportedInMultiActions: false, UserTitleEnabled: false, States: [{ Image: `imgs/${kind}`, TitleAlignment: 'bottom', FontSize: 11 }], Tooltip: `${name} en Culto Studio`, PropertyInspectorPath: 'ui/inspector.html' })),
  Author: 'Culto Studio', Name: 'Culto Studio', Description: 'Control nativo local de proyección, escenas y orden del culto.', Category: 'Culto Studio', CategoryIcon: 'imgs/category', Icon: 'imgs/plugin',
  CodePath: 'bin/plugin.js', UUID: 'com.cultostudio.control', Version: '1.0.0.0', SDKVersion: 2,
  Nodejs: { Version: '24' }, OS: [{ Platform: 'windows', MinimumVersion: '10' }], Software: { MinimumVersion: '7.1' }
};
await writeFile(join(output, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
console.log(`Built ${output}`);
