using TMPro;
using UnityEngine;
using System.Collections;
using static DataModels;

public class OrderItem : MonoBehaviour
{
    public AnswerModel answer;
    public SpriteRenderer answerImage;
    public TextMeshPro answerText;
    [SerializeField] private MeshRenderer textMesh;
    public bool touched;
    public bool isConsumable;

    [SerializeField] private GameObject target;
    public int orderIndex;
    [SerializeField] private Vector3 textBounds = new Vector3(40, 20, 1);

    [SerializeField] private Animator animator;
    public ImageScript imageScript;

    [SerializeField] private Sprite[] spritesheet;
    [SerializeField] private SpriteRenderer itemBG;

    [Header("Countdown")]
    [SerializeField] private SpaceCountdownPrompt countdownPrompt;
    [SerializeField] private float secondsPerFrame = 1f;

    private GameManager gameManager;
    private Transform playerTransform;
    private Coroutine countdownCoroutine;
    private Coroutine resetSpriteCoroutine;
    private bool playerInProximity;

    void Start()
    {
        animator.SetBool("Static", true);
        touched = false;
        playerInProximity = false;
        isConsumable = true;
        ApplyNormalSprite();
        HideCountdown();
    }

    public void Initialize(GameManager manager, Transform player)
    {
        gameManager = manager;
        playerTransform = player;
    }

    public void SetAnswer(AnswerModel answerModel)
    {
        if (answerModel.IsValid())
        {
            if (answerModel.imageContent != null)
            {
                imageScript.SetImage_KeepRatio(answerModel.imageContent);
                answerImage.enabled = true;
                answerText.enabled = false;
            }
            else if (answerModel.textContent != null)
            {
                RTLFixer.SetTextInTMP(answerText, answerModel.textContent);
                answerText.sortingOrder = (answerModel.orderIndex - 1) * 2 + 6;
                answerText.enabled = true;

                answerImage.enabled = false;
                imageScript.HideImage();

                textMesh.bounds = new Bounds(Vector3.zero, textBounds);
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        answer = answerModel;
        orderIndex = answerModel.orderIndex;
    }

    public void SetObjectTouched(bool touchedBool, bool keepCountdownInProximity = true)
    {
        touched = touchedBool;

        if (touched && animator.GetBool("Static") == false)
        {
            StartCountdown();
        }
        else if (!touched && (!keepCountdownInProximity || !playerInProximity))
        {
            StopCountdown();
        }

        if (isConsumable)
        {
            ApplyNormalSprite();
        }
    }

    public void SetPlayerInProximity(bool isInProximity)
    {
        bool wasInProximity = playerInProximity;
        playerInProximity = isInProximity;

        if (!playerInProximity && wasInProximity)
        {
            StopCountdown();
        }
    }

    public void MulberryChangeView(bool isStaticView)
    {
        animator.SetBool("Static", isStaticView);

        if (touched)
        {
            if (isStaticView)
            {
                StopCountdown();
            }
            else
            {
                StartCountdown();
            }
        }

        if (isConsumable)
        {
            ApplyNormalSprite();
        }
    }

    public void ShowFeedback(bool correct, float delay)
    {
        StopCountdown();

        if (resetSpriteCoroutine != null)
        {
            StopCoroutine(resetSpriteCoroutine);
            resetSpriteCoroutine = null;
        }

        isConsumable = false;

        if (correct)
        {
            itemBG.sprite = spritesheet[2];
        }
        else
        {
            itemBG.sprite = spritesheet[1];
            resetSpriteCoroutine = StartCoroutine(ResetSpriteRoutine(delay));
        }
    }

    private IEnumerator ResetSpriteRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (itemBG.sprite == spritesheet[1])
        {
            isConsumable = true;
            ApplyNormalSprite();
        }

        resetSpriteCoroutine = null;
    }

    private void StartCountdown()
    {
        if (!isConsumable || countdownPrompt == null || gameManager == null || playerTransform == null)
            return;

        StopCountdown();
        gameManager.SetCountdownActive(true);
        countdownCoroutine = StartCoroutine(CountdownRoutine());
        ApplyNormalSprite();
    }

    private IEnumerator CountdownRoutine()
    {
        countdownPrompt.Show(transform, playerTransform);

        int frameCount = countdownPrompt.FrameCount;

        if (frameCount <= 0)
        {
            countdownCoroutine = null;
            countdownPrompt.Hide();
            gameManager.SetCountdownActive(false);
            ApplyNormalSprite();
            yield break;
        }

        for (int i = 0; i < frameCount; i++)
        {
            if (!playerInProximity || !isConsumable)
            {
                break;
            }

            countdownPrompt.SetFrame(i);
            yield return new WaitForSeconds(secondsPerFrame);
        }

        countdownCoroutine = null;
        countdownPrompt.Hide();
        gameManager.SetCountdownActive(false);
        ApplyNormalSprite();
    }

    private void StopCountdown()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        if (gameManager != null)
        {
            gameManager.SetCountdownActive(false);
        }

        HideCountdown();

        if (isConsumable)
        {
            ApplyNormalSprite();
        }
    }

    public void HideCountdown()
    {
        if (countdownPrompt != null)
        {
            countdownPrompt.Hide();
        }
    }

    private void ApplyNormalSprite()
    {
        if ((touched || countdownCoroutine != null) && !animator.GetBool("Static"))
        {
            itemBG.sprite = spritesheet[3];
        }
        else
        {
            itemBG.sprite = spritesheet[4];
        }
    }

    private void OnDestroy()
    {
        StopCountdown();
    }
}
