# Reflection Screens Step-By-Step Implementation Plan

## Summary
- Implement reflection as a deterministic end-of-question pipeline in the same scene: freeze gameplay, auto-move the live worm to a staging point directly below `positioner_PlayerSpawn`, animate it along an authored spline into the spawn position while coiling, darken the screen, remove the collected answers, then fade to full black.
- After the reflection finishes, keep the view on the dynamic camera and start the next question only after the screen is fully black. The new question is then created under black and the screen fades back in.
- Default path choice for v1: use a single semicircle spline. Build the code so the path provider can later be swapped to full-circle or double-circle variants without changing the reflection pipeline.
- Chosen lifecycle for this version: use the live gameplay worm for the auto-move/coiling phase, then rebuild after full black before the next question starts.

## Implementation Changes

### 1. Scene authoring in Unity
- In `MainGame.unity`, under `Stage`, add an empty `ReflectionRig` root.
- Under `ReflectionRig`, create:
  - `ReflectionStartAnchor`
  - `ReflectionSplineRoot`
  - `ReflectionCoilAnchor`
  - `FullScreenFade`
- Position `ReflectionStartAnchor` directly below `positioner_PlayerSpawn` on the same X, with a serialized vertical offset so it can be tuned in the editor.
- Position `ReflectionCoilAnchor` exactly at the final centered upright reflection position. For v1, place it on top of `positioner_PlayerSpawn` or slightly above it if the final composition needs breathing room.
- Add a fullscreen UI `Image` named `FullScreenFade` under the existing `Canvas`. Default color should be black with alpha `0`.
- Add a second fullscreen UI `Image` named `BackgroundDarkenOverlay` behind gameplay HUD but above the background image if you want reusable partial darkening later. This is optional for this plan because the requested sequence ends in full black.
- Keep `dynamicVcam` and `staticVcam` as-is. Reflection for this plan stays on the dynamic camera until the fade reaches black.
- On the worm prefab, add a child `ReflectionVisualRoot` if one does not already exist. This is the transform that will rotate/scale for the coiling pose without disturbing the gameplay root unexpectedly.
- If the worm head sprite is separate, expose a `HeadVisual` transform so orientation can be interpolated cleanly during auto-move.

### 2. New runtime types
Add a small set of reflection-specific types. Keep them focused and testable.

```csharp
public enum ReflectionPhase
{
    None,
    MovingToStartAnchor,
    FollowingSpline,
    Darkening,
    RemovingAnswers,
    FadingToBlack,
    WaitingForNextQuestion
}

public enum ReflectionOutcome
{
    Success,
    Failure,
    Timeout
}

[System.Serializable]
public class ReflectionSettings
{
    public float moveToStartDuration = 0.35f;
    public float splineTravelDuration = 0.8f;
    public float darkenDuration = 0.35f;
    public float removeAnswersStepDuration = 0.12f;
    public float fadeToBlackDuration = 0.45f;
    public float startAnchorYOffset = 2.0f;
    public AnimationCurve travelEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}
```

```csharp
public struct ReflectionContext
{
    public ReflectionOutcome Outcome;
    public QuestionModel Question;
    public List<AnswerModel> CollectedAnswers;
}
```

### 3. New controller responsibilities
- Keep `GameManager` as orchestrator only.
- Add `ReflectionController` to own the full coroutine pipeline.
- Add `ScreenFadeController` to own the fullscreen black image.
- Add `SplinePathProvider` to evaluate the authored path.
- Add `WormPresentationController` or