# Basic Static View Considerations

This note is only about the most basic version of static view:

- no directional input for a short time enters static view
- directional input exits static view back to dynamic view
- the transition should feel smooth
- the whole map should become visible
- the player should still be visible
- mulberries and the text on them should remain readable

It intentionally ignores the rest of the roadmap for now.

## What "static view" means in the PPTX

The PPTX is more specific than the current markdown plan.

From slide 17:

- static view is explicitly "the screen you see as long as you are not moving"
- its purpose is to show where you are relative to the mulberries
- it should also let you read the content written on the mulberries
- moving closes this screen

The visual mockups in the later storyboard slides make the intent even clearer:

- static view is not just "same camera, but stopped"
- static view is a composed presentation state
- the map is framed widely enough to understand spatial relationships at a glance
- the worm becomes much smaller on screen
- the mulberries stay visually prominent enough to read
- the top question card stays present
- later static examples also add the bottom worm/content lane

Important visual takeaway:

- dynamic view = close, local, movement-focused
- static view = wide, legible, overview-focused

## What the images imply

The storyboard images after the onboarding text show a consistent pattern:

- slides 19-20 style mockups show the worm tiny in the frame while the mulberries remain large enough to scan
- dynamic slides immediately after that return to a close camera with the minimap visible
- later static mockups repeat the same idea and add the bottom lane, which confirms that static mode is meant to be a distinct presentation mode, not just a paused gameplay camera

The strongest visual cues are:

- the worm shrinks dramatically relative to the screen
- the mulberries do not shrink proportionally with the worm
- text on mulberries is still intended to be readable in static mode
- the change reads like a camera/presentation transition, not like objects teleporting

That means the first implementation should treat static view as a coordinated blend of:

- camera framing
- player visual scale
- mulberry visual scale
- mulberry text scale

## What not to do

Do not try to "cheat" this with Z-position changes.

Why:

- your current `Main Camera` in `Assets/Scenes/MainGame.unity` is orthographic
- in orthographic projection, changing Z does not create the kind of apparent size change you want
- changing to perspective just for this would fight the current 2D setup, sorting, and camera behavior
- using Z for fake scale would be harder to tune and less predictable than authoring the look directly

So for this project, Z is the wrong lever.

## Camera recommendation

Use:

- one real `Main Camera`
- one `CinemachineBrain` on that camera
- separate Cinemachine virtual cameras for dynamic and static

That is cleaner than manually animating the `Main Camera` because:

- blends are built in
- each mode can be previewed and tuned in the Unity editor
- the static framing can be authored once and then left alone
- you avoid writing custom interpolation code for position and zoom

For the basic version:

- keep the current gameplay camera as `DynamicVcam`
- add a `StaticVcam`
- `DynamicVcam` follows the player as it does now
- `StaticVcam` should be positioned at the center of the playable area and sized to include the full map with margin
- switch by priority, not by enabling/disabling the main camera

## Smooth transition recommendation

The smoothest simple version is:

- idle timer enters static mode after a short delay
- any directional input immediately exits static mode
- Cinemachine handles the camera blend
- Animator clips handle the scale changes

Recommended first-pass timings:

- enter static after about `0.35` to `0.6` seconds with no directional input
- exit static immediately on movement input
- camera blend around `0.4` to `0.6` seconds with ease in/out
- scale animations timed to the same blend window

This gives the feeling that the game "settles" into overview mode instead of snapping.

## Use Unity-authored animation, not script math

Your preference here is a very good fit for this feature.

The script should only decide:

- are we in dynamic or static mode
- which vcam has priority
- which animator bool is on

The look should live in Unity-authored assets:

- Animator clips for mulberry visual scale
- Animator clips for mulberry text scale
- Animator clips for player visual scale
- Cinemachine blend settings for camera motion

This is the right level of scripting for the feature.

## Important implementation detail: animate visuals, not gameplay roots

Do not scale the transform that movement and collisions depend on.

Instead:

- give the player a child like `VisualRoot`
- give the mulberry prefab a child like `VisualRoot`
- animate those child transforms
- leave the gameplay root, collider, and interaction logic unchanged

This avoids:

- changing collision sizes by accident
- breaking trigger distances
- making movement feel different between modes

For the "grow with respect to the center" requirement:

- make sure the animated visual root has its pivot centered on the mulberry card
- make sure the text is centered relative to that visual root
- then a simple scale animation will visually grow from the center in a stable way

## How to interpret the PPTX visually for the first pass

For the basic implementation, do not try to perfectly recreate every UI difference from the storyboard yet.

Implement only these visual truths:

- the map becomes fully visible
- the worm becomes smaller on screen
- mulberries become easier to scan than they would be with camera zoom alone
- mulberry text remains readable
- returning to movement restores the close gameplay view

It is fine to postpone:

- the bottom worm/content lane
- minimap removal/replacement
- magnifier interactions
- extra overlays and highlights

## Minimal code responsibilities in the current project

Based on the current code:

- `Assets/Scripts/SnakeMove.cs` already reads directional input, but it does not yet expose a simple public "has movement input" state
- `Assets/Scripts/GameManager.cs` currently owns player spawning and the single serialized Cinemachine camera reference

So the minimal code work should be:

- expose a public directional-input state from `SnakeMove`
- let `GameManager` track idle time
- let `GameManager` switch between dynamic and static mode
- let `GameManager` set camera priority and animator parameters

The code should not be responsible for hand-tuning scale curves every frame.

## Recommended first version

The cleanest first version is:

1. Add a `StaticVcam` for full-map framing.
2. Add one bool parameter such as `IsStaticView` to the relevant animators.
3. Animate mulberry visuals and mulberry text up in static mode.
4. Animate only the player's visual child down in static mode.
5. Enter static mode after a short no-input delay.
6. Exit immediately on directional input.

If this version feels good, then later work can layer on:

- bottom lane presentation
- minimap behavior changes
- more authored UI states
- reflection mode

## Final recommendation

Treat static view as a small authored presentation state, not as a camera hack.

For this project, the most elegant approach is:

- Cinemachine virtual camera blend for framing
- Animator clips for scale
- minimal script logic only for state switching

That matches both the PPTX visuals and your preference to build the feel through Unity tools instead of code-heavy runtime calculations.
