import streamDeck, { action, SingletonAction } from '@elgato/streamdeck';
import { Bridge } from './bridge.js';
import { actions, commandFor, feedback, keyImage } from './state.js';

const bridge = new Bridge();
const visible = new Map();
const rendered = new Map();

async function render(context) {
  const entry = visible.get(context);
  if (!entry) return;
  const display = feedback(entry.kind, bridge.state, entry.settings);
  const signature = JSON.stringify(display);
  if (rendered.get(context) === signature) return;
  await entry.action.setImage(keyImage(display));
  await entry.action.setTitle(display.title);
  rendered.set(context, signature);
}

async function inspector() {
  await streamDeck.ui.sendToPropertyInspector({ event: 'state', connected: bridge.state !== null, state: bridge.state, error: bridge.error });
}

bridge.onChange = () => {
  for (const context of visible.keys()) render(context).catch(() => {});
  inspector().catch(() => {});
};

for (const kind of Object.keys(actions)) {
  class Control extends SingletonAction {
    onWillAppear(ev) {
      visible.set(ev.action.id, { kind, action: ev.action, settings: ev.payload.settings });
      render(ev.action.id).catch(() => {});
    }
    onWillDisappear(ev) { visible.delete(ev.action.id); rendered.delete(ev.action.id); }
    onDidReceiveSettings(ev) {
      const entry = visible.get(ev.action.id);
      if (entry) { entry.settings = ev.payload.settings; render(ev.action.id).catch(() => {}); }
    }
    async onKeyDown(ev) {
      const command = commandFor(kind, bridge.state, ev.payload.settings);
      if (!command || !await bridge.command(ev.action.id, command)) await ev.action.showAlert();
    }
    onPropertyInspectorDidAppear() { inspector().catch(() => {}); }
    async onSendToPlugin(ev) {
      if (ev.payload?.event === 'refresh') { await bridge.refresh(); await inspector(); }
    }
  }
  const Registered = action({ UUID: `com.cultostudio.control.${kind}` })(Control);
  streamDeck.actions.registerAction(new Registered());
}

await streamDeck.connect();
await bridge.refresh();
setInterval(() => bridge.refresh(), 750);
