using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;using UnityEngine.SocialPlatforms.Impl;


public class GlobalSceneManager : MonoBehaviour
{
    public static int score = 100;
    public static DataModels.GameModel Game;
    public static int time = 180; //seconds
    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] TextMeshProUGUI timeText;

    private static InputSystem_Actions sharedActions;
    // Single shared input instance for the whole game — one source of truth for all scripts.
    // ponytail: lazily created, never disposed. Safe while domain reload is ON (statics reset
    // each Play). If you ever disable domain reload, reset sharedActions in a [RuntimeInitializeOnLoadMethod].
    private static void EnsureActions()
    {
        if (sharedActions == null)
        {
            sharedActions = new InputSystem_Actions();
            sharedActions.Enable();   // enables both the Player and UI maps
        }
    }

    public static InputSystem_Actions.PlayerActions Player
    {
        get
        {
            EnsureActions();
            return sharedActions.Player;
        }
    }

    public static InputSystem_Actions.UIActions UI
    {
        get
        {
            EnsureActions();
            return sharedActions.UI;
        }
    }

    public void StartMainGame()
    {
        SceneManager.LoadScene("MainGame");
    }

    void Update()
    {
        // Space/Interact skips the intro and jumps straight to MainGame, matching the Skip button.
        if (SceneManager.GetActiveScene().name != "StartAnimation")
        {
            return;
        }
        if (Player.Interact.WasPressedThisFrame())
        {
            StartMainGame();
        }
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
                RTLFixer.SetTextInTMP(timeText, $"{time / 60:00}:{time % 60:00} דקות");
            }
        }
    }
}
