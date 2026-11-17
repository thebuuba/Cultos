// Exercises the actual bundled official-SDK plugin via the Stream Deck WebSocket protocol.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const os = require('node:os');
const http = require('node:http');
const { spawn } = require('node:child_process');
const { createRequire } = require('node:module');
const root = path.resolve(__dirname, '../..');
const requirePlugin = createRequire(path.join(root, 'integrations/streamdeck/package.json'));
const { WebSocketServer } = requirePlugin('ws');
const pluginDirectory = path.join(root, 'integrations/streamdeck/com.cultostudio.control.sdPlugin');
const prefix = 'com.cultostudio.control.';
const cueA = '11111111-1111-1111-1111-111111111111';
const cueB = '22222222-2222-2222-2222-222222222222';
const snapshot = {
  planName: 'Culto de prueba', preparedTitle: 'Texto preparado', preparedCueId: cueA,
  liveTitle: 'Bienvenida', liveCueId: cueA, hasOutput: true, hasExternalOutput: false,
  isBlackout: false, isPaused: false, isBackground: false, volume: 70, position: 0, duration: 100,
  canPause: true, canReturnToMedia: true, liveKind: 'Video', preparedKind: 'Text', buttons: [],
  order: [{ id: cueA, title: 'Bienvenida', kind: 'Text' }, { id: cueB, title: 'Lectura', kind: 'Bible' }],
  scenes: [{ id: 'blackout', label: 'Negro', kind: 'blackout', available: true, active: false },
    { id: 'image', label: 'Imagen', kind: 'image', available: false, active: false }]
};
const commands = [], messages = [], errors = [];
const instances = new Map();
let token = 'A'.repeat(64), dropReply = false, socket, child;
async function until(predicate, failure, timeout = 10000) {
  const deadline = Date.now() + timeout;
  while (!predicate()) {
    assert.ok(Date.now() < deadline, failure + (errors.length ? '\nPlugin: ' + errors.join('').slice(-1500) : ''));
    await new Promise(resolve => setTimeout(resolve, 25));
  }
}
const server = http.createServer(async (request, response) => {
  if (request.headers.authorization !== 'Bearer ' + token) { response.writeHead(401).end(); return; }
  response.setHeader('Content-Type', 'application/json');
  if (request.url === '/api/state') { response.end(JSON.stringify(snapshot)); return; }
  assert.equal(request.url, '/api/command'); assert.equal(request.method, 'POST');
  let body = ''; for await (const chunk of request) body += chunk;
  const command = JSON.parse(body); commands.push(command);
  assert.match(command.requestId, /^[0-9a-f-]{36}$/i);
  if (command.action === 'blackout') snapshot.isBlackout = !snapshot.isBlackout;
  if (command.action === 'volume') snapshot.volume = command.value;
  if (command.action === 'volume-step') snapshot.volume = Math.max(0, Math.min(100, snapshot.volume + command.value));
  if (command.action === 'cue') snapshot.liveCueId = command.cueId;
  if (dropReply) { dropReply = false; request.socket.destroy(); return; }
  response.end(JSON.stringify({ success: true, message: 'Acción aplicada.' }));
});
const websocket = new WebSocketServer({ host: '127.0.0.1', port: 0 });
function event(context, type, extra = {}) {
  const instance = instances.get(context);
  socket.send(JSON.stringify({ event: type, context, device: 'owned-device', action: prefix + instance.kind,
    payload: { controller: 'Keypad', coordinates: { column: 0, row: 0 }, settings: instance.settings,
      state: 0, isInMultiAction: false, resources: {}, ...extra } }));
}
function appear(context, kind, settings = {}) { instances.set(context, { kind, settings }); event(context, 'willAppear'); }
function inspectorState(connected) {
  return messages.some(message => message.event === 'sendToPropertyInspector'
    && message.payload?.event === 'state' && message.payload.connected === connected);
}
(async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'CultoStreamDeckPlugin-'));
  const descriptor = path.join(directory, 'streamdeck-connection.json');
  try {
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    if (!websocket.address()) await new Promise(resolve => websocket.once('listening', resolve));
    const address = 'http://127.0.0.1:' + server.address().port;
    await fs.writeFile(descriptor, JSON.stringify({ schemaVersion: 1, address, token, processId: process.pid }));
    websocket.on('connection', connected => {
      socket = connected;
      connected.on('message', bytes => {
        const message = JSON.parse(bytes.toString()); messages.push(message);
        if (message.event === 'getGlobalSettings') connected.send(JSON.stringify({ event: 'didReceiveGlobalSettings', payload: { settings: {} } }));
        if (message.event === 'getSettings' && instances.has(message.context)) event(message.context, 'didReceiveSettings');
      });
    });
    const info = { application: { font: 'Arial', language: 'es', platform: 'windows', platformVersion: '10', version: '7.1.0' },
      plugin: { uuid: 'com.cultostudio.control', version: '1.0.0.0' }, devicePixelRatio: 1,
      colors: { buttonMouseOverBackgroundColor: '#333333', buttonPressedBackgroundColor: '#222222', buttonPressedBorderColor: '#444444', buttonPressedTextColor: '#ffffff', highlightColor: '#00ff00' },
      devices: [{ id: 'owned-device', name: 'Fixture', type: 0, size: { columns: 5, rows: 3 } }] };
    child = spawn(process.execPath, ['bin/plugin.js', '-port', String(websocket.address().port), '-pluginUUID', 'plugin-test-registration',
      '-registerEvent', 'registerPlugin', '-info', JSON.stringify(info)],
    { cwd: pluginDirectory, env: { ...process.env, CULTO_STREAMDECK_CONNECTION: descriptor }, windowsHide: true });
    child.stdout.on('data', bytes => errors.push(bytes.toString())); child.stderr.on('data', bytes => errors.push(bytes.toString()));
    await until(() => messages.some(message => message.event === 'registerPlugin' && message.uuid === 'plugin-test-registration'), 'Official SDK did not register');
    appear('black-key', 'blackout'); appear('volume-key', 'volume-up'); appear('scene-key', 'scene', { sceneId: 'blackout' });
    appear('order-key', 'order-item', { cueId: cueB });
    event('scene-key', 'propertyInspectorDidAppear'); event('scene-key', 'sendToPlugin', { event: 'refresh' });
    await until(() => inspectorState(true), 'Inspector did not receive connected PC state');
    await until(() => messages.some(message => message.event === 'setImage' && message.context === 'black-key'), 'Button did not receive its image');
    console.log('PASS bundled official-SDK plugin registers and publishes actual PC state and button images');

    messages.length = 0;
    event('black-key', 'keyDown'); event('black-key', 'keyUp');
    await until(() => commands.length === 1 && inspectorState(true), 'Button did not dispatch and refresh');
    assert.equal(commands[0].action, 'blackout');
    await until(() => messages.some(message => message.context === 'black-key' && ['setImage', 'setState'].includes(message.event)), 'Blackout feedback did not update');
    event('volume-key', 'keyDown'); event('volume-key', 'keyUp');
    await until(() => commands.length === 2, 'Volume key did not dispatch');
    assert.equal(commands[1].action, 'volume-step'); assert.equal(commands[1].value, 5);
    console.log('PASS native keys dispatch once and receive confirmed blackout/volume feedback');

    snapshot.order.reverse();
    await new Promise(resolve => setTimeout(resolve, 1200));
    event('order-key', 'keyDown'); event('order-key', 'keyUp');
    await until(() => commands.length === 3, 'Configured order key did not dispatch');
    assert.equal(commands[2].action, 'cue'); assert.equal(commands[2].cueId, cueB);
    console.log('PASS configured order key uses stable cue ID after PC reordering');

    appear('volume-down-key', 'volume-down');
    await new Promise(resolve => setTimeout(resolve, 300));
    const beforeVolumePair = snapshot.volume;
    event('volume-key', 'keyDown'); event('volume-key', 'keyUp');
    event('volume-down-key', 'keyDown'); event('volume-down-key', 'keyUp');
    await until(() => commands.length === 5, 'Paired volume presses did not reach PC');
    assert.equal(snapshot.volume, beforeVolumePair, 'Opposite volume keys used stale absolute state');
    console.log('PASS rapid opposite volume keys apply atomic steps instead of cached absolute values');

    await new Promise(resolve => setTimeout(resolve, 300));
    dropReply = true; event('black-key', 'keyDown'); event('black-key', 'keyUp');
    await until(() => commands.length === 6, 'Lost-reply command did not reach PC');
    await new Promise(resolve => setTimeout(resolve, 1800));
    assert.equal(commands.length, 6, 'Plugin replayed an uncertain command');
    console.log('PASS lost command response is never automatically replayed');

    messages.length = 0; await fs.unlink(descriptor);
    await until(() => inspectorState(false), 'Missing bridge did not show disconnected state');
    token = 'B'.repeat(64);
    await fs.writeFile(descriptor, JSON.stringify({ schemaVersion: 1, address, token, processId: process.pid }));
    messages.length = 0;
    await until(() => inspectorState(true), 'Plugin did not reconnect after descriptor/token rotation');
    assert.equal(commands.length, 6);
    console.log('PASS bridge shutdown/restart rotates credentials and reconnects without replay');
    assert.ok(!errors.join('').includes(token), 'Plugin logs exposed credentials');
  } finally {
    if (child) { child.kill(); await new Promise(resolve => { if (child.exitCode !== null) resolve(); else child.once('exit', resolve); }); }
    for (const client of websocket.clients) client.terminate();
    await new Promise(resolve => websocket.close(resolve));
    server.closeAllConnections(); await new Promise(resolve => server.close(resolve));
    await fs.rm(directory, { recursive: true, force: true });
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
