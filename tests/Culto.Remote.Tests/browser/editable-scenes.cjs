const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.CULTO_PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname, '../../..');
const png = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=';
let state = { planName: 'Escenas propias', volume: 80, hasOutput: false, buttons: [], order: [], scenes: [
  { id: 'own-1', label: 'Bienvenida', kind: 'image', available: true, active: false, imageData: png, color: '#354E6D' },
  { id: 'black', label: 'Negro', kind: 'blackout', available: true, active: false, color: '#08090C' }
] };
const commands = [];
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await page.route('http://culto.test/**', async route => {
      const url = new URL(route.request().url());
      if (url.pathname === '/') return route.fulfill({ contentType: 'text/html', body: fs.readFileSync(path.join(root, 'src/Culto.Remote/wwwroot/index.html')) });
      if (url.pathname === '/api/state') return route.fulfill({ json: state });
      if (url.pathname === '/api/command') { commands.push(route.request().postDataJSON()); return route.fulfill({ json: { success: true, message: 'Confirmado' } }); }
      if (url.pathname.startsWith('/assets/')) return route.fulfill({ contentType: 'font/ttf', body: fs.readFileSync(path.join(root, 'src/Culto.Desktop/Assets/Fonts', url.pathname.includes('semibold') ? 'Lexend-SemiBold.ttf' : 'Lexend-Regular.ttf')) });
      return route.fulfill({ status: 404 });
    });
    await page.goto('http://culto.test/#' + 'A'.repeat(64));
    const tile = page.locator('.scene-key[data-scene-id="own-1"]');
    await tile.waitFor();
    assert.equal(await tile.locator('img').getAttribute('src'), png);
    await page.waitForFunction(() => document.querySelector('.scene-artwork').naturalWidth > 0);
    assert.equal(await tile.evaluate(el => getComputedStyle(el).color), 'rgb(255, 255, 255)');
    await tile.click();
    await page.waitForFunction(() => !document.querySelector('.scene-key').disabled);
    assert.equal(commands.length, 1); assert.equal(commands[0].buttonId, 'own-1'); assert.equal(commands[0].action, 'scene');
    state.scenes.reverse(); state.scenes[1].label = 'Nombre editado'; state.scenes[1].color = '#F5FBF3';
    await page.waitForFunction(() => document.querySelector('#scenes .scene-key')?.dataset.sceneId === 'black');
    assert.match(await tile.textContent(), /Nombre editado/);
    assert.equal(await tile.evaluate(el => getComputedStyle(el).color), 'rgb(24, 35, 47)');
    const folder = path.join(root, '.tools/deckboard'); fs.mkdirSync(folder, { recursive: true });
    for (const width of [320, 390, 1024]) {
      await page.setViewportSize({ width, height: 900 });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, `Overflow at ${width}`);
      await page.screenshot({ path: path.join(folder, `editable-scenes-${width}.png`), fullPage: true });
    }
    state.scenes = [state.scenes[0]];
    await tile.waitFor({ state: 'detached' });
    assert.equal(await page.locator('.scene-key').count(), 1);
    state.scenes = [];
    await page.locator('#scenes-section').waitFor({ state: 'hidden' });
    console.log('PASS editable scenes browser: portable artwork, contrast, stable dispatch, edits, order, deletion, empty deck, responsive 320/390/1024');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
