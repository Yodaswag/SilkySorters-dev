using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using UnityEngine.UI;
using static DataModels;
using Random = UnityEngine.Random; //Instead of writing DataModels.GameModel/QuestionsModel/AnswerModel, just added a single using static class line at the top of each file that needs it.
public class GameManager : MonoBehaviour
{
    public bool isStaticView = true;
    [Header ("Player and Camera")]
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
        RemovingAnswersFromBody,
        FadingToBlack,
        WaitingForNextQuestion
    }
    public ReflectionPhases currentReflectionPhase = ReflectionPhases.None;
    public SplineContainer reflectionSpline;
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI screenStatusText; //
    [SerializeField] private TextMeshProUGUI finalScoreText; // תיבה נפרדת רק לציון הסופי
    [SerializeField] private GameObject restartBtn; //Only appears after game won
    [SerializeField] TextMeshProUGUI questionText; 
    [SerializeField] TextMeshProUGUI progress; //Progress TextMeshPro UI (i.e 0/5 questions)
    [SerializeField] TextMeshProUGUI topic; //Topic TextMeshPro UI (i.e. Hebrew for 7th grade)
    [SerializeField] private Image linearProgressFill; // בשביל המילוי של מד-ההתקדמות הליניארי שלנו
    [SerializeField] private GameObject uiDuringMainGame; // Used to hide pause button, question and timer when game over
    [SerializeField] private float padding = 0.75f;
    [SerializeField] private TextMeshProUGUI potionText; //מציג מספר שיקויים בUI כטקסט
    [SerializeField] private GameObject potionParentObject;
    
    [Header("UI - Labels")]
    [SerializeField] private GameObject labelPrefab;
    private List<Vector3> labelPositions = new List<Vector3>();
    private List<GameObject> labelTextObjects = new List<GameObject>();
    [SerializeField] float labelYOffset = 1.5f;
    
    [Header("Global Timer")]
    private float totalgameTime; //זמן כולל למשחק
    private int totalGameMistakes;
    [SerializeField] private TextMeshProUGUI timerText; //טקסט UI שמציג זמן
    private float currentGameTime;// הזמן שנותר בפועל
    private float awardedTimePerAnswer = 5f; //Should be 0 or 5. TODO: Consider changing to bool (talk with Oren)
    private float awardedTimeThisQuestion;
    private bool isTimerRunning; //משתנה בוליאני שנועד לבדוק אם הטיימר רץ
    private float score; // משתנה גלובלי שיכיל את הציון למשחק
    void Start()
    {
        uiDuringMainGame.SetActive(true);
        gameWon = false;
        Time.timeScale = 1f;
        restartBtn.SetActive(false);
        screenStatusText.gameObject.SetActive(false);
        
        //Initialize mulberry positioner list
        foreach (Transform child in PositionerGroup_Mulberries.transform) 
        {
            mulberryPositionerList.Add(child);
        }
        
        dynamicVcamComposer = dynamicVcam.GetComponent<CinemachinePositionComposer>();
        
        GetGame();
    }

    // Update is called once per frame
    void Update()
    {
        if (isTimerRunning) // בדיקה אם הטיימר רץ והזמן לא נגמר והמשחק לא הסתיים
        {
            //כדי שהטיימר לא ירוץ במסך מעבר בין שאלה שבו השחקן לוחץ על רווח כדי להמשיך
            if (screenStatusText == null || !screenStatusText.gameObject.activeSelf)
            {
                currentGameTime -= Time.deltaTime; //הטיימר רץ והזמן יורד
   
            }
            if (currentGameTime <= 0f)
            {
                currentGameTime = 0f;
                UpdateTimerUI();
                TimeIsUp();
                return; //כדי לא להמשיך את הלוגיקה של המקשים לאחר שהמשחק נגמר
            }
            UpdateTimerUI();
        }
        // ריסטארט רק אם המשחק נגמר (כדי שלא יעשו ריסטארט בטעות באמצע משחק) או אם נגמר הזמן.
        if (screenStatusText != null && screenStatusText.gameObject.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.R) && (gameWon))
            {
                RestartGame();
            }
            else if (Input.GetKeyDown(KeyCode.Space) && !gameWon && currentReflectionPhase == ReflectionPhases.WaitingForNextQuestion) // TODO: Add better reflection logic into the if
            {
                NextQuestion();
                Time.timeScale = 1f;
            }

        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    void GetGame()
    { 
        totalgameTime = 0f;
        
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
       isTimerRunning = true;
       awardedTimeThisQuestion = 0;
       
       
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
           }
           else
           {
               potionText.color = Color.cyan;
           }
       }

       currentReflectionPhase = ReflectionPhases.None;
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

        
            // Create placeholders for the body
            while (snakeTail.GetLength()-1 < currentQuestion.orderedAnswers.Count) //minus 1 because the head is the first position
            {
                snakeTail.AddTail();
            }
        
            snakeTail.SetNextPlaceholder(); //After creating tail circles, set next placeholder
        }
        silkyInstances[0].GetComponent<SnakeGrow>().contentViewSnakeTail = silkyInstances[1].GetComponent<SnakeTail>();
        snakeTail = silkyInstances[0].GetComponent<SnakeTail>(); //Ensure GameManager's snakeTail is the main characters (unknown if needed - precaution as of now)
        SetContentSilkyPosition();
    }
    
    
    // ליצור מסיח על המסך
    void CreateAnswer(AnswerModel answerModel,List<Transform> dupMulberries)
    {
        if (dupMulberries.Count > 0)
        {
            int randPos = Random.Range(0, dupMulberries.Count);
            GameObject newAnswerPrefab = Instantiate(orderItemPrefab, dupMulberries[randPos]); // יוצר מופע של הפריפאב
            newAnswerPrefab.name = "OrderItem_" + answerModel.orderIndex;
            OrderItem orderItemScript = newAnswerPrefab.GetComponent<OrderItem>();
            orderItemScript.SetAnswer(answerModel);
            orderItems.Add(orderItemScript); // מוסיפים לרשימת המופעים שעל המסך
            dupMulberries.RemoveAt(randPos);
        }
        else
        {
            Debug.Log("Not enough positioners for the amount of order items");
        }
    }

    public void UsePotion()
    {
        // TODO: Add sound effect for failure/mistake
        if (numPotions > 0)
        {
            numPotions--;
            potionText.text = numPotions.ToString();
            if (numPotions == 0)
            {
                potionText.color = Color.red;
            }
            // TODO: Add swirly eyes animation
            // TODO: Add brief game pause where the player can't move and the character sprites transparency goes up and down
        }
        else
        {
            QuestionFailed();
        }
    }
    
    public void AddPotion(int potionsToReceive)
    {
        numPotions += potionsToReceive;
        potionText.text = numPotions.ToString();
        potionText.color = Color.cyan;
        //TODO: Add animation effect for potion received
    }
    
    public void AddTime()
    {
        currentGameTime += awardedTimePerAnswer;
        awardedTimeThisQuestion += awardedTimePerAnswer;
        //TODO: Add animation effect for time received
    }
    
    void DestroyAllAnswers()
    {
        foreach (OrderItem orderItem in orderItems)
        {
            if (orderItem != null)
                Destroy(orderItem.gameObject);
        }
        
        orderItems.Clear();
    }
    
    private void ScreenStatus(string screenToShow, Color screenColor) //פונקציה שתפקידה לעדכן את הסטטוס של המסך בסיוּם שאלה (בין שמדובר באכילת תות שגוי, בהצלחה או כאשר נגמר הזמן)
    {
        if (screenStatusText != null)
        {
            screenStatusText.gameObject.SetActive(true);
            screenStatusText.text = screenToShow;
            screenStatusText.color = screenColor;
        }
    }
    
    //Note: EndQuestion is intentionally separated from ScreenStatus for separation of concerns between the functions (helps with the parameters being called) and for future modularity
    private void EndQuestion()
    {
        isTimerRunning = false;
        totalgameTime += game.timePerQuestion+awardedTimeThisQuestion-currentGameTime; //חקן היה על השאלה מחברים את הזמן המוקצה לכל שאלה עם הזמן שהתקבל בשאלה ומחסירים את הזמן שנותר כדי לקבל את סה"כ הזמן שהש
        DestroyAllAnswers(); //Should happen before the option is given to press space to continue
        currentReflectionPhase = ReflectionPhases.MovingToStartAnchor;
    }
    
    //פונקציה שמטפלת בסיום שאלה בהצלחה
    public void QuestionSuccess() //TODO: Consider making single function with bool input success/fail
    {
        EndQuestion();
        questionNumber++;
      //מסירים את שאלה מן המאגר רק לאחר שהשחקן ענה נכון
        allQuestions.Remove(currentQuestion);
        score += 100f / (currentQuestion.attempts * game.questionList.Count); //נוסחה לחישוב הציון. אצל נטע כתוב totalQuestions במקום game.questionList.Count 
        UpdateProgressBar(); //מטעינים את מד-ההתקדמות
        if (questionNumber >= game.questionList.Count) WinConditionReached();
        else
        {
            ScreenStatus("כל הכבוד! הצלחת את השאלה במלואה! \n לחצו רווח כדי להמשיך לשאלה הבאה",Color.cyan);
        }
    }

    private void QuestionFailed() //פונקציה שמטפלת בסיוּם שאלה באי-הצלחה כאשר השחקן אכל תות לא לפי הסדר הנכון
    {
        EndQuestion();
        totalGameMistakes++;
        ScreenStatus("אכלתם תות לא נכון \n לחצו על רווח כדי להמשיך ולנסות שוב",Color.red);
    }
    
    public void Pause()
    {
        EndQuestion();
        // KillCommonGameObjects();
        ScreenStatus("|| \n עצרתם לקחת אוויר? לחצו רווח כדי להמשיך",Color.cyan);
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
        EndQuestion();
        ScreenStatus("נגמר לכם הזמן! \n לחצו על רווח כדי להמשיך ולנסות שוב", Color.red);
    }
    private void UpdateProgressBar() //פונקציה שאחראית על עדכון מד-ההתקדמות
    {
        progress.text = questionNumber + "/" + game.questionList.Count;
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
        timerText.text = seconds.ToString(); 
    }
    private void NextQuestion()
    {
        if (questionNumber < game.questionList.Count)
        {
            KillCommonGameObjects();
            CreateQuestion(); 
            screenStatusText.gameObject.SetActive(false);
        }
        else
        {
            WinConditionReached();
        }
    }

    private void WinConditionReached()
    {
        isTimerRunning = false;
        if (screenStatusText != null) 
        {
            //הגדרת סטרינג עבור סקרין טו שואו
            string screenToShow = "ניצחתם! \n" +
                                  "לחצו R או על הכפתור כדי להתחיל מחדש \n" +
                                  "ציון סופי: " + "      "+ " נקודות \n" +
                                  "זמן כולל: " + Mathf.Floor(totalgameTime) + " שניות | כמות טעויות: " +
                                  totalGameMistakes; 
            // מדליקים את תיבת הציון ומכניסים אליה את המספר
            if (finalScoreText != null)
            {
                finalScoreText.gameObject.SetActive(true);
                float roundedScore = Mathf.Round(score);
                finalScoreText.text = roundedScore.ToString(); 
            }
            ScreenStatus(screenToShow,Color.cyan);
            KillCommonGameObjects();
        }
        gameWon = true;
        restartBtn.SetActive(gameWon); //Not done in update as it would be expensive
        uiDuringMainGame.SetActive(false);
    }

    public void ChangeView(bool shouldBeStaticView)
    {
        if (shouldBeStaticView != isStaticView)
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

