using UnityEngine;
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
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return)) //TODO: Add 3 second decision countdown before switching to static view
        {
            if (objectTouchedScript != null)
            {
                int answersProvided = snakeTail.GetAnswersProvided().Count + 1; //Add 1 because the list starts at 0
                // Player may only eat the next correct item in sequence (answersProvidedCount).
                if (answersProvided == objectTouchedScript.orderIndex) {
                    // PlayOneShot = מנגן את הסאונד פעם אחת בלי לעצור סאונדים אחרים
                    if (audioSource != null && eatSound != null)
                    {
                        audioSource.PlayOneShot(eatSound);
                    }
                
                    snakeTail.AddAnswer(objectTouchedScript.answer);
                    contentViewSnakeTail.AddAnswer(objectTouchedScript.answer);
                    eatEffect.Play();
                    
                    
                    if (answersProvided == gameManager.currentQuestion.orderedAnswers.Count)
                    {
                        gameManager.QuestionSuccess();
                    }
                    else // Give potions and bonus time. Only relevant if the question wasn't completed.
                    {
                        gameManager.AddTime();
                        if (gameManager.game.hasPotions)
                        {
                            int potionsToRecieve = gameManager.game.awardPotionInds.Count(x => x == objectTouchedScript.answer.orderIndex);
                            if (potionsToRecieve > 0)
                            {
                                gameManager.AddPotion(potionsToRecieve);
                            }
                        }
                    }
                    
                    Destroy(objectTouched);
                }
                else
                {
                    gameManager.UsePotion();
                }
            }
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D other = collision.collider;
        //to ensure that 2 items aren't highlighted simultaneously
        if (objectTouchedScript != null && objectTouched != other.transform.parent.gameObject)
        {
            if (other.transform.parent.CompareTag("Item"))
                objectTouchedScript.SetObjectTouched(false);
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