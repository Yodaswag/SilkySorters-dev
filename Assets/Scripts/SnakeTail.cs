using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static DataModels;

public class SnakeTail : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] Transform snakeHeadGfx;
    [SerializeField] GameObject singleTailPrefab;
    [SerializeField] public float rectWidth; 
    [SerializeField] public float rectHeight;
    [Tooltip("If true, segments rotate to face direction. If false, they stay upright (like in your screenshots).")]
    [SerializeField] public bool rotateSegments;

    [Header("State")]
    private List<SingleTail> snakeTail = new List<SingleTail>();
    private List<AnswerModel> answersProvided = new List<AnswerModel>();
    public List<Vector3> positions = new List<Vector3>();
    public GameManager gameManager;

    private int positionsBuffer = 5;
    private float placeholder_alpha = 0.35f;
    private float next_placeholder_alpha = 0.65f;
    // We expose this so the tails know how far along the current path segment we are (0.0 to 1.0)
    public float CurrentLerpT { get; private set; } 

    void Awake()
    {
        positions.Add(snakeHeadGfx.position);
    }
    
    void Update()
    {
        // Calculate direction from the last recorded point to the current head position
        Vector3 displacement = snakeHeadGfx.position - positions[0];
        Vector3 direction = displacement.normalized;
        float distanceTraveled = displacement.magnitude;

        // CALCULATE DYNAMIC GAP
        // If moving Horizontal (x=1), use Width. If moving Vertical (y=1), use Height.
        // We use Lerp to handle diagonal movement smoothly.
        float requiredGap = GetGapForDirection(direction);

        if (distanceTraveled > requiredGap)
        {
            // Add new point based on the exact required gap
            Vector3 newPoint = positions[0] + (direction * requiredGap);
            positions.Insert(0, newPoint);
            
            // Remove the oldest point to keep the list size manageable
            // (Only remove if we have more points than tails + buffer)
            if (positions.Count > snakeTail.Count + positionsBuffer) 
            {
                positions.RemoveAt(positions.Count - 1);
            }

            // Reduce distance by the gap we just "consumed"
            distanceTraveled -= requiredGap;
            
            // Recalculate the gap for the NEW remaining distance (in case direction changed sharply)
            // This ensures smooth transitions at corners
            requiredGap = GetGapForDirection((snakeHeadGfx.position - positions[0]).normalized);
        }

        // Save the percentage (0 to 1) for the tails to use
        CurrentLerpT = distanceTraveled / requiredGap;
    }

    // Helper math to determine how "long" the snake segment is in a specific direction
    public float GetGapForDirection(Vector3 dir)
    {
        // The function Math.Abs(dir.x) returns 1 when horizontal, 0 when vertical.
        return Mathf.Lerp(rectHeight, rectWidth, Mathf.Abs(dir.x));
    }

    public void AddTail()
    {
        // Spawn each body part directly below the previous one for the starting positions of the tails.
        Vector3 startPos = positions[positions.Count - 1];
        // startPos.y -= rectHeight;
        startPos.x -= rectWidth;
        
        GameObject tail = Instantiate(singleTailPrefab, startPos, Quaternion.identity, transform);
        SingleTail tailScript = tail.GetComponent<SingleTail>();
        
        // Setup data
        tailScript.Init(this, snakeTail.Count);

        snakeTail.Add(tailScript);
        
        // Add extra buffer positions to the history so new tails don't snap
        positions.Add(startPos); 

        // Visuals
        tailScript.tailBG.color = new Color(1f, 1f, 1f, placeholder_alpha);
        tailScript.tailBG.sortingOrder = (snakeTail.Count-1)*2 + 5;
        tailScript.textComp.sortingOrder = tailScript.tailBG.sortingOrder + 1;
    }

    public void AddAnswer(AnswerModel answer) //פונקצייה שממלאת את ה-PLACEHOLDERS של תולעת המשי שלנו בתשובות
    {
        if (answersProvided.Count >= snakeTail.Count) return;

        SingleTail tailScript = snakeTail[answer.orderIndex-1]; //orderIndex fully maps to the desired positions and that's why we can use it
        answersProvided.Add(answer);
        
        if (answer.IsValid())
        {
            if (answer.isImage) 
            {
                tailScript.imageComp.sprite = answer.imageContent; 
                tailScript.imageComp.sortingOrder = tailScript.tailBG.sortingOrder + 1;
                tailScript.imageComp.gameObject.SetActive(true);
                if(tailScript.textComp) tailScript.textComp.gameObject.SetActive(false);
            }
            else
            {
                RTLFixer.SetTextInTMP(tailScript.textComp, answer.textContent);
                tailScript.textComp.sortingOrder = tailScript.tailBG.sortingOrder + 1;
                tailScript.textComp.gameObject.SetActive(true);
                if(tailScript.imageComp) tailScript.imageComp.gameObject.SetActive(false);
            }
        }
        tailScript.tailBG.color = new Color(1f,1f,1f,1f); //Set to "not placeholder" or full body part
        // if (answer.orderIndex <= gameManager.currentQuestion.orderedAnswers.Count-1) // This helps avoid the end label being set as a placeholder
        //     SetNextPlaceholder();
        SetNextPlaceholder();
    }

    public void SetNextPlaceholder()
    {
        if (answersProvided.Count < snakeTail.Count-1) //-1 because of end label
        {
            snakeTail[answersProvided.Count].tailBG.color = new Color(1f,1f,1f,next_placeholder_alpha);    
        }
    }
    
    public List<AnswerModel> GetAnswersProvided() => answersProvided;
    public int GetLength() => positions.Count;
}