using UnityEngine;
using System.Collections;
using System.Linq;

public class SnakeGrow : MonoBehaviour
{
    [SerializeField] private SnakeTail snakeTail;
    public SnakeTail contentViewSnakeTail;
    [SerializeField] private ParticleSystem eatEffect;
    [SerializeField] private FloatingWorldText floatingWorldText;
    [SerializeField] private Transform anchor;
    public GameManager gameManager;

    [SerializeField] private AudioClip eatSound;
    private AudioSource audioSource;

    private GameObject objectTouched;
    private OrderItem objectTouchedScript;

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
    }
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (inputActions.Player.Interact.WasPressedThisFrame()) 
        {
            if (objectTouchedScript != null  && objectTouchedScript.isConsumable)
            {
                int answersProvided = snakeTail.GetAnswersProvided().Count + 1; 
                
                if (answersProvided == objectTouchedScript.orderIndex) 
                {
                    objectTouchedScript.ShowFeedback(true,gameManager.feedbackDelay);
                    if (audioSource != null && eatSound != null)
                    {
                        audioSource.PlayOneShot(eatSound);
                    }
                    
                    snakeTail.AddAnswer(objectTouchedScript.answer);
                    contentViewSnakeTail.AddAnswer(objectTouchedScript.answer);
                    
                    if (answersProvided != gameManager.currentQuestion.orderedAnswers.Count) //If it isn't the last item
                    {
                        gameManager.AddTime();
                        if (gameManager.game.hasPotions)
                        {
                            int targetIndex = objectTouchedScript.answer.orderIndex;
                            int potionsToRecieve = 0;
                            foreach (int ind in gameManager.game.awardPotionInds)
                            {
                                if (ind == targetIndex)
                                    potionsToRecieve++;
                            }
                            if (potionsToRecieve > 0)
                            {
                                gameManager.AddPotion(potionsToRecieve);
                            }
                        }
                    }

                    StartCoroutine(HandleSuccessRoutine(objectTouched, answersProvided));
                }
                else
                {
                    objectTouchedScript.ShowFeedback(false,gameManager.feedbackDelay);
                    gameManager.UsePotion(objectTouchedScript.answer);
                }
            }
        }
    }

    private IEnumerator HandleSuccessRoutine(GameObject targetObject, int answersProvided)
    {
        yield return new WaitForSeconds(gameManager.feedbackDelay);

        if (eatEffect != null) eatEffect.Play();

        if (answersProvided == gameManager.currentQuestion.orderedAnswers.Count)
        {
            gameManager.QuestionSuccess();
        }

        if (targetObject != null)
        {
            Destroy(targetObject);
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D other = collision.collider;
        if (objectTouchedScript != null && objectTouched != other.transform.parent.gameObject)
        {
            if (other.transform.parent.CompareTag("Item"))
                objectTouchedScript.SetObjectTouched(false, false);
        }

        if (other.gameObject.CompareTag("Item"))
        {
            objectTouched = other.transform.parent.gameObject;
            objectTouchedScript = objectTouched.GetComponent<OrderItem>();
            objectTouchedScript.SetObjectTouched(true);
        }
    }
    
    private void OnCollisionExit2D(Collision2D collision)
    {
        Collider2D other = collision.collider;
        if (objectTouched == other.transform.parent.gameObject)
        {
            objectTouchedScript.SetObjectTouched(false);
            objectTouchedScript = null;
            objectTouched = null;
        }
    }

    public void ShowFloatingWorldText(string message, Color color)
    {
        Vector3 baseOffset = Vector3.zero;

        if (message == "+5s")
            baseOffset = new Vector3(-0.2f, 0.15f, 0f);
        else if (message == "+Potion")
            baseOffset = new Vector3(0.2f, 0.07f, 0f);

        Vector3 randomOffset = new Vector3(
            Random.Range(-0.08f, 0.08f),
            Random.Range(-0.03f, 0.06f),
            0f
        );

        FloatingWorldText instance = Instantiate(floatingWorldText, anchor);
        instance.transform.localPosition = baseOffset + randomOffset;
        instance.Initialize(message, color, gameManager.transform);
    }
}
