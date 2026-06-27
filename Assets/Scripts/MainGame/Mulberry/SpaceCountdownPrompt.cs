using UnityEngine;

public class SpaceCountdownPrompt : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] countdownSprites;
    [SerializeField] private Vector3 offsetAbove = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private Vector3 offsetBelow = new Vector3(0f, -0.5f, 0f);

    private Transform target;
    private Transform player;
    private bool isShowing;

    public int FrameCount
    {
        get
        {
            if (countdownSprites == null)
                return 0;
            return countdownSprites.Length;
        }
    }

    private void Awake()
    {
        Hide();
    }

    private void Update()
    {
        if (!isShowing)
            return;

        PositionPrompt();
    }

    public void Show(Transform targetTransform, Transform playerTransform)
    {
        target = targetTransform;
        player = playerTransform;
        isShowing = true;
        gameObject.SetActive(true);
        SetFrame(0);
        PositionPrompt();
    }

    public void SetFrame(int frameIndex)
    {
        if (spriteRenderer == null || countdownSprites == null || countdownSprites.Length == 0)
            return;

        int safeIndex = Mathf.Clamp(frameIndex, 0, countdownSprites.Length - 1);
        spriteRenderer.sprite = countdownSprites[safeIndex];
    }

    public void Hide()
    {
        isShowing = false;
        target = null;
        player = null;
        gameObject.SetActive(false);
    }

    private void PositionPrompt()
    {
        if (target == null || player == null)
            return;

        //If player comes from above, show from below - avoid overlap of player and space prompt
        bool showBelow = false;
        if (target.position.y < player.position.y)
            showBelow = true;

        Vector3 offset = offsetAbove;
        if (showBelow)
            offset = offsetBelow;
        transform.position = target.position + offset;
    }
}