// Bundled Elgato SDK plugin -> actual loopback bridge -> actual WPF presentation session.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const os = require('node:os');
const { spawn } = require('node:child_process');
const { createRequire } = require('node:module');
const root = path.resolve(__dirname, '../..');
const requirePlugin = createRequire(path.join(root, 'integrations/streamdeck/package.json'));
const { WebSocketServer } = requirePlugin('ws');
async function until(predicate, message) {
  const deadline = Date.now() + 12000;
  while (!await predicate()) {
    assert.ok(Date.now() < deadline, message);
    await new Promise(resolve => setTimeout(resolve, 40));
  }
}
(async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'CultoNativeEndToEnd-'));
  const ready = path.join(directory, 'fixture.json');
  const websocket = new WebSocketServer({ host: '127.0.0.1', port: 0 });
  let app, plugin, socket, descriptor;
  const messages = [], output = [];
  try {
    const executable = process.env.CULTO_STREAMDECK_DESKTOP_FIXTURE || path.join(root, 'tests/Culto.Desktop.Smoke/bin/Debug/net10.0-windows/Culto.Desktop.Smoke.exe');
    app = spawn(path.join(root, '.tools/dotnet/dotnet.exe'), [executable.replace(/\.exe$/, '.dll'), '--stream-deck-native-fixture'], { cwd: root, windowsHide: true,
      env: { ...process.env, CULTO_STREAMDECK_FIXTURE: ready } });
    app.stdout.on('data', chunk => output.push(chunk.toString())); app.stderr.on('data', chunk => output.push(chunk.toString()));
    await until(async () => { try { await fs.access(ready); return true; } catch { return false; } }, 'Desktop did not publish its owned native fixture');
    const fixture = JSON.parse(await fs.readFile(ready, 'utf8'));
    descriptor = JSON.parse(await fs.readFile(fixture.descriptorPath, 'utf8'));
    const state = async () => {
      const response = await fetch(descriptor.address + '/api/state', { headers: { Authorization: 'Bearer ' + descriptor.token }, signal: AbortSignal.timeout(1500) });
      assert.ok(response.ok, 'Real bridge rejected fixture credentials'); return response.json();
    };
    if (!websocket.address()) await new Promise(resolve => websocket.once('listening', resolve));
    websocket.on('connection', client => { socket = client; client.on('message', bytes => messages.push(JSON.parse(bytes.toString()))); });
    const info = { application: { font: 'Arial', language: 'es', platform: 'windows', platformVersion: '10', version: '7.1.0' },
      plugin: { uuid: 'com.cultostudio.control', version: '1.0.0.0' }, colors: {}, devicePixelRatio: 1,
      devices: [{ id: 'native-fixture', name: 'Fixture', type: 0, size: { columns: 5, rows: 3 } }] };
    plugin = spawn(process.execPath, ['bin/plugin.js', '-port', String(websocket.address().port), '-pluginUUID', 'native-fixture-registration',
      '-registerEvent', 'registerPlugin', '-info', JSON.stringify(info)], {
      cwd: path.join(root, 'integrations/streamdeck/com.cultostudio.control.sdPlugin'), windowsHide: true,
      env: { ...process.env, CULTO_STREAMDECK_CONNECTION: fixture.descriptorPath }
    });
    plugin.stdout.on('data', chunk => output.push(chunk.toString())); plugin.stderr.on('data', chunk => output.push(chunk.toString()));
    await until(() => messages.some(message => message.event === 'registerPlugin'), 'Native plugin did not register');
    function event(kind, name) {
      socket.send(JSON.stringify({ event: name, action: 'com.cultostudio.control.' + kind, context: kind, device: 'native-fixture',
        payload: { controller: 'Keypad', coordinates: { column: 0, row: 0 }, settings: {}, state: 0, isInMultiAction: false, resources: {} } }));
    }
    for (const kind of ['project', 'next', 'blackout', 'volume-up']) event(kind, 'willAppear');
    await until(() => messages.some(message => message.event === 'setTitle' && message.context === 'project' && message.payload.title.includes('Proyectar')), 'Plugin did not connect to real desktop state');
    event('project', 'keyDown'); event('project', 'keyUp');
    await until(async () => (await state()).liveCueId === fixture.preparedCueId, 'Plugin key did not project the actual prepared PC cue');
    const first = await state();
    assert.equal(first.hasOutput, true);
    event('next', 'keyDown'); event('next', 'keyUp');
    await until(async () => (await state()).liveCueId !== fixture.preparedCueId, 'Next key did not advance actual worship order');
    event('blackout', 'keyDown'); event('blackout', 'keyUp');
    await until(async () => (await state()).isBlackout, 'Blackout key did not hide actual output');
    const previousVolume = (await state()).volume;
    event('volume-up', 'keyDown'); event('volume-up', 'keyUp');
    await until(async () => (await state()).volume === Math.min(100, previousVolume + 5), 'Volume key did not change actual Culto Studio volume');
    console.log('PASS official plugin projects, advances, blacks out and adjusts volume through the actual WPF bridge');
    await fs.writeFile(ready + '.stop', 'stop');
    await until(() => app.exitCode !== null, 'Owned desktop fixture did not close');
    assert.equal(app.exitCode, 0, output.join('').slice(-2000));
    await until(async () => { try { await fs.access(fixture.descriptorPath); return false; } catch { return true; } }, 'Desktop left native credentials after shutdown');
    assert.ok(!output.join('').includes(descriptor.token), 'Native test logs exposed credentials');
    console.log('PASS desktop shutdown closes its native bridge and removes its credential descriptor');
  } finally {
    for (const process of [plugin, app]) if (process && process.exitCode === null) {
      process.kill(); await new Promise(resolve => { if (process.exitCode !== null) resolve(); else process.once('exit', resolve); });
    }
    for (const client of websocket.clients) client.terminate();
    await new Promise(resolve => websocket.close(resolve));
    await fs.rm(directory, { recursive: true, force: true });
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
