// Run with CULTO_PLAYWRIGHT_MODULE pointing to an installed Playwright package.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.CULTO_PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname, '../../..');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(condition) {
  const end = Date.now() + 5000;
  while (!condition()) { assert.ok(Date.now() < end, 'Timed out waiting for volume delivery'); await pause(15); }
}
async function remote(browser, delay = 20) {
  const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
  const state = { volume: 80, hasOutput: true, canPause: true, duration: 180, position: 0, buttons: [], order: [], scenes: [] };
  const host = { calls: [], inFlight: 0, peak: 0, staleRead: false, stateStarted: false, reject: false, state };
  await page.route('http://culto.test/**', async route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/') return route.fulfill({ contentType: 'text/html', body: fs.readFileSync(path.join(root, 'src/Culto.Remote/wwwroot/index.html')) });
    if (url.pathname.startsWith('/assets/lexend-')) return route.fulfill({ contentType: 'font/ttf', body: fs.readFileSync(path.join(root, 'src/Culto.Desktop/Assets/Fonts', url.pathname.includes('semibold') ? 'Lexend-SemiBold.ttf' : 'Lexend-Regular.ttf')) });
    if (url.pathname === '/api/state') {
      const snapshot = JSON.stringify(state);
      if (host.staleRead) { host.stateStarted = true; await pause(500); }
      return route.fulfill({ contentType: 'application/json', body: snapshot });
    }
    const command = route.request().postDataJSON();
    assert.equal(command.action, 'volume');
    host.calls.push(command.value); host.peak = Math.max(host.peak, ++host.inFlight);
    await pause(delay);
    if (!host.reject) state.volume = command.value;
    host.inFlight--;
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: !host.reject, message: host.reject ? 'Volumen no aplicado' : 'Listo' }) });
  });
  await page.goto('http://culto.test/#token=' + 'a'.repeat(50));
  await page.waitForFunction(() => !document.getElementById('volume').disabled);
  host.input = value => page.$eval('#volume', (slider, value) => {
    slider.focus(); slider.value = value; slider.dispatchEvent(new Event('input', { bubbles: true }));
  }, value);
  host.release = () => page.$eval('#volume', slider => slider.dispatchEvent(new Event('change', { bubbles: true })));
  host.page = page;
  return host;
}
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    {
      const host = await remote(browser);
      try {
        for (const value of [75, 70, 65, 60, 55, 50, 45, 40, 35, 30]) {
          await host.input(value); await pause(35);
          assert.equal(await host.page.locator('#volume').isEnabled(), true, 'Volume slider locks while dragging');
        }
        assert.ok(host.calls.length >= 3, 'Volume is not sent until the slider is released');
        assert.equal(await host.page.locator('#volume-value').textContent(), '30 %');
        await host.release(); await until(() => host.state.volume === 30);
        assert.equal(host.peak, 1, 'Volume requests overlap');
        assert.ok(host.calls.every((value, i) => !i || value <= host.calls[i - 1]), 'Volume updates arrive out of order');
        console.log('PASS Volume follows input before release without locking the slider');
      } finally { await host.page.close(); }
    }
    {
      const host = await remote(browser, 220);
      try {
        await host.input(70); await until(() => host.calls.length === 1);
        for (const value of [65, 50, 20, 40, 55]) { await host.input(value); await pause(10); }
        await host.release(); await until(() => host.state.volume === 55);
        assert.deepEqual(host.calls, [70, 55], 'Slow connection queued obsolete values or lost the final position');
        assert.equal(host.peak, 1);
        console.log('PASS Slow connections keep only the newest pending volume, including direction changes');
      } finally { await host.page.close(); }
    }
    {
      const host = await remote(browser, 100);
      try {
        host.staleRead = true;
        const snapshot = host.page.evaluate(() => refresh(true));
        await until(() => host.stateStarted);
        await host.input(35); await host.release(); await until(() => host.state.volume === 35);
        await snapshot;
        assert.equal(await host.page.locator('#volume').inputValue(), '35', 'Older state read moved the slider back');
        assert.equal(await host.page.locator('#volume-value').textContent(), '35 %', 'Older state read replaced the live percentage');
        console.log('PASS Stale polling cannot overwrite the new volume');
      } finally { await host.page.close(); }
    }
    {
      const host = await remote(browser);
      try {
        host.reject = true; await host.input(25);
        await host.page.waitForSelector('#notice.error');
        assert.match(await host.page.locator('#notice').textContent(), /Volumen no aplicado/);
        assert.equal(host.state.volume, 80);
        assert.equal(await host.page.locator('#volume').isEnabled(), true, 'A rejected command should not break a healthy connection');
        console.log('PASS Rejected volume shows an error instead of claiming delivery');
      } finally { await host.page.close(); }
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
