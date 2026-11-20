# Animated scene templates implementation plan

Goal: add four usable scene templates and per-layer entrance/exit effects to the existing scene editor and compositor.
Architecture: extend persisted SceneLayer with validated animation options; one animation clock per LayerSceneCanvas drives opacity and render transforms without changing source/decoder ownership. Program/preview share the existing live compositor. Animated scenes flatten to a live composite for OBS Move.
Stack: existing .NET 10/WPF, source pool and Move runtime; no dependencies.
Scope approved: first release of templates and simple layer animation, no keyframe editor.
Existing uncommitted review fixes must be preserved. Do not commit or publish unrelated changes.

## Contracts
- SceneAnimationOptions: Entrance/Exit string IDs None, Fade, Rise, SlideLeft, Zoom; DurationMilliseconds default600 (100..3000), DelayMilliseconds default0 (0..10000), HoldMilliseconds default0 (0..3600000). Validate(); Copy via record with {}. Hold0 remains until scene departure; Hold>0 schedules Exit only when Exit!=None.
- SceneLayer.Animation default new options, validated and deep-copied.
- SceneAnimationSample: double Opacity, OffsetX, OffsetY, Scale; bool Complete. Offsets are fractions of layer width/height (max .12); scale .9..1.08. Baseline is (1,0,0,1).
- SceneAnimationTimeline.SampleEntry(options,double elapsedMilliseconds) handles delay, entrance, optional hold+automatic exit. SampleExit(options,double elapsedMilliseconds) starts visible and eases out. None exit remains visible/completes immediately. Entry None with delay hides until delay; all values finite.
- SceneTemplates.Catalog: IReadOnlyList<SceneTemplate> with Id/Name/Description. Create(string id,Cue? background=null) -> SceneComposition. IDs welcome,reading,sermon,announcement. Templates supply editable text, animation stagger; optional full-canvas image/video background, video muted+looped. Source IDs for chosen background stable across templates.
- SceneLayer.Loop bool defaultfalse; layer playback uses global loop OR any visible instance Loop for a shared source.

## Tasks
- [x] Core: tests first for old JSON, isolation, validation, timing endpoints/delay/hold/exit, four templates, background source identity; implement models/timeline/templates.
- [x] Editor: template selector/apply, per-layer entrance/exit/duration/delay/hold + loop, repeat-preview button; preserve atomic field edits and cancellation; tests UI persistence/controls.
- [x] Renderer: tests first for source reuse, transformed/opacity samples and cleanup. One canvas stopwatch/render subscription, prepare initial frame, explicit begin entrance/exit from mixer; no decoder/audio changes. Exit extends non-Cut visual duration up to configured layer duration. Move captures animated full composite and starts clocks on first live Move frame.
- [x] Integrate/review: compile, Core/Remote, layer editor/pool, scene gestures, renderer animations, mixed playback/Move where runtime available; verify live preview is the same compositor, inspect screenshot. Stage local publish; install only after user closes running app.

Progress/rulings are recorded here to survive compaction. Existing feature branch is the task workspace; original pending review fixes remain in place.

Verification so far: Core425 and Remote78 passed; renderer animation36 checks passed including actual local video loop/mute, retained-media foreground replay, and native Move composite frames. Templates retain 16:9 text viewports to avoid distortion. Review caught foreground replay routing via media ActiveSurface, now routed to current visual surface. Review also identified conflicting bodies on shared text identity; editor correction assigns independent identity only when content changes.

Final verification: renderer40 checks, source pool10, layer editor9, scene gestures16 all pass. Source viewport regression reproduced clipping (960x541 instead of1280x720); Canvas source depot removes layout clipping, validated by renderer pixels, full-source clip bounds and inspected reading-template PNG. Independent review found no remaining important issues. Release publish succeeded at .tools/animated-scenes-publish. Existing install backup created. User closed app; nine managed/config/apphost files installed and hashes verified, then app reopened and startup checked. No commit requested for this feature.

Packaging correction: installed app is self-contained win-x64; portable publish apphost/deps/runtimeconfig cannot replace its bundled-runtime configuration. Restored those three unchanged package files from backup into install and stage; six updated managed DLL/PDB files remain. All nine hashes match stage. Confirmed main window title and Responding=true after corrected startup. Preserve bundled runtime metadata on future managed-only updates, or publish with the matching self-contained RID.
