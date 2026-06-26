# RemovingAnswersFromBody Implementation Plan

**Goal:** Fill the `RemovingAnswersFromBody` stub with a two-pass body-drain animation: first a green-highlight sweep (head→tail), then a removal sweep (tail→head) that drains segments into the progress bar on success or simply fades them on failure.

**Architecture:** A single coroutine `DrainBodyIntoProgress()` on `GameManager` runs two passes over the body segments. `SnakeMove` launches the coroutine with a bool guard to avoid re-entry from `Update()`. No particle trail for now.

**Tech Stack:** Unity C# (existing `GameManager`, `SnakeMove`, `SnakeTail`, `SingleTail`)

---

## Two-Pass Flow

### Pass 1 — Green highlight sweep (head → tail)

Iterate segments from index 0 (head-side) toward the tail:
- **Success:** All segments are filled → all get `BodySpriteSheet_3` (green) one by one.
- **Failure/Timeout:** Filled segments get green until hitting the first wrong answer → that segment gets `BodySpriteSheet_1` (red flash) → sweep stops.

### Pass 2 — Removal sweep (tail → head)

Iterate segments from last index (tail) back toward head:
- **Success:** Each segment hides its text/image, fades its BG to alpha 0, and increments the progress bar fill per segment.
- **Failure/Timeout:** After the red flash, ALL filled segments simply fade out (alpha → 0). No progress fill. No per-segment animation — just a batch fade.

---

## Proposed Changes

### SnakeTail — Expose segment access

#### [MODIFY] [SnakeTail.cs](file:///c:/Users/user/Documents/HIT%20Coding/Year%20Bet/Semester%20Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/MainGame/Silky/SnakeTail.cs)

Add two public methods near the existing `GetAnswersProvided()` and `GetLength()` at the bottom of the class (~line 141):

```csharp
public int GetSegmentCount() => snakeTail.Count;

public SingleTail GetSegment(int index) => snakeTail[index];
```

---

### GameManager — Add outcome tracking and drain coroutine

#### [MODIFY] [GameManager.cs](file:///c:/Users/user/Documents/HIT%20Coding/Year%20Bet/Semester%20Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/MainGame/General/GameManager.cs)

**1. New fields** (add near the existing `pendingStatusText` / `pendingStatusColor` block, around line 110):

```csharp
[Header("Reflection - Body Drain")]
[SerializeField] private Sprite bodySprite_Correct;   // BodySpriteSheet_3 (green)
[SerializeField] private Sprite bodySprite_Error;      // BodySpriteSheet_1 (red)
[SerializeField] private float highlightStepDelay = 0.2f; // Seconds between each green highlight (Pass 1)
[SerializeField] private float drainStepDelay = 0.25f;    // Seconds between each segment removal (Pass 2)
[SerializeField] private float redFlashDuration = 0.4f;   // How long the red sprite shows on wrong answer
[SerializeField] private float failureFadeDuration = 0.3f; // Duration of the batch fade on failure

private bool lastQuestionWasSuccess;
```

**2. Set `lastQuestionWasSuccess`** in the three outcome methods:

- In `QuestionSuccess()` (line ~449): add `lastQuestionWasSuccess = true;` before `EndQuestion()`.
- In `QuestionFailed()` (line ~460): add `lastQuestionWasSuccess = false;` before `EndQuestion()`.
- In `TimeIsUp()` (line ~492): add `lastQuestionWasSuccess = false;` before `EndQuestion()`.

**3. New coroutine** — `DrainBodyIntoProgress()`:

```csharp
/// <summary>
/// Two-pass body drain animation:
/// Pass 1 (head→tail): Highlight segments green. On failure, stop at wrong answer and flash red.
/// Pass 2 (tail→head): On success, remove segments one by one and fill progress bar.
///                      On failure, batch-fade all segments out.
/// </summary>
public IEnumerator DrainBodyIntoProgress()
{
    int segmentCount = snakeTail.GetSegmentCount();
    int answersCount = snakeTail.GetAnswersProvided().Count;

    // ────────────────────────────────────────────
    // PASS 1: Green highlight sweep (head → tail)
    // ────────────────────────────────────────────
    int greenCount = lastQuestionWasSuccess ? segmentCount : answersCount;

    for (int i = 0; i < greenCount; i++)
    {
        SingleTail segment = snakeTail.GetSegment(i);

        if (!lastQuestionWasSuccess && i == answersCount - 1)
        {
            // This is the wrong answer — flash red
            if (bodySprite_Error != null)
            {
                segment.tailBG.sprite = bodySprite_Error;
                segment.tailBG.color = Color.white;
            }
            yield return new WaitForSeconds(redFlashDuration);
            break; // Stop the sweep here
        }

        // Correct answer — highlight green
        if (bodySprite_Correct != null)
        {
            segment.tailBG.sprite = bodySprite_Correct;
            segment.tailBG.color = Color.white;
        }
        yield return new WaitForSeconds(highlightStepDelay);
    }

    // ────────────────────────────────────────────
    // PASS 2: Removal sweep
    // ────────────────────────────────────────────
    if (lastQuestionWasSuccess)
    {
        // Success: remove segments one by one (tail → head), fill progress incrementally
        float progressPerSegment = (1f / game.questionList.Count) / segmentCount;

        for (int i = segmentCount - 1; i >= 0; i--)
        {
            SingleTail segment = snakeTail.GetSegment(i);

            // Hide content
            if (segment.textComp != null)
                segment.textComp.gameObject.SetActive(false);
            if (segment.imageComp != null)
                segment.imageComp.gameObject.SetActive(false);

            // Fade out background
            segment.tailBG.color = new Color(1f, 1f, 1f, 0f);

            // Increment progress bar
            if (linearProgressFill != null)
            {
                linearProgressFill.fillAmount += progressPerSegment * 0.999f;
            }

            yield return new WaitForSeconds(drainStepDelay);
        }
    }
    else
    {
        // Failure/Timeout: batch-fade all filled segments at once
        // First set all filled segments to fade
        for (int i = 0; i < answersCount; i++)
        {
            SingleTail segment = snakeTail.GetSegment(i);

            if (segment.textComp != null)
                segment.textComp.gameObject.SetActive(false);
            if (segment.imageComp != null)
                segment.imageComp.gameObject.SetActive(false);

            segment.tailBG.color = new Color(1f, 1f, 1f, 0f);
        }

        yield return new WaitForSeconds(failureFadeDuration);
    }
}
```

> [!NOTE]
> On failure, the last filled segment (index `answersCount - 1`) is the wrong answer that caused the failure. The green sweep highlights segments 0 through `answersCount - 2` in green, then flashes `answersCount - 1` in red.
> On timeout, `answersCount` may equal the number of correct answers (no wrong answer was eaten). In that case, all filled segments still go green and then fade — but with no progress fill.

> [!NOTE]
> The `0.999f` multiplier on `progressPerSegment` matches the existing convention in `UpdateProgressBar()`.

---

### GameManager — Timeout edge case

On timeout, the player didn't eat a wrong berry — they just ran out of time. In this case `answersCount` equals the number of correct answers collected so far. The code handles this naturally:
- `lastQuestionWasSuccess = false` → enters the failure branch
- The `i == answersCount - 1` red-flash check: we need to **not** flash the last correct answer red on timeout.

To handle this, add one more field:

```csharp
private bool lastQuestionWasWrongAnswer; // true = wrong berry eaten, false = timeout/pause
```

Set in the outcome methods:
- `QuestionSuccess()`: don't need it (success path doesn't use it)
- `QuestionFailed()`: `lastQuestionWasWrongAnswer = true;`
- `TimeIsUp()`: `lastQuestionWasWrongAnswer = false;`

Then modify the red-flash condition in Pass 1:

```csharp
if (!lastQuestionWasSuccess && lastQuestionWasWrongAnswer && i == answersCount - 1)
{
    // This is the wrong answer — flash red
    ...
}
```

On timeout (no wrong answer), all filled segments simply get green, then batch-fade with no progress. Clean.

---

### SnakeMove — Call the coroutine from the stub

#### [MODIFY] [SnakeMove.cs](file:///c:/Users/user/Documents/HIT%20Coding/Year%20Bet/Semester%20Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/MainGame/Silky/SnakeMove.cs)

Add a field:

```csharp
private bool isDraining = false;
```

Replace the stub (lines 134-137):

```csharp
case GameManager.ReflectionPhases.RemovingAnswersFromBody:
    if (!isDraining)
    {
        isDraining = true;
        StartCoroutine(RunBodyDrain());
    }
    break;
```

Add the coroutine method:

```csharp
private IEnumerator RunBodyDrain()
{
    yield return StartCoroutine(gameManager.DrainBodyIntoProgress());

    // Drain complete — advance to next phase
    isDraining = false;
    gameManager.currentReflectionPhase = GameManager.ReflectionPhases.WaitingForNextQuestion;
    gameManager.RevealScreenStatus();
}
```

---

## Summary of all file changes

| File | Change |
|---|---|
| [SnakeTail.cs](file:///c:/Users/user/Documents/HIT%20Coding/Year%20Bet/Semester%20Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/MainGame/Silky/SnakeTail.cs) | Add `GetSegmentCount()` and `GetSegment(int)` public methods |
| [GameManager.cs](file:///c:/Users/user/Documents/HIT%20Coding/Year%20Bet/Semester%20Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/MainGame/General/GameManager.cs) | Add 7 serialized fields + 2 private bools, set them in 3 outcome methods, add `DrainBodyIntoProgress()` coroutine |
| [SnakeMove.cs](file:///c:/Users/user/Documents/HIT%20Coding/Year%20Bet/Semester%20Bet/Unity/TriangleProject/SilkySorters/Assets/Scripts/MainGame/Silky/SnakeMove.cs) | Add `isDraining` bool, replace stub with guarded coroutine call + `RunBodyDrain()` method |

## Unity Inspector Setup Required After Code Changes

1. Select the **Stage** object (where `GameManager` lives).
2. In the `GameManager` component, find the new **"Reflection - Body Drain"** header.
3. Drag **BodySpriteSheet_3** into `Body Sprite Correct` slot.
4. Drag **BodySpriteSheet_1** into `Body Sprite Error` slot.
5. Tune timing values to taste:
   - `Highlight Step Delay` — 0.2s (speed of green sweep)
   - `Drain Step Delay` — 0.25s (speed of removal on success)
   - `Red Flash Duration` — 0.4s (how long the red shows)
   - `Failure Fade Duration` — 0.3s (batch fade speed on failure)

## Verification Plan

### Manual Verification
1. **Success path:** Complete a question → after coiling, segments highlight green head→tail one by one, then segments vanish tail→head with progress bar filling incrementally per segment.
2. **Failure path (wrong berry):** Eat a wrong berry → after coiling, correct segments highlight green head→tail, wrong answer flashes red, then ALL filled segments fade out simultaneously. Progress bar does NOT move.
3. **Timeout path:** Let timer expire → after coiling, filled segments highlight green (no red flash), then all fade out simultaneously. Progress bar does NOT move.
4. After the drain completes, the "press space to continue" text should appear.
5. Pressing space should proceed to the next question normally.
6. Verify the drain resets properly across multiple questions (the `isDraining` flag clears correctly).
