import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

test('descriptor rejects credentials, alternate hosts and malformed tokens', async () => {
  const { validDescriptor } = await import('../src/bridge.js');
  const good = { schemaVersion: 1, address: 'http://127.0.0.1:4321', token: 'a'.repeat(64), processId: 1 };
  assert.ok(validDescriptor(good));
  for (const address of ['http://localhost:4321', 'https://127.0.0.1:4321', 'http://127.0.0.1:4321/other', 'http://a:b@127.0.0.1:4321', 'http://127.1:4321', 'http://192.168.1.1:4321'])
    assert.equal(validDescriptor({ ...good, address }), false, address);
  assert.equal(validDescriptor({ ...good, token: 'secret' }), false);
});

test('real HTTP transport authenticates, guards concurrent presses, never replays lost commands and discovers rotated descriptor', async t => {
  const { Bridge } = await import('../src/bridge.js');
  const dir = await mkdtemp(join(tmpdir(), 'culto-deck-'));
  const path = join(dir, 'connection.json');
  let commands = 0;
  let loseReply = false;
  const server = createServer(async (req, res) => {
    assert.equal(req.headers.authorization, `Bearer ${'a'.repeat(64)}`);
    if (req.url === '/api/state') return res.end(JSON.stringify({ volume: 42, isPaused: true }));
    commands++;
    const chunks = [];
    for await (const chunk of req) chunks.push(chunk);
    const command = JSON.parse(Buffer.concat(chunks));
    assert.equal(command.action, 'next');
    assert.match(command.requestId, /^[a-f0-9-]{36}$/);
    if (loseReply) return req.socket.destroy();
    await new Promise(resolve => setTimeout(resolve, 30));
    res.end(JSON.stringify({ success: true, message: 'ok' }));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(async () => { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); await rm(dir, { recursive: true }); });
  const descriptor = { schemaVersion: 1, address: `http://127.0.0.1:${server.address().port}`, token: 'a'.repeat(64), processId: 1 };
  await writeFile(path, JSON.stringify(descriptor));
  const bridge = new Bridge(path);
  await bridge.refresh();
  assert.equal(bridge.state.volume, 42);
  const results = await Promise.all([bridge.command('key', { action: 'next' }), bridge.command('key', { action: 'next' })]);
  assert.deepEqual(results, [true, false]);
  assert.equal(commands, 1);
  await new Promise(resolve => setTimeout(resolve, 260));
  loseReply = true;
  assert.equal(await bridge.command('key', { action: 'next' }), false);
  assert.equal(bridge.state, null);
  await bridge.refresh();
  assert.equal(commands, 2);
  await writeFile(path, JSON.stringify({ ...descriptor, token: 'invalid' }));
  await bridge.refresh();
  assert.equal(bridge.state, null);
  await writeFile(path, JSON.stringify(descriptor));
  await bridge.refresh();
  assert.equal(bridge.state.isPaused, true);
});

test('feedback and commands follow confirmed state and stable selections', async () => {
  const { feedback, commandFor } = await import('../src/state.js');
  assert.equal(feedback('pause', null).title, 'Sin conexión');
  const state = { isPaused: true, isBlackout: true, volume: 67, preparedTitle: 'Siguiente', liveTitle: 'En vivo', preparedCueId: 'cue-b', liveCueId: 'cue-a', scenes: [{ id: 's1', label: 'Logo', active: true, available: true }], order: [{ id: 'cue-b', title: 'Lectura' }] };
  assert.equal(feedback('pause', state).active, true);
  assert.equal(feedback('blackout', state).active, true);
  assert.equal(feedback('volume-up', state).title, 'Volumen\n67%');
  assert.match(feedback('project', state).title, /Siguiente/);
  assert.match(feedback('order-item', state, { cueId: 'cue-b' }).title, /Lista/);
  assert.equal(feedback('scene', state, { sceneId: 's1' }).active, true);
  assert.deepEqual(commandFor('scene', state, { sceneId: 's1' }), { action: 'scene', buttonId: 's1' });
  assert.deepEqual(commandFor('order-item', state, { cueId: 'cue-b' }), { action: 'cue', cueId: 'cue-b' });
  assert.deepEqual(commandFor('volume-up', state), { action: 'volume-step', value: 5 });
  assert.equal(commandFor('scene', state, { sceneId: 'missing' }), null);
});

test('opposite volume keys send independent atomic steps from the same cached volume', async () => {
  const { commandFor } = await import('../src/state.js');
  const state = { volume: 50 };
  assert.deepEqual(commandFor('volume-up', state), { action: 'volume-step', value: 5 });
  assert.deepEqual(commandFor('volume-down', state), { action: 'volume-step', value: -5 });
  assert.deepEqual(commandFor('volume-up', { volume: 100 }), { action: 'volume-step', value: 5 });
});

test('concurrent refresh callers await the same confirmed HTTP snapshot', async t => {
  const { Bridge } = await import('../src/bridge.js');
  const dir = await mkdtemp(join(tmpdir(), 'culto-refresh-'));
  const path = join(dir, 'connection.json');
  let requests = 0;
  const server = createServer(async (_req, res) => {
    requests++;
    await new Promise(resolve => setTimeout(resolve, 40));
    res.end(JSON.stringify({ volume: 55 }));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(async () => { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); await rm(dir, { recursive: true }); });
  await writeFile(path, JSON.stringify({ schemaVersion: 1, address: `http://127.0.0.1:${server.address().port}`, token: 'a'.repeat(64), processId: 1 }));
  const bridge = new Bridge(path);
  const first = bridge.refresh();
  await bridge.refresh();
  assert.equal(bridge.state.volume, 55);
  await first;
  assert.equal(requests, 1);
});

test('media actions are unavailable when confirmed state cannot control playback', async () => {
  const { feedback, commandFor } = await import('../src/state.js');
  const state = { volume: 50, canPause: false, liveKind: 'WindowCapture', canReturnToMedia: false };
  assert.equal(commandFor('pause', state), null);
  assert.equal(commandFor('restart', state), null);
  assert.equal(commandFor('return-media', state), null);
  assert.equal(feedback('pause', state).title, 'Desde YouTube');
  assert.equal(feedback('restart', state).color, '#4b5563');
  assert.equal(feedback('return-media', state).color, '#4b5563');
  assert.equal(feedback('project', { ...state, preparedTitle: 'Un título muy largo para aparecer completo en una tecla de Stream Deck' }).title.split('\n').length, 3);
});
