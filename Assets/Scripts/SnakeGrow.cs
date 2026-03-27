using UnityEngine;

public class SnakeGrow : MonoBehaviour
{
    [SerializeField] private SnakeTail snakeTail;
    [SerializeField] private ParticleSystem eatEffect;
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
        if (Input.GetKeyDown(KeyCode.Return))
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
                    eatEffect.Play();
                    Destroy(objectTouched);
                    
                    if (answersProvided == gameManager.currentQuestion.orderedAnswers.Count)
                    {
                        gameManager.QuestionSuccess();
                    }
                }
                else
                {
                    gameManager.QuestionFailed();
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
    
    
    
}