const assert = require('node:assert/strict');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { createRequire } = require('node:module');
const root = path.resolve(__dirname, '../..');
const { WebSocketServer } = createRequire(path.join(root, 'integrations/streamdeck/package.json'))('ws');
const { chromium } = require(process.env.CULTO_PLAYWRIGHT_MODULE || 'playwright');
(async () => {
  const server = new WebSocketServer({ host: '127.0.0.1', port: 0 });
  const browser = await chromium.launch({ headless: true });
  let socket;
  const sent = [];
  try {
    if (!server.address()) await new Promise(resolve => server.once('listening', resolve));
    server.on('connection', client => { socket = client; client.on('message', bytes => sent.push(JSON.parse(bytes.toString()))); });
    const page = await browser.newPage({ viewport: { width: 320, height: 560 } });
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.goto(pathToFileURL(path.join(root, 'integrations/streamdeck/com.cultostudio.control.sdPlugin/ui/inspector.html')).href);
    await page.evaluate(port => connectElgatoStreamDeckSocket(String(port), 'owned-inspector', 'registerPropertyInspector', '{}',
      JSON.stringify({ action: 'com.cultostudio.control.scene', payload: { settings: { sceneId: 'blackout' } } })), server.address().port);
    await page.waitForTimeout(100);
    const snapshot = { preparedTitle: 'Bienvenida', liveTitle: '<img src=x onerror=alert(1)>', volume: 70,
      scenes: [{ id: 'blackout', label: 'Negro', available: true }, { id: 'image', label: 'Imagen', available: false }] };
    function state() { socket.send(JSON.stringify({ event: 'sendToPropertyInspector', payload: { event: 'state', connected: true, state: snapshot, error: null } })); }
    state();
    await page.waitForFunction(() => document.querySelector('#choice').options.length === 3);
    assert.equal(await page.locator('#choice').inputValue(), 'blackout');
    assert.match(await page.locator('#live').textContent(), /<img src=x/);
    assert.equal(await page.locator('#live img').count(), 0);
    await page.evaluate(() => { window.firstOption = document.querySelector('#choice').options[1]; });
    state();
    await page.waitForTimeout(100);
    assert.equal(await page.evaluate(() => firstOption === document.querySelector('#choice').options[1]), true, 'Polling rebuilt the active selection control');
    await page.locator('#choice').selectOption('image');
    await page.waitForTimeout(100);
    assert.ok(sent.some(message => message.event === 'setSettings' && message.payload.sceneId === 'image'));
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    assert.deepEqual(errors, []);
    await page.screenshot({ path: path.join(root, '.tools/stream-deck-inspector.png'), fullPage: true });
    console.log('PASS native inspector lists PC scenes, saves stable selection, preserves dropdown during polling and renders safe text');
  } finally {
    await browser.close();
    for (const client of server.clients) client.terminate();
    await new Promise(resolve => server.close(resolve));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
