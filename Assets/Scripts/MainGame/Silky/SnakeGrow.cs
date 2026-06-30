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

    [Header("Face")]
    [SerializeField] private SpriteRenderer leftEye;
    [SerializeField] private SpriteRenderer rightEye;
    [SerializeField] private Transform mouth;        // z=180 happy (smile), z=0 sad (frown)
    [SerializeField] private Sprite cuteEye;
    [SerializeField] private Sprite sleepyEye;
    [SerializeField] private Sprite spiralEye;
    [SerializeField] private float eyeSpinSpeed = 360f;   // deg/sec while spiral eyes spin
    private bool eyesSpinning;

    [Header("Mistake Flash")]
    [SerializeField] private float flashDuration = 0.6f;   // total beat length; controls stay frozen this long
    [SerializeField] private int flashPulses = 3;          // fade-out/in cycles across the beat
    [SerializeField] private float flashMinAlpha = 0.25f;  // dimmest point of each pulse (scales each renderer's own alpha)

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
        if (eyesSpinning)
        {
            leftEye.transform.Rotate(0f, 0f, eyeSpinSpeed * Time.deltaTime);
            rightEye.transform.Rotate(0f, 0f, eyeSpinSpeed * Time.deltaTime);
        }

        if (inputActions.Player.Interact.WasPressedThisFrame())
        {
            if (objectTouchedScript != null && gameManager.controlsEnabled && objectTouchedScript.isConsumable)
            {
                int answersProvided = snakeTail.GetAnswersProvided().Count; 
                
                if (answersProvided == objectTouchedScript.orderIndex) 
                {
                    objectTouchedScript.ShowFeedback(true,gameManager.feedbackDelay);
                    if (audioSource != null && eatSound != null)
                    {
                        audioSource.PlayOneShot(eatSound);
                    }
                    
                    snakeTail.AddAnswer(objectTouchedScript.answer);
                    contentViewSnakeTail.AddAnswer(objectTouchedScript.answer);
                    
                    if (answersProvided != gameManager.currentQuestion.orderedAnswers.Count-1) //If it isn't the last item
                    {
                        gameManager.AddTime();
                    }

                    StartCoroutine(HandleSuccessRoutine(objectTouched, answersProvided));
                }
                else
                {
                    objectTouchedScript.ShowFeedback(false,gameManager.feedbackDelay);
                    MakeWormSad();
                    gameManager.UsePotion(objectTouchedScript.answer);
                }
            }
        }
    }

    private IEnumerator HandleSuccessRoutine(GameObject targetObject, int answersProvided)
    {
        yield return new WaitForSeconds(gameManager.feedbackDelay);

        if (eatEffect != null) eatEffect.Play();

        if (answersProvided == gameManager.currentQuestion.orderedAnswers.Count-1)
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

    private void SetBothEyes(Sprite eyeSprite)
    {
        leftEye.sprite = eyeSprite;
        rightEye.sprite = eyeSprite;
    }

    private void ResetEyeRotation() // back to default orientation z=180
    {
        leftEye.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        rightEye.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
    }

    public void MakeWormSad() // wrong answer / timeout: spiral eyes spin + frown
    {
        SetBothEyes(spiralEye);
        eyesSpinning = true;
        mouth.localRotation = Quaternion.Euler(0f, 0f, 0f);
    }

    public void MakeWormSleep() // after first reflection pass: sleepy eyes only (mouth untouched)
    {
        eyesSpinning = false;
        ResetEyeRotation();
        SetBothEyes(sleepyEye);
    }

    public void MakeWormHappy() // potion case, control returns: cute eyes + smile
    {
        eyesSpinning = false;
        ResetEyeRotation();
        SetBothEyes(cuteEye);
        mouth.localRotation = Quaternion.Euler(0f, 0f, 180f);
    }

    // Potion-cushioned mistake: freeze controls, pulse the whole worm's opacity while the spiral keeps
    // spinning (spin runs in Update via eyesSpinning), then return control and reset the face to happy.
    public void StartMistakeFlash()
    {
        StartCoroutine(MistakeFlashRoutine());
    }

    private IEnumerator MistakeFlashRoutine()
    {
        gameManager.controlsEnabled = false; // freezes movement (SnakeMove) and eating (gated in Update)

        SpriteRenderer[] wormRenderers = GetComponentsInChildren<SpriteRenderer>();
        Color[] originalColors = new Color[wormRenderers.Length];
        for (int i = 0; i < wormRenderers.Length; i++)
        {
            originalColors[i] = wormRenderers[i].color;
        }

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float cycles = (elapsed / flashDuration) * flashPulses;
            float wave = Mathf.Cos(cycles * 2f * Mathf.PI) * 0.5f + 0.5f; // 1 at each cycle's ends, 0 at the dip
            float pulse = Mathf.Lerp(flashMinAlpha, 1f, wave);
            for (int i = 0; i < wormRenderers.Length; i++)
            {
                Color c = originalColors[i];
                c.a = originalColors[i].a * pulse; // scale each renderer's own alpha so placeholders stay proportional
                wormRenderers[i].color = c;
            }
            yield return null;
        }

        for (int i = 0; i < wormRenderers.Length; i++)
        {
            wormRenderers[i].color = originalColors[i]; // restore exact originals (filled=1, placeholders=0.35/0.65)
        }

        gameManager.controlsEnabled = true; // control returns
        MakeWormHappy();                    // spiral stops, cute eyes, smile
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
