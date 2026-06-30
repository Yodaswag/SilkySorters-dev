# Start Screen Worm Intro — Design Spec

- **Date:** 2026-06-29
- **Scene:** MainGame (opening, before Q1)
- **Owner:** Ori (implements all Unity changes manually; this doc guides)
- **Out of scope:** butterfly, speech bubble, greeting audio (separate task)

## Goal

Replace MainGame's immediate jump into question 1 with an intro "start screen": a decorative
silk-worm (head + 2 empty body segments) glides in along a **partial arc of the existing
Reflection Path Spline**, settles, and waits behind a **"לתחילת המסע"** sun button. Pressing it
(or Space) begins question 1. The pause button is disabled for the whole intro — and for every
reflection phase — and enabled only during active play.

## Why reuse the reflection machinery

Modeling the start screen as a between-question sequence means **one rule** governs the pause
button — off whenever a sequence is running (`currentReflectionPhase != None` **or** `introActive`),
on only during active play. Covers the intro and every end-of-question reflection with no scattered flags.

The intro itself adds **no new `ReflectionPhases` value** (owner's constraint): the glide is a
self-contained coroutine, the wait reuses the existing `WaitingForNextQuestion`.

## Flow

1. MainGame loads. `GetGame()` runs setup, then calls **`StartScreenIntro()`** instead of
   `StartQuestionTransition()` ([GameManager.cs:315](../../../Assets/Scripts/MainGame/General/GameManager.cs)).
2. World fades night → day (reuse `FadeWorld`). Static camera framing (full leaf visible, matches mockup).
3. **`SpawnIntroWorm()`**: worm head spawns at `Positioner_StartScreenWormSpawn`; 2 empty body
   segments via `SnakeTail.AddTail()` trail it.
4. **`StartScreenIntro()`** coroutine calls **`SnakeMove.PlayStartGlide()`**: worm follows the
   spline from `startGlideStartT` to `startGlideEndT` (partial arc) by setting `transform.position`
   (the same direct-set the reflection coil already uses). No reflection phase, no enum change.
   Body trails through the existing `positions[]` history automatically.
5. At endT, **`EnterStartWait()`**: sun button "לתחילת המסע" shown; phase = `WaitingForNextQuestion`.
6. Space/click → `ContinueToNextQuestion()` (already routes `questionNumber == 0` →
   `StartQuestionTransition` → Q1). The intro worm is destroyed by `KillCommonGameObjects` and the
   real Q1 worm is built under the fade — seamless swap.

## State model

- **No new `ReflectionPhases` value.** Glide = a self-contained coroutine; wait = **reuse** the
  existing `WaitingForNextQuestion`.
- `bool introActive` — true for the whole `StartScreenIntro` (spawn → glide → handoff to the wait).
- One pause helper `UpdatePauseButton()`:
  `pauseButton.SetActive(currentReflectionPhase == ReflectionPhases.None && !introActive)`.
  Called from `SetReflectionPhase` and whenever `introActive` flips.
- Defensive guard atop `Pause()`:
  `if (currentReflectionPhase != ReflectionPhases.None || introActive) return;`
  (if/else style, no ternary). Kills the re-entrant `EndQuestion()` bug even if the button slips through.
- (Optional) skip: while `introActive`, Space calls `SnakeMove.SkipStartGlide()` to jump to the rest pose.

## Glide mechanism — capped reuse of Reflection Path Spline

**Reflection Path Spline** ([MainGame.unity:133](../../../Assets/Scenes/MainGame.unity)): closed
7-knot loop, container world **(-0.72, 5.14)**, `m_Closed: 1`. Knot world positions (≈ `t = i/7`):

| t | knot | world (x, y) |
|------|------|--------------|
| 0.00 | K0 | (-0.72, 5.14) |
| 0.14 | K1 | ( 3.45, 3.39) |
| 0.29 | K2 | ( 3.49, -0.81) |
| 0.43 | K3 | (-0.06, -2.60) |
| 0.57 | K4 | (-3.54, -0.81) |
| 0.71 | K5 | (-4.18, 2.32) |
| 0.86 | K6 | (-3.02, 4.06) |

Loop center ≈ **(-0.66, 1.53)**, radius ≈ 3.5, upper-center of the world.

**Spawn:** `Positioner_StartScreenWormSpawn`, currently **(-4.25, 1.75)** — left side of the loop, ≈ **t 0.68**.

New serialized knobs (recommend on `SnakeMove`, beside the other glide fields):

- `[SerializeField] float startGlideStartT` — set so `EvaluatePosition(startT)` ≈ the positioner,
  avoiding a spawn snap. **≈ 0.68** for the current placement.
- `[SerializeField] float startGlideEndT` — where the worm rests.
- Speed reuses `animationMoveSpeed`.

**`SnakeMove.PlayStartGlide(SplineContainer spline)`** —
new **public coroutine**, sibling to `FollowSplinePath`. **Do NOT modify the proven
`FollowSplinePath`** ([SnakeMove.cs:207](../../../Assets/Scripts/MainGame/Silky/SnakeMove.cs)).
Lives in `SnakeMove` because it owns the worm transform/`rb`; driven by a coroutine, not a phase.
Uses its own `[SerializeField]` `startGlideStartT`/`startGlideEndT` and reuses `animationMoveSpeed`:

- Drive a `progress` 0→1 over time (`animationMoveSpeed * SkipFactor / splineLength * dt`).
- `t = Mathf.Lerp(startGlideStartT, startGlideEndT, progress)`.
- `transform.position = spline.EvaluatePosition(t)` — same direct-set the reflection coil uses at
  [SnakeMove.cs:228](../../../Assets/Scripts/MainGame/Silky/SnakeMove.cs).
- Rotation from `EvaluateTangent(t)`, negated when `endT < startT` (travel direction), then `- 90`
  (sprite points up by default).
- **No** `SetNightfall`, **no** `Darkening`, **no** camera `TargetOffset` lerp.
- Returns when `progress >= 1`; `GameManager.StartScreenIntro` then calls `EnterStartWait()`.

Direction is implied by `startT` vs `endT` — no separate reverse bool. `startGlideStartT` /
`startGlideEndT` are `[SerializeField]` on `SnakeMove`.

## Worm setup — `SpawnIntroWorm()` (trim of `ResetPlayer` [GameManager.cs:367](../../../Assets/Scripts/MainGame/General/GameManager.cs))

- `KillCommonGameObjects()` first (clean slate).
- Instantiate `silkyPlayerPrefab` at `positioner_StartScreenWormSpawn`. Set
  `SnakeMove`/`SnakeGrow`/`SnakeTail` `.gameManager = this`. Point `dynamicVcam.Follow`/`LookAt` at
  it (or leave static — see camera note).
- `AddTail()` exactly **twice** → 2 empty placeholder segments (alpha 0.35).
- Do **not** create a question or answers. This worm is purely decorative; it's destroyed when Q1 starts.
- `ChangeView(true)` for static framing (full leaf, matches mockup).

## Button — reuse MoonButton

`EnterStartWait()` (clone of `EnterWaitingForNextDay` [GameManager.cs:534](../../../Assets/Scripts/MainGame/General/GameManager.cs)):

```csharp
moonButtonImage.gameObject.SetActive(true);
moonButtonImage.sprite = sunImage;            // Assets/Sprites/MainGame/UI/ContinueButtonIcons/sunIcon.png
RTLFixer.SetTextInTMP(moonButtonLabel, "לתחילת המסע");
SetReflectionPhase(ReflectionPhases.WaitingForNextQuestion);
```

New field: `[SerializeField] Sprite sunImage;` (mirrors existing `leftArrowImage` / `butterflyImage`).

## Inspector wiring (Ori, in MainGame)

- `[SerializeField] Transform positioner_StartScreenWormSpawn` → `Positioner_StartScreenWormSpawn`
- `[SerializeField] GameObject pauseButton` → `Button-Pause`
- `[SerializeField] Sprite sunImage` → `sunIcon.png`
- `startGlideStartT` ≈ 0.68, `startGlideEndT` = rest spot (tune in Play)

## File-by-file (no enum change)

1. `GameManager.GetGame` ([:315](../../../Assets/Scripts/MainGame/General/GameManager.cs)) — swap immediate `StartQuestionTransition()` → `StartScreenIntro()`.
2. `GameManager` — new `StartScreenIntro()` coroutine + `SpawnIntroWorm()` (trim of `ResetPlayer`). `StartScreenIntro` sets `introActive`, hides pause, fades to day, spawns the worm, caches its `SnakeMove`, `yield`s on `PlayStartGlide(...)`, then `EnterStartWait()`.
3. `GameManager` — new `EnterStartWait()` (sun + "לתחילת המסע", phase = `WaitingForNextQuestion`); new `sunImage` field.
4. `SnakeMove` — new public coroutine `PlayStartGlide(...)`; new `startGlideStartT`/`startGlideEndT` fields. (Optional `SkipStartGlide()`.) **No** new `Update` case, **no** enum.
5. `GameManager` — pause: new `UpdatePauseButton()` (`phase == None && !introActive`), called from `SetReflectionPhase` ([:264](../../../Assets/Scripts/MainGame/General/GameManager.cs)) and on `introActive` flips; guard atop `Pause()` ([:592](../../../Assets/Scripts/MainGame/General/GameManager.cs)); new `pauseButton` + `introActive` fields.
6. (Optional) `GameManager.Update` ([:216](../../../Assets/Scripts/MainGame/General/GameManager.cs)) — while `introActive`, route Space → `SkipStartGlide()`.

## Risk / fallback

Reusing the closed end-coil circle **locks the entrance geometry**: only `startT`, `endT`,
direction, and camera framing are tunable — the loop itself can't move (it's authored for the
end-of-question coil). The mockup rest pose (head center, body trailing left) must be reachable on
this loop. If no `startT`/`endT` lands it cleanly, **fallback** = a dedicated **open** intro spline
(worm follows knot 0 → last, no cap math); same `PlayStartGlide` code, pass an `introSpline`
instead of `reflectionSpline`. Keep this in pocket.

## Deferred (separate pass, post-feature): `SequenceManager` extraction

Out of scope for today. `GameManager` is a ~930-line God object; reflection + the question
transitions are a coherent seam. Later, move them into a `SequenceManager` (owning
`SetReflectionPhase`, `RevealReflection`/`RevealSegments`/`RemoveContent`, drain/arc, nightfall,
moon/skip buttons, `SkipFactor`, **and** `StartQuestionTransition` + the new `StartScreenIntro`),
with a `GameManager` back-reference for `CreateQuestion`/data. **No behavior change** — a mechanical
move. Today's code is written so this later extraction stays mechanical: keep the new intro methods
grouped and dependent only on already-public `GameManager` members.

## Verification (Editor, Play)

- On load: worm spawns at positioner, glides a short arc, settles; **no snap** at spawn (tune `startT`).
- Sun button + "לתחילת המסע" appears after the glide; **pause button hidden** throughout the intro.
- **Space and click both** start Q1; the real Q1 worm appears with the correct answer-count segments.
- During Q1 (phase `None`): pause button visible and works; pressing pause → reflection runs; pause hidden again.
- No re-entrant `EndQuestion()` if pause is pressed during any reflection phase (guard holds).
