# SilkySorters — Code Audit

Scope: every `.cs` under `Assets/Scripts` + folder structure. No code was changed — this is a recommendation document. Audited against HIT Triangle student conventions + the project's own rules in `CLAUDE.md` (no `?:`, no `=>`, leave serialized fields unguarded, Manager/Item split, one language per comment block).

Finding format: `file:line: [HARD|SOFT]: problem → fix`.

---

## 0. Priority triage (read this first)

**Must fix before 30.6 submission**
- Remove leftover `Debug.Log` calls (3).
- Remove dead/unused `using`s (2) and the dead-code comment blocks.
- Repoint `ServerManager.projectURL` off `localhost` (already in `CLAUDE.md` gotchas).
- `SnakeGrow` shows `"-Potion"` but `GameManager.UsePotion` passes `"-Potion"` while the offset switch checks `"+Potion"` → label never gets its offset (see SnakeGrow finding).

**Worth fixing (clean, low-risk, graded on)**
- 3 real duplicate blocks → extract (potion UI, image-zoom guard, answer-render).
- Cache the main `SnakeGrow` instead of `GetComponent` on every potion/time event.
- `snake_case` serialized/private fields → `camelCase`.
- `SkipFactor` ternary → `if/else` (project bans `?:`).

**Optional / do NOT churn**
- Folder layout is type-first, not feature-first. Rubric flags it; reorganising = thousands of `.meta` GUID churn for zero functional gain right before submission. **Recommend leaving it.** (See §2.)
- Trivial `=> field` getters: harmless, low priority. The no-arrow rule targets new code you hand-write; batch-convert only if you want full consistency.

---

## 1. Hanging items (Debug / TODO / dead code)

### Debug calls
```
ServerManager.cs:62:  [HARD]: Debug.Log(endpoint) left in fetch path → remove (or gate behind #if UNITY_EDITOR).
GameManager.cs:443:   [HARD]: Debug.Log("Not enough positioners...") → remove, or make Debug.LogWarning (it's a real misconfig warning).
ServerManager.cs:26:  [SOFT]: Debug.LogError in ShowError is deliberate error logging — keep, but it's the only Debug call that should survive. Confirm intent.
```

### TODO / FIXME (resolve or delete before submission — graders read these)
```
GameManager.cs:104:  [SOFT]: TODO awardedTimePerAnswer bool vs 5f — decide with Oren, then delete comment.
GameManager.cs:379:  [SOFT]: TODO ResetPlayer kill+reinstantiate — known design debt, fine to keep as a note.
GameManager.cs:449:  [SOFT]: TODO add failure SFX.
GameManager.cs:473:  [SOFT]: TODO add time-received anim.
GameManager.cs:625:  [SOFT]: TODO merge QuestionSuccess/QuestionFailed into one bool fn.
SnakeMove.cs:94-95:  [SOFT]: TODO extract ReflectionController / add remaining phase behaviour.
OrderItem.cs:72:     [SOFT]: TODO "Verify" initial-hidden state — verify and delete.
MulberryProximityZone.cs:4: [HARD]: TODO admits this whole script is a band-aid for a null-ref whose root cause is unsolved → see §3 MulberryProximityZone.
```

### Dead code / dead usings
```
GlobalSceneManager.cs:3: [HARD]: using UnityEngine.SocialPlatforms.Impl is unused IDE auto-import, AND two usings share one line → delete the import, split the line.
SnakeGrow.cs:3:          [HARD]: using System.Linq — no LINQ used in file → delete.
SnakeTail.cs:89:         [HARD]: commented-out "// startPos.y -= rectHeight;" → delete.
SnakeTail.cs:136-137:    [HARD]: commented-out if-block above SetNextPlaceholder → delete.
SnakeMove.cs:134:        [HARD]: commented-out "// SetGlow(currentSplineTime);" → delete (or implement).
DataModels.cs:6-7:       [HARD]: public static Game/score are unused dead state (GlobalSceneManager holds the live ones) → delete.
GameManager.cs:85:       [SOFT]: potionYellow sprite field declared, never assigned/used → delete or wire up.
```

---

## 2. Folder structure

Current layout is **type-first**: `Assets/Scripts`, `Assets/Prefabs`, `Assets/Sprites`, `Assets/Animations`, … with `Scripts/MainGame/{General,Helper,Mulberry,Silky}` grouped by responsibility.

```
Assets/Scripts/:            [SOFT]: Rubric prefers feature-first (Assets/<Feature>/Scripts|Prefabs|Images).
                                    Project is type-first and internally consistent → DO NOT reorganise.
                                    Reason: moving assets rewrites every .meta GUID and risks broken refs
                                    days before submission, for zero runtime benefit.
Assets/Scripts/ root:       [SOFT]: GlobalSceneManager.cs, ServerManager.cs, SoundToggle.cs sit in the root.
                                    First two are legitimately cross-scene/global. SoundToggle is UI-only →
                                    could move to Scripts/UI/ for tidiness. Low value.
Assets/Sprites/MainGame/New/: [SOFT]: "New" is a non-descriptive bucket. Migration to UI/ already in progress
                                    (see git status) — finish it, drop "New".
Assets/Scripts/MainGame/Helper/: [OK]: RTLFixer + ImageScript correctly isolated as shared helpers.
```

Per-scene manager check: `GameManager` (MainGame), `GlobalSceneManager` (cross-scene), `ServerManager` (StartGame) — one manager per scene. **Good.**

---

## 3. Per-file findings

### ServerManager.cs
```
:242-244: [HARD]: empty Update() → delete (no per-frame work).
:8 (file): [HARD]: comments mix Hebrew (lines 23,30,38,66,72,81,88,114,177,224,247) and English (26) → pick one language for the file.
:14:       [HARD]: projectURL hardcoded "https://localhost:7296/" → move to a [SerializeField] or config; must repoint before any non-local build.
:248-279:  [SOFT]: ServerGame/ServerQuestion/ServerItem are correctly SEPARATE from DataModels (good split), but live inside ServerManager.cs → move to ServerModels.cs to mirror DataModels.cs.
:131 LoadImage: [SOFT]: Sprite.Create'd textures are never released; on replay the old Texture2D leaks. Low priority for a short game, note it.
```
Positives: `async void CheckCode` is a correct UI entry point; internal calls are `async Task<T>`; `Parse*` use `foreach`, no LINQ, no ternaries; null results are guarded and propagated. This file follows the async/parse convention well.

### GlobalSceneManager.cs
```
:3:   [HARD]: see §1 — dead using + two-usings-on-one-line.
:8:   [SOFT]: score=100 magic default with no comment (time has //seconds) → comment or name it.
:30 Awake: [SOFT]: FinalScreen-render logic lives in a cross-scene manager's Awake. Acceptable, but mixes "scene loader" and "final screen view" responsibilities.
```

### SoundToggle.cs
```
[OK]: clean. 3 serialized fields, no Header (borderline, fine). Toggle is a proper OnClick entry point. No action needed.
```

### GameManager.cs  (997 lines — the big one)
**Duplicates (worst first):**
```
:354-368 vs :450-460: [HARD]: potion-UI update (text + red/normal colour + sprite) duplicated in CreateQuestion and UsePotion → extract UpdatePotionUI(). Diff in §4.
```
**Naming (HARD — project/rubric require camelCase private/serialized, no snake_case, no public-for-inspector):**
```
:35:  [HARD]: [SerializeField] GameObject PositionerGroup_Mulberries → PascalCase+underscore; rename mulberryPositionerGroup.
:24:  [HARD]: public Transform positioner_PlayerSpawn → public + snake_case; make [SerializeField] private playerSpawnPositioner.
:28,112-114: [HARD]: positioner_SilkyContentView / positioner_StartScreenHead/Body1/Body2 → snake_case → camelCase.
:20:  [SOFT]: public CinemachinePositionComposer dynamicVcamComposer is public only so SnakeMove can read it → expose via property or keep, but it's assigned in Start, not the Inspector.
```
**public mutable state read by sibling scripts** (currentQuestion, game, numPotions, controlsEnabled, skipRequested, currentReflectionPhase, reflectionSpline, isCountdownActive):
```
:39-97 (various): [SOFT]: these are public for cross-script reads (SnakeMove/SnakeGrow/SnakeTail). Rubric wants private+[SerializeField]. Proper fix = read-only properties, but that's a wide change. At minimum mark the ones never set externally as { get; private set; }.
```
**Style:**
```
:73:  [HARD]: public float SkipFactor => skipRequested ? skipSpeedMultiplier : 1f — uses BOTH => and ?: (two project bans) → if/else property. Diff in §4.
:935: [SOFT]: Convert.ToInt16(score) caps at 32767 → use ToInt32 for headroom.
:455-467 / :459-460 / :472 / :517 / :680 + ResetPlayer :392-393,:415-416: [SOFT]: repeated silkyInstances[0].GetComponent<SnakeGrow>() on potion/time/reflect events → cache mainSnakeGrow once in ResetPlayer. Diff in §4.
```
**Comments / headers:**
```
file: [HARD]: heavy Hebrew+English mix in the same file → pick one language.
:95 [Header("Global Timer")]: [SOFT]: header sits above isCountdownActive/controlsEnabled then unrelated feedbackDelay/timer fields → regroup or rename.
```
**Architecture:**
```
[SOFT]: God-class — camera, timer, score, potions, spawning, label layout, and the whole reflection animation sequence in one 997-line file. Not a hard violation (it IS the single scene manager), but the reflection/drain coroutines (RevealReflection, RemoveContent, RevealSegments, Fade*, Launch arc) are a cohesive unit → extract ReflectionSequencer. The SnakeMove:94 TODO already calls for this. Big change — schedule post-submission.
```

### DataModels.cs
```
:4-7: [HARD]: unused public static Game/score → delete (dead per CLAUDE.md).
:19 IsValid(): [SOFT]: non-trivial public method, no /// summary → add one line.
[OK]: nested serializable GameModel/QuestionModel/AnswerModel kept separate from Server* classes — correct model split.
```

### FloatingWorldText.cs
```
[OK]: clean, self-contained, self-destructs. Code-driven rise+fade (Update lerp) instead of an animation clip — per your "prefer animation assets" rule this could be an Animator clip, but it's a throwaway one-shot; acceptable as a thin orchestrator. No action.
```

### ImageScript.cs
```
:161: [SOFT]: if (spriteMask) implicit-null vs if (spriteMask != null) used at :143/:97 → pick one style.
[OK]: all-Hebrew comments (consistent within file), full /// summaries, ZoomIn/ZoomOut use guard clauses. Good file.
```

### RTLFixer.cs
```
:13:  [SOFT]: one long Hebrew comment in an otherwise-English file → mixed-language; move/translate.
:160 SetTextInTMP: [SOFT]: public, no /// summary (FixRtl has one) → add.
:52-100 ReverseLtrChunks: [SOFT/ponytail]: ArrayPool<char> rent/return for short UI label strings is heavier than the problem. A plain char[] reverse (no pool, no LINQ) is simpler and allocates trivially for label-length text. Optional.
[OK]: static utility (not a MonoBehaviour) — correct.
```

### OrderItem.cs
```
:290-308: [HARD]: identical 4-term guard "if (!isRevealed || answer==null || answer.imageContent==null || !animator.GetBool(\"Static\")) return;" in OnPointerClick/Enter/Exit → extract CanZoomImage(). Diff in §4.
:10-23: [HARD]: public answer/answerImage/answerText/imageScript/touched/isConsumable/orderIndex mixed with [SerializeField] private fields → inconsistent exposure. answerImage/answerText/imageScript should be [SerializeField] private; touched/isConsumable/orderIndex are runtime state read by SnakeGrow → expose via property, not public field.
:151-158 / :251-262: [SOFT]: spritesheet[0..4] magic indices (0 hidden,1 wrong,2 correct,3 touched,4 idle?) → name them const int or an enum.
[SOFT]: Item holds countdown-coroutine orchestration and calls gameManager.SetCountdownActive — borderline Manager logic in an Item. Acceptable, but the countdown ownership could move up.
```

### MulberryProximityZone.cs
```
:4: [HARD]: self-described redundant band-aid for a null-ref "whose true root cause is unsolved". Either find the root cause in OrderItem and delete this script, or remove the TODO and document why the extra trigger zone is the intended design. Don't ship an admitted band-aid with an open TODO.
[OK]: if it stays, the code itself is clean and cached (GetComponentInParent in Awake).
```

### SpaceCountdownPrompt.cs
```
[OK]: FrameCount guards null, Update only repositions while showing, Hide/Show symmetric. No action.
```

### SingleTail.cs
```
:18:  [SOFT]: PlaceholderAlpha => placeholderAlpha — expression-bodied property (project no-arrow). Low priority.
:77:  [HARD]: public void SetBGAlpha(float a) => ... — arrow on a method body (no-arrow rule). Convert to braces.
[OK]: passive Item driven by manager.positions in LateUpdate — correct Manager/Item split. Launch/clone flight code is well-commented.
```

### SnakeTail.cs
```
:111-176: [HARD]: AddAnswer and AddWrongAnswer share a near-identical image/text render block (differs only by textComp.color=red and the trailing SetNextPlaceholder) → extract RenderAnswerContent(tailScript, answer, isWrong). Diff in §4.
:24-26: [HARD]: positionsBuffer / placeholder_alpha / next_placeholder_alpha — snake_case private fields → camelCase (placeholderAlpha, nextPlaceholderAlpha).
:25 vs SingleTail:17: [SOFT]: the 0.35 placeholder alpha is duplicated as a literal in both SnakeTail and SingleTail → single source of truth.
:11-12,:22: [SOFT]: [SerializeField] public rectWidth/rectHeight and public gameManager — both serialized and public is redundant; pick [SerializeField] private + a setter, or public alone.
:178-181: [SOFT]: four => getters (no-arrow). Low priority batch.
:89,:136-137: [HARD]: dead commented code → delete (also in §1).
```

### SnakeGrow.cs
```
:3:   [HARD]: unused using System.Linq → delete.
:233-240 vs GameManager.cs:459: [HARD/BUG]: ShowFloatingWorldText switches on message=="+Potion" for its offset, but the only potion call (GameManager.UsePotion) passes "-Potion" → the potion text never receives its intended offset. Align the strings, and replace magic-string matching with an enum or explicit offset parameter.
:81-106 Update: [SOFT]: eat/feedback logic in Update reads gameManager.currentQuestion.orderedAnswers.Count repeatedly — fine, but consider caching the count for the active question.
[OK]: audioSource cached in Start; serialized refs used instead of GetComponent in Update.
```

### SnakeMove.cs
```
:94-95: [SOFT]: TODOs (extract ReflectionController, finish phases).
:134:   [HARD]: dead commented SetGlow → delete.
[OK]: switch on the reflection-phase enum (matches "switch for 3+ enum branches"); rb/headTransform cached via EnsureInitialized so no GetComponent churn in physics step.
```

---

## 4. Recommended diffs (high-value, low-risk)

> Written in the project style: explicit `if/else`, full method bodies, no `?:`, no `=>`. You apply these manually.

### 4.1 GameManager — extract `UpdatePotionUI()` (kills the §3 duplicate)
```diff
+    // Single source of truth for the potion counter's text + colour + sprite.
+    private void UpdatePotionUI()
+    {
+        potionText.text = numPotions.ToString();
+        if (numPotions == 0)
+        {
+            potionText.color = Color.red;
+            potionImage.sprite = potionRed;
+        }
+        else
+        {
+            potionText.color = Color.black;
+            potionImage.sprite = potionNormal;
+        }
+    }
```
```diff
     // in CreateQuestion()
        if (game.hasPotions)
        {
            numPotions = 2;
-           potionText.text = numPotions.ToString();
-           if (numPotions == 0)
-           {
-               potionText.color = Color.red;
-               potionImage.sprite = potionRed;
-           }
-           else
-           {
-               potionText.color = Color.black;
-               potionImage.sprite = potionNormal;
-           }
+           UpdatePotionUI();
        }
        else
        {
            numPotions = 0;
        }
```
```diff
     // in UsePotion()
        if (numPotions > 0)
        {
            numPotions--;
-           potionText.text = numPotions.ToString();
-           if (numPotions == 0)
-           {
-               potionText.color = Color.red;
-               potionImage.sprite = potionRed;
-           }
+           UpdatePotionUI();
            mainSnakeGrow.ShowFloatingWorldText("-Potion", Color.red);
            mainSnakeGrow.StartMistakeFlash();
        }
```
(Note: `numPotions` is always 2 in CreateQuestion, so the `==0` branch there was dead anyway — the shared helper is harmless and correct.)

### 4.2 GameManager — cache the main SnakeGrow
```diff
+    private SnakeGrow mainSnakeGrow; // main player character's SnakeGrow, cached per question in ResetPlayer
```
```diff
     // in ResetPlayer(), after silkyInstances[0] is created
        silkyInstances[0].GetComponent<SnakeGrow>().contentViewSnakeTail = silkyInstances[1].GetComponent<SnakeTail>();
+       mainSnakeGrow = silkyInstances[0].GetComponent<SnakeGrow>();
        snakeTail = silkyInstances[0].GetComponent<SnakeTail>();
```
Then replace every `silkyInstances[0].GetComponent<SnakeGrow>()` (lines ~459, 460, 472, 517, 680) with `mainSnakeGrow`.

### 4.3 GameManager — SkipFactor: drop the ternary + arrow
```diff
-    public float SkipFactor => skipRequested ? skipSpeedMultiplier : 1f;
+    public float SkipFactor
+    {
+        get
+        {
+            if (skipRequested)
+                return skipSpeedMultiplier;
+            return 1f;
+        }
+    }
```

### 4.4 OrderItem — extract the image-zoom guard
```diff
+    // The three pointer handlers all require the same revealed-image-in-static-view state.
+    private bool CanZoomImage()
+    {
+        if (!isRevealed) return false;
+        if (answer == null) return false;
+        if (answer.imageContent == null) return false;
+        if (!animator.GetBool("Static")) return false;
+        return true;
+    }
```
```diff
     public void OnPointerClick(PointerEventData e)
     {
-        if (!isRevealed || answer == null || answer.imageContent == null || !animator.GetBool("Static")) return;
+        if (!CanZoomImage()) return;
         gameManager.ShowImagePopup(answer.imageContent);
     }

     public void OnPointerEnter(PointerEventData e)
     {
-        if (!isRevealed || answer == null || answer.imageContent == null || !animator.GetBool("Static")) return;
+        if (!CanZoomImage()) return;
         magnifierIcon.color = Color.white;
         magnifierIcon.transform.localScale = 0.5f * Vector3.one;
     }

     public void OnPointerExit(PointerEventData e)
     {
-        if (!isRevealed || answer == null || answer.imageContent == null || !animator.GetBool("Static")) return;
+        if (!CanZoomImage()) return;
         magnifierIcon.color = Color.grey;
         magnifierIcon.transform.localScale = 0.25f * Vector3.one;
     }
```

### 4.5 SnakeTail — extract `RenderAnswerContent()` (merges AddAnswer/AddWrongAnswer bodies)
```diff
+    // Shared render path for a slot's content. isWrong tints the text red (used by AddWrongAnswer).
+    private void RenderAnswerContent(SingleTail tailScript, AnswerModel answer, bool isWrong)
+    {
+        if (!answer.IsValid())
+            return;
+
+        if (answer.isImage)
+        {
+            tailScript.imageComp.gameObject.SetActive(true);
+            tailScript.imageScript.SetImage_KeepRatio(answer.imageContent);
+            tailScript.imageComp.sortingOrder = tailScript.tailBG.sortingOrder + 1;
+            if (tailScript.textComp) tailScript.textComp.gameObject.SetActive(false);
+        }
+        else
+        {
+            RTLFixer.SetTextInTMP(tailScript.textComp, answer.textContent);
+            if (isWrong)
+                tailScript.textComp.color = Color.red;
+            tailScript.textComp.sortingOrder = tailScript.tailBG.sortingOrder + 1;
+            tailScript.textComp.gameObject.SetActive(true);
+            if (tailScript.imageComp) tailScript.imageComp.gameObject.SetActive(false);
+        }
+    }
```
```diff
     public void AddAnswer(AnswerModel answer)
     {
         if (answersProvided.Count >= snakeTail.Count) return;
         SingleTail tailScript = snakeTail[answer.orderIndex];
         answersProvided.Add(answer);
-        if (answer.IsValid())
-        {
-            if (answer.isImage) { ... }   // ~18 lines
-            else { ... }
-        }
+        RenderAnswerContent(tailScript, answer, false);
         tailScript.ShowFilled();
         SetNextPlaceholder();
     }

     public void AddWrongAnswer(AnswerModel answer)
     {
         int slotIndex = answersProvided.Count;
         if (slotIndex >= snakeTail.Count) return;
         SingleTail tailScript = snakeTail[slotIndex];
-        if (answer.IsValid()) { ... }     // same ~18 lines, +red text
+        RenderAnswerContent(tailScript, answer, true);
         tailScript.ShowFilled();
     }
```

### 4.6 Dead-using / dead-comment cleanup
```diff
- using UnityEngine.SceneManagement;using UnityEngine.SocialPlatforms.Impl;
+ using UnityEngine.SceneManagement;
```
```diff
  // SnakeGrow.cs
- using System.Linq;
```
```diff
  // ServerManager.cs — delete the empty method
-    void Update()
-    {
-    }
```
```diff
  // SnakeMove.cs:134
-                // SetGlow(currentSplineTime);                  //Moonlight glow rises with the coil
```
```diff
  // DataModels.cs — delete unused statics
- public static GameModel Game;
- public static int score;
```

---

## 5. Summary

```
HARD: 24   SOFT: 23   DUPLICATES: 3   HANGING: 12
```
- HANGING = 3 Debug calls + 8 TODO + (counted separately) dead usings/comments.
- DUPLICATES = potion-UI (GameManager), image-zoom guard (OrderItem), answer-render (SnakeTail).

**Biggest wins, in order:** (1) the 3 extractions in §4, (2) cache `mainSnakeGrow`, (3) fix the `"-Potion"/"+Potion"` string mismatch, (4) strip Debug/dead-usings/dead-comments, (5) `snake_case → camelCase` rename pass. **Skip** the folder reorg and the trivial `=>`-getter conversions unless you want full rubric purity.

Overall: the codebase is in good shape for a student project — proper async/parse separation, real Manager/Item split, RTL handled centrally, strong inline documentation of the reflection sequence. The debt is concentrated in `GameManager` (size + naming + duplication) and a couple of honest band-aids (`MulberryProximityZone`).
