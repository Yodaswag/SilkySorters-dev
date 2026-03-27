# Step By Step For Ori

This document is meant to be used while the Unity editor is open.
It is not a design pitch. It is an implementation guide.

The goal is to move the current prototype from:

- dynamic movement only
- immediate text-only success/fail screens
- labels implemented as actual worm segments

to a flow that is much closer to the GDD:

- static inspection mode
- dynamic exploration mode
- timed berry hover/choice mode
- immediate correct/incorrect feedback
- reflection transitions with worm coiling
- success reflection
- failure reflection
- the supporting UI/features those states depend on

## Assumptions For This Guide

These are the working decisions this guide assumes:

- We stay in **one scene / one world**. No duplicated static map.
- We keep the current **per-question timer** model, not the GDD's global run timer.
- A correct berry gives **+5 seconds to the current question timer**.
- On failure, the question stays unresolved and returns to the **open question pool**, just like the current prototype behavior.
- Potion rescue is **not implemented now**. We only scaffold for it.
- Static inspection should happen **when the player is not moving**.
- There should be an Inspector checkbox that changes this behavior so static inspection only happens when the player presses **M**.

## Important Project Reality Check Before You Start

Current project reality:

- `Assets/Scripts/GameManager.cs` currently owns too much of the flow.
- `Assets/Scripts/SnakeMove.cs` does not expose enough state for "idle => static inspection".
- `Assets/Scripts/SnakeTail.cs` currently treats start/end labels as real worm segments.
- `Assets/Scripts/OrderItem.cs` has only touched/highlight behavior, not a full visual-state system.
- `Assets/Scenes/MainGame.unity` currently has one Cinemachine camera named `CinemachineCamera`, a scene object named `Stage` with the `GameManager`, a `Canvas`, and a `MusicAudioSource`.

The biggest refactor you should accept early is this:

> The worm body should represent only the ordered answers.
> The "start" and "end" labels should become separate tag visuals, not actual body segments.

If you do not make this change early, every later static/reflection layout will fight you.

---

## Recommended Implementation Order

Do the work in this order:

1. Refactor the worm so labels are not body segments.
2. Add a proper question phase/state machine to `GameManager`.
3. Expose movement/idle state from `SnakeMove`.
4. Add static inspection mode with the idle/M toggle.
5. Add hover-choice mode with Enter prompt + timer.
6. Add immediate success/fail feedback.
7. Add a reflection snapshot model.
8. Add worm presentation modes: dynamic row/path, static docked row, reflection coil.
9. Implement success reflection.
10. Implement failure reflection.
11. Add the supporting UI/features from the GDD.
12. Remove the old "ScreenStatus is the main UX" behavior.

Do not try to jump straight to the reflection animation before steps 1-6 compile and work.

---

# Part 1 - Refactor The Worm Body So Labels Are Not Segments

## Why this comes first

The GDD screenshots show:

- body slots for the actual ordered answers
- small label tags above the body
- clean static and reflection layouts

Right now, the code creates:

- start label as a body segment
- placeholders as body segments
- end label as a body segment

That will make static inspection and reflection much harder than they need to be.

## Unity actions

1. Open `Assets/Prefabs/Silky.prefab`.
2. Under the root `Silky`, create an empty child named `BoundaryLabels`.
3. Under `BoundaryLabels`, create two children:
   - `StartLabelTag`
   - `EndLabelTag`
4. On each tag object, add:
   - `TextMeshPro`
   - a small background sprite if you want an actual pill/tag look now
5. Position them loosely above the worm for now. Final positioning will be scripted.

## Script changes

### Change `SnakeTail` so the answer slots are only the ordered answers

The idea is:

- `GameManager` should no longer call `AddLabel(...)` as part of body construction.
- `SnakeTail` should create exactly `question.orderedAnswers.Count` visual slots.
- boundary labels should be handled separately.

Use this as the new shape of the API:

```csharp
using TMPro;
using UnityEngine;
using static DataModels;

public class SnakeTail : MonoBehaviour
{
    [Header("Boundary Labels")]
    [SerializeField] private TMP_Text startLabelText;
    [SerializeField] private TMP_Text endLabelText;

    public void ConfigureBoundaryLabels(
        string startLabel,
        bool startLabelRtl,
        string endLabel,
        bool endLabelRtl)
    {
        RTLFixer.SetTextInTMP(startLabelText, startLabel, startLabelRtl);
        RTLFixer.SetTextInTMP(endLabelText, endLabel, endLabelRtl);
    }

    public void BuildAnswerSlots(int answerCount)
    {
        ClearBodySlots();

        for (int i = 0; i < answerCount; i++)
        {
            AddTail();
        }

        SetNextPlaceholder();
    }

    private void ClearBodySlots()
    {
        foreach (Transform child in transform)
        {
            if (child.name.Contains("SingleTailPrefab"))
            {
                Destroy(child.gameObject);
            }
        }

        snakeTail.Clear();
        answersProvided.Clear();
        positions.Clear();
        positions.Add(snakeHeadGfx.position);
    }
}
```

### Update `GameManager.ResetPlayer()`

Replace the current label-as-segment setup with:

```csharp
void ResetPlayer()
{
    playerInstance = Instantiate(silkyPlayerPrefab);
    snakeTail = playerInstance.GetComponent<SnakeTail>();
    snakeTail.gameManager = this;

    vcam.Follow = playerInstance.transform;
    vcam.LookAt = playerInstance.transform;

    playerInstance.GetComponent<SnakeGrow>().gameManager = this;

    snakeTail.ConfigureBoundaryLabels(
        currentQuestion.orderStartLabel,
        currentQuestion.isStartLabelRTL,
        currentQuestion.orderEndLabel,
        currentQuestion.isEndLabelRTL);

    snakeTail.BuildAnswerSlots(currentQuestion.orderedAnswers.Count);
}
```

## Done criteria for this part

- The worm body length matches exactly the number of answers in the question.
- The start/end labels are visible as separate tags, not body cells.
- The current game still compiles and can start a question.

---

# Part 2 - Add A Real Question Phase State Machine

## Why this comes second

The current project goes:

- move
- eat
- immediate success/fail text

The GDD needs:

- intro static
- dynamic
- hover choice
- static inspect
- immediate feedback
- reflection
- next question

That means the current `GameManager` methods need to stop behaving like final outcomes and start behaving like phase transitions.

## Unity actions

None yet. This step is script-first.

## Script changes

Create a small runtime model inside `GameManager.cs` or in a new script called `QuestionFlowTypes.cs`.

```csharp
public enum QuestionPhase
{
    QuestionIntroStatic,
    DynamicExplore,
    ChoiceHover,
    StaticInspect,
    ImmediateSuccessFeedback,
    ImmediateFailureFeedback,
    ReflectionSuccess,
    ReflectionFailureIntro,
    ReflectionFailureHold,
    ReflectionFailureFadeOut,
    TransitionNextQuestion,
    Paused,
    GameWon
}

public enum QuestionOutcome
{
    None,
    Success,
    WrongChoice,
    TimeUp
}
```

Add these fields to `GameManager`:

```csharp
[Header("Question Phase")]
[SerializeField] private QuestionPhase currentPhase = QuestionPhase.QuestionIntroStatic;
[SerializeField] private bool manualStaticInspectionOnly = false;
[SerializeField] private KeyCode staticInspectionToggleKey = KeyCode.M;
[SerializeField] private float idleBeforeStaticSeconds = 0.25f;

private float idleTimer;
private SnakeMove snakeMove;
private QuestionOutcome pendingOutcome = QuestionOutcome.None;
```

Add one central phase switch method:

```csharp
private void SetPhase(QuestionPhase nextPhase)
{
    currentPhase = nextPhase;

    switch (currentPhase)
    {
        case QuestionPhase.QuestionIntroStatic:
        case QuestionPhase.StaticInspect:
            SetMovementEnabled(false);
            break;

        case QuestionPhase.DynamicExplore:
            SetMovementEnabled(true);
            break;

        case QuestionPhase.ChoiceHover:
        case QuestionPhase.ImmediateSuccessFeedback:
        case QuestionPhase.ImmediateFailureFeedback:
        case QuestionPhase.ReflectionSuccess:
        case QuestionPhase.ReflectionFailureIntro:
        case QuestionPhase.ReflectionFailureHold:
        case QuestionPhase.ReflectionFailureFadeOut:
            SetMovementEnabled(false);
            break;
    }
}

private void SetMovementEnabled(bool enabled)
{
    if (snakeMove != null)
    {
        snakeMove.SetMovementEnabled(enabled);
    }
}
```

## Important note

Do not delete `QuestionSuccess()`, `QuestionFailed()`, and `TimeIsUp()` yet.
First change them so they only request phase changes instead of ending the whole question in one jump.

---

# Part 3 - Expose Idle / Movement State From `SnakeMove`

## Why

You asked for:

- static inspection when the player is not moving
- optional override so static inspection only happens when the player presses `M`

You cannot do that cleanly unless `GameManager` can ask `SnakeMove`:

- is there movement input?
- is movement currently enabled?

## Script changes in `Assets/Scripts/SnakeMove.cs`

Add a real movement lock and expose state:

```csharp
using UnityEngine;

public class SnakeMove : MonoBehaviour
{
    public bool MovementEnabled { get; private set; } = true;
    public bool HasMovementInput => targetInput.sqrMagnitude > 0.01f;
    public bool IsActuallyMoving => currentInputVector.sqrMagnitude > 0.01f;

    public void SetMovementEnabled(bool enabled)
    {
        MovementEnabled = enabled;

        if (!enabled)
        {
            targetInput = Vector2.zero;
            currentInputVector = Vector2.zero;
            smoothInputVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        if (!MovementEnabled)
        {
            targetInput = Vector2.zero;
            return;
        }

        targetInput = inputActions.Player.Move.ReadValue<Vector2>();
    }

    private void HandleMovement()
    {
        if (!MovementEnabled)
        {
            return;
        }

        // existing movement code here
    }
}
```

## Unity actions

1. Open `Assets/Prefabs/Silky.prefab`.
2. Confirm the `SnakeMove` component is still on the root `Silky` object.
3. No inspector wiring changes should be needed for this step.

## Done criteria

- Movement can be disabled and re-enabled from `GameManager`.
- `GameManager` can check whether the player is moving or idle.

---

# Part 4 - Implement Static Inspection Mode First

This is the first real feature you asked to prioritize.

## Why this is the right first feature

Static inspection is the foundation for:

- question intro
- content readability
- success feedback readability
- reflection layout reuse

If static inspection works well, reflection becomes much easier because it reuses the same idea:
freeze gameplay, switch presentation, show content clearly.

## Scene setup

### Camera setup

Use multiple Cinemachine cameras in the same scene.
This keeps the implementation much easier and still respects the "single world" decision.

## Unity actions

1. Open `Assets/Scenes/MainGame.unity`.
2. Rename the current `CinemachineCamera` to `DynamicVcam`.
3. Duplicate `DynamicVcam` twice.
4. Rename the duplicates to:
   - `StaticVcam`
   - `ReflectionVcam`
5. Create three empty scene objects:
   - `DynamicViewAnchor`
   - `StaticViewAnchor`
   - `ReflectionViewAnchor`
6. Put those anchors under the scene object `Stage` so they are easy to find.
7. Set up the cameras:
   - `DynamicVcam` should keep following the player.
   - `StaticVcam` should frame the whole map and the lower presentation lane.
   - `ReflectionVcam` should be a tighter zoom for the coiled worm.
8. Use priority changes from code instead of enabling/disabling cameras by hand.

### Add the lower presentation lane

1. Select `Canvas`.
2. Create a new `Image` named `StaticInspectionLaneBG`.
3. Anchor it to the bottom of the screen.
4. Give it the soft turquoise band color from the mockups.
5. Keep it disabled at first.

## Script changes

You need one presentation helper script on the player prefab.
Create `Assets/Scripts/WormPresentationController.cs`.

```csharp
using System.Collections.Generic;
using UnityEngine;
using static DataModels;

public enum WormPresentationMode
{
    DynamicPath,
    StaticDockedRow,
    ReflectionCoil
}

public enum WormFaceState
{
    Neutral,
    Happy,
    Dizzy,
    SleepHappy,
    SleepSad
}

public class WormPresentationController : MonoBehaviour
{
    [SerializeField] private SnakeTail snakeTail;
    [SerializeField] private Transform dockedRoot;
    [SerializeField] private Transform reflectionRoot;

    public WormPresentationMode CurrentMode { get; private set; }

    public void SetMode(WormPresentationMode mode)
    {
        CurrentMode = mode;

        switch (mode)
        {
            case WormPresentationMode.DynamicPath:
                snakeTail.SetPoseMode(TailPoseMode.PathFollow);
                break;

            case WormPresentationMode.StaticDockedRow:
                snakeTail.SetPoseMode(TailPoseMode.DockedRow);
                snakeTail.SetPresentationSlots(BuildDockedRowSlots());
                break;

            case WormPresentationMode.ReflectionCoil:
                snakeTail.SetPoseMode(TailPoseMode.ReflectionCoil);
                snakeTail.SetPresentationSlots(BuildReflectionCoilSlots());
                break;
        }
    }

    private List<Vector3> BuildDockedRowSlots()
    {
        // fill based on answer count and desired lane spacing
        return new List<Vector3>();
    }

    private List<Vector3> BuildReflectionCoilSlots()
    {
        // fill later in the reflection step
        return new List<Vector3>();
    }
}
```

Now add the idle/manual static inspection transition logic to `GameManager`.

```csharp
private void UpdateQuestionPresentationState()
{
    if (snakeMove == null || currentPhase != QuestionPhase.DynamicExplore)
        return;

    if (manualStaticInspectionOnly)
    {
        if (Input.GetKeyDown(staticInspectionToggleKey))
        {
            EnterStaticInspectionMode();
        }

        return;
    }

    if (snakeMove.IsActuallyMoving)
    {
        idleTimer = 0f;
        return;
    }

    idleTimer += Time.deltaTime;

    if (idleTimer >= idleBeforeStaticSeconds)
    {
        EnterStaticInspectionMode();
    }
}

private void EnterStaticInspectionMode()
{
    idleTimer = 0f;
    SetPhase(QuestionPhase.StaticInspect);
    SetCameraMode(CameraMode.StaticInspect);
    staticInspectionLaneBg.SetActive(true);
    wormPresentation.SetMode(WormPresentationMode.StaticDockedRow);
}

private void ExitStaticInspectionMode()
{
    SetPhase(QuestionPhase.DynamicExplore);
    SetCameraMode(CameraMode.Dynamic);
    staticInspectionLaneBg.SetActive(false);
    wormPresentation.SetMode(WormPresentationMode.DynamicPath);
}
```

Then in `Update()`:

```csharp
private void Update()
{
    UpdateQuestionPresentationState();

    if (currentPhase == QuestionPhase.StaticInspect && snakeMove != null)
    {
        if (manualStaticInspectionOnly && Input.GetKeyDown(staticInspectionToggleKey))
        {
            ExitStaticInspectionMode();
        }
        else if (!manualStaticInspectionOnly && snakeMove.HasMovementInput)
        {
            ExitStaticInspectionMode();
        }
    }

    // keep the rest of your timer / pause / restart handling
}
```

## Inspector setup

When you finish this part:

1. Select `Stage`.
2. In the `GameManager` component, verify these new serialized fields are visible:
   - `Manual Static Inspection Only`
   - `Static Inspection Toggle Key`
   - `Idle Before Static Seconds`
3. Leave the checkbox **off** by default.
4. Set the key to `M`.

## Done criteria

- If the checkbox is off, releasing movement puts the game into static inspection after a short delay.
- If the checkbox is on, static inspection only happens when `M` is pressed.
- The same key or movement input can return to dynamic exploration.

---

# Part 5 - Add Timed Hover Choice Mode

## Why

The GDD has a real "I am near a berry and can choose it" state.
That is not the same thing as either dynamic movement or static inspection.

You need a distinct phase for:

- yellow berry highlight
- floating Enter prompt
- 4-second countdown
- later green/red confirmation result

## Unity actions

1. Select `Canvas`.
2. Create a new UI group named `ChoicePromptUI`.
3. Inside it create:
   - `EnterPromptText`
   - `ChoiceCountdownFrame`
4. Keep the whole group disabled by default.
5. If you want the easiest first version, make the countdown visual just a text label first.
6. After the logic works, replace it with the disappearing 4-edge frame from the GDD.

## Script changes

Create a small HUD controller: `Assets/Scripts/QuestionHudController.cs`

```csharp
using TMPro;
using UnityEngine;

public class QuestionHudController : MonoBehaviour
{
    [SerializeField] private CanvasGroup choicePromptGroup;
    [SerializeField] private TMP_Text choicePromptText;

    public void ShowChoicePrompt(float secondsLeft)
    {
        choicePromptGroup.alpha = 1f;
        choicePromptGroup.gameObject.SetActive(true);
        choicePromptText.text = $"Press Enter ({Mathf.CeilToInt(secondsLeft)}s)";
    }

    public void HideChoicePrompt()
    {
        choicePromptGroup.alpha = 0f;
        choicePromptGroup.gameObject.SetActive(false);
    }
}
```

Add hover tracking to `GameManager`:

```csharp
[Header("Choice Hover")]
[SerializeField] private float choiceHoverDuration = 4f;
private float choiceHoverRemaining;
private OrderItem hoveredOrderItem;
```

When the player enters berry range, switch to hover mode:

```csharp
public void EnterChoiceHover(OrderItem item)
{
    hoveredOrderItem = item;
    choiceHoverRemaining = choiceHoverDuration;

    item.SetVisualState(OrderItemVisualState.HoverCandidate);
    SetPhase(QuestionPhase.ChoiceHover);
    hud.ShowChoicePrompt(choiceHoverRemaining);
}
```

Update the countdown:

```csharp
private void UpdateChoiceHover()
{
    if (currentPhase != QuestionPhase.ChoiceHover || hoveredOrderItem == null)
        return;

    choiceHoverRemaining -= Time.deltaTime;
    hud.ShowChoicePrompt(choiceHoverRemaining);

    if (Input.GetKeyDown(KeyCode.Return))
    {
        ConfirmHoveredChoice();
        return;
    }

    if (choiceHoverRemaining <= 0f)
    {
        EnterStaticInspectionMode();
        hoveredOrderItem.SetVisualState(OrderItemVisualState.Default);
        hoveredOrderItem = null;
        hud.HideChoicePrompt();
    }
}
```

## Important implementation note

Do **not** let `SnakeGrow` directly decide success/failure anymore.
`SnakeGrow` should become a detector that tells `GameManager`:

- "player is near this item"
- "player left this item"
- "player confirmed this item"

That is much closer to the GDD flow.

---

# Part 6 - Add Immediate Correct / Incorrect Feedback

## Why

Before the reflection sequence starts, the GDD still has immediate feedback:

- correct: green berry, happy worm, `+5s`, then berry disappears
- wrong: dizzy worm, red berry, short failure beat, then reflection

This feedback should happen before the big reflection state.

## Script changes

### Extend `OrderItem`

Replace the current touched/highlight-only idea with a proper visual state enum:

```csharp
public enum OrderItemVisualState
{
    Default,
    HoverCandidate,
    ConfirmedCorrect,
    ConfirmedWrong,
    Hidden
}
```

Add a setter like:

```csharp
public void SetVisualState(OrderItemVisualState state)
{
    switch (state)
    {
        case OrderItemVisualState.Default:
            highlight.SetActive(false);
            break;

        case OrderItemVisualState.HoverCandidate:
            highlight.SetActive(true);
            // yellow
            break;

        case OrderItemVisualState.ConfirmedCorrect:
            highlight.SetActive(true);
            // green
            break;

        case OrderItemVisualState.ConfirmedWrong:
            highlight.SetActive(true);
            // red
            break;

        case OrderItemVisualState.Hidden:
            gameObject.SetActive(false);
            break;
    }
}
```

### Add a floating time reward

In `QuestionHudController`:

```csharp
public IEnumerator PlayTimeReward(string text)
{
    floatingRewardText.text = text;
    floatingRewardText.gameObject.SetActive(true);

    float t = 0f;
    Vector3 start = floatingRewardText.rectTransform.anchoredPosition;
    Vector3 end = start + new Vector3(0f, 30f, 0f);

    while (t < 0.5f)
    {
        t += Time.deltaTime;
        float alpha = 1f - (t / 0.5f);
        floatingRewardText.rectTransform.anchoredPosition = Vector3.Lerp(start, end, t / 0.5f);
        floatingRewardText.alpha = alpha;
        yield return null;
    }

    floatingRewardText.gameObject.SetActive(false);
}
```

### Update correct resolution in `GameManager`

```csharp
private IEnumerator ResolveCorrectChoice(OrderItem item)
{
    SetPhase(QuestionPhase.ImmediateSuccessFeedback);

    item.SetVisualState(OrderItemVisualState.ConfirmedCorrect);
    wormPresentation.SetFace(WormFaceState.Happy);

    currentGameTime += 5f;
    UpdateTimerUI();

    snakeTail.AddAnswer(item.answer);

    StartCoroutine(hud.PlayTimeReward("+5s"));

    yield return new WaitForSeconds(0.5f);

    item.SetVisualState(OrderItemVisualState.Hidden);

    if (snakeTail.GetAnswersProvided().Count >= currentQuestion.orderedAnswers.Count)
    {
        StartSuccessReflection();
    }
    else
    {
        EnterStaticInspectionMode();
    }
}
```

### Update wrong resolution in `GameManager`

```csharp
private IEnumerator ResolveWrongChoice(OrderItem item)
{
    SetPhase(QuestionPhase.ImmediateFailureFeedback);

    item.SetVisualState(OrderItemVisualState.ConfirmedWrong);
    wormPresentation.SetFace(WormFaceState.Dizzy);

    yield return new WaitForSeconds(1f);

    StartFailureReflection(QuestionOutcome.WrongChoice, item.answer);
}
```

## Done criteria

- Correct choices feel like a beat, not an instant hard cut.
- Wrong choices also feel like a beat before reflection starts.

---

# Part 7 - Add A Reflection Snapshot Model Before You Animate Anything

## Why

Reflection needs stable data.
Do not try to build reflection by reading "whatever the current scene happens to look like" in the middle of a transition.

Build a snapshot object first.

## Script changes

Create `Assets/Scripts/QuestionReflectionSnapshot.cs`

```csharp
using System.Collections.Generic;
using static DataModels;

public class QuestionReflectionSnapshot
{
    public QuestionModel question;
    public List<AnswerModel> collectedAnswers = new();
    public AnswerModel wrongAnswer;
    public QuestionOutcome outcome;
    public float remainingTime;
}
```

In `GameManager`, build it at the moment the question ends:

```csharp
private QuestionReflectionSnapshot BuildReflectionSnapshot(
    QuestionOutcome outcome,
    AnswerModel wrongAnswer = null)
{
    return new QuestionReflectionSnapshot
    {
        question = currentQuestion,
        collectedAnswers = new List<AnswerModel>(snakeTail.GetAnswersProvided()),
        wrongAnswer = wrongAnswer,
        outcome = outcome,
        remainingTime = currentGameTime
    };
}
```

This snapshot should be the thing you pass into the reflection sequence.

---

# Part 8 - Add Worm Presentation Modes For Reflection

## Why

Reflection is not just "freeze the current worm".
It is a different presentation mode:

- zoomed in
- coiled body
- clear readable content
- neutral/green/red borders
- sleeping or dizzy face

## Unity actions

1. In `Silky.prefab`, add an empty child named `DockedSlotRoot`.
2. Add another empty child named `ReflectionSlotRoot`.
3. Create a few editor gizmos or placeholder child empties if that helps you visualize positions while tuning.

## Script changes

In `SnakeTail`, add a pose mode:

```csharp
public enum TailPoseMode
{
    PathFollow,
    DockedRow,
    ReflectionCoil
}

private TailPoseMode poseMode = TailPoseMode.PathFollow;
private readonly List<Vector3> presentationSlots = new();

public void SetPoseMode(TailPoseMode mode)
{
    poseMode = mode;
}

public void SetPresentationSlots(List<Vector3> slots)
{
    presentationSlots.Clear();
    presentationSlots.AddRange(slots);
}
```

Then in `SingleTail.Update()`:

```csharp
void Update()
{
    if (manager == null) return;

    if (manager.CurrentPoseMode != TailPoseMode.PathFollow)
    {
        Vector3 slot = manager.GetPresentationSlot(myIndex);
        transform.position = Vector3.Lerp(transform.position, slot, Time.deltaTime * 8f);
        transform.rotation = Quaternion.identity;
        return;
    }

    // existing path-follow behavior
}
```

## Reflection coil layout rule

Do not hardcode a different layout per question.
Use one function that generates a readable coiled shape from the slot count.

The first version can be simple:

- head on upper-right
- several segments across the upper row
- one vertical drop
- the rest on a lower row

That is enough to match the spirit of the mockups.

---

# Part 9 - Implement Success Reflection

## Why

Success reflection has three jobs:

1. show the whole completed answer clearly
2. celebrate learning with sequential green confirmation
3. move progress only now, at the end of the question

## Unity actions

1. Select `Canvas`.
2. Create a `DimNightOverlay` image covering the screen.
3. Create a `ReflectionCheckmarkPrefab` if you have an icon.
4. If you do not have final art yet, use a TMP text object with `✓` as a placeholder.

## Script changes

Add methods to `QuestionHudController`:

```csharp
public void SetNightOverlayVisible(bool visible)
{
    dimNightOverlay.gameObject.SetActive(visible);
}

public void SpawnCheckmark(Vector3 worldPosition)
{
    // instantiate UI/world-space marker
}
```

In `GameManager`, success reflection should be a coroutine:

```csharp
private IEnumerator RunSuccessReflection(QuestionReflectionSnapshot snapshot)
{
    SetPhase(QuestionPhase.ReflectionSuccess);
    SetCameraMode(CameraMode.Reflection);

    hud.SetNightOverlayVisible(true);
    wormPresentation.SetMode(WormPresentationMode.ReflectionCoil);
    wormPresentation.SetFace(WormFaceState.SleepHappy);

    yield return new WaitForSeconds(0.35f);

    for (int i = 0; i < snapshot.collectedAnswers.Count; i++)
    {
        snakeTail.MarkSlotCorrect(i);
        hud.SpawnCheckmark(snakeTail.GetSlotWorldPosition(i));
        PlaySuccessPitchStep(i);
        yield return new WaitForSeconds(0.2f);
    }

    yield return RunProgressDrain(snapshot.collectedAnswers.Count);

    CommitQuestionSuccessAfterReflection();
}
```

## Important logic rule

Do not increment `questionNumber` and do not call `allQuestions.Remove(currentQuestion)` at the moment the last berry is eaten.

Do those only after success reflection finishes.

That means the current `QuestionSuccess()` method should become:

- build snapshot
- start success reflection

and **not** final-commit the question immediately.

## Progress drain rule

Because we are keeping per-question timer but still want the GDD's end-question progress behavior:

- each question still counts as one unit of final progress
- each body segment can visually drain into the progress meter one by one
- only after the drain ends does the actual question-progress commit happen

If you want the easiest implementation:

- animate each segment flying to the bar
- only update the bar fill at the end

If you want a closer match to the GDD:

- update the bar a little after every drained segment

---

# Part 10 - Implement Failure Reflection

## Why

Failure reflection is not just "show red".
It needs to:

- preserve what the player got right
- show the first wrong item clearly
- pause long enough for reflection
- then reset visually for the next day/question

## Unity actions

1. Under `Canvas`, create:
   - `ButterflyGuideRoot`
   - `ButterflySpeechBubble`
   - `ReflectionPrimaryButton`
2. Disable them by default.
3. If you do not have the butterfly art yet, use a placeholder `Image`.
4. If you do not have X art yet, use a TMP text object with `X`.

## Script changes

Add HUD methods:

```csharp
public void ShowButterfly(string line)
{
    butterflyRoot.SetActive(true);
    butterflySpeechText.text = line;
}

public void HideButterfly()
{
    butterflyRoot.SetActive(false);
}

public void ShowReflectionButton(string label, UnityEngine.Events.UnityAction action)
{
    reflectionButton.gameObject.SetActive(true);
    reflectionButtonText.text = label;
    reflectionButton.onClick.RemoveAllListeners();
    reflectionButton.onClick.AddListener(action);
}
```

### Failure reflection sequence

Use this order:

1. Immediate fail beat already happened.
2. Enter reflection camera + dim overlay.
3. Pose worm into reflection coil.
4. Show collected answers and empty placeholders.
5. Mark correct slots green in order.
6. Mark the first wrong slot red with an `X`.
7. Show butterfly line and `אנסה מחדש מחר בבוקר`.
8. Wait until the player clicks.
9. Fade the body back to placeholder state.
10. Change butterfly line to a good-night line.
11. Change button to `המשך ליום הבא ->`.
12. On click, advance to the next question from the unresolved pool.

### Sample coroutine shape

```csharp
private IEnumerator RunFailureReflection(QuestionReflectionSnapshot snapshot)
{
    SetPhase(QuestionPhase.ReflectionFailureIntro);
    SetCameraMode(CameraMode.Reflection);

    hud.SetNightOverlayVisible(true);
    wormPresentation.SetMode(WormPresentationMode.ReflectionCoil);
    wormPresentation.SetFace(WormFaceState.Dizzy);

    yield return new WaitForSeconds(0.35f);

    for (int i = 0; i < snapshot.collectedAnswers.Count; i++)
    {
        snakeTail.MarkSlotCorrect(i);
        hud.SpawnCheckmark(snakeTail.GetSlotWorldPosition(i));
        yield return new WaitForSeconds(0.2f);
    }

    int wrongIndex = snapshot.collectedAnswers.Count;
    snakeTail.MarkSlotWrong(wrongIndex, snapshot.wrongAnswer);
    hud.SpawnWrongMark(snakeTail.GetSlotWorldPosition(wrongIndex));

    SetPhase(QuestionPhase.ReflectionFailureHold);

    hud.ShowButterfly("אוי, אכלת תות שהבטן עדיין לא הייתה מוכנה לעכל.");
    hud.ShowReflectionButton("אנסה מחדש מחר בבוקר", BeginFailureFadeOut);
}
```

### Important note about timeout

`TimeIsUp()` should also end in failure reflection.

The easiest first version is:

- no wrong berry content shown
- next unfilled slot stays neutral/empty
- butterfly line explains that time ran out

Do not postpone the timeout path until the end.
If you leave it disconnected, the state machine will feel inconsistent.

---

# Part 11 - Supporting Features From The GDD That Should Be Done After The Core Flow Works

These are not "nice to have". They are part of making the static/reflection flow feel like the GDD.

## 11.1 - Replace `ScreenStatus` as the main player-facing UX

Right now `ScreenStatus` is doing the heavy lifting.
Once reflection exists:

- keep `ScreenStatus` only as a debug fallback
- stop using it as the primary success/failure UI

## 11.2 - Add a real minimap

### Why

The GDD repeatedly uses the minimap in:

- dynamic exploration
- hover selection
- correct feedback
- maximum-item cases where the full worm does not fit the frame

### Unity actions

1. Under `Canvas`, create `MiniMapPanel`.
2. Add a `RawImage` or plain `Image` as the map background.
3. Add a container for markers.
4. Add simple colored markers:
   - player marker
   - berry markers

### First implementation rule

Do not make a live rendered minimap camera first unless you really need it.
The simplest version is enough:

- define map bounds
- map world positions to UI positions
- move colored UI dots

## 11.3 - Add urgency at low time

From the GDD:

- timer red at 5 seconds
- more stressful audio under 10 seconds

### Use the current scene setup

Use `MusicAudioSource`.
If you do not yet have a second tense music asset, do this first:

- timer color to red at `<= 5`
- slightly raise `MusicAudioSource.pitch` below `10`
- restore pitch when the next question starts

That is a perfectly acceptable first implementation.

## 11.4 - Wire a real mute toggle

The scene already has sound icons.
Add a small audio UI script that:

- toggles `MusicAudioSource.mute`
- swaps between the on/off icon objects
- survives question transitions

## 11.5 - Scaffold potion rescue without implementing it

Add fields like:

```csharp
[Header("Potion Scaffold")]
[SerializeField] private int currentPotionCount = 0;
[SerializeField] private GameObject potionUiRoot;
```

And add a placeholder method:

```csharp
private bool HasPotionRescueAvailable()
{
    return currentPotionCount > 0;
}
```

But for now, always run the no-potion branch.

---

# Part 12 - What To Remove Or Delay From The Current Code

These are the places where the old structure will fight the new flow.

## 12.1 - `SnakeGrow` should stop deciding outcomes directly

Right now it does:

- correct => `QuestionSuccess()`
- wrong => `QuestionFailed()`

That is too aggressive for the GDD.

Refactor it so it only reports to `GameManager`:

- current nearby item
- confirm pressed
- collision enter/exit

Let `GameManager` decide the phase transition.

## 12.2 - Do not destroy the player immediately at end of question

Reflection needs the live worm.

That means:

- do not destroy the player on success/fail immediately
- destroy/rebuild only after reflection completes and the next question is actually starting

## 12.3 - Do not update final progress too early

The progress bar should commit:

- after success reflection drain completes
- not when the last berry is eaten

On failure or timeout:

- no progress gain

---

# Suggested Script List When You Finish The Refactor

If you want a clean target structure, end up roughly here:

- `GameManager.cs` - question lifecycle and phase transitions
- `SnakeMove.cs` - movement + idle exposure + movement lock
- `SnakeTail.cs` - body slots, answer fill, pose mode support
- `SingleTail.cs` - dynamic follow or presentation slot behavior
- `OrderItem.cs` - berry visuals and answer display
- `WormPresentationController.cs` - dynamic/static/reflection worm presentation
- `QuestionHudController.cs` - prompt, overlays, butterfly, reflection buttons, floating rewards
- `MiniMapController.cs` - marker-based minimap
- optional later: `AudioUiController.cs`

---

# Practical Work Rhythm

Use this rhythm while implementing:

1. Make one step compile.
2. Test in Play Mode.
3. Only then continue.

Recommended checkpoints:

- Checkpoint A: labels are no longer body segments
- Checkpoint B: static inspection works with idle and with M-only mode
- Checkpoint C: hover prompt/countdown works
- Checkpoint D: correct and wrong immediate feedback works
- Checkpoint E: success reflection works
- Checkpoint F: failure reflection works
- Checkpoint G: minimap / mute / urgency polish works

---

# Final "Done" Checklist

You are done when all of these are true:

- The worm can switch between dynamic movement, static inspection row, and reflection coil.
- Static inspection can be triggered by idle movement or by `M`, controlled by a serialized checkbox.
- The question starts in static inspection.
- Dynamic mode returns when the player moves.
- Hovering near a berry shows yellow selection UI and a 4-second Enter countdown.
- Correct berries give green feedback and `+5s`.
- Wrong berries give red/dizzy feedback before reflection.
- Success reflection shows green sequential checks and only then commits progress.
- Failure reflection preserves correct content, shows the wrong point in red, pauses for butterfly feedback, then resets visually for the next day/question.
- Timeout uses the same reflection logic.
- `ScreenStatus` is no longer the main player-facing flow.

If you want to keep this manageable, stop after each checkpoint and test before moving on.
