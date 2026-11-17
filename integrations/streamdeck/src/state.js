export const actions = {
  project: ['Proyectar', '▶'], previous: ['Anterior', '◀'], next: ['Siguiente', '▶'],
  background: ['Logo', '◆'], blackout: ['Negro', '■'], pause: ['Pausa', 'Ⅱ'], restart: ['Reiniciar', '↺'],
  'volume-down': ['Volumen −', '−'], 'volume-up': ['Volumen +', '+'], 'return-media': ['Volver a medios', '↩'],
  scene: ['Escena', '◆'], 'order-item': ['Elemento del orden', '☷']
};

export function commandFor(kind, state, settings = {}) {
  if (!state || !actions[kind]) return null;
  if ((kind === 'pause' || kind === 'restart') && state.canPause === false) return null;
  if (kind === 'return-media' && state.canReturnToMedia === false) return null;
  if (kind === 'scene') return state.scenes?.some(s => s.id === settings.sceneId && s.available) ? { action: 'scene', buttonId: settings.sceneId } : null;
  if (kind === 'order-item') return state.order?.some(c => c.id === settings.cueId) ? { action: 'cue', cueId: settings.cueId } : null;
  if (kind.startsWith('volume-')) return { action: 'volume-step', value: kind === 'volume-up' ? 5 : -5 };
  return { action: kind };
}

export function feedback(kind, state, settings = {}) {
  if (!state) return { title: 'Sin conexión', active: false, color: '#4b5563', symbol: '×' };
  if ((kind === 'pause' || kind === 'restart') && state.canPause === false)
    return { title: state.liveKind === 'WindowCapture' ? 'Desde YouTube' : 'No disponible', active: false, color: '#4b5563', symbol: actions[kind][1] };
  if (kind === 'return-media' && state.canReturnToMedia === false)
    return { title: 'No disponible', active: false, color: '#4b5563', symbol: actions[kind][1] };
  let title = actions[kind][0], active = false;
  if (kind === 'project') { title = `Proyectar\n${state.preparedTitle || 'Sin preparar'}`; active = state.preparedCueId != null && state.preparedCueId === state.liveCueId; }
  if (kind === 'pause') { active = state.isPaused; title = active ? 'Reanudar' : 'Pausa'; }
  if (kind === 'blackout') { active = state.isBlackout; title = active ? 'Negro\nACTIVO' : 'Negro'; }
  if (kind === 'background') active = state.isBackground;
  if (kind.startsWith('volume-')) title = `Volumen\n${state.volume}%`;
  if (kind === 'scene') {
    const scene = state.scenes?.find(s => s.id === settings.sceneId);
    title = scene ? `${scene.label}${scene.available ? '' : '\nNo disponible'}` : 'Elegir escena';
    active = scene?.active === true;
  }
  if (kind === 'order-item') {
    const cue = state.order?.find(c => c.id === settings.cueId);
    active = cue != null && cue.id === state.liveCueId;
    title = cue ? `${cue.id === state.liveCueId ? 'EN VIVO' : cue.id === state.preparedCueId ? 'Lista' : 'Orden'}\n${cue.title}` : 'Elegir elemento';
  }
  if (kind === 'return-media') active = state.canReturnToMedia;
  const lines = title.split('\n').flatMap(line => line.match(/.{1,14}/gu) || ['']);
  title = lines.slice(0, 3).join('\n');
  if (lines.length > 3) title = title.slice(0, -1) + '…';
  return { title, active, color: active ? '#059669' : '#1e3a5f', symbol: actions[kind][1] };
}

export function keyImage({ color, symbol }) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144"><rect width="144" height="144" rx="18" fill="${color}"/><text x="72" y="52" text-anchor="middle" fill="white" font-family="Segoe UI,Arial" font-size="38">${symbol}</text></svg>`;
  return `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`;
}
