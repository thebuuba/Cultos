# Deckboard scenes and mobile synchronization

User request: colorful Deckboard style for desktop scenes and show YouTube scenes on phone.

- [x] Publish built-in scenes (including current YouTube availability) in authenticated snapshots.
- [x] Dispatch scene buttons through the existing desktop scene actions, without file pickers on phone.
- [x] Render colored icon keys on PC and square responsive phone scene grid; keep custom deck.
- [x] Verify protocol, desktop actions, browser/mobile behavior, responsive screenshots and regressions.
- [x] Package 0.37.0 locally without closing the running user's program; solution build clean, Core261/Remote65 pass. Native remote-scenes includes exact owned Chrome capture; old remote-order/YouTube controls and real browser viewport checks pass.

Review: independent reviewer found no blocker. Matched desktop YouTube active predicate to phone for generic legacy captures. Fixed Renderer.Smoke build references needed by already-present BrowserSource files; no browser feature work resumed.

Scope: no resumption of unfinished browser source development beyond compilation compatibility. Preserve all existing changes. Work in existing feat/culto-studio branch.
