# Reflection Screens Implementation Plan

## Summary
- Current playable core already exists in [GameManager.cs](</mnt/c/Users/user/Documents/HIT Coding/Year Bet/Semester Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/GameManager.cs:1>), [SnakeMove.cs](</mnt/c/Users/user/Documents/HIT Coding/Year Bet/Semester Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/SnakeMove.cs:1>), [SnakeGrow.cs](</mnt/c/Users/user/Documents/HIT Coding/Year Bet/Semester Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/SnakeGrow.cs:1>), and [SnakeTail.cs](</mnt/c/Users/user/Documents/HIT Coding/Year Bet/Semester Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/SnakeTail.cs:1>): question loop, movement, boost, collision highlight, per-question timer, `+5s`, potions, score/win, pause button, static/dynamic vcam switching, and a separate content-view worm.
- The reflection work should be built as a presentation/state-system refactor, not as one more branch inside `GameManager`. The clean default is: keep the current per-question worm lifecycle overall, but do not destroy the main worm until the reflection pipeline finishes.
- Prefer Unity-authored systems where they fit: Cinemachine for camera states/blends, Animator/AnimationClips for face and UI motion, CanvasGroup/Image color tweening for fades, serialized anchors/profiles for layout. Keep pure logic in small testable C# models.

## Pending Implementation Overview Vs The GDD
- Not implemented at all yet: About screen, game-code entry/validation flow, onboarding-choice screen, onboarding sequence, opening story animation, image magnify popup, timeout screen UX, ending metamorphosis storyboard, summary screen buttons for “other game” and “retry”.
- Only partially implemented: static view exists as a camera/berry-animation toggle, but it does not match the authored GDD flow of intro reveal, idle/manual entry rules, minimap support, hover-choice countdown, or post-pick static inspection.
- Reflection-related features are missing: immediate success/failure feedback beats, no-potion failure reflection, potion-rescue branch, coiled upright worm pose, sequential check/X marking, butterfly dialog, nightfall/background-only darkening, segment drain into progress, sleep-state endings.
- Progress timing differs from the GDD: current progress commits immediately on question success; the GDD wants visual drain first, then commit.
- Input architecture is still prototype-level: `SnakeGrow` uses `KeyCode.Return` directly instead of the Input System action map, which will make hover-choice/reflection gating harder unless unified first.
- Audio/UI are partial: the scene has pause and a volume slider wired to `AudioListener.volume`, but not the GDD’s real mute-toggle state, urgency-audio state, or reflection-specific cues.
- Testing infrastructure is minimal: no asmdefs or Unity test assemblies were found, so any elegant implementation should deliberately carve out pure runtime models plus a small debug harness for scene-side previewing.

## TODO Review Before Reflection
- Implement before reflection refactor: replace the `QuestionSuccess` / `QuestionFailed` split with a state-driven outcome pipeline. That TODO aligns directly with reflection and should not be postponed.
- Implement before reflection visuals: the “swirly eyes” and brief stunned lockout on failure should become the immediate failure beat that leads into failure reflection.
- Implement before reflection polish: the time-award animation should be built now because it is part of the same event/presentation pipeline as post-pick feedback.
- Implement during the same milestone if cheap: a failure SFX hook should be added when the new feedback pipeline lands.
- Defer for now: potion-received animation can wait unless the potion-rescue branch is included in the first reflection milestone.
- Defer for now: converting `awardedTimePerAnswer` to a bool is not a win here; keep it numeric and serialized so reward tuning stays flexible and testable.
- No other C# TODO/FIXME/HACK comments were found outside `GameManager.cs`.

## Main Reflection Plan
- Introduce `QuestionPhase`, `QuestionOutcome`, and `ReflectionSnapshot` as explicit runtime types. `GameManager` becomes an orchestrator only; outcome resolution, worm presentation, HUD presentation, and background mood each move into dedicated controllers.
- Add a `WormPresentationController` with modes `DynamicFollow`, `StaticDocked`, and `ReflectionCoil`. `SnakeTail` remains responsible for slot content, but pose/layout selection moves out of the movement history code.
- Keep the main worm alive from the final pickup/failure trigger through reflection. Hide the content-view worm during reflection. Destroy/rebuild only when the next question actually starts. This gives continuity without forcing a full game-wide singleton.
- Add serialized authoring anchors in the scene: `StaticDockAnchor`, `ReflectionAnchor`, and `ReflectionCameraTarget`. Reflection should auto-move the main worm from any world position/orientation into `ReflectionAnchor`, zero movement input, disable collisions, and hand pose control to `WormPresentationController`.
- Generate the reflection coil from a reusable layout profile, not hard-coded per question. Use a serialized `WormPresentationProfile` for spacing, scale, anchor offsets, face offsets, and max/min question sizes so the same pipeline handles short and long worms.
- Build a `ReflectionSnapshot` before any transition starts. It should contain question id, ordered answers, collected answers in order, first wrong answer if any, label texts, timer/potion state, and logical progress state. Reflection must replay from the snapshot, never from “whatever the live scene happens to look like”.
- Implement immediate feedback as a distinct short phase before reflection. Success beat: green berry, happy face, `+5s` float, short hold. Failure beat: red berry, dizzy face, movement lock, short stun. Timeout enters the same failure pipeline with different copy.
- Implement a `BackgroundMoodController` that targets only the `background` UI Image already present in `MainGame.unity`, or a background-only sibling layer under gameplay/HUD. Do not touch `Global Light 2D`; that would darken the worm, berries, and UI.
- Reuse `BackgroundMoodController` for pause, reflection, and image-popup darkening, but do not reuse `Pause()` itself. Current `Pause()` ends the question, destroys common objects, and sets `Time.timeScale = 0`, so only the dimming mechanism and input-lock pattern should be shared.
- Ensure all reflection and pause fades use unscaled time. This is a key caveat for elegance and testability: if the dimming is validated through pause first, any Animator/tween used there must be configured to keep updating while `Time.timeScale == 0`.
- Add a `QuestionHudController` for hover prompt, countdown, floating `+5s`, butterfly bubble, reflection CTA buttons, check/X markers, and urgency visuals. This keeps HUD logic out of `GameManager`.
- Add a `ProgressPresentationController` with separate logical progress and visual progress. Logical progress commits once per completed question; visual progress drains segment-by-segment during success reflection before the commit is finalized.
- Build the success reflection sequence as: dim background, camera blend to reflection vcam, auto-move worm to anchor, pose into upright coil, fade in content, switch to sleeping-happy face, mark segments green in answer order, then drain segments from tailward-to-headward into the progress bar, then fade to next question/day.
- Build the failure reflection sequence as: dim background, camera blend, auto-move worm, pose coil, keep dizzy eyes initially, replay correct segments in green, mark the first wrong segment last in red with `X`, show butterfly line and “try again tomorrow morning”, then fade marked content back to placeholders, switch to sleeping-sad face, then continue to next question/day without progress gain.
- Scaffold potion rescue as an extension point, not as part of the first reflection delivery. The state machine should reserve a `FailureRescued` branch, but the first implementation should ship the no-potion branch cleanly.

## Reusability And Test Pipeline
- Add a temporary in-editor debug panel or inspector buttons that can trigger “preview success reflection” and “preview failure reflection” from synthetic `ReflectionSnapshot` data. This lets the human implementer test layout and animation without replaying a full question.
- Validate background darkening first through pause, but only after the dimmer is extracted into `BackgroundMoodController` and switched to unscaled-time updates.
- Add small EditMode tests for pure models only: snapshot construction, progress-drain math, question outcome transitions, and reflection sequencing guards. Keep scene-heavy verification manual or PlayMode-based.
- Unify player interaction onto the Input System before hover-choice/reflection work expands. That keeps transitions testable and prevents hidden keyboard-only paths.
- Preserve built-in Unity authoring wherever possible: camera priorities and blends in Cinemachine, face/overlay animations in Animator, and serialized layout profiles instead of magic numbers in scripts.

## Assumptions And Defaults
- Default architecture: one gameplay scene, one main worm instance per active question, but the worm survives into reflection and is only rebuilt on the next question start.
- Recommended default on the singleton debate: do not convert to a whole-game singleton now. It is not needed for the reflection milestone, and it increases risk in spawn/reset/camera code. Revisit only if later onboarding, cutscenes, or save continuity truly require uninterrupted identity across all questions.
- Reflection should use the main worm, not the content-view worm, to preserve continuity.
- Nightfall/darkening should be background-only and must never be implemented by changing the global 2D light.
- Build the no-potion failure reflection first; potion rescue is a follow-up branch.
- Keep the current per-question timer model for now, even though the GDD sometimes frames time at run-level.

## MCP Assessment For Playing The Active Unity Instance
- As of April 11, 2026, there are Unity MCP repos that could plausibly let an agent inspect or drive a live Unity Editor instance, but this current Codex session is not connected to one. I found no MCP resources/templates available here, even though the project already includes `com.coplaydev.unity-mcp` in [manifest.json](</mnt/c/Users/user/Documents/HIT Coding/Year Bet/Semester Bet/Unity/TriangleProject/SilkySorters/Packages/manifest.json:1>).
- Best fit from GitHub research: `ozankasikci/unity-editor-mcp`. Its README explicitly lists play-mode controls (`play_game`, `pause_game`, `stop_game`, `get_editor_state`), screenshot capture for Game/Scene View, UI automation, and console access, which is enough in principle to launch play mode and assess game state. Source: https://github.com/ozankasikci/unity-editor-mcp
- Strong alternative: `akiojin/unity-mcp-server` documents editor automation, input simulation for playmode testing, deterministic screenshots from Game/Scene views, and Unity connection over a local bridge. Its GitHub page also says the repo is deprecated and development moved to `akiojin/unity-cli`, so I would not choose it fresh unless its successor is evaluated too. Source: https://github.com/akiojin/unity-mcp-server
- Relevant because it is already in your project: `CoplayDev/unity-mcp` clearly supports connecting to a live Unity instance, selecting among multiple Unity instances, reading `editor_state`, and managing cameras/scenes/UI. From the material I found, it looks strong for editor inspection and manipulation; the public README excerpt is less explicit about full playthrough/input simulation than the two repos above, though it does mention multi-view screenshots in release notes. Source: https://github.com/CoplayDev/unity-mcp
- Practical conclusion: yes, a Unity MCP could likely let me assess the active editor state and possibly play/test the game, but not from this session as-is. To make that possible here, the Unity-side package and the Codex-side MCP connection would both need to be actively configured and exposed to the session.
