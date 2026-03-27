using UnityEngine;

public class SnakeMove : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float boostMultiplier = 2f;
    [SerializeField] private float smoothTime = 0.1f;

    [Header("References")]
    [SerializeField] private Transform headTransform;

    private InputSystem_Actions inputActions;

    private Vector2 targetInput;
    private Vector2 currentInputVector;
    private Vector2 smoothInputVelocity;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    private void Start()
    {
        if (headTransform == null)
        {
            headTransform = transform;
        }
    }

    private void Update()
    {
        // Capture input state every frame to prevent dropped inputs
        targetInput = inputActions.Player.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // Smooth and apply movement in FixedUpdate to sync with the physics engine 
        // Passing Time.fixedDeltaTime explicitly prevents SmoothDamp from defaulting to Time.deltaTime
        currentInputVector = Vector2.SmoothDamp(
            currentInputVector, 
            targetInput, 
            ref smoothInputVelocity, 
            smoothTime, 
            Mathf.Infinity, 
            Time.fixedDeltaTime
        );

        HandleMovement();
    }

    private void HandleMovement()
    {
        float currentSpeed = moveSpeed;

        if (inputActions.Player.Boost.IsInProgress())
        {
            currentSpeed *= boostMultiplier;
        }

        if (currentInputVector.sqrMagnitude > 0.001f)
        {
            Vector3 movement = new Vector3(currentInputVector.x, currentInputVector.y, 0f);

            transform.Translate(movement * (currentSpeed * Time.fixedDeltaTime), Space.World);

            float angle = Mathf.Atan2(currentInputVector.y, currentInputVector.x) * Mathf.Rad2Deg;
            
            // Sprite points "up" by default, so we subtract 90 degrees to align facing direction with movement vector.
            headTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }
}