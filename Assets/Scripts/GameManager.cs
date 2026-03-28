using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static DataModels; //Instead of writing DataModels.GameModel/QuestionsModel/AnswerModel, just added a single using static class line at the top of each file that needs it.
public class GameManager : MonoBehaviour
{
    [Header ("Player and Camera")]
    [SerializeField] CinemachineCamera vcam; //Since the player instance gets destroyed and reinstantiated every question - the camera must be attached to follow it.
    [SerializeField] GameObject silkyPlayerPrefab;
    public GameObject playerInstance;
    [SerializeField] private SnakeTail snakeTail;
    
    [Header ("Mulberries (Order Items)")]
    [SerializeField] GameObject orderItemPrefab;
    private List<GameObject> orderItems = new List<GameObject>(); //List of game objects of mulberry answer items
    [SerializeField] private float minX = -16f;
    [SerializeField] private float maxX = 16f;
    [SerializeField] private float minY = -8f;
    [SerializeField] private float maxY = 8f;
    [SerializeField] private float spawnCheckRadius = 5f;
    [SerializeField] private LayerMask foreground; //Used to not mix collision check with OverlapCircle on spawn with the LeafBG. Note: LeafBG must have a background for cinemachine camera clamping.
    
    [Header("Game Controls")]
    private bool gameWon;
    private int questionNumber;  //מספר השאלה הנוכחי
    public QuestionModel currentQuestion; // השאלה שעכשיו עונים עליה. //Used by SnakeGrow and SnakeTail and thus public 
    [SerializeField] GameModel game; 
    List<QuestionModel>allQuestions;  //רשימה של כל השאלות שיש
    int questionsCount; // כמה שאלות יש במשחק בכללי
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI screenStatusText; //
    [SerializeField] private TextMeshProUGUI finalScoreText; // תיבה נפרדת רק לציון הסופי
    [SerializeField] private GameObject restartBtn; //Only appears after game won
    [SerializeField] TextMeshProUGUI questionText; 
    [SerializeField] TextMeshProUGUI progress; //Progress TextMeshPro UI (i.e 0/5 questions)
    [SerializeField] TextMeshProUGUI topic; //Topic TextMeshPro UI (i.e Hebrew for 7th grade)
    [SerializeField] private Image linearProgressFill; // בשביל המילוי של מד-ההתקדמות הליניארי שלנו
    [SerializeField] private GameObject uiDuringMainGame; // Used to hide pause button, question and timer when game over
    [Header("Global Timer")]
    private float totalgameTime; //זמן כולל למשחק
    private int totalGameMistakes;
    [SerializeField] private TextMeshProUGUI timerText; //טקסט UI שמציג זמן
    private float currentGameTime;// הזמן שנותר בפועל
    private bool isTimerRunning; //משתנה בוליאני שנועד לבדוק אם הטיימר רץ
    private float score; // משתנה גלובלי שיכיל את הציון למשחק
    void Start()
    {
        uiDuringMainGame.SetActive(true);
        gameWon = false;
        Time.timeScale = 1f;
        restartBtn.SetActive(false);
        screenStatusText.gameObject.SetActive(false);

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
            else if (Input.GetKeyDown(KeyCode.Space) && !gameWon)
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
        CreateQuestion();
        topic.text = game.gameName;
    }
    // פונקציה ליצירת שאלות
    //אם אנחנו רוצים לשנות את הלוגיקה כך שתכלול שאלות אקראיות - יש לחסום את המתודה מלקבל פרמטרים ואז לייצר מספר אקראי בתוכה. נצטרך גם ליישם את RemoveQuestion בשביל השאלות שהשחקן הצליח בהן
    void CreateQuestion() //If we wanted to change up the logic to include random questions we would make this method accept no parameters and then generate the random number in it, we'd also need to implement RemoveQuestion for questions the player succeeded on
    {
        //הגרלת שאלה אקראית מתוך השאלות שנותרו
        int randomQuestionNumber = Random.Range(0, allQuestions.Count);
        currentQuestion = allQuestions[randomQuestionNumber]; // השאלה הראשונה-אקראית
        
        // currentQuestion = allQuestions[questionIndex]; // השאלה הראשונה 
       RTLFixer.FixRtl(questionText, currentQuestion.questionContent); //Send the TMPro (question text) and the content to fixed to the RTLFixer
       currentGameTime = game.timePerQuestion;
       UpdateTimerUI(); //קריאה לפונקציה שמעדכנת את הזמן
       isTimerRunning = true;
       currentQuestion.attempts++; //בשליפת שאלה, נוסיף ניסיון מענה
       ResetPlayer(); //לשים בהערה אם רוצים שהתולעת תהיה אחת רציפה שמתמלאת ומתרוקנת 
       foreach (AnswerModel answer in currentQuestion.orderedAnswers) // Create all mulberries after 
       {
           CreateAnswer(answer);  
       }
    }

    // Used when starting a new question. Currently the code logic is that we destroy the previous silkworm and create a new with the correct amount of placeholders rather than emptying it.
    void ResetPlayer()
    {
        playerInstance = Instantiate(silkyPlayerPrefab);
        snakeTail = playerInstance.GetComponent<SnakeTail>();
        snakeTail.gameManager = this;
        vcam.Follow = playerInstance.transform;
        vcam.LookAt = playerInstance.transform;
        
        playerInstance.GetComponent<SnakeGrow>().gameManager = this;
        
        // Create placeholders for main body
        while (snakeTail.GetLength()-1 < currentQuestion.orderedAnswers.Count) //-1 because the head is the first position
        {
            snakeTail.AddTail();
        }
        
        snakeTail.SetNextPlaceholder(); //After creating tail circles, set next placeholder
    }

    void DestroyAllAnswers()
    {
        foreach (GameObject orderItem in orderItems)
        {
            Destroy(orderItem);
        }
        
        orderItems.Clear();
    }
    
    // ליצור מסיח על המסך
    void CreateAnswer(AnswerModel answerModel)
    {
        Vector2 spawnPos = FindEmptyPosition();
        if (spawnPos != Vector2.zero) // אם מצאנו מקום תקין
        {
            GameObject newAnswerPrefab = Instantiate(orderItemPrefab, spawnPos, Quaternion.identity); // יוצר מופע של הפריפאב
            newAnswerPrefab.name = "OrderItem_" + answerModel.orderIndex;
            OrderItem orderItemScript = newAnswerPrefab.GetComponent<OrderItem>();
            orderItemScript.SetAnswer(answerModel);
            orderItems.Add(newAnswerPrefab); // מוסיפים לרשימת המופעים שעל המסך
        }

        else //אם לא מצאנו מקום תקין
        {
            CreateAnswer(answerModel); //רקורסיה ללמצוא מקום חדש
        }
    }

    Vector2 FindEmptyPosition()
    {
        int maxAttempts = 50;
        for (int i = 0; i < maxAttempts; i++)
        {
            // הגרלת מיקום
            float x = Random.Range(minX, maxX);
            float y = Random.Range(minY, maxY);
            Vector2 potentialPos = new Vector2(x, y);

            //   האם המיקום הזה פנוי
            // הפקודה הזו בודקת אם יש Collider כלשהו ברדיוס שהגדרנו
            Collider2D hit = Physics2D.OverlapCircle(potentialPos, spawnCheckRadius,foreground); // We had to define a collider to LeafBG for cinemachine clamping and confinement, therefore we always have at least one collision per position and we need an array.
            if (hit == null) // By necessity, if the collision array has more than 1 collider2d, it's either the player character or another item
            {
                return potentialPos;
            }
        }

        return Vector2.zero;
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
        totalgameTime += game.timePerQuestion-currentGameTime;
        DestroyAllAnswers(); //Should happen before the option is given to press space to continue
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

    public void QuestionFailed() //פונקציה שמטפלת בסיוּם שאלה באי-הצלחה כאשר השחקן אכל תות לא לפי הסדר הנכון
    {
        EndQuestion();
        totalGameMistakes++;
        ScreenStatus("אכלתם תות לא נכון \n לחצו על רווח כדי להמשיך ולנסות שוב",Color.red);
    }
    
    public void Pause()
    {
        EndQuestion();
        if (playerInstance != null)
        {
            Destroy(playerInstance);
        }
        ScreenStatus("|| \n עצרתם לקחת אוויר? לחצו רווח כדי להמשיך",Color.cyan);
        Time.timeScale = 0f;
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
            if (playerInstance != null)
            {
                Destroy(playerInstance); //Reset the playerInstance, a new one will be created by CreateQuestion() //לשים בהערה אם רוצים שהתולעת תהיה אחת רציפה שמתלמאת ומתרוקנת 
            }
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
                finalScoreText.text = score.ToString(); 
            }
            ScreenStatus(screenToShow,Color.cyan);
            Destroy(playerInstance);
        }
        gameWon = true;
        restartBtn.SetActive(gameWon); //Not done in update as it would be expensive
        uiDuringMainGame.SetActive(false);
    }
}

