# Catálogo de efectos bíblicos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ofrecer 291 efectos de texto utilizables y verificables en el editor y en ambas salidas.

**Architecture:** Un catálogo inmutable de presets alimenta la normalización, el selector y un motor WPF que anima fragmentos de una única captura temporal del texto. Los tres efectos anteriores conservan su implementación. La captura desaparece al terminar y vuelve el texto nativo.

**Tech Stack:** C#, .NET 10, WPF, xUnit y ensayos WPF existentes.

**Spec:** `docs/superpowers/specs/2026-11-16-catalogo-transiciones-texto.md`

## Global Constraints

- Mantener todos los cambios pendientes y trabajar en la rama de características actual.
- No añadir dependencias, servicios, commits ni alterar datos reales en las pruebas.
- 288 presets nuevos: 12 movimientos × 3 alcances × 8 variantes; conservar los tres IDs anteriores.
- Máximo 80 fragmentos por animación; cambiar a líneas para textos extensos.
- Fondo fijo, última selección prevalece, negro y cierre cancelan todos los relojes.

### Task 1: Catálogo y persistencia

Files: `src/Culto.Core/BibleTextTransitionCatalog.cs`, `BibleVisualStyle.cs`, `tests/Culto.Core.Tests/BibleTextTransitionCatalogTests.cs`.

Contract: enums `BibleTextMotion` (Slide, Wipe, Radial, Zoom, Rotate, Skew, Bounce, Spring, Blur, Wave, Fold, Unroll), `BibleTextScope` (Passage, Lines, Words); record `BibleTextTransitionPreset(string Id, string Name, string Family, BibleTextMotion Motion, BibleTextScope Scope, int Variant)`; static `BibleTextTransitionCatalog.All`, `.TryGet(string? id, out BibleTextTransitionPreset preset)`; `BibleVisualStyle.FavoriteTransitions` es una cadena de IDs separados por `|`, normalizada a IDs válidos distintos (máximo 64).

- [x] Escribir pruebas de conteo, IDs únicos, familias y serialización de todos los presets, IDs antiguos e inválidos.
- [x] Ejecutarlas antes y después de implementar el contrato.
- [x] Revisar compatibilidad de normalización y no mutación del original.

### Task 2: Selector

Files: `src/Culto.Desktop/Dialogs/BibleStyleDialog.cs`, nueva parcial `BibleStyleDialog.Transitions.cs`, `tests/Culto.Desktop.Smoke/BibleEditorTests.cs`.

Consume Task 1. Conservar `BibleTransition` (ComboBox, SelectedValuePath=Tag), `BibleTransitionDuration`, `TestBibleTransitionButton`. Añadir `BibleTransitionSearch`, `BibleTransitionFamily`, `BibleTransitionFavorite` y `BibleTransitionFavoritesOnly`.

- [x] Probar selección, búsqueda, familia, favorito, cancelación y reapertura.
- [x] Implementar el selector con el tema actual y sin perder la selección al filtrar.
- [x] Verificar editor a 1180 y 940 px y previsualización.

### Task 3: Renderizado e integración

Files: nueva parcial `ProjectionSurface.BibleTextPresets.cs`, `ProjectionSurface.cs`, `ProjectionSurface.BibleTextTransition.cs`, `tests/Culto.Renderer.Smoke/BibleTextTransitionTests.cs`.

Consume Task 1. El motor existente invoca presets nuevos y comparte cancelación. Una única imagen de texto se divide en recortes de pasaje/líneas/palabras, con transformaciones, máscaras y desenfoque nativos.

- [x] Probar familias, alcances, cancelación y límites de fragmentos en salida directa y mezclada.
- [x] Implementar los 12 movimientos y sus ocho variantes; limpiar los relojes y la imagen al cancelar.
- [x] Ejecutar Core, editor y renderer; revisar capturas y cobertura de los 288 presets.
- [x] Revisar el conjunto, documentar y actualizar la distribución local si está cerrada.
