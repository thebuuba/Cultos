// Exercises the shipped browser script at its DOM/network boundary; visual checks run separately.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const html = fs.readFileSync(path.join(__dirname, '../../src/Culto.Remote/wwwroot/index.html'), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
function element(tag = 'div') {
  const classes = new Set(), attributes = {}, listeners = {};
  return {
    tagName: tag.toUpperCase(), children: [], value: 0, disabled: false, hidden: false, dataset: {}, style: { setProperty(name, value) { this[name] = value; } },
    get firstChild() { return this.children[0]; },
    get textContent() { return this._text || this.children.map(child => child.textContent).join(''); },
    set textContent(value) { this._text = String(value); this.children = []; },
    classList: { toggle(name, enabled) { enabled ? classes.add(name) : classes.delete(name); }, contains(name) { return classes.has(name); } },
    setAttribute(name, value) { attributes[name] = String(value); }, getAttribute(name) { return attributes[name] ?? null; },
    removeAttribute(name) { delete attributes[name]; },
    addEventListener(name, handler) { listeners[name] = handler; },
    async dispatch(name) { if (!this.disabled) return listeners[name]?.({ target: this, preventDefault() {} }); },
    replaceChildren(...children) { this.children = children; this._text = ''; },
    appendChild(child) { child.parentNode = this; this.children.push(child); return child; },
    append(...children) { children.forEach(child => this.appendChild(child)); },
    replaceWith(other) { const at = this.parentNode.children.indexOf(this); this.parentNode.children[at] = other; other.parentNode = this.parentNode; }
  };
}
const elements = new Map([...html.matchAll(/id="([^"]+)"/g)].map(match => [match[1], element()]));
const staticButtons = [...html.matchAll(/<button\b([^>]*)>/g)].map(match => {
  const attributes = match[1], id = attributes.match(/id="([^"]+)"/)?.[1];
  const button = id ? elements.get(id) : element('button');
  button.tagName = 'BUTTON'; button.disabled = attributes.includes('disabled');
  const action = attributes.match(/data-action="([^"]+)"/)?.[1];
  if (action) button.dataset.action = action;
  if (attributes.includes('data-preserve-other="true"')) button.dataset.preserveOther = 'true';
  return button;
});
const descendants = node => [node, ...node.children.flatMap(descendants)];
const actionButtons = () => [...staticButtons, ...[...elements.values()].flatMap(descendants)].filter((node, index, all) => node.dataset.action && all.indexOf(node) === index);
const storage = new Map(), calls = [], token = 'A'.repeat(64);
let state = {
  planName: 'Culto de prueba', liveTitle: 'Bienvenida', preparedTitle: 'Lectura',
  isBlackout: false, isPaused: true, volume: 70, position: 3, duration: 10,
  hasOutput: true, hasExternalOutput: false, isBackground: false,
  liveCueId: 'cue-a', preparedCueId: 'cue-b',
  order: [{ id: 'cue-a', title: 'Bienvenida', kind: 'Text' }, { id: 'cue-b', title: 'Lectura', kind: 'Bible' }],
  buttons: [{ id: 'deck-1', label: 'Salmo', color: '#A78BFA' }]
};
let commandHandler = async () => ({ success: false, message: 'Acción rechazada' });
let stateHandler = async () => structuredClone(state);
const context = vm.createContext({
  document: {
    hidden: false, activeElement: null, getElementById: id => elements.get(id),
    querySelectorAll: selector => selector.includes('#volume') ? [...actionButtons(), elements.get('volume'), elements.get('position')] : actionButtons(),
    createElement: element, createElementNS: (_, tag) => element(tag), addEventListener() {}
  },
  location: { hash: '#token=' + token, pathname: '/', search: '' },
  history: { replaceState() { context.location.hash = ''; } },
  sessionStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
  setTimeout: () => 1, clearTimeout() {}, AbortController, URLSearchParams, crypto: globalThis.crypto,
  fetch: async (url, options) => {
    calls.push({ url, options });
    const body = await (url === '/api/state' ? stateHandler() : commandHandler(JSON.parse(options.body)));
    return { ok: true, status: 200, json: async () => body };
  }
});
const run = source => vm.runInContext(source, context);
const settle = () => new Promise(setImmediate);
const cue = id => actionButtons().find(button => button.dataset.cueId === id);
const commands = () => calls.filter(call => call.url === '/api/command');
(async () => {
  run(script); await settle();
  assert.equal(context.location.hash, '');
  assert.equal(storage.get('culto.remote.token'), token);
  assert.equal(calls[0].options.headers.Authorization, 'Bearer ' + token);
  assert.equal(elements.get('connection').textContent, 'Conectado');
  assert.equal(elements.get('order').children.length, 2, 'The phone must render the real PC order');
  assert.match(cue('cue-a').textContent, /Bienvenida/);
  assert.equal(cue('cue-a').getAttribute('aria-current'), 'true');
  assert.match(cue('cue-b').textContent, /Preparado/);

  // A rejected tap preserves the confirmed live item and remains visible across polling.
  await cue('cue-b').dispatch('click');
  const sent = JSON.parse(commands()[0].options.body);
  assert.equal(sent.action, 'cue'); assert.equal(sent.cueId, 'cue-b');
  assert.match(sent.requestId, /^[0-9a-f]{32}$/);
  assert.equal(cue('cue-a').getAttribute('aria-current'), 'true');
  assert.equal(elements.get('live-title').textContent, 'Bienvenida');
  await run('refresh()');
  assert.equal(elements.get('notice').textContent, 'Acción rechazada');

  // Pending taps cannot produce duplicate commands or an optimistic live marker.
  let finishCommand;
  commandHandler = () => new Promise(resolve => { finishCommand = resolve; });
  const pendingTap = cue('cue-b').dispatch('click'); await settle();
  await run("send('cue', { cueId: 'cue-b' })");
  assert.equal(commands().length, 2);
  assert.equal(cue('cue-b').disabled, true);
  assert.equal(cue('cue-a').getAttribute('aria-current'), 'true');
  state.liveCueId = 'cue-b'; state.liveTitle = 'Lectura';
  finishCommand({ success: true, message: 'Proyección confirmada' }); await pendingTap;
  assert.equal(cue('cue-b').getAttribute('aria-current'), 'true');
  assert.equal(cue('cue-a').getAttribute('aria-current'), null);
  assert.equal(elements.get('live-title').textContent, 'Lectura');

  // PC edits, reorder/removal, identical titles and literal HTML never change ID routing.
  state.order = [{ id: 'cue-b', title: '<img src=x onerror=alert(1)>', kind: 'Bible' }, { id: 'cue-c', title: 'Lectura', kind: 'Video' }];
  state.preparedCueId = 'cue-c'; await run('refresh()');
  assert.equal(elements.get('order').children.length, 2);
  assert.equal(cue('cue-a'), undefined);
  assert.match(cue('cue-b').textContent, /<img src=x onerror=alert\(1\)>/);
  assert.equal(descendants(cue('cue-b')).some(node => node.tagName === 'IMG'), false);
  commandHandler = async () => ({ success: false, message: 'Elemento eliminado en la consola' });
  await cue('cue-c').dispatch('click');
  assert.equal(JSON.parse(commands().at(-1).options.body).cueId, 'cue-c');

  state.hasExternalOutput = true; await run('refresh()');
  assert.equal(cue('cue-b').getAttribute('aria-current'), 'true', 'HDMI is a destination, not different cue ownership');
  assert.match(elements.get('output-target').textContent, /HDMI/);
  state.hasExternalOutput = false; await run('refresh()');
  assert.equal(cue('cue-b').getAttribute('aria-current'), 'true');
  assert.match(elements.get('output-target').textContent, /Local/);
  state.isBlackout = true; await run('refresh()');
  assert.match(elements.get('blackout-state').textContent, /NEGRO/);
  assert.equal(elements.get('blackout').getAttribute('aria-pressed'), 'true');
  assert.equal(cue('cue-b').getAttribute('aria-current'), 'true');
  assert.match(cue('cue-b').textContent, /En negro/);
  state.isBlackout = false; state.isBackground = true; await run('refresh()');
  assert.equal(elements.get('background').getAttribute('aria-pressed'), 'true');
  assert.equal(cue('cue-b').getAttribute('aria-current'), 'true');
  assert.match(cue('cue-b').textContent, /Logo/);
  state.isBackground = false; state.hasOutput = false; await run('refresh()');
  assert.match(elements.get('blackout-state').textContent, /Sin salida/);
  assert.notEqual(cue('cue-b').getAttribute('aria-current'), 'true');

  // Commands serialize after any existing poll, then read a fresh post-command snapshot.
  state.hasOutput = true;
  let finishPoll;
  const stale = structuredClone(state);
  stateHandler = () => new Promise(resolve => { finishPoll = resolve; });
  const oldPoll = run('refresh()'); await settle();
  const commandCount = commands().length;
  commandHandler = async () => { state.liveCueId = 'cue-c'; state.liveTitle = 'Vídeo'; return { success: true }; };
  const nextTap = run("send('cue', { cueId: 'cue-c' })"); await settle();
  assert.equal(commands().length, commandCount, 'Wait for the pre-command poll before sending');
  stateHandler = async () => structuredClone(state); finishPoll(stale);
  await oldPoll; await nextTap;
  assert.equal(cue('cue-c').getAttribute('aria-current'), 'true');
  assert.equal(elements.get('live-title').textContent, 'Vídeo');

  // Acknowledgment without a readable new snapshot cannot claim updated projection.
  commandHandler = async () => ({ success: true });
  stateHandler = async () => { throw Error('snapshot unavailable'); };
  await cue('cue-b').dispatch('click');
  assert.equal(elements.get('connection').textContent, 'Sin conexión');
  assert.equal(elements.get('live-title').textContent, 'Vídeo');
  assert.equal(cue('cue-c').getAttribute('aria-current'), 'true');
  assert.match(elements.get('notice').textContent, /No se pudo actualizar/);
  stateHandler = async () => structuredClone(state); await run('refresh()');

  // Lost command response is not retried, and reconnect never replays it.
  commandHandler = async () => { throw Error('sin red'); };
  const beforeFailure = commands().length;
  await cue('cue-b').dispatch('click');
  assert.equal(commands().length, beforeFailure + 1);
  assert.equal(elements.get('connection').textContent, 'Sin conexión');
  assert.equal(cue('cue-b').disabled, true);
  assert.match(elements.get('notice').textContent, /No se confirmó/);
  await run("send('next')"); assert.equal(commands().length, beforeFailure + 1);
  await run('refresh()'); assert.equal(commands().length, beforeFailure + 1);
  assert.equal(cue('cue-b').disabled, false);
  assert.match(elements.get('notice').textContent, /No se confirmó/);

  // Existing physical-console controls keep their actual action/value contracts.
  commandHandler = async () => ({ success: false });
  assert.equal(staticButtons.some(button => ['previous', 'next'].includes(button.dataset.action)), false);
  for (const action of ['blackout', 'background', 'project', 'pause', 'restart']) {
    await staticButtons.find(button => button.dataset.action === action).dispatch('click');
    assert.equal(JSON.parse(commands().at(-1).options.body).action, action);
  }
  elements.get('volume').value = 32; await elements.get('volume').dispatch('change');
  await run('sendVolume()'); // This harness stubs timers; flush the queued volume explicitly.
  assert.equal(JSON.parse(commands().at(-1).options.body).value, 32);
  assert.equal(JSON.parse(commands().at(-1).options.body).action, 'volume');
  elements.get('position').value = 250; await elements.get('position').dispatch('change');
  assert.equal(JSON.parse(commands().at(-1).options.body).value, 0.25);
  assert.equal(JSON.parse(commands().at(-1).options.body).action, 'seek');
  state.duration = 0; state.liveCueId = 'outside-plan'; await run('refresh()');
  assert.equal(elements.get('position').disabled, true);
  assert.equal(actionButtons().some(button => button.getAttribute('aria-current') === 'true'), false);

  state.order = []; await run('refresh()');
  assert.equal(elements.get('order').children.length, 0); assert.equal(elements.get('order-empty').hidden, false);
  state.order = null; await run('refresh()');
  assert.equal(elements.get('order').children.length, 0);
  assert.equal(elements.get('deck').children.length, 1, 'Legacy/custom deck remains usable without an order');
  commandHandler = async () => ({ success: false });
  await elements.get('deck').children[0].dispatch('click');
  assert.equal(JSON.parse(commands().at(-1).options.body).buttonId, 'deck-1');

  const customButtons = state.buttons;
  state.buttons = ['project', 'next', 'previous'].map(action => ({ id: action, action, label: action }));
  await run('refresh()');
  assert.equal(elements.get('deck').children.length, 0, 'Removed shortcuts stay hidden for saved decks');
  assert.equal(elements.get('deck-empty').hidden, false);
  state.buttons = customButtons;
  await run('refresh()');

  state.scenes = [
    { id: 'blackout', label: 'Fondo negro', kind: 'blackout', available: true, active: false },
    { id: 'youtube', label: 'YouTube', kind: 'youtube', available: true, active: false, detail: '<img onerror=alert(1)>' },
    { id: 'image', label: 'Imagen', kind: 'image', available: false, active: false }
  ];
  await run('refresh()');
  const scene = id => descendants(elements.get('scenes')).find(button => button.dataset.sceneId === id && button.dataset.preserveOther !== 'true');
  assert.equal(elements.get('scenes-section').hidden, false);
  assert.equal(elements.get('scenes').children.length, 3);
  assert.equal(scene('image').disabled, true);
  assert.equal(scene('youtube').disabled, false);
  commandHandler = async command => {
    assert.equal(command.action, 'scene'); assert.equal(command.buttonId, 'youtube');
    state.scenes[1].active = true; return { success: true, message: 'YouTube en pantalla' };
  };
  await scene('youtube').dispatch('click');
  assert.equal(scene('youtube').getAttribute('aria-pressed'), 'true');
  assert.match(scene('youtube').textContent, /En pantalla/);
  const sameButton = scene('youtube');
  state.scenes[1].available = false; state.scenes[1].active = false; await run('refresh()');
  assert.equal(scene('youtube'), sameButton, 'Polling availability must preserve keyboard focus');
  assert.equal(scene('youtube').disabled, true);
  const beforeUnavailable = commands().length; await scene('youtube').dispatch('click');
  assert.equal(commands().length, beforeUnavailable);
  const preserveScene = descendants(elements.get('scenes')).find(button => button.dataset.sceneId === 'blackout' && button.dataset.preserveOther === 'true');
  commandHandler = async command => { assert.equal(command.preserveOther, true); assert.equal(command.buttonId, 'blackout'); return { success: true }; };
  await preserveScene.dispatch('click');
  const stablePreserve = preserveScene;
  state.visualTitle = 'Logo'; state.audioTitle = 'Pista'; state.canReturnToMedia = true; state.preparedKind = 'Video'; await run('refresh()');
  assert.equal(descendants(elements.get('scenes')).find(button => button === stablePreserve), stablePreserve);
  assert.equal(elements.get('visual-title').textContent, 'Imagen: Logo');
  assert.equal(elements.get('audio-title').textContent, 'Audio: Pista');
  assert.equal(elements.get('return-media').disabled, false);
  commandHandler = async command => { assert.equal(command.preserveOther, true); assert.equal(command.action, 'project'); return { success: true }; };
  await elements.get('prepared-preserve').dispatch('click');
  commandHandler = async command => { assert.equal(command.action, 'return-media'); assert.equal(command.preserveOther, undefined); return { success: true }; };
  await elements.get('return-media').dispatch('click');
  state.canReturnToMedia = false; state.preparedKind = 'WindowCapture'; await run('refresh()');
  assert.equal(elements.get('return-media').disabled, true);
  assert.equal(elements.get('prepared-preserve').hidden, true);
  state.scenes = null; await run('refresh()');
  assert.equal(elements.get('scenes-section').hidden, true);

  context.fetch = async () => ({ status: 401, ok: false });
  await run('refresh()');
  assert.equal(storage.has('culto.remote.token'), false);
  assert.equal(elements.get('connection').textContent, 'Sin conexión');
  console.log('Mobile smoke PASS: real order, stable IDs, safe text, live/output states, PC edits, failed commands, duplicate prevention, poll serialization, reconnect, legacy deck and auth.');
})().catch(error => { console.error(error); process.exitCode = 1; });
