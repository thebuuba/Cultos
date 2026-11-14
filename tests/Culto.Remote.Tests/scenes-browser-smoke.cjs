// Real browser test of the shipped mobile page against an owned loopback fixture.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.CULTO_PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname, '../..');
const html = fs.readFileSync(path.join(root, 'src/Culto.Remote/wwwroot/index.html'));
const state = {
  planName: 'Culto del sábado', preparedTitle: 'Bienvenida', liveTitle: 'Sin salida', hasOutput: false,
  isBlackout: false, isPaused: false, volume: 70, position: 0, duration: 0, canPause: false,
  buttons: [{ id: 'next', label: 'Siguiente', color: '#4274F4', action: 'next' }], order: [],
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
    assert.equal(await page.locator('#scenes button').count(), 5);
    assert.equal(await page.locator('[data-scene-id="image"]').isDisabled(), true);
    await youtube.click(); await page.waitForFunction(() => document.querySelector('[data-scene-id="youtube"]').getAttribute('aria-pressed') === 'true');
    assert.equal(commands.length, 1); assert.equal(commands[0].action, 'scene'); assert.equal(commands[0].buttonId, 'youtube');
    const directory = path.join(root, '.tools/deckboard'); fs.mkdirSync(directory, { recursive: true });
    for (const width of [320, 390, 540, 1024]) {
      await page.setViewportSize({ width, height: 900 });
      const geometry = await page.evaluate(() => ({ overflow: document.documentElement.scrollWidth > innerWidth,
        keys: [...document.querySelectorAll('#scenes button')].map(el => { const r = el.getBoundingClientRect(); return [r.width, r.height]; }) }));
      assert.equal(geometry.overflow, false, `Horizontal overflow at ${width}`);
      for (const [w, h] of geometry.keys) { assert.ok(w >= 70 && h >= 70); assert.ok(Math.abs(w - h) <= 2, `Non-square key at ${width}: ${w}x${h}`); }
      await page.screenshot({ path: path.join(directory, `mobile-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []);
    console.log('PASS real mobile browser: YouTube dispatch, unavailable keys, live marker and square keys without overflow at 320/390/540/1024px');
  } finally { await browser.close(); server.close(); }
})().catch(error => { console.error(error); server.close(); process.exitCode = 1; });
