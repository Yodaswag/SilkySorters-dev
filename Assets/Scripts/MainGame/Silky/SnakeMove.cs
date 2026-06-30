using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

public class SnakeMove : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float animationMoveSpeed = 10f;
    [SerializeField] private float boostMultiplier = 2f;
    [SerializeField] private float smoothing = 0.2f; // 0..1 per physics step; higher = snappier, lower = floatier

    [Header("References")]
    [SerializeField] private Transform headTransform;
    [SerializeField] private Rigidbody2D rb;
    
    private Vector2 targetInput;
    private Vector2 currentInputVector;
    
    private float currentSplineTime = 0f;
    private bool comingFromRight = false;
    private bool isDraining = false;
    
    public GameManager gameManager;

    private void EnsureInitialized()
    {
        if (headTransform == null)
        {
            headTransform = transform;
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private Vector2 GetRootPosition()
    {
        if (rb != null)
            return rb.position;
        return (Vector2)transform.position;
    }

    private void MoveRoot(Vector2 nextPosition)
    {
        if (rb != null)
        {
            rb.MovePosition(nextPosition);
        }
        else
        {
            transform.position = nextPosition;
        }
    }

    private void Update()
    {
        EnsureInitialized();

        if (gameManager == null)
        {
            return; //Don't perform any part of the update function until ResetPlayer has set gameManager for this instance
        }
        
        // TODO: Consider moving to seperate script ReflectionController.cs
        // TODO: Add behaviour for the rest of reflection phases
        switch (gameManager.currentReflectionPhase)
        {
            case GameManager.ReflectionPhases.None:
                // Capture input state every frame to prevent dropped inputs
                targetInput = Vector2.zero;
                if (gameManager.controlsEnabled)
                    targetInput = GlobalSceneManager.Player.Move.ReadValue<Vector2>();
                
                // movement key dismisses the image popup
                if (targetInput.sqrMagnitude > 0.001f)
                    gameManager.HideImagePopup();   // no-op if popup already hidden
                break;

            case GameManager.ReflectionPhases.MovingToStartAnchor:
                Vector3 targetPos = gameManager.reflectionSpline.transform.position;
                Vector3 direction = (targetPos - transform.position).normalized;

                comingFromRight = (transform.position.x - targetPos.x) > 0;
                if (Vector3.Distance(transform.position, targetPos) < 0.05f)
                {
                    gameManager.SetReflectionPhase(GameManager.ReflectionPhases.FollowingSpline);
                    currentSplineTime = 0f;
                }
                
                // 2D Rotation for LookAt functionality
                if (direction != Vector3.zero)
                {
                    float lookAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    headTransform.rotation = Quaternion.Euler(0f, 0f, lookAngle - 90f);
                }

                transform.position = Vector3.MoveTowards(transform.position, targetPos, animationMoveSpeed * gameManager.SkipFactor * Time.deltaTime);
                gameManager.ChangeView(false); //Change to dynamic
                break;

            case GameManager.ReflectionPhases.FollowingSpline: //Coiling animation השתבללות
                comingFromRight = false;
                FollowSplinePath(comingFromRight); //If coming from right, play the spline animation in reverse (counter-clockwise). If not, play it normally (clockwise).
                gameManager.SetNightfall(currentSplineTime); //Background dims in lockstep with the coil
                // SetGlow(currentSplineTime);                  //Moonlight glow rises with the coil
                break;
            
            case GameManager.ReflectionPhases.Darkening: //stub - Darkening will likely happen continously during MovingTowardsAnchor and FollowingSpline
                gameManager.SetReflectionPhase(GameManager.ReflectionPhases.RevealingResults);
                break;
            
            case GameManager.ReflectionPhases.RevealingResults:
                if (!isDraining)
                {
                    isDraining = true;
                    StartCoroutine(RunBodyDrain());
                }
                break;
            
            case GameManager.ReflectionPhases.WaitingForNextQuestion: //stub
                break;
        }
    }

    private void FixedUpdate()
    {
        EnsureInitialized();

        if (gameManager == null)
        {
            return;
        }
        
        // Smooth and apply movement in FixedUpdate to sync with the physics engine.
        // Ease currentInputVector a fraction of the way toward targetInput each physics step.
        currentInputVector = Vector2.Lerp(currentInputVector, targetInput, smoothing);

        if (gameManager.currentReflectionPhase == GameManager.ReflectionPhases.None && gameManager.controlsEnabled)
            HandleMovement(); 
    }

    private void HandleMovement()
    {
        float currentSpeed = moveSpeed;

        if (GlobalSceneManager.Player.Boost.IsInProgress())
        {
            currentSpeed *= boostMultiplier;
        }

        if (currentInputVector.sqrMagnitude > 0.001f)
        {
            Vector2 movement = currentInputVector * (currentSpeed * Time.fixedDeltaTime);
            rb.MovePosition(rb.position + movement);

            float angle = Mathf.Atan2(currentInputVector.y, currentInputVector.x) * Mathf.Rad2Deg;
            
            // Sprite points "up" by default, so we subtract 90 degrees to align facing direction with movement vector.
            headTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            
            if (gameManager != null) gameManager.ChangeView(false); //The bool represents shouldBeStaticView. false = change to dynamic
        }
        else
        {
            if (gameManager != null) gameManager.ChangeView(true); //The bool represents shouldBeStaticView. false = change to static
        }
    }
    
    private IEnumerator RunBodyDrain()
    {
        yield return StartCoroutine(gameManager.RevealReflection());
        isDraining = false;
        // RevealReflection sets the next phase + reflection button (MistakeReviewPause or WaitingForNextQuestion)
    }

    private void FollowSplinePath(bool isReversed)
    {
        if (gameManager.reflectionSpline == null) return;

        float splineLength = gameManager.reflectionSpline.CalculateLength();
        currentSplineTime += (animationMoveSpeed * gameManager.SkipFactor / splineLength) * Time.deltaTime;

        if (currentSplineTime >= 1f)
        {
            currentSplineTime = 1f;
            gameManager.SetReflectionPhase(GameManager.ReflectionPhases.Darkening);
        }

        // Convert bool to 0 (false) or 1 (true)
        int reverseInt = System.Convert.ToInt32(isReversed);

        // evalTime: 
        // If 0 -> Mathf.Abs(0 - t) = t 
        // If 1 -> Mathf.Abs(1 - t) = 1 - t
        float evalTime = Mathf.Abs(reverseInt - currentSplineTime); //Note: The animation is a circle. The point is equal to the first point, allowing the last knot to be subtituted with the first knot.
        
        transform.position = gameManager.reflectionSpline.EvaluatePosition(evalTime);

        // tangentMultiplier: 
        // If 0 -> 1 - (2 * 0) = 1 (normal direction)
        // If 1 -> 1 - (2 * 1) = -1 (inverted direction)
        int tangentMultiplier = 1 - (2 * reverseInt);
        Vector3 tangent = gameManager.reflectionSpline.EvaluateTangent(evalTime) * tangentMultiplier;
        
        if (tangent != Vector3.zero)
        {
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            headTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
        
        if (gameManager.dynamicVcamComposer != null)
        {
            Vector3 startOffset = Vector3.zero;
            Vector3 endOffset = new Vector3(3f, 0f, 0f);
    
            gameManager.dynamicVcamComposer.TargetOffset = Vector3.Lerp(startOffset, endOffset, currentSplineTime);
        }
    }
}