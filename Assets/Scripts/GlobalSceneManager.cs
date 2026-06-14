using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;using UnityEngine.SocialPlatforms.Impl;


public class GlobalSceneManager : MonoBehaviour
{
    public static int score = 100;
    public static DataModels.GameModel Game;
    public static int time = 180; //seconds
    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] TextMeshProUGUI timeText;
    
    public void StartMainGame()
    {
        SceneManager.LoadScene("MainGame");
    }

    public void ShowFinalScreen(int gameScore, int gameTime)
    {
        score = gameScore;
        time = gameTime;
        SceneManager.LoadScene("FinalScreen");
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene("StartGame");
    }
    void Awake()
    {
        if (SceneManager.GetActiveScene().name == "FinalScreen")
        {
            if (scoreText != null && timeText != null)
            {
                scoreText.text = score.ToString();
                RTLFixer.SetTextInTMP(timeText,time + " שניות"); //TODO: Format time as mm:ss instead of total seconds
            }
        }
    }
}
