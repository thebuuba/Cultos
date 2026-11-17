import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';

export function validDescriptor(value) {
  return value?.schemaVersion === 1 && /^http:\/\/127\.0\.0\.1:([1-9]\d{0,4})$/.test(value.address)
    && Number(value.address.split(':').at(-1)) <= 65535 && /^[a-f\d]{64}$/i.test(value.token)
    && Number.isSafeInteger(value.processId) && value.processId > 0;
}

export class Bridge {
  constructor(path = process.env.CULTO_STREAMDECK_CONNECTION || join(process.env.LOCALAPPDATA || '', 'CultoStudio', 'streamdeck-connection.json')) {
    this.path = path;
    this.state = null;
    this.error = null;
    this.busy = new Set();
    this.lastPress = new Map();
    this.onChange = () => {};
  }
  async descriptor() {
    const value = JSON.parse(await readFile(this.path, 'utf8'));
    if (!validDescriptor(value)) throw new Error('Invalid descriptor');
    return value;
  }
  async request(descriptor, route, body) {
    const response = await fetch(descriptor.address + route, {
      method: body ? 'POST' : 'GET', redirect: 'error', signal: AbortSignal.timeout(1500),
      headers: { Authorization: `Bearer ${descriptor.token}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
      ...(body ? { body: JSON.stringify(body) } : {})
    });
    if (!response.ok) throw new Error('Bridge unavailable');
    return response.json();
  }
  offline() { this.state = null; this.error = 'Sin conexión. Abre Culto Studio y activa Stream Deck.'; this.onChange(); }
  refresh() {
    if (this.refreshing) return this.refreshing;
    this.refreshing = (async () => {
      try {
        const state = await this.request(await this.descriptor(), '/api/state');
        if (!state || typeof state !== 'object' || !Number.isFinite(state.volume)) throw new Error('Invalid state');
        this.state = state;
        this.error = null;
        this.onChange();
      } catch { this.offline(); }
      finally { this.refreshing = null; }
    })();
    return this.refreshing;
  }
  async command(key, command) {
    const now = Date.now();
    if (!this.state || this.busy.has(key) || now - (this.lastPress.get(key) || 0) < 250) return false;
    this.busy.add(key);
    this.lastPress.set(key, now);
    try {
      const result = await this.request(await this.descriptor(), '/api/command', { ...command, requestId: randomUUID() });
      if (!result.success) {
        this.error = 'La consola rechazó la acción. Revisa la selección y el estado.';
        this.onChange();
        return false;
      }
      await this.refresh();
      return true;
    } catch { this.offline(); return false; }
    finally { this.busy.delete(key); }
  }
}
