# Single-World Plan for Static Mode, Success/Failure Reflection, and Adjacent Spec Gaps

**Summary**
- Replace the current immediate success/fail text flow in `GameManager` with an explicit question-state machine. Keep everything in one scene/world; do not duplicate the map or create a separate reflection scene.
- Keep the prototype’s per-question timer and the current open unresolved-question pool behavior on failure. Correct picks add `+5s` to the current question timer.
- Critical path for this milestone is: question intro static mode, dynamic exploration, timed hover/selection, static inspection after picks, and no-potion success/failure reflection with coiling. Potion rescue is scaffolded only.

**Implementation Changes**
- `GameManager` should own a `QuestionPhase` enum and become the single orchestrator for transitions. The phase set should cover: `QuestionIntroStatic`, `DynamicExplore`, `ChoiceHover`, `StaticInspect`, `ImmediateSuccessFeedback`, `ImmediateFailureFeedback`, `ReflectionSuccess`, `ReflectionFailure`, `ReflectionFailureHold`, `ReflectionFailureFadeOut`, `TransitionNextQuestion`, `Paused`, and `GameWon`.
- Start every question in `QuestionIntroStatic`: full-map framing, worm docked in the bottom content lane, all berries visible, question panel in fixed position, timer running. The first movement input transitions to `DynamicExplore`.
- `DynamicExplore` keeps the existing moving worm behavior and follow framing. It is acceptable for long worms to extend outside the frame here, because the full content becomes readable in static mode.
- When the head enters berry interaction range, transition to `ChoiceHover`: lock movement, tint the berry yellow, tint the minimap marker yellow if the minimap is present, and show a floating `Enter` prompt with a 4-second countdown. The countdown visual should remove one yellow border edge per second, matching the slide note.
- If the player presses `Enter`, resolve the choice immediately. If the 4-second window expires without `Enter`, transition to `StaticInspect` facing that berry. Movement input from `StaticInspect` returns to `DynamicExplore`; if the player keeps pushing into the berry, it remains a solid collider and is not auto-consumed.
- `StaticInspect` is the default non-moving presentation state both at question start and after each confirmed correct pick. It uses the same world and the same worm, but wide framing plus a docked bottom-lane pose so the question content is readable alongside the full map.
- Extend `SnakeTail` and `SingleTail` with a pose system instead of using path-follow only. Add `TailPoseMode` with `PathFollow`, `DockedRow`, and `ReflectionCoil`. In non-dynamic modes, segments lerp to deterministic target slots rather than following movement history.
- Add serialized presentation profiles for camera size/position, worm scale, segment spacing, and text/image scale per mode. This is where the static-mode mulberry/font resizing and worm shrinking live, instead of hardcoding values per script.
- Add a small head-expression controller so the existing worm can switch among neutral, happy, dizzy, sleeping-happy, and sleeping-sad face states during immediate feedback and reflection.
- Extend `OrderItem` with explicit visual states: `Default`, `HoverCandidate`, `ConfirmedCorrect`, `ConfirmedWrong`, and `ConsumedHidden`. These states must drive berry outline/color and any mirrored minimap state.
- Correct choice flow: berry turns green, worm face turns happy, the next body segment fills, `+5s` is added to the current question timer, a floating `+5s` text with clock icon rises/fades over about half a second, and the green berry disappears after a short hold. If more items remain, return to `StaticInspect`; if this was the last item, enter success reflection.
- Wrong choice flow for this milestone: always use the no-potion branch. Show an immediate dizzy/red fail screen for about one second, then enter failure reflection. Add scaffold-only runtime fields and events for future potion count, rescue-beetle arrival, and resume-question behavior, but do not implement that branch now.
- Time-up should use the same failure entry and failure reflection pipeline, with failure copy and no progress gain.
- Success reflection should happen in the same world: dim overlay, zoom-in, same worm posed into a coiled `ח`-like shape sized by segment count, content/labels fade in on the body, face changes to sleeping-happy, then segments are marked green one by one from the head-adjacent filled segment to the last filled segment. Each mark gets a green check and an ascending-pitch success cue.
- After all success marks appear, drain the filled body segments into the progress meter from last filled back to first filled. Each drained segment contributes `1 / totalQuestions / itemsInQuestion` of the full run. Only after the drain finishes should the question count and progress fill become final. If there are no unresolved questions left, branch to the win summary instead of the next question.
- Failure reflection should also happen in the same world: dim overlay, zoom-in, same worm posed into the coiled layout, dizzy eyes during the intro, collected content plus empty placeholders fade in, placeholders start with neutral grey borders, then markings replay in order until the first incorrect segment is marked last with a red border/text and a red `X`.
- After that red mark, spawn the butterfly guide, speech bubble, and a hold button labeled `אנסה מחדש מחר בבוקר`. This stage can persist indefinitely so the player can study the reflection.
- On clicking that button, advance to the failure fade-out stage: marked content fades back to placeholders, progress stays unchanged, face becomes sleeping-sad, butterfly says good night, and the CTA changes to `המשך ליום הבא ->`. That second button starts the transition to the next question/day.
- After any failure reflection completes, keep the failed question unresolved and return to the existing open unresolved-question pool. Do not force the same question to repeat.
- Supporting UI that should be implemented because these states depend on it: a simple minimap with player arrow and item markers, night/dim overlay, butterfly speech bubble, check/X markers, reflection CTA buttons, and real mute-button state wiring against the existing scene audio source.
- Urgency polish should ship in the same milestone because it appears in both static and dynamic slides: timer text turns red at `<= 5s`, urgent audio mode starts below `10s`, and the existing music source should be reused rather than replaced with a new audio architecture.

**Test Plan**
- Start a question and verify the initial state is static/full-map; first movement input enters dynamic.
- Enter berry proximity and verify yellow hover, floating `Enter`, 4-second countdown, and movement lock; let it expire and verify transition to static inspection; move again and verify return to dynamic.
- Confirm a correct berry and verify green berry, happy face, `+5s` timer bonus, floating text fade, filled next segment, berry removal, and return to static.
- Finish a question correctly and verify success reflection order: coil, fade-in, green checks in collection order, segment-drain into progress, then next question or win.
- Confirm a wrong berry and verify immediate dizzy/red fail screen, failure reflection with prior-correct segments green, first wrong segment red/X last, butterfly/button hold, fade-out to placeholders, unchanged progress, then next question selected from unresolved pool.
- Let the timer expire and verify it follows the failure reflection path without progress gain.
- Validate min/max item counts: minimal questions still coil cleanly; maximal questions may overflow the dynamic camera but remain readable in static and reflection states.
- Verify pause/mute cannot break or skip queued transitions, and that replaying after failure still spawns a clean worm/body snapshot.

**Assumptions and Explicit Defaults**
- Architecture is single-world only. Multiple cameras or virtual cameras inside the same scene are allowed, but there is no duplicated map and no separate reflection scene.
- Timer intentionally stays per-question even though the slides describe a run-level timer. The `+5s` bonus applies to the current question timer.
- Potion rescue is intentionally scaffolded only for this milestone. All live failure behavior uses the no-potion reflection branch.
- `ScreenStatus` can remain as a debug fallback, but it is no longer the player-facing end-question UX once these flows are implemented.
