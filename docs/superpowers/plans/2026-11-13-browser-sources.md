# Browser Sources Implementation Plan

**Goal:** HTML local y URL reutilizables, a pantalla completa y superpuestos con transparencia.
**Architecture:** Cue.Browser + Chromium WPF + capas sobre el compositor actual.
**Spec:** docs/superpowers/specs/2026-11-13-browser-sources.md

## Tasks

- [ ] 1. Modelo BrowserSourceOptions y copias de Cue; pruebas Core de validación y persistencia.
- [ ] 2. BrowserSourceView/CEF y contenido de proyección; ensayo nativo de HTML animado y transparencia.
- [ ] 3. Administrador/propiedades Navegador, almacenamiento, orden, capas y controles de escena.
- [ ] 4. Ensayos integración, revisión independiente y correcciones.
- [ ] 5. Build/Core/Remote/UI, documentación y publicación local versionada.

## Execution rulings

- Trabajar en este workspace compartido conserva la entrega 0.36.2 aún sin commit; no crear un checkout que excluya esos cambios.
- Aplicar la autorización del usuario para agregar la función, sin solicitar aprobaciones adicionales por tareas reversibles.
- No hacer commit/push ni modificar la versión del usuario que permanece abierta.
