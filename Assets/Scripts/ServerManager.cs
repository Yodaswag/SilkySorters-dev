using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

public class ServerManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private GameObject startButton;
    [SerializeField] private TMP_Text errorText; 

    string projectURL = "https://localhost:7296/"; 
    string apiURL = "api/Unity/GetCode/"; 
    string imagesFolder = "uploadedFiles/"; 

    void Start()
    {
        if (errorText != null) errorText.text = "";
    }
    
    void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        bool enterPressed = Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        if (enterPressed && startButton.activeSelf)
        {
            CheckCode();
        }
    }

    // הפונקציה עודכנה כדי להשתמש ב-RTLFixer עבור הודעות השגיאה בעברית
    private void ShowError(string uiMessage, string logMessage)
    {
        Debug.LogError("ServerManager: " + logMessage); // English only — keep Hebrew out of the Console
        if (errorText != null) 
        {
            RTLFixer.SetTextInTMP(errorText, uiMessage);
            errorText.alignment = TextAlignmentOptions.Center; // מומלץ ליישור טקסט בעברית
        }
    }

    public async void CheckCode()
    {
        string code = codeInput.text;
        
        // ולידציה: מניעת שליחת בקשה אם הקוד ריק או מכיל רק רווחים
        if (string.IsNullOrWhiteSpace(code))
        {
            ShowError("אנא הזינו קוד משחק.", "empty game code submitted");
            return;
        }

        startButton.SetActive(false);
        if (errorText != null) errorText.text = ""; // איפוס שגיאות קודמות

        DataModels.GameModel newGame = await GetGameFromServer(code);

        if (newGame != null)
        {
            GlobalSceneManager.Game = newGame;
            SceneManager.LoadScene("StartAnimation"); 
        }

        startButton.SetActive(true);
    }
    
    async Task<DataModels.GameModel> GetGameFromServer(string code)
    {
        string endpoint = projectURL + apiURL + code; 
        Debug.Log(endpoint);
        
        string jsonString = await GetDataFromServer(endpoint); 
        
        // מקרה קצה: שרת החזיר תשובה ריקה או שגיאה
        if (string.IsNullOrEmpty(jsonString))
        {
            return null; 
        }

        // מקרה קצה: השרת החזיר דף HTML (כמו 404) או טקסט שאינו ג'ייסון
        if (!jsonString.TrimStart().StartsWith("{"))
        {
            ShowError("שגיאה: קוד המשחק אינו תקין או שלא נמצא משחק.", "response was not JSON (game not found / invalid code)");
            return null;
        }
        
        ServerGame serverGame = JsonUtility.FromJson<ServerGame>(jsonString);

        // מקרה קצה: המשחק לא פורסם
        if (!serverGame.isPublish)
        {
            ShowError("המשחק עדיין לא פורסם.", "game found but not published");
            return null;
        }

        // מקרה קצה: המשחק נטען אבל אין בו שאלות
        if (serverGame.questions == null || serverGame.questions.Count == 0)
        {
            ShowError("שגיאה: למשחק זה אין שאלות.", "game has no questions");
            return null;
        }

        DataModels.GameModel unityGame = await ParseGameAsync(serverGame);
        return unityGame; 
    }

    async Task<string> GetDataFromServer(string url)
    {
        using var http = UnityWebRequest.Get(url);
        var get = http.SendWebRequest();
        while (get.isDone == false)
        {
            await Task.Yield();
        }

        if (http.result == UnityWebRequest.Result.Success)
        {
            return http.downloadHandler.text;
        }
        else if (http.result == UnityWebRequest.Result.ConnectionError)
        {
            // לא הצלחנו אפילו להגיע לשרת — השרת כבוי / לא זמין / עדיין לא קיים
            ShowError("לא ניתן להתחבר לשרת. ודאו שהשרת פעיל ונסו שוב.",
                      "connection error - server unreachable/offline at " + url);
            return null;
        }
        else
        {
            string errorMessage = http.downloadHandler.text;
            if (string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = "שגיאת שרת או שהקוד אינו קיים.";
            }
            ShowError(errorMessage, "server returned HTTP " + http.responseCode);
            return null;
        }
    }

    public async Task<Sprite> LoadImage(string endpoint)
    {
        using var http = UnityWebRequestTexture.GetTexture(endpoint);
        var get = http.SendWebRequest();
        while (get.isDone == false)
        {
            await Task.Yield();
        }

        if (http.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(http);
            if (texture == null) return null;

            Rect spriteRect = new Rect(0, 0, texture.width, texture.height);
            Vector2 spritePivot = new Vector2(0.5f, 0.5f);
            return Sprite.Create(texture, spriteRect, spritePivot);
        }
        else
        {
            return null;
        }
    }
    
    public async Task<DataModels.GameModel> ParseGameAsync(ServerGame serverGame)
    {
        DataModels.GameModel gameModel = new DataModels.GameModel();
        gameModel.gameName = serverGame.gameName;
        gameModel.timePerQuestion = serverGame.time;
        gameModel.hasPotions = serverGame.hasPotion;
        gameModel.questionList = new List<DataModels.QuestionModel>();

        for (int i = 0; i < serverGame.questions.Count; i++)
        {
            DataModels.QuestionModel parsedQuestion = await ParseQuestionAsync(serverGame.questions[i]);
            
            if (parsedQuestion == null) return null; 
            
            gameModel.questionList.Add(parsedQuestion);
        }

        return gameModel;
    }

    public async Task<DataModels.QuestionModel> ParseQuestionAsync(ServerQuestion serverQuestion)
    {
        // מקרה קצה: שאלה בלי פריטים
        if (serverQuestion.items == null || serverQuestion.items.Count == 0)
        {
            ShowError("שגיאה: ישנה שאלה ללא פריטים.", "a question has no items");
            return null;
        }

        DataModels.QuestionModel questionModel = new DataModels.QuestionModel();
        questionModel.questionContent = serverQuestion.instruction;
        questionModel.orderStartLabel = serverQuestion.startLabel;
        questionModel.orderEndLabel = serverQuestion.endLabel;
        questionModel.attempts = 0;
        questionModel.orderedAnswers = new List<DataModels.AnswerModel>();

        List<int> usedIndices = new List<int>();

        for (int i = 0; i < serverQuestion.items.Count; i++)
        {
            int currentIndex = serverQuestion.items[i].orderIndex;
            if (usedIndices.Contains(currentIndex))
            {
                ShowError("שגיאה: קיימת כפילות במיקומי הפריטים בשאלה.", "duplicate item order index in a question");
                return null;
            }
            usedIndices.Add(currentIndex);

            DataModels.AnswerModel parsedAnswer = await ParseAnswerAsync(serverQuestion.items[i]);
            
            if (parsedAnswer == null) return null;

            questionModel.orderedAnswers.Add(parsedAnswer);
        }

        return questionModel;
    }

    public async Task<DataModels.AnswerModel> ParseAnswerAsync(ServerItem serverItem)
    {
        DataModels.AnswerModel answerModel = new DataModels.AnswerModel();
        answerModel.orderIndex = serverItem.orderIndex;
        answerModel.isImage = serverItem.isImage;

        if (serverItem.isImage)
        {
            string imageEndpoint = projectURL + imagesFolder + serverItem.content;
            answerModel.imageContent = await LoadImage(imageEndpoint);
            
            // מקרה קצה: תמונה לא קיימת בשרת
            if (answerModel.imageContent == null)
            {
                ShowError("שגיאה: התמונה עבור פריט לא נמצאה בשרת.", "item image not found on server");
                return null;
            }
            
            answerModel.textContent = ""; 
        }
        else
        {
            answerModel.textContent = serverItem.content;
            answerModel.imageContent = null; 
        }

        return answerModel;
    }
}

//מחלקות מהשרת
[System.Serializable]
public class ServerGame
{
    public string gameName;
    public int gameID;
    public bool hasPotion;
    public bool isPublish;
    public int time;
    public int userID;
    public List<ServerQuestion> questions;
}

[System.Serializable]
public class ServerQuestion
{
    public string endLabel;
    public int gameID;
    public string instruction;
    public int questionID;
    public string startLabel;
    public List<ServerItem> items;
}

[System.Serializable]
public class ServerItem
{
    public int answerID;
    public string content;
    public bool isImage;
    public int orderIndex;
    public int questionID;
}