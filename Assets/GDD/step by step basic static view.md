# Step By Step For Basic Static View

This plan is only for the basic static view flow.

Scope:

- no directional input for a short time enters static view
- directional input exits static view back to dynamic view
- camera transition should be smooth
- the whole map should fit on screen in static view
- the player should stay visible
- mulberries and their text should become more readable in static view
- after that, add the bottom `תולעת חיווי תוכן`

This plan intentionally ignores:

- reflection mode
- onboarding-specific one-off camera choreography
- final polish of all UI states

## 1. Current Scene State

From the current `MainGame.unity`:

- `Main Camera` has a `CinemachineBrain`
- `DynamicVcam` already exists
- `StaticVcam` already exists
- `DynamicVcam` currently has:
- `CinemachinePositionComposer`
- `CinemachineConfiner2D`
- orthographic size around `5.5`
- `StaticVcam` currently has:
- a fixed transform
- orthographic size around `12`
- no follow/composer/confiner yet
- the Brain is currently using:
- `Smart Update`
- `Late Update` blend update
- default blend style `Ease In Out`
- default blend time `2` seconds

That last value is too slow for gameplay. It is fine for cinematic transitions, but not for "player stopped moving so enter static view".

## 2. Main Design Decision

Use:

- one real Unity `Main Camera`
- one `CinemachineBrain` on that camera
- `DynamicVcam` for normal gameplay
- `StaticVcam` for idle/static inspection
- Animator state changes for readability scaling
- code only for deciding when the mode changes

Do not use:

- direct animation of the `Main Camera` transform
- Z-position hacks for fake scale
- Timeline as the core driver of the repeated gameplay transition

Why:

- the real render camera should stay the one camera controlled by the Brain
- virtual cameras are the right Cinemachine abstraction for gameplay state changes
- your project is orthographic, so Z does not give the effect you actually want
- Timeline is great for authored sequences, but awkward for a constantly interruptible gameplay state that depends on moment-to-moment player input

## 3. When To Use Cinemachine Vs Normal Camera Vs Animator Vs Timeline

Use the normal Unity Camera for:

- rendering
- URP camera settings
- volume layer mask / volume trigger
- the `CinemachineBrain`

Do not use the normal Unity Camera for:

- hand-authored gameplay zoom transitions between dynamic and static
- switching between gameplay views manually

Use Cinemachine for:

- choosing which gameplay view is live
- blending between gameplay views
- framing and confining the gameplay camera
- future authored shot systems

Use Animator for:

- player visual shrinking in static mode
- mulberry visual scaling in static mode
- mulberry text scaling in static mode
- showing/hiding or animating the bottom static-view presentation lane

Use Timeline for:

- intro/outro sequences
- onboarding moments that follow a fixed script
- butterfly/dialog/storyboard sequences from the PPTX

Do not use Timeline for:

- repeated idle detection
- constantly entering/leaving static mode during normal play

## 4. Cinemachine Features Relevant To This Task

### Highly relevant right now

`CinemachineBrain`

- This is the system that decides which vcam drives the real camera.
- It also owns the default blend and custom blends.
- For this task it should remain the central transition controller.

`CinemachineCamera`

- This is the base virtual camera object for both dynamic and static mode.
- Priority switching is the cleanest first implementation.

`CinemachinePositionComposer`

- Already present on `DynamicVcam`.
- Very relevant for dynamic mode because it keeps the player framed with damping.
- Not needed on `StaticVcam` if static view is a fixed full-map shot.

`CinemachineConfiner2D`

- Very relevant for `DynamicVcam`.
- Keeps the gameplay camera inside the leaf/map bounds.
- Usually not needed on `StaticVcam` if the static shot is fixed and already frames the map correctly.

`Custom Blends`

- Very relevant.
- You want a specific short blend between `DynamicVcam` and `StaticVcam`, not one global default for every camera transition in the whole game.

### Relevant soon, but not required for the first switch

`CinemachineStoryboard`

- Useful for intro/outro and for previsualizing storyboard panels from the PPTX.
- Good for one-off authoring and shot matching.
- Not useful as the runtime implementation of static mode itself.

`CinemachineVolumeSettings`

- Lets a vcam activate and blend a URP/HDRP Volume profile.
- This is for per-camera post effects, not framing.
- Could be useful later for:
- making reflection mode feel dreamier
- giving intro/outro camera states their own grading
- adding subtle static-mode mood changes
- Not needed for the first basic static mode.

`CinemachineTargetGroup`

- Useful if later you want a camera to consider multiple important objects as one target.
- Example future use: frame player plus a selected mulberry cluster.
- Not the right first solution if your goal is "show the whole map every time".

`CinemachineGroupFraming`

- Useful with a `TargetGroup` when you want automatic framing of multiple targets.
- Potentially useful for future "context framing" modes.
- Not ideal for the first static mode because your storyboard intent is a stable authored overview, not a camera that resizes itself differently based on which berries exist.

### Relevant for other tasks, but not recommended for the core static-mode loop

`CinemachineStateDrivenCamera`

- Chooses child cameras based on Animator states.
- Good when the camera should follow an animation state machine.
- Not the best choice here because static view is driven by gameplay input inactivity, not by character animation states.
- If you later build a dedicated camera-state Animator for cutscenes, then it becomes more attractive.

`CinemachineSequencerCamera`

- Good for simple automatic sequences without Timeline.
- Useful for short authored sequences.
- Not right for idle-driven gameplay transitions.

`CinemachineMixingCamera`

- Blends child cameras by weight.
- Powerful, but overkill here.
- Better suited to advanced continuous rigs than to a simple dynamic/static toggle.

`CinemachineClearShot`

- Chooses the best unobstructed shot among child cameras.
- Mainly useful for 3D occlusion-heavy games.
- Not relevant for this top-down 2D gameplay mode.

### Not appropriate for this task

`CinemachinePixelPerfect`

- This exists to make Cinemachine cooperate with Unity's Pixel Perfect Camera in pixel-art projects.
- It is not a tool for matching Figma layout or storyboard spacing.
- It can also make blends temporarily non-pixel-perfect during transitions.
- Your current goal is presentation, readability, and authored scaling, not pixel-art snapping.
- So do not introduce it for static mode.

## 5. Important Inspector Settings Explained

### Standby Update

This controls how often a vcam updates while it is not live.

`Never`

- cheapest
- fine for fixed cameras that do not need continuous evaluation

`Always`

- updates every frame even when not live
- useful if a camera must stay fully ready at all times
- costs more

`Round Robin`

- updates occasionally while on standby
- a middle ground for performance

Recommendation here:

- `DynamicVcam`: `Round Robin` is fine
- `StaticVcam`: `Never` is fine if it is just a fixed full-map shot

### Blend Hint

Blend hints tell Cinemachine how to behave during transitions.

For your task:

- leave `BlendHint` at `None` for both cameras to start

Why:

- `Spherical Position` and `Cylindrical Position` are more useful when orbiting around tracked targets
- `Screen Space Aim When Targets Differ` matters more for look-at target changes
- `Inherit Position` can be useful for special rigs, but it is not needed for the first dynamic/static pass
- `Freeze When Blending Out` is for cases where outgoing live updates would make the blend unstable; not your first problem here

If a future transition feels wrong, revisit `BlendHint`, but do not start there.

### Cinemachine Volume Settings

This extension applies a camera-specific URP/HDRP Volume profile when that vcam is live.

It is about:

- color grading
- bloom
- vignette
- depth of field
- similar post-processing effects

It is not about:

- framing
- target following
- zoom logic
- UI precision

Use it later for mood. Do not use it to solve static-view readability.

### Storyboard

Storyboard overlays a still image over the camera output.

This is useful for:

- matching a planned shot during development
- building intro/outro animatics
- letting the PPTX storyboard act as visual reference inside Unity

It is not a replacement for:

- actual gameplay UI
- camera framing logic
- runtime static mode implementation

## 6. Sequencing Strategy

For this feature, use three different sequencing systems for three different jobs.

`Gameplay state sequencing`

- owned by code
- detects lack of input
- chooses dynamic or static mode

`Camera sequencing`

- owned by Cinemachine Brain + custom blends
- blends between `DynamicVcam` and `StaticVcam`

`Visual presentation sequencing`

- owned by Animator state machines
- controls player shrink, mulberry growth, text growth, and later bottom-lane reveal

This separation is important because it keeps each tool doing the thing it is best at.

## 7. Step 1: Make Camera Switching Work First

This is the bare minimum version and it should be implemented before any readability animations.

### Scene setup

1. Keep the current `Main Camera` as the only real camera.
2. Keep `DynamicVcam` as the gameplay follow camera.
3. Keep `StaticVcam` as the full-map camera.
4. Do not animate the `Main Camera` transform directly.
5. On `DynamicVcam`, keep `PositionComposer` and `Confiner2D`.
6. On `StaticVcam`, keep it simple:
7. no follow target
8. no composer
9. no confiner unless you later make it dynamic

### Brain setup

1. Keep `Update Method` as `Smart Update`.
2. Keep `Blend Update Method` as `Late Update`.
3. Replace the current global default of `2s Ease In Out` with either:
4. a shorter default blend such as `0.45s Ease In Out`
5. or, better, a custom blend asset specifically for `DynamicVcam -> StaticVcam` and `StaticVcam -> DynamicVcam`

Best option:

- keep the Brain default fairly neutral for the rest of the game
- create a custom blend asset with:
- `DynamicVcam -> StaticVcam = Ease In Out, about 0.45s`
- `StaticVcam -> DynamicVcam = Ease In Out, about 0.30s to 0.40s`

Why asymmetric:

- entering static should feel like settling
- exiting static should feel responsive

### Vcam setup

1. Enable explicit priority on both vcams.
2. Example:
3. `DynamicVcam = 20`
4. `StaticVcam = 10`
5. When entering static mode, swap them so `StaticVcam` becomes the higher-priority camera.

### Static shot setup

1. Center `StaticVcam` on the map, not on the player.
2. Adjust `Orthographic Size` until:
3. the full gameplay map fits
4. the player is still visible
5. mulberries are visible but not yet necessarily readable enough
6. Add margin so the shot does not feel cramped.

Do not solve mulberry readability in this step. Only solve the camera switch.

### Code responsibilities

Refactor the current setup so `GameManager` no longer thinks there is only one gameplay vcam.

Recommended responsibilities:

- `SnakeMove`
- expose `HasDirectionalInput`
- optionally expose `IsActuallyMoving` later if needed

- `GameManager`
- track idle timer
- decide whether static mode should be active
- raise/lower vcam priorities
- later, set animator parameters

The first version of the logic should be:

- if there is directional input, reset idle timer and ensure dynamic mode
- if there is no directional input, accumulate idle timer
- once the timer passes the threshold, switch to static mode

Recommended first values:

- idle threshold: `0.4` seconds
- enter blend: `0.45` seconds
- exit blend: `0.35` seconds

### What to test before moving on

1. Moving around keeps `DynamicVcam` live.
2. Releasing input for the threshold switches to `StaticVcam`.
3. Pressing movement again returns to `DynamicVcam`.
4. The transition feels smooth rather than abrupt.
5. The map is fully visible in static view.
6. Nothing depends on Timeline yet.

If this does not feel good, do not add animations yet. Fix the camera switch first.

## 8. Step 2: Add Readability Animations With Animator

Only start this after Step 1 feels correct.

### Core rule

Animate visual children, not gameplay roots.

Do not scale:

- the player root that movement logic uses
- the mulberry root that collisions and trigger logic use

Instead create visual children such as:

- player `VisualRoot`
- mulberry `VisualRoot`
- mulberry `TextRoot`

Then animate those child transforms.

### Why this is important

Scaling gameplay roots can accidentally change:

- collision feel
- trigger overlap feel
- pickup distances
- debug readability

### Animator state design

Use a bool parameter:

- `IsStaticView`

For the player visual animator:

- state `Dynamic`
- state `Static`
- in `Static`, the player visual is smaller

For the mulberry visual animator:

- state `Dynamic`
- state `Static`
- in `Static`, the mulberry card grows slightly

For the mulberry text animator:

- state `Dynamic`
- state `Static`
- in `Static`, text grows enough to read comfortably at the full-map shot

### Transition settings

Use bool-driven transitions, not triggers.

Why:

- the state is persistent, not one-shot
- the player can repeatedly move/stop
- bools are easier to reason about for this kind of mode

Recommended transition setup:

- transition both ways using `IsStaticView`
- no exit time required
- short transition duration, roughly matching the camera blend
- use eased animation curves authored in the clip

### Animation authoring advice

For the player:

- animate scale down only
- do not animate position unless the visual pivot is wrong

For mulberries:

- animate scale from centered pivots
- keep growth subtle enough that berries do not overlap badly

For text:

- consider a separate root so text can scale more than the berry itself
- keep the text centered so it looks like it grows from the middle

### Code responsibilities for Step 2

Code should only:

- set `IsStaticView` on the player visual animator
- set `IsStaticView` on all active mulberry animators
- make sure newly spawned mulberries initialize to the current mode

Do not write code that manually lerps scale every frame if Animator can do it.

### What to test before moving on

1. Static camera enters and exits correctly.
2. Player appears smaller in static mode.
3. Mulberries feel more readable in static mode.
4. Mulberry text is visibly more readable.
5. Returning to dynamic mode restores the tighter gameplay read.

## 9. Step 3: Add The Bottom `תולעת חיווי תוכן`

This should come after the camera switch and readability animations.

### Design recommendation

Treat the bottom content worm as presentation UI, not as the gameplay snake.

That means:

- use a separate dedicated object hierarchy for it
- do not try to reuse the live movement snake transform directly
- feed it content/state from gameplay data

Why:

- the storyboard treats it like a presentation strip
- it has different layout needs than the gameplay worm
- later static/reflection modes will likely want even more authored behavior here

### Suggested implementation direction

1. Add a bottom band container under the existing Canvas.
2. Add a dedicated static-view presenter root inside it.
3. Add an Animator to the lane root.
4. Use Animator to:
5. reveal the lane on enter static mode
6. hide the lane on return to dynamic mode
7. later animate slot population and emphasis

### State sequencing

Recommended order:

1. enter static mode
2. camera begins blending
3. scale animators begin transitioning
4. bottom lane animates in slightly after the transition starts

That sequencing is important because it avoids too many simultaneous changes in the same frame.

### Why Animator is better than Timeline here

The bottom lane is still part of a repeatable gameplay state.

So:

- Animator is a better fit than Timeline
- Timeline would be too rigid and interrupt-unfriendly

Timeline is still useful later if you have a one-time onboarding sequence that spotlights the lane.

## 10. Recommended Cinemachine Choices For This Project

Use now:

- `CinemachineBrain`
- `CinemachineCamera`
- `CinemachinePositionComposer`
- `CinemachineConfiner2D`
- custom blends

Use later when needed:

- `CinemachineStoryboard`
- `CinemachineVolumeSettings`
- `CinemachineTargetGroup`
- `CinemachineGroupFraming`
- `Timeline`

Only use if a future feature truly needs them:

- `StateDrivenCamera`
- `SequencerCamera`
- `MixingCamera`
- `ClearShot`

Do not use for this feature:

- `CinemachinePixelPerfect`
- Z-position tricks
- direct `Main Camera` animation as the core gameplay solution

## 11. Concrete Implementation Order

Follow this order exactly:

1. Make `DynamicVcam <-> StaticVcam` switching work on lack of input.
2. Tune the Brain blend so it feels good for gameplay.
3. Lock in the full-map `StaticVcam` framing.
4. Add Animator-driven player/mulberry/text scale changes.
5. Tune readability using animation curves and clip timing in the editor.
6. Add the bottom `תולעת חיווי תוכן`.
7. Only after that, consider storyboard overlays, volumes, or more advanced Cinemachine rigs.

## 12. Final Recommendation

The most appropriate first implementation is not a camera hack and not a code-heavy system.

It should be:

- code decides when static mode starts and ends
- Cinemachine decides how the camera blends
- Animator decides how the visuals reshape for readability

That matches the PPTX, the static-view mockups with the light-blue lower band, and your preference to author the feel in Unity tools rather than in custom runtime math.
