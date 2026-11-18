// Real Chromium/WebKit lifecycle and pairing tests against an owned local server.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium, webkit } = require(process.env.CULTO_PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname, '../../..');
const html = fs.readFileSync(path.join(root, 'src/Culto.Remote/wwwroot/index.html'));
const token = 'A'.repeat(64);
const state = { planName: 'Prueba', liveTitle: 'Bienvenida', preparedTitle: 'Texto', hasOutput: true,
  volume: 70, position: 0, duration: 0, buttons: [], order: [], scenes: [] };
let stateReads = 0, commands = 0, stallState = false, stallCommand = false, stopped = false;
const server = http.createServer(async (req, res) => {
  if (req.url === '/') { res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(html); return; }
  if (req.url === '/away') { res.end('<title>Otra pagina</title>'); return; }
  if (req.url.startsWith('/assets/')) { res.statusCode = 404; res.end(); return; }
  assert.equal(req.headers.authorization, undefined);
  if (stopped) { res.writeHead(503); res.end(); return; }
  res.setHeader('Content-Type', 'application/json');
  if (req.url === '/api/state') {
    stateReads++;
    if (!stallState) res.end(JSON.stringify(state));
  } else if (req.url === '/api/command') {
    for await (const chunk of req) { /* Read the received command without replaying it. */ }
    commands++;
    if (!stallCommand) res.end(JSON.stringify({ success: true }));
  } else { res.writeHead(404); res.end(); }
});
async function until(predicate, message) {
  const deadline = Date.now() + 6000;
  while (!await predicate()) {
    assert.ok(Date.now() < deadline, message);
    await new Promise(resolve => setTimeout(resolve, 25));
  }
}
async function hidden(page, value) {
  await page.evaluate(value => {
    Object.defineProperty(document, 'hidden', { configurable: true, value });
    document.dispatchEvent(new Event('visibilitychange'));
  }, value);
}
async function connected(page) {
  await page.waitForFunction(() => document.getElementById('connection').textContent === 'Conectado'
    && !document.getElementById('blackout').disabled);
}
(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = 'http://127.0.0.1:' + server.address().port;
  try {
    for (const [name, engine, options] of [
      ['Android / Chromium', chromium, { isMobile: true, hasTouch: true, userAgent: 'Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/130.0.0.0 Mobile Safari/537.36' }],
      ['iPhone / WebKit', webkit, { isMobile: true, hasTouch: true, userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile/15E148 Safari/604.1' }]
    ].filter(([name]) => !process.env.CULTO_TEST_CHROMIUM_ONLY || name.includes('Chromium'))) {
      stopped = stallState = stallCommand = false;
      const browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: 'chrome' } : {}) });
      const context = await browser.newContext({ ...options, viewport: { width: 390, height: 844 } });
      try {
        let page = await context.newPage();
        const errors = [];
        context.on('page', page => page.on('pageerror', error => errors.push(error.message)));
        page.on('pageerror', error => errors.push(error.message));
        await page.goto(base); await connected(page);
        assert.equal(new URL(page.url()).hash, '');
        const client = await page.evaluate(() => localStorage.getItem('culto.remote.client'));
        await page.close();
        page = await context.newPage(); await page.goto(base); await connected(page);
        assert.equal(await page.evaluate(() => localStorage.getItem('culto.remote.client')), client);
        console.log('PASS ' + name + ': IP access survives a closed tab');

        stallState = true;
        await page.evaluate(() => { void refresh(); });
        await page.waitForFunction(() => polling !== null);
        const beforeResume = commands;
        await hidden(page, true); stallState = false;
        await hidden(page, false); await connected(page);
        assert.equal(commands, beforeResume);
        console.log('PASS ' + name + ': suspended read recovers without replay');

        stallCommand = true;
        const beforeCommand = commands;
        await page.locator('#blackout').click();
        await until(() => commands === beforeCommand + 1, 'Command did not reach server');
        await hidden(page, true); await hidden(page, false); await connected(page);
        assert.equal(commands, beforeCommand + 1);
        assert.match(await page.locator('#notice').textContent(), /No se confirm/);
        stallCommand = false;
        console.log('PASS ' + name + ': suspended command releases controls and is not repeated');

        await context.setOffline(true);
        await page.evaluate(() => dispatchEvent(new Event('offline')));
        await page.waitForFunction(() => document.getElementById('connection').textContent === 'Sin conexión');
        const beforeOnline = stateReads;
        await context.setOffline(false);
        await page.evaluate(() => dispatchEvent(new Event('online')));
        await connected(page);
        assert.ok(stateReads > beforeOnline);
        assert.equal(commands, beforeCommand + 1);
        await page.goto(base + '/away'); await page.goBack(); await connected(page);
        assert.equal(await page.evaluate(() => localStorage.getItem('culto.remote.token')), null);
        console.log('PASS ' + name + ': network recovery and browser back restore IP access');

        stopped = true;
        await page.evaluate(() => { void refresh(); });
        await page.waitForFunction(() => document.getElementById('connection').textContent === 'Sin conexión');
        assert.equal(await page.locator('#blackout').isEnabled(), false);
        stopped = false;
        await connected(page);
        assert.equal(await page.locator('#blackout').isEnabled(), true);
        assert.equal(commands, beforeCommand + 1, 'Reactivation never replays an old command');
        // WebKit reports a network-policy pageerror for fetches interrupted by our deliberate offline switch.
        assert.deepEqual(errors.filter(message => !message.endsWith('/api/state due to access control checks.')), []);
        console.log('PASS ' + name + ': console reactivation reconnects automatically by IP');
      } finally { await context.close(); await browser.close(); server.closeAllConnections(); }
    }
  } finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
