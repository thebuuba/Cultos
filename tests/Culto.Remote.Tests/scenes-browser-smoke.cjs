// Real browser test of the shipped mobile page against an owned loopback fixture.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.CULTO_PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname, '../..');
const html = fs.readFileSync(path.join(root, 'src/Culto.Remote/wwwroot/index.html'));
const state = {
  planName: 'Culto del sábado', preparedTitle: 'Bienvenida', preparedKind: 'Video', visualTitle: 'Logo', audioTitle: 'Música', canReturnToMedia: true, liveTitle: 'Sin salida', hasOutput: false,
  isBlackout: false, isPaused: false, volume: 70, position: 0, duration: 0, canPause: false,
  buttons: [{ id: 'next', label: 'Siguiente', color: '#4274F4', action: 'next' }], order: [{ id: 'local-video', title: 'Video local', kind: 'Video' }, { id: 'youtube-order', title: 'YouTube', kind: 'WindowCapture' }],
  scenes: [
    { id: 'logo', label: 'Logo', kind: 'logo', available: true },
    { id: 'blackout', label: 'Fondo negro', kind: 'blackout', available: true },
    { id: 'image', label: 'Imagen', kind: 'image', available: false },
    { id: 'video', label: 'Video', kind: 'video', available: true, detail: 'Somos un pueblo' },
    { id: 'youtube', label: 'YouTube', kind: 'youtube', available: true, detail: 'Himno en Chrome' }
  ]
};
const commands = [];
const server = http.createServer(async (req, res) => {
  if (req.url === '/api/state') { res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify(state)); }
  else if (req.url.startsWith('/api/catalog')) { res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify([{ id: 'local-library', title: 'Audio local', kind: 'Audio' }])); }
  else if (req.url === '/api/command') {
    let body = ''; for await (const chunk of req) body += chunk;
    commands.push(JSON.parse(body));
    state.hasOutput = true; state.liveTitle = 'YouTube'; state.liveKind = 'WindowCapture'; state.scenes[4].active = true;
    res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify({ success: true, message: 'YouTube en pantalla' }));
  } else if (req.url.startsWith('/assets/')) {
    const name = req.url.endsWith('semibold.ttf') ? 'Lexend-SemiBold.ttf' : 'Lexend-Regular.ttf';
    const font = path.join(root, 'src/Culto.Desktop/Assets/Fonts', name);
    if (fs.existsSync(font)) { res.setHeader('Content-Type', 'font/ttf'); res.end(fs.readFileSync(font)); }
    else { res.statusCode = 404; res.end(); }
  } else { res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(html); }
});
(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const browser = await chromium.launch({ executablePath: process.env.CULTO_CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 1 });
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.goto(`http://127.0.0.1:${server.address().port}/#token=${'A'.repeat(64)}`);
    const youtube = page.locator('[data-scene-id="youtube"]');
    await youtube.waitFor(); await page.waitForFunction(() => document.getElementById('connection').textContent === 'Conectado');
    assert.equal(await page.locator('#scenes .scene-key').count(), 5);
    assert.equal(await page.locator('#scenes .preserve-action').count(), 4);
    assert.equal(await page.locator('#visual-title').textContent(), 'Imagen: Logo');
    assert.equal(await page.locator('#audio-title').textContent(), 'Audio: Música');
    assert.equal(await page.locator('#prepared-preserve').textContent(), 'Solo audio del preparado');
    await page.locator('#scenes .preserve-action[data-scene-id=logo]').click();
    await page.waitForFunction(() => document.getElementById('connection').textContent === 'Conectado' && !document.getElementById('return-media').disabled);
    assert.equal(commands[0].preserveOther, true); assert.equal(commands[0].buttonId, 'logo');
    await page.locator('#return-media').click();
    await page.waitForFunction(() => !document.getElementById('return-media').disabled);
    assert.equal(commands[1].action, 'return-media');
    await page.locator('#prepared-preserve').click();
    await page.waitForFunction(() => !document.getElementById('prepared-preserve').disabled);
    assert.equal(commands[2].action, 'project'); assert.equal(commands[2].preserveOther, true);
    await page.locator('#order .preserve-action').click();
    await page.waitForFunction(() => !document.querySelector('#order .preserve-action').disabled);
    assert.equal(commands[3].action, 'cue'); assert.equal(commands[3].cueId, 'local-video'); assert.equal(commands[3].preserveOther, true);
    assert.equal(await page.locator('#order .preserve-action').count(), 1);
    await page.locator('#catalog-source').selectOption('videos');
    await page.locator('#catalog-results .preserve-action').waitFor();
    await page.locator('#catalog-results .preserve-action').click();
    await page.waitForFunction(() => !document.querySelector('#catalog-results .preserve-action').disabled);
    assert.equal(commands[4].action, 'library'); assert.equal(commands[4].cueId, 'local-library'); assert.equal(commands[4].preserveOther, true);
    await page.locator('#scenes .preserve-action[data-scene-id=logo]').focus();
    await page.waitForTimeout(1700);
    assert.equal(await page.locator('#scenes .preserve-action[data-scene-id=logo]').evaluate(el => document.activeElement === el), true);
    state.canReturnToMedia = false; state.preparedKind = 'WindowCapture'; state.scenes[2].available = true;
    await page.waitForFunction(() => document.getElementById('return-media').disabled && document.getElementById('prepared-preserve').hidden);
    assert.equal(await page.locator('#scenes .preserve-action[data-scene-id=image]').isDisabled(), false);
    commands.length = 0;
    assert.equal(await page.locator('.scene-key[data-scene-id="image"]').isDisabled(), false);
    await youtube.click(); await page.waitForFunction(() => document.querySelector('[data-scene-id="youtube"]').getAttribute('aria-pressed') === 'true');
    assert.equal(commands.length, 1); assert.equal(commands[0].action, 'scene'); assert.equal(commands[0].buttonId, 'youtube');
    const directory = path.join(root, '.tools/deckboard'); fs.mkdirSync(directory, { recursive: true });
    for (const width of [320, 390, 540, 1024]) {
      await page.setViewportSize({ width, height: 900 });
      const geometry = await page.evaluate(() => ({ overflow: document.documentElement.scrollWidth > innerWidth,
        keys: [...document.querySelectorAll('#scenes .scene-key')].map(el => { const r = el.getBoundingClientRect(); return [r.width, r.height]; }) }));
      assert.equal(geometry.overflow, false, `Horizontal overflow at ${width}`);
      for (const [w, h] of geometry.keys) { assert.ok(w >= 70 && h >= 70); assert.ok(Math.abs(w - h) <= 2, `Non-square key at ${width}: ${w}x${h}`); }
      await page.screenshot({ path: path.join(directory, `mobile-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []);
    console.log('PASS real mobile browser: one-shot scene/prepared/order/catalog actions, return, separate status, polling focus, YouTube dispatch and responsive keys at 320/390/540/1024px');
  } finally { await browser.close(); server.close(); }
})().catch(error => { console.error(error); server.close(); process.exitCode = 1; });
