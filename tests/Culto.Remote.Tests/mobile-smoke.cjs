// Runs the embedded page's actual script against a minimal DOM/fetch boundary.
// Browser layout and physical phone verification are separate manual checks.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const html = fs.readFileSync(path.join(__dirname, '../../src/Culto.Remote/wwwroot/index.html'), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const elements = new Map();
function element() {
  return {
    textContent: '', value: 0, disabled: true, dataset: {}, style: {},
    classList: { toggle() {} }, setAttribute() {}, addEventListener() {}, replaceChildren() {}, appendChild() {}
  };
}
for (const id of ['connection', 'notice', 'plan', 'live-title', 'prepared-title', 'blackout-state',
  'blackout', 'pause', 'play-state', 'time', 'volume-value', 'volume', 'position', 'deck', 'deck-empty', 'retry'])
  elements.set(id, element());
const buttons = Array.from({ length: 7 }, element);
const storage = new Map(), calls = [], token = 'A'.repeat(64);
const state = {
  planName: 'Culto de prueba', liveTitle: 'Bienvenida', preparedTitle: 'Lectura',
  isBlackout: false, isPaused: true, volume: 70, position: 3, duration: 10, buttons: []
};
const context = vm.createContext({
  document: {
    hidden: false, activeElement: null, getElementById: id => elements.get(id),
    querySelectorAll: () => [...buttons, elements.get('volume'), elements.get('position')],
    createElement: element, addEventListener() {}
  },
  location: { hash: '#token=' + token, pathname: '/', search: '' },
  history: { replaceState() { context.location.hash = ''; } },
  sessionStorage: {
    getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value),
    removeItem: key => storage.delete(key)
  },
  setTimeout: () => 1, clearTimeout() {}, AbortController, URLSearchParams, crypto: globalThis.crypto,
  fetch: async (url, options) => {
    calls.push({ url, options });
    return { ok: true, status: 200, json: async () => url === '/api/state' ? state : { success: false, message: 'Acción rechazada' } };
  }
});
(async () => {
  vm.runInContext(script, context);
  await new Promise(setImmediate);
  assert.equal(context.location.hash, '');
  assert.equal(storage.get('culto.remote.token'), token);
  assert.equal(calls[0].url, '/api/state');
  assert.equal(calls[0].options.headers.Authorization, 'Bearer ' + token);
  assert.equal(elements.get('live-title').textContent, 'Bienvenida');
  assert.equal(elements.get('connection').textContent, 'Conectado');

  await vm.runInContext("send('project')", context);
  assert.equal(calls.length, 2);
  assert.equal(elements.get('live-title').textContent, 'Bienvenida');
  assert.equal(elements.get('notice').textContent, 'Acción rechazada');
  const sent = JSON.parse(calls[1].options.body);
  assert.equal(sent.action, 'project');
  assert.match(sent.requestId, /^[0-9a-f]{32}$/);

  context.fetch = async () => { throw Error('sin red'); };
  await vm.runInContext('refresh()', context);
  assert.equal(elements.get('connection').textContent, 'Sin conexión');
  assert.equal(buttons[0].disabled, true);
  await vm.runInContext("send('next')", context);
  assert.equal(calls.length, 2);
  console.log('Mobile smoke PASS: fragment/sessionStorage, Bearer, failed command preserves state, disconnect disables commands.');
})().catch(error => { console.error(error); process.exitCode = 1; });
