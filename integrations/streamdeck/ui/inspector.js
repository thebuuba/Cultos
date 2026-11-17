let socket, context, actionId, settings = {}, currentState, optionsSignature;
const status = document.getElementById('status');
const choice = document.getElementById('choice');
function send(event, payload) { if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ event, context, ...(event === 'sendToPlugin' ? { action: actionId } : {}), payload })); }
function render() {
  const isScene = actionId.endsWith('.scene'), isCue = actionId.endsWith('.order-item');
  document.getElementById('selection').hidden = !isScene && !isCue;
  document.getElementById('label').textContent = isScene ? 'Escena' : 'Elemento del orden';
  const selected = settings[isScene ? 'sceneId' : 'cueId'];
  const items = (isScene ? currentState?.scenes : currentState?.order) || [];
  const nextSignature = JSON.stringify([isScene, selected, items.map(item => [item.id, item.label || item.title, item.available])]);
  if (nextSignature !== optionsSignature) {
    choice.replaceChildren(new Option('Seleccionar…', ''));
    for (const item of items) choice.add(new Option((item.label || item.title) + (isScene && !item.available ? ' (no disponible)' : ''), item.id));
    if (selected && !items.some(i => i.id === selected)) choice.add(new Option('Selección no disponible', selected));
    choice.value = selected || '';
    optionsSignature = nextSignature;
  }
  document.getElementById('live').textContent = currentState ? `Preparado: ${currentState.preparedTitle || '—'} · En vivo: ${currentState.liveTitle || '—'} · Volumen: ${currentState.volume}%` : '';
}
window.connectElgatoStreamDeckSocket = function (port, uuid, registerEvent, info, actionInfo) {
  const action = JSON.parse(actionInfo);
  context = uuid; actionId = action.action; settings = action.payload.settings || {};
  socket = new WebSocket(`ws://127.0.0.1:${port}`);
  socket.onopen = () => { socket.send(JSON.stringify({ event: registerEvent, uuid })); send('sendToPlugin', { event: 'refresh' }); };
  socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.event === 'sendToPropertyInspector' && message.payload.event === 'state') {
      currentState = message.payload.state;
      status.textContent = message.payload.error || (message.payload.connected ? 'Conectado a Culto Studio' : 'Sin conexión');
      status.style.borderColor = message.payload.connected ? '#10b981' : '#f59e0b';
      render();
    }
    if (message.event === 'didReceiveSettings') { settings = message.payload.settings || {}; render(); }
  };
  socket.onclose = () => { status.textContent = 'Stream Deck desconectado'; };
  render();
};
choice.addEventListener('change', () => {
  const field = actionId.endsWith('.scene') ? 'sceneId' : 'cueId';
  settings = { [field]: choice.value };
  send('setSettings', settings);
});
document.getElementById('refresh').addEventListener('click', () => send('sendToPlugin', { event: 'refresh' }));
