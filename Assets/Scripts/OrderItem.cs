using TMPro;
using UnityEngine;
using static DataModels;

public class OrderItem : MonoBehaviour
{
    public AnswerModel answer;
    public SpriteRenderer answerImage; 
    public TextMeshPro answerText;
    [SerializeField] private MeshRenderer textMesh;
    [SerializeField] private GameObject highlight;
    public bool touched;
    
    [SerializeField] private GameObject target;
    public int orderIndex;
    [SerializeField] private Vector3 textBounds = new Vector3(40, 20, 1);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        touched = false;
        if (highlight != null ) {highlight.SetActive(touched);} //touched is false at start so set to false
    }
    
    public void SetAnswer(AnswerModel answerModel)
    {
        if (answerModel.IsValid()) {
            if (answerModel.imageContent != null) //יש תמונה
            {
                answerImage.sprite = answerModel.imageContent;
                answerImage.enabled = true;
                answerText.enabled = false;
            }
            else if (answerModel.textContent != null) //אין תמונה-יש טקסט
            {
                //תתאים את הטקסט
                // מקבלת טקסטמשפרו-מיקום של הטקסט, לשים שֵם
                // תזין במקום של הטקסט את התוכן-משתמש בסרוויס שמתאים עברית אנגלית
                RTLFixer.SetTextInTMP(answerText, answerModel.textContent,answerModel.isRTL);
                answerText.sortingOrder = (answerModel.orderIndex-1)*2+6; //+6 and times 2 to be different from each text component of the body parts
                answerText.enabled = true;
                answerImage.enabled = false;
                
                // המצלמה חתכה טקסט כשהמרכז שלו יצא מגבולותיה אז החלטנו להגדיר את הבאונדס של הטקסט פעם אחת בקוד כדי לסדר את זה. הפונקציה הבאה מגדירה מחדש באונדס.
                textMesh.bounds = new Bounds(Vector3.zero, textBounds); // Defining new bounds as functions such as Expand and SetMinMax didn't work
            }
        }
        else // אם אין כלום / לא תקין
        {
            Destroy(gameObject); // תשמיד את האובייקט    
            return; // אל תמשיך את הפונקציה 
        }

        //אם אני פה יש תוכן
        answer = answerModel;
        orderIndex = answerModel.orderIndex;
    }
    public void SetObjectTouched(bool touchedBool)
    {
        touched = touchedBool;
        highlight.SetActive(touched);
    }
}
