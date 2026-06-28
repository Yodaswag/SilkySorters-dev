using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.UI;
using static DataModels; //Instead of writing DataModels.GameModel/QuestionsModel/AnswerModel, just added a single using static class line at the top of each file that needs it.
using Random = UnityEngine.Random; 
public class GameManager : MonoBehaviour
{
    [SerializeField] GlobalSceneManager globalSceneManager;
    
    [Header ("Player and Camera")]
    public bool isStaticView = true;
    [SerializeField] CinemachineCamera dynamicVcam; //Since the player instance gets destroyed and reinstantiated every question - the camera must be attached to follow it.
    public CinemachinePositionComposer dynamicVcamComposer;
    [SerializeField] CinemachineCamera staticVcam;
    [SerializeField] GameObject silkyPlayerPrefab;
    public Transform positioner_PlayerSpawn;
    [SerializeField] private SnakeTail snakeTail;
    
    [Header ("Silky Content View")] //For the static view silkworm at the bottom left of the screen
    [SerializeField] private Transform positioner_SilkyContentView;
    [SerializeField] private GameObject silkyContentViewPrefab;
    private List<GameObject> silkyInstances = new List<GameObject>();
    
    [Header ("Mulberries (Order Items)")]
    [SerializeField] GameObject orderItemPrefab;
    private List<OrderItem> orderItems = new List<OrderItem>(); //List of game objects of mulberry answer items
    [SerializeField] private GameObject PositionerGroup_Mulberries;
    private List<Transform> mulberryPositionerList = new List<Transform>();
    
    [Header("Game Controls")]
    private bool gameWon;
    private int questionNumber;  //מספר השאלה הנוכחי
    public QuestionModel currentQuestion; // השאלה שעכשיו עונים עליה. //Used by SnakeGrow and SnakeTail and thus public 
    public GameModel game; 
    List<QuestionModel>allQuestions;  //רשימה של כל השאלות שיש
    int questionsCount; // כמה שאלות יש במשחק בכללי
    public int numPotions;

    public enum ReflectionPhases
    {
        None,
        MovingToStartAnchor,
        FollowingSpline,
        Darkening,
        RevealingResults,    // reveal pass: marks each body segment correct/wrong, then drains to progress (success/pause) or holds (failure)
        MistakeReviewPause,  // failure only: scored body stays visible so the learner studies the mistake; MoonButton "אנסה שוב מחר"
        WaitingForNextQuestion
    }
    public ReflectionPhases currentReflectionPhase = ReflectionPhases.None;

    public enum QuestionOutcome { Success, WrongAnswer, Timeout, Pause }
    private QuestionOutcome lastOutcome;
    public SplineContainer reflectionSpline;
    
    [Header("UI")]
    [SerializeField] private GameObject moonButton;
    [SerializeField] private TMP_Text moonButtonLabel;
    [SerializeField] private Image moonButtonImage;
    [SerializeField] private Sprite butterflyImage;
    [SerializeField] private Sprite leftArrowImage;
    [SerializeField] private GameObject skipButton;   // smaller, overlaps moonButton; never active at the same time as moonButton
    [SerializeField] private float skipSpeedMultiplier = 12f; // reflection-animation speed-up while skip is requested
    private bool isReflectionBusy;            // true while the MistakeReviewPause -> WaitingForNextQuestion fade runs
    public bool skipRequested;
    public float SkipFactor => skipRequested ? skipSpeedMultiplier : 1f;
    
    [SerializeField] private TextMeshProUGUI finalScoreText; // תיבה נפרדת רק לציון הסופי
    [SerializeField] private GameObject restartBtn; //Only appears after game won
    [SerializeField] TextMeshProUGUI questionText; 
    [SerializeField] TextMeshProUGUI topic; //Topic TextMeshPro UI (i.e. Hebrew for 7th grade)
    [SerializeField] private Image linearProgressFill; // בשביל המילוי של מד-ההתקדמות הליניארי שלנו
    [SerializeField] private GameObject uiDuringMainGame; // Used to hide pause button, question and timer when game over
    [SerializeField] private float padding = 0.75f;
    [SerializeField] private TextMeshProUGUI potionText; //מציג מספר שיקויים בUI כטקסט
    [SerializeField] private Image potionImage;
    [SerializeField] private Sprite potionNormal;
    [SerializeField] private Sprite potionYellow;
    [SerializeField] private Sprite potionRed;
    [SerializeField] private GameObject potionParentObject;
    
    [Header("UI - Labels")]
    [SerializeField] private GameObject labelPrefab;
    private List<Vector3> labelPositions = new List<Vector3>();
    private List<GameObject> labelTextObjects = new List<GameObject>();
    [SerializeField] float labelYOffset = 1.5f;
    
    [Header("Global Timer")]
    public bool isCountdownActive;
    public bool controlsEnabled = false;

    public float feedbackDelay = 0.5f;
    private float totalGameTime; //זמן כולל למשחק
    private int totalGameMistakes;
    [SerializeField] private TextMeshProUGUI timerText; //טקסט UI שמציג זמן
    private float currentGameTime;// הזמן שנותר בפועל
    private float awardedTimePerAnswer = 5f; //Should be 0 or 5. TODO: Consider changing to bool (talk with Oren)
    private float awardedTimeThisQuestion;
    private bool isTimerRunning; //משתנה בוליאני שנועד לבדוק אם הטיימר רץ
    private float score; // משתנה גלובלי שיכיל את הציון למשחק
    
    [Header("Reflection Nightfall")]
    [SerializeField] private SpriteRenderer nightOverlay;
    [SerializeField] private float nightMaxAlpha = 0.6f;

    [Header("Reflection - Body Drain")]
    [SerializeField] private float highlightStepDelay = 0.2f;      // delay between each segment turning green (Pass 1)
    [SerializeField] private float redFlashDuration = 0.4f;         // how long wrong/timeout red flash holds
    [SerializeField] private float timeoutRestoreStepDelay = 0.1f;  // delay between each unfilled segment restoring (timeout only)
    [SerializeField] private float prePass2Delay = 0.3f;            // pause after Pass 1 finishes, before Pass 2 starts
    [SerializeField] private float drainStepDelay = 0.1f;           // gap between consecutive segment launches (success)
    [SerializeField] private float failureFadeDuration = 0.3f;      // duration of the batch fade on failure/timeout
    [SerializeField] private Transform progressBarWorldAnchor;       // empty GO placed in scene at the progress bar's world position

    [Header("Reflection - Drain Arc")]
    [Tooltip("Constant flight speed in world units/sec")]
    [SerializeField] private float arcSpeed = 10f;
    [Tooltip("Bow height in world units at mid-flight. 0 = straight line.")]
    [SerializeField] private float arcBulge = 0.8f;
    [Tooltip("Bow direction in degrees, measured from the flight direction.")]
    [SerializeField] private float arcBulgeAngle = 90f;

    [Header("Question Start Transition")]
    [SerializeField] private RawImage worldFadeOverlay; //Canvas black overlay over the camera view, sorted above all world content; takes the world night->day while being behind the question and in front of the rest of the UI
    [SerializeField] private float startTransitionFadeDuration = 0.45f;
    [SerializeField] private RectTransform questionGroup;        //The "Question" parent (Question BG + QuestionText) — moved/scaled as one
    [SerializeField] private RectTransform questionIntroAnchor;  //Editor-placed sibling (same anchors as questionGroup) at the centered intro spot
    [SerializeField] private float questionIntroScale = 2f;      //How much bigger the question is at the intro pose vs its home pose
    [SerializeField] private float questionIntroMoveDuration = 0.6f;
    private Vector2 questionHomeAnchoredPos; //Authored top pose, captured at Start (anchoredPosition is camera-independent)
    private Vector3 questionHomeScale;
    
    [Header("Image Popup")]
    [SerializeField] GameObject imagePopupGroup;   // the canvas popup parent
    [SerializeField] Image popupImage;
    
    private InputSystem_Actions inputActions;

    private void EnsureInitialized()
    {
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
        }
    }

    private void OnEnable()
    {
        EnsureInitialized();
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }
    }
    
    private void Awake()
    {
        EnsureInitialized();
        if (GlobalSceneManager.Game != null)
            game = GlobalSceneManager.Game;
    }
    
    void Start()
    {
        uiDuringMainGame.SetActive(true);
        gameWon = false;
        Time.timeScale = 1f;
        restartBtn.SetActive(false);
        moonButton.SetActive(false);
        skipButton.SetActive(false);
        
        //Initialize mulberry positioner list
        foreach (Transform child in PositionerGroup_Mulberries.transform) 
        {
            mulberryPositionerList.Add(child);
        }
        
        dynamicVcamComposer = dynamicVcam.GetComponent<CinemachinePositionComposer>();

        questionHomeAnchoredPos = questionGroup.anchoredPosition; //Capture the authored top pose before any transition moves it
        questionHomeScale = questionGroup.localScale;

        GetGame();
    }

    // Update is called once per frame
    void Update()
    {
        if (isTimerRunning) // בדיקה אם הטיימר רץ והזמן לא נגמר והמשחק לא הסתיים
        {
            currentGameTime -= Time.deltaTime; // הזמן יורד רק במהלך המשחק — isTimerRunning כבוי בכל שלבי הרפלקציה
            if (currentGameTime <= 0f)
            {
                currentGameTime = 0f;
                UpdateTimerUI();
                TimeIsUp();
                return; //כדי לא להמשיך את הלוגיקה של המקשים לאחר שהמשחק נגמר
            }
            UpdateTimerUI();
        }
        // Space/Interact routes by reflection phase: skip the animation, or advance the MoonButton.
        if (inputActions.Player.Interact.WasPressedThisFrame())
        {
            switch (currentReflectionPhase)
            {
                case ReflectionPhases.MovingToStartAnchor:
                case ReflectionPhases.FollowingSpline:
                case ReflectionPhases.Darkening:
                case ReflectionPhases.RevealingResults:
                    OnSkipPressed();
                    break;
                case ReflectionPhases.MistakeReviewPause:
                case ReflectionPhases.WaitingForNextQuestion:
                    OnMoonButtonPressed();
                    break;
            }
        }
    }

    public void ContinueToNextQuestion()
    {
        if (gameWon) return;
        if (currentReflectionPhase != ReflectionPhases.WaitingForNextQuestion) return;

        if (questionNumber >= game.questionList.Count) // last question done
        {
            WinConditionReached();
            return;
        }
        
        NextQuestion();
        Time.timeScale = 1f;
    }

    // Single entry point for both Space/Interact and the MoonButton onClick.
    public void OnMoonButtonPressed()
    {
        if (isReflectionBusy) return;
        if (currentReflectionPhase == ReflectionPhases.MistakeReviewPause)
            StartCoroutine(RemoveThenWait());                 // clear the reviewed body, then offer "ליום הבא"
        else if (currentReflectionPhase == ReflectionPhases.WaitingForNextQuestion)
            ContinueToNextQuestion();
    }

    // Single entry point for both Space/Interact and the SkipButton onClick.
    public void OnSkipPressed()
    {
        skipRequested = true;
    }

    // Single choke point for reflection-phase changes: keeps exactly one reflection button visible (or neither).
    public void SetReflectionPhase(ReflectionPhases phase)
    {
        currentReflectionPhase = phase;

        bool animating = phase == ReflectionPhases.MovingToStartAnchor || phase == ReflectionPhases.FollowingSpline
                      || phase == ReflectionPhases.Darkening || phase == ReflectionPhases.RevealingResults;
        bool waiting = phase == ReflectionPhases.MistakeReviewPause || phase == ReflectionPhases.WaitingForNextQuestion;

        skipButton.SetActive(animating);   // SetActive(true) on an already-active object is a no-op, so the fade-in plays once
        moonButton.SetActive(waiting);
    }
    
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    void GetGame()
    { 
        totalGameTime = 0f;
        
        questionNumber = 0; // לא עניתי עדיין
        UpdateProgressBar(); //מאתחל את מד-ההתקדמות
        allQuestions = new List<QuestionModel>(game.questionList); //מאתחלת את הרשימה כעותק
        score = 0; // נאפס את הציון בתחילת המשחק
        // נאפס את כמות הניסיונות לכל השאלות במאגר כדי שמשחקים חוזרים יתחילו בלי שאלות קודמות במאגר
        foreach (QuestionModel q in allQuestions)
        {
            q.attempts = 0;
        }
        if (finalScoreText != null)
        {
            finalScoreText.gameObject.SetActive(false); // מסתירים את הציון בתחילת המשחק
        }

        if (game.hasPotions)
        {
            potionParentObject.SetActive(true);
        }
        else
        {
            potionParentObject.SetActive(false);
        }
        
        topic.text = game.gameName;

        CreateQuestion();
        StartCoroutine(PresentFirstQuestion());
    }
    // פונקציה ליצירת שאלות
    //אם אנחנו רוצים לשנות את הלוגיקה כך שתכלול שאלות אקראיות - יש לחסום את המתודה מלקבל פרמטרים ואז לייצר מספר אקראי בתוכה. נצטרך גם ליישם את RemoveQuestion בשביל השאלות שהשחקן הצליח בהן
    void CreateQuestion() //If we wanted to change up the logic to include random questions we would make this method accept no parameters and then generate the random number in it, we'd also need to implement RemoveQuestion for questions the player succeeded on
    { 
       //הגרלת שאלה אקראית מתוך השאלות שנותרו
       int randomQuestionNumber = Random.Range(0, allQuestions.Count); 
       currentQuestion = allQuestions[randomQuestionNumber]; // השאלה הראשונה-אקראית
       RTLFixer.SetTextInTMP(questionText, currentQuestion.questionContent); //Send the TMPro (question text) and the content to fixed to the RTLFixer
       questionText.alignment = TextAlignmentOptions.Center;
       
       currentGameTime = game.timePerQuestion;
       UpdateTimerUI(); //קריאה לפונקציה שמעדכנת את הזמן
       awardedTimeThisQuestion = 0; //Timer is started by the presenter coroutines, only after the question's reveal finishes
       
       
       currentQuestion.attempts++; //בשליפת שאלה, נוסיף ניסיון מענה
       ResetPlayer(); //לשים בהערה אם רוצים שהתולעת תהיה אחת רציפה שמתמלאת ומתרוקנת 
       
       List<Transform> dupMulberries = new List<Transform>(mulberryPositionerList); //Needs to be initialized once per question, therefore is passed as parameter to CreateAnswer()
       foreach (AnswerModel answer in currentQuestion.orderedAnswers) // Create all mulberries
       {
           CreateAnswer(answer,dupMulberries);  
       }
       
       //Initialize Potions
       numPotions = 0;
       if (game.hasPotions)
       {
           if (game.awardPotionInds != null && game.awardPotionInds.Count > 0)
           {
               foreach (int potionInd in game.awardPotionInds)
               {
                   if (potionInd == 0)
                   {
                       numPotions++; // If the game model has potions that are awarded at 0, add them when creating the questions
                   }
               }
           }
           
           potionText.text = numPotions.ToString();
           
           // Initialize potion color - red indicates 0 potions/sudden death
           if (numPotions == 0)
           {
               potionText.color = Color.red;
               potionImage.sprite = potionRed;
           }
           else
           {
               potionText.color = Color.black;
               potionImage.sprite = potionNormal;
           }
       }

       skipRequested = false;
       SetReflectionPhase(ReflectionPhases.None);
    }

    // Used when starting a new question. Currently, the code logic is that we destroy the previous silkworm and create a new with the correct amount of placeholders rather than emptying it.
    void ResetPlayer() // TODO: Consider not killing & instatiating the main player character, allowing easier and better continuity after coiling, etc...
    {
        KillCommonGameObjects();
        silkyInstances.Add(Instantiate(silkyPlayerPrefab,positioner_PlayerSpawn)); //Main player character (dynamic mode) - Set to 0
        silkyInstances.Add(Instantiate(silkyContentViewPrefab, positioner_SilkyContentView)); //Content view character (static mode) - Set to 1

        for (int i = 0; i < silkyInstances.Count; i++)
        {
            //Attach Components
            snakeTail = silkyInstances[i].GetComponent<SnakeTail>();
            snakeTail.gameManager = this;
            if (i == 0) //Only the main player character has SnakeMove & SnakeGrow
            {
                silkyInstances[i].GetComponent<SnakeMove>().gameManager = this;
                silkyInstances[i].GetComponent<SnakeGrow>().gameManager = this;
                
                // Setup dynamic vcam - only for main player character
                dynamicVcam.Follow = silkyInstances[i].transform;
                dynamicVcam.LookAt = silkyInstances[i].transform;
            }
            else if (i == 1)
            {
                // Modify the snakeTail-defined rect width&height to adjust with the scaling of the content view silkworm
                snakeTail.rectWidth *= positioner_SilkyContentView.localScale.x;
                snakeTail.rectHeight *= positioner_SilkyContentView.localScale.y;
            }

        
            // Create placeholders for the body - relevant to both
            while (snakeTail.GetLength()-1 < currentQuestion.orderedAnswers.Count) //minus 1 because the head is the first position
            {
                snakeTail.AddTail();
            }
        
            snakeTail.SetNextPlaceholder(); //After creating tail circles, set next placeholder - relevant to both
        }
        silkyInstances[0].GetComponent<SnakeGrow>().contentViewSnakeTail = silkyInstances[1].GetComponent<SnakeTail>();
        snakeTail = silkyInstances[0].GetComponent<SnakeTail>(); //Ensure GameManager's snakeTail is the main characters (unknown if needed - precaution as of now)
        SetContentSilkyPosition();
    }
    
    // ליצור מסיח על המסך
    void CreateAnswer(AnswerModel answerModel, List<Transform> dupMulberries)
    {
        if (dupMulberries.Count > 0)
        {
            int randPos = Random.Range(0, dupMulberries.Count);
            GameObject newAnswerPrefab = Instantiate(orderItemPrefab, dupMulberries[randPos]);
            newAnswerPrefab.name = "OrderItem_" + answerModel.orderIndex;

            OrderItem orderItemScript = newAnswerPrefab.GetComponent<OrderItem>();
            orderItemScript.SetAnswer(answerModel);

            Transform playerTransform = null;
            if (silkyInstances.Count > 0)
                playerTransform = silkyInstances[0].transform;
            orderItemScript.Initialize(this, playerTransform);

            orderItems.Add(orderItemScript);
            dupMulberries.RemoveAt(randPos);
        }
        else
        {
            Debug.Log("Not enough positioners for the amount of order items");
        }
    }
    
    public void UsePotion(AnswerModel wrongAnswer = null)
    {
        // TODO: Add sound effect for failure/mistake
        if (numPotions > 0)
        {
            numPotions--;
            potionText.text = numPotions.ToString();
            if (numPotions == 0)
            {
                potionText.color = Color.red;
                potionImage.sprite = potionRed;
            }
            silkyInstances[0].GetComponent<SnakeGrow>().ShowFloatingWorldText("-Potion", Color.red);
            // TODO: Add swirly eyes animation
            // TODO: Add brief game pause where the player can't move and the character sprites transparency goes up and down
        }
        else
        {
            QuestionFailed(wrongAnswer);
        }
    }
    
    public void AddPotion(int potionsToReceive)
    {
        numPotions += potionsToReceive;
        potionText.text = numPotions.ToString();
        potionText.color = Color.black;
        potionImage.sprite = potionNormal;
        silkyInstances[0].GetComponent<SnakeGrow>().ShowFloatingWorldText("+Potion", Color.black);
    }
    
    public void AddTime()
    {
        currentGameTime += awardedTimePerAnswer;
        awardedTimeThisQuestion += awardedTimePerAnswer;
        silkyInstances[0].GetComponent<SnakeGrow>().ShowFloatingWorldText("+5s", Color.yellow);
        //TODO: Add animation effect for time received
    }
    
    void DestroyAllAnswers() // Destroy all mulberries on the screen.
    {
        foreach (OrderItem orderItem in orderItems)
        {
            if (orderItem != null)
                Destroy(orderItem.gameObject);
        }
        
        orderItems.Clear();
    }
    
    public void ShowImagePopup(Sprite sprite)
    {
        if (sprite == null) return;
        popupImage.sprite = sprite;
        popupImage.preserveAspect = true;
        imagePopupGroup.SetActive(true);
    }

    
    public void HideImagePopup()
    {
        if (imagePopupGroup.activeSelf)
            imagePopupGroup.SetActive(false);
    }
    
    
    //For Darkening
    public void SetNightfall(float t) //t: 0 = day, 1 = night. Driven by SnakeMove during the coil.
    {
        if (nightOverlay == null) return;
        Color c = nightOverlay.color;
        c.a = Mathf.Lerp(0f, nightMaxAlpha, Mathf.Clamp01(t));
        nightOverlay.color = c;
    }
    
    // Called by SnakeMove once the body has coiled and darkened. Runs the reveal pass, then either holds on
    // the mistake (failure) or clears the body and offers the next day (success/pause).
    public IEnumerator RevealReflection()
    {
        yield return StartCoroutine(RevealSegments());
        if (lastOutcome == QuestionOutcome.WrongAnswer || lastOutcome == QuestionOutcome.Timeout)
        {
            EnterMistakeReviewPause();
        }
        else // Success, Pause
        {
            yield return StartCoroutine(RemoveContent());
            EnterWaitingForNextDay();
        }
    }

    // Failure stage 2: fired by Space/Interact or the MoonButton while reviewing the mistake.
    private IEnumerator RemoveThenWait()
    {
        isReflectionBusy = true;
        yield return StartCoroutine(RemoveContent());
        EnterWaitingForNextDay();
        isReflectionBusy = false;
    }

    private void EnterMistakeReviewPause() // failure only: keep the scored body on screen for review
    {
        skipRequested = false;
        RTLFixer.SetTextInTMP(moonButtonLabel, "אנסה שוב מחר");
        moonButtonImage.gameObject.SetActive(false); // this stage has no image
        SetReflectionPhase(ReflectionPhases.MistakeReviewPause);
    }

    private void EnterWaitingForNextDay()
    {
        skipRequested = false;
        moonButtonImage.gameObject.SetActive(true);
        moonButtonImage.sprite = leftArrowImage;
        RTLFixer.SetTextInTMP(moonButtonLabel, "ליום הבא");
        if (questionNumber >= game.questionList.Count) // last question, reached only on success
        {
            moonButtonImage.sprite = butterflyImage;
            RTLFixer.SetTextInTMP(moonButtonLabel, "לסיום המסע");
        }
        SetReflectionPhase(ReflectionPhases.WaitingForNextQuestion);
    }

    
    //Note: EndQuestion is intentionally separated from ScreenStatus for separation of concerns between the functions (helps with the parameters being called) and for future modularity
    private void EndQuestion()
    {
        isTimerRunning = false;
        totalGameTime += game.timePerQuestion+awardedTimeThisQuestion-currentGameTime; //חקן היה על השאלה מחברים את הזמן המוקצה לכל שאלה עם הזמן שהתקבל בשאלה ומחסירים את הזמן שנותר כדי לקבל את סה"כ הזמן שהש

        DestroyAllAnswers(); //Should happen before the option is given to press space to continue
        SetReflectionPhase(ReflectionPhases.MovingToStartAnchor);
    }
    
    //פונקציה שמטפלת בסיום שאלה בהצלחה
    public void QuestionSuccess() //TODO: Consider making single function with bool input success/fail
    {
        questionNumber++;
      //מסירים את שאלה מן המאגר רק לאחר שהשחקן ענה נכון
        allQuestions.Remove(currentQuestion);
        score += 100f / (currentQuestion.attempts * game.questionList.Count); //נוסחה לחישוב הציון. אצל נטע כתוב totalQuestions במקום game.questionList.Count
        lastOutcome = QuestionOutcome.Success;
        EndQuestion();
    }

    private void QuestionFailed(AnswerModel wrongAnswer = null) //פונקציה שמטפלת בסיוּם שאלה באי-הצלחה כאשר השחקן אכל תות לא לפי הסדר הנכון
    {
        if (wrongAnswer != null)
        {
            snakeTail.AddWrongAnswer(wrongAnswer);
            if (silkyInstances.Count > 1)
                silkyInstances[1].GetComponent<SnakeTail>().AddWrongAnswer(wrongAnswer);
        }
        totalGameMistakes++;
        lastOutcome = QuestionOutcome.WrongAnswer;
        EndQuestion();
    }

    public void Pause()
    {
        lastOutcome = QuestionOutcome.Pause;
        EndQuestion();
        // Time.timeScale = 0f; // Consider adding a continuous coiling (השתבללות) animation (move along circle spline) that gives a "loading" gif vibe
    }

    private void KillCommonGameObjects()
    {
        KillGameObjectList(silkyInstances);
        KillGameObjectList(labelTextObjects);
        labelPositions.Clear();
    }
    private void KillGameObjectList(List<GameObject> list)
    {
        if (list.Count > 0)
        {
            foreach (GameObject gameObject in list)
            {
                Destroy(gameObject);
            }
            list.Clear();  // Clear the list after all gameObject have been destroyed
        }
    }
    private void TimeIsUp() //פונקציה שמטפלת במצב שבו נגמר הזמן
    {
        lastOutcome = QuestionOutcome.Timeout;
        EndQuestion();
    }
    private void UpdateProgressBar() //פונקציה שאחראית על עדכון מד-ההתקדמות
    {
        if (linearProgressFill == null)  // בדיקת תקינות
        {
            return;
        }
        linearProgressFill.fillAmount = questionNumber*0.999f / game.questionList.Count; // חישוב הטעינה. בכוונה יש FLOAT כדי לא לחלק int ב-int  
        //0.999f is as taught in class. Alternative way would to be to just cast to float.
    }
    private void UpdateTimerUI()
    {
        if (timerText == null)
            return;
        int seconds = Mathf.CeilToInt(currentGameTime); //מעגל כלפי מעלה כדי להצג שניות שלמות
        timerText.text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");

        if (seconds < 10)
        {
            timerText.color = Color.darkRed;   
        }
        else
        {
            timerText.color = Color.black;
        }
    }
    private void NextQuestion()
    {
        StartCoroutine(StartQuestionTransition());
    }

    private IEnumerator StartQuestionTransition()
    {
        controlsEnabled = false;
        yield return FadeWorld(0f, 1f, startTransitionFadeDuration); //Take the world to full night

        // --- under full dark: camera cut + content swap are hidden ---
        ChangeView(true);                                   //Switch to static framing (cut hidden by the dark)
        dynamicVcamComposer.TargetOffset = Vector3.zero;    //Recenter the dynamic cam on the new worm, invisibly
        KillCommonGameObjects();
        CreateQuestion();
        SetNightfall(0f);
        SetQuestionIntroPose();
        yield return FadeWorld(1f, 0f, startTransitionFadeDuration); //Back to day, static framing, big centered question
        yield return AnimateQuestionToHome();

        foreach (OrderItem orderItem in orderItems)
        {
            if (orderItem != null)
                orderItem.RevealMulberry();
        }

        controlsEnabled = true;
        isTimerRunning = true; //Start the clock only once the question has settled at the top
    }

    private void SetQuestionIntroPose() //Snap the whole Question group to the large, centered intro pose
    {
        questionGroup.anchoredPosition = questionIntroAnchor.anchoredPosition;
        questionGroup.localScale = questionHomeScale * questionIntroScale;
    }

    private IEnumerator AnimateQuestionToHome()
    {
        Vector2 startPos = questionGroup.anchoredPosition;
        Vector3 startScale = questionGroup.localScale;
        float t = 0f;
        while (t < questionIntroMoveDuration)
        {
            t += Time.deltaTime;
            float k = t / questionIntroMoveDuration;
            questionGroup.anchoredPosition = Vector2.Lerp(startPos, questionHomeAnchoredPos, k);
            questionGroup.localScale = Vector3.Lerp(startScale, questionHomeScale, k);
            yield return null;
        }
        questionGroup.anchoredPosition = questionHomeAnchoredPos;
        questionGroup.localScale = questionHomeScale;
    }

    private IEnumerator PresentFirstQuestion() //First question gets the same reveal, starting from black
    {
        controlsEnabled = false;
        if (worldFadeOverlay != null)
        {
            Color c = worldFadeOverlay.color; c.a = 1f; worldFadeOverlay.color = c;
        }
        SetQuestionIntroPose();
        yield return FadeWorld(1f, 0f, startTransitionFadeDuration);
        yield return AnimateQuestionToHome();

        foreach (OrderItem orderItem in orderItems)
        {
            if (orderItem != null)
                orderItem.RevealMulberry();
        }

        controlsEnabled = true;
        isTimerRunning = true; //Start the clock only once the question has settled at the top
    }

    private IEnumerator FadeWorld(float from, float to, float duration)
    {
        if (worldFadeOverlay == null) yield break;
        Color c = worldFadeOverlay.color;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t / duration);
            worldFadeOverlay.color = c;
            yield return null;
        }
        c.a = to;
        worldFadeOverlay.color = c;
    }

    // Reveal pass: mark correct segments green, flag the wrong/missed ones red. Shared by every outcome.
    private IEnumerator RevealSegments()
    {
        int correctCount = snakeTail.GetAnswersProvided().Count;
        int totalSegments = snakeTail.GetSegmentCount();
        bool hasWrongAnswer = lastOutcome == QuestionOutcome.WrongAnswer;
        bool isTimeout = lastOutcome == QuestionOutcome.Timeout;

        if (totalSegments == 0) yield break;

        // ── PASS 1: Green highlight sweep (head → tail) ──────────────────────
        for (int i = 0; i < correctCount; i++)
        {
            snakeTail.GetSegment(i).ShowCorrect();
            yield return Wait(highlightStepDelay);
        }

        // Wrong answer: flash the wrong segment red and hold
        if (hasWrongAnswer)
        {
            snakeTail.GetSegment(correctCount).ShowError();
            yield return Wait(redFlashDuration);
        }

        // Timeout: flash ALL unfilled segments red simultaneously, hold, then restore one by one (tail → head)
        if (isTimeout)
        {
            for (int i = correctCount; i < totalSegments; i++)
                snakeTail.GetSegment(i).ShowError();

            yield return Wait(redFlashDuration);

            for (int i = totalSegments - 1; i >= correctCount; i--)
            {
                snakeTail.GetSegment(i).ShowEmpty();
                yield return Wait(timeoutRestoreStepDelay);
            }
        }

        yield return Wait(prePass2Delay);
    }

    // Removal pass: success drains the body into the progress bar; failure/pause fade the body to placeholders.
    private IEnumerator RemoveContent()
    {
        int correctCount = snakeTail.GetAnswersProvided().Count;
        int totalSegments = snakeTail.GetSegmentCount();
        int totalFilled = correctCount;
        if (lastOutcome == QuestionOutcome.WrongAnswer)
            totalFilled += 1;

        if (totalSegments == 0) yield break;

        // ── PASS 2: Outcome-specific removal ─────────────────────────────────
        if (lastOutcome == QuestionOutcome.Success)
        {
            float targetFill = questionNumber * 0.999f / game.questionList.Count;
            float startFill = 0f;
            if (linearProgressFill != null)
                startFill = linearProgressFill.fillAmount;

            float progressPerSegment = 0f;
            if (correctCount > 0)
                progressPerSegment = (targetFill - startFill) / correctCount;

            Vector3 coilCenter = GetCoilCenter();
            Vector3 anchorPos = coilCenter + Vector3.down * 3f;
            if (progressBarWorldAnchor != null)
                anchorPos = progressBarWorldAnchor.position;

            for (int i = correctCount - 1; i >= 0; i--)
            {
                yield return StartCoroutine(snakeTail.GetSegment(i)
                    .LaunchToProgressBar(anchorPos, arcSpeed * SkipFactor, arcBulge, arcBulgeAngle));

                if (linearProgressFill != null)
                    linearProgressFill.fillAmount += progressPerSegment;

                if (i > 0)
                    yield return Wait(drainStepDelay);
            }
        }
        else if (lastOutcome == QuestionOutcome.WrongAnswer || lastOutcome == QuestionOutcome.Pause)
        {
            yield return StartCoroutine(FadeSegmentsToPlaceholder(0, totalFilled));
        }
        else // Timeout: only the correct (green) segments remain — unfilled already restored above
        {
            if (correctCount > 0)
                yield return StartCoroutine(FadeSegmentsToPlaceholder(0, correctCount));
        }
    }

    // WaitForSeconds scaled by the skip speed-up: when skip is requested the reveal/drain still plays
    // (and the progress bar still climbs) but races to the end.
    private IEnumerator Wait(float seconds)
    {
        yield return new WaitForSeconds(seconds / SkipFactor);
    }

    // Centre of the coil circle the body wrapped onto, sampled from the reflection spline
    private Vector3 GetCoilCenter()
    {
        if (reflectionSpline != null)
        {
            Vector3 sum = Vector3.zero;
            const int samples = 16;
            for (int i = 0; i < samples; i++)
                sum += (Vector3)reflectionSpline.EvaluatePosition(i / (float)samples);
            return sum / samples;
        }

        // Fallback: average of the current segment positions
        int n = snakeTail.GetSegmentCount();
        if (n == 0) return snakeTail.transform.position;
        Vector3 acc = Vector3.zero;
        for (int i = 0; i < n; i++) acc += snakeTail.GetSegment(i).transform.position;
        return acc / n;
    }

    private IEnumerator FadeSegmentsToPlaceholder(int fromIndex, int count)
    {
        if (count <= 0) yield break;

        float targetAlpha = snakeTail.GetSegment(fromIndex).PlaceholderAlpha;
        for (int i = fromIndex; i < fromIndex + count; i++)
            snakeTail.GetSegment(i).PrepareEmpty();

        float elapsed = 0f;
        while (elapsed < failureFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, targetAlpha, elapsed / failureFadeDuration);
            for (int i = fromIndex; i < fromIndex + count; i++)
                snakeTail.GetSegment(i).SetBGAlpha(alpha);
            yield return null;
        }
        for (int i = fromIndex; i < fromIndex + count; i++)
            snakeTail.GetSegment(i).SetBGAlpha(targetAlpha);
    }

    private void WinConditionReached()
    {
        gameWon = true;
        globalSceneManager.ShowFinalScreen(Convert.ToInt16(score),Convert.ToInt16(totalGameTime));
    }

    public void ChangeView(bool shouldBeStaticView)
    {
        if (shouldBeStaticView != isStaticView && !isCountdownActive)
        {
            isStaticView = shouldBeStaticView;
            staticVcam.Priority = Convert.ToInt32(isStaticView)*2+1; //If 0 -> 1, if 1 -> 3. The dynamicVcam priority is 2.
            foreach (OrderItem orderItem in orderItems)
            {
                if (orderItem != null)
                    orderItem.MulberryChangeView(isStaticView);
            }
        }
    }
    
    public void SetCountdownActive(bool active)
    {
        isCountdownActive = active;
    }
    
    
    // Set position of תולעת חיווי תוכן
    Bounds GetMaxBounds(GameObject g) { //Helper function. Source: https://gamedev.stackexchange.com/questions/86863/calculating-the-bounding-box-of-a-game-object-based-on-its-children
        var b = new Bounds(g.transform.position, Vector3.zero);
        for (int i = 0; i < g.transform.childCount; i++)
        {
            Bounds childBounds = g.transform.GetChild(i).GetComponent<Renderer>().bounds;
            b.Encapsulate(childBounds);
        }
        return b;
    }

    void SetContentSilkyPosition()
    {
        // fixes x position of silky content view so that the leftmost tail is aligned with the edge of the camera view/edge of the map
        float cameraLeftBound = dynamicVcam.gameObject.GetComponentInChildren<CinemachineConfiner2D>().BoundingShape2D.bounds.min.x;
        float curContentSilkyLeftBound = GetMaxBounds(silkyInstances[1]).min.x;
        Vector3 posShift = new Vector3(curContentSilkyLeftBound - cameraLeftBound - padding, 0, 0);
        silkyInstances[1].transform.position -= posShift;
        SetLabels(posShift);
    }
    
    void SetLabels(Vector3 posShift)
    {
        if (labelPositions != null)
        {
            posShift.y -= labelYOffset;
            List<Vector3> contentSilkyTailPositions = silkyInstances[1].GetComponent<SnakeTail>().positions;
            Vector3 firstPos = contentSilkyTailPositions[1] - posShift;
            Vector3 lastPos = contentSilkyTailPositions[^1] - posShift;
            
            labelTextObjects.Add(Instantiate(labelPrefab,firstPos,Quaternion.identity)); //Start label
            RTLFixer.SetTextInTMP(labelTextObjects[0].GetComponentInChildren<TextMeshPro>(),currentQuestion.orderStartLabel);
            labelTextObjects[0].GetComponentInChildren<TextMeshPro>().alignment = TextAlignmentOptions.Center;
            
            labelTextObjects.Add(Instantiate(labelPrefab,lastPos,Quaternion.identity)); //End label
            RTLFixer.SetTextInTMP(labelTextObjects[1].GetComponentInChildren<TextMeshPro>(),currentQuestion.orderEndLabel);
            labelTextObjects[1].GetComponentInChildren<TextMeshPro>().alignment = TextAlignmentOptions.Center;
        }
    }
}

