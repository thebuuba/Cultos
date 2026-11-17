# Stream Deck native integration implementation plan

**Goal:** Ship an installable Elgato plugin with direct local commands, live button feedback, automatic reconnection, and scene/order selection.

**Architecture:** A desktop-controlled, authenticated loopback-only RemoteHost reuses existing snapshot and command operations. It publishes an ephemeral connection descriptor in the user's CultoStudio data directory. The official Elgato Node SDK plugin reads this descriptor, polls state, handles physical button events once, and updates key images/titles from confirmed state. The existing optional hotkeys remain a secondary compatibility mode.

**Tech stack:** .NET 10 WPF/ASP.NET Core; official @elgato/streamdeck SDK; Node built-in HTTP/filesystem/crypto; Elgato CLI packaging. Windows only. No Marketplace publication.

## Contract and constraints

- Descriptor: `%LOCALAPPDATA%/CultoStudio/streamdeck-connection.json`, JSON `{schemaVersion:1,address:"http://127.0.0.1:<port>",token:"<64 hex>",processId:<pid>}`. A custom path may be used in tests.
- Local bridge listens on an automatically assigned loopback port only. It uses current RemoteSnapshot, POST `/api/command`, GET `/api/state` contracts and bearer authentication. Never print/store real credentials in tracked files or plugin settings.
- Explicit `StreamDeckPluginEnabled` setting and native integration toggle. Hotkey option stays independently disabled by default.
- Commands must preserve the foreground window and use existing validations, stable scene/cue IDs, and request-ID deduplication. No automatic replay of actions after failure/reconnection.
- Plugin validates descriptor scheme, numeric loopback host, port and token before issuing requests. Missing/stale config shows disconnected state and recovers automatically.
- Native actions: prepared projection, previous/next, logo, blackout, pause, restart, volume down/up, return-media, configurable scene, configurable order cue.
- Live feedback must follow server state, not optimistic toggle state. Property inspector selects existing scene/cue IDs without accepting executable paths or scripts.
- Deliver a validated `.streamDeckPlugin` artifact and installation instructions. Preserve the user's existing Elgato profiles and plugins.

## Tasks

- [x] Desktop bridge and native UI: failing lifecycle/auth/isolation tests, minimal implementation, persistence and close cleanup, command dispatch tests.
- [x] Official SDK plugin: transport/state tests, actions/feedback, property inspector, deterministic icon assets, manifest and pinned package lock.
- [x] Integration: mock Stream Deck WebSocket host exercising real packaged plugin against a local test server; descriptor rotation and lost-command/no-replay checks.
- [x] Validate/package with official CLI; run appropriate native/core/remote/browser regressions; review focused changes.
- [x] Update local application and deliver the plugin installer with accurate physical-device test limits.
