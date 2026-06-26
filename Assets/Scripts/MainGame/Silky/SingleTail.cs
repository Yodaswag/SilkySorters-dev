using System.Collections;
using TMPro;
using UnityEngine;

public class SingleTail : MonoBehaviour
{
    public SpriteRenderer imageComp;
    public ImageScript imageScript;
    public SpriteRenderer tailBG;
    public TextMeshPro textComp;

    [SerializeField] private Sprite spriteCorrect; // BodySpriteSheet_3 (green)
    [SerializeField] private Sprite spriteError;   // BodySpriteSheet_1 (red)
    [SerializeField] private float placeholderAlpha = 0.35f;
    public float PlaceholderAlpha => placeholderAlpha;

    [Header("Error Flash")]
    [SerializeField] private float errorFlashAlpha = 0.3f;     // explicit opacity held during the red flash
    [SerializeField] private Color errorFlashTint = Color.red; // hue overlaid on the body during the red flash

    [Header("Launch Trail (success flight)")]
    [Tooltip("Pre-placed ParticleSystem child, played only during the success flight. Set its Stop Action = Disable.")]
    [SerializeField] private ParticleSystem launchTrail;

    private Sprite spritePlaceholder;
    private SnakeTail manager;
    private int myIndex;

    private void Awake()
    {
        if (tailBG != null) spritePlaceholder = tailBG.sprite;
        if (launchTrail != null) { launchTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } // idle until a flight
    }

    public void ShowCorrect()
    {
        if (spriteCorrect != null) { tailBG.sprite = spriteCorrect; tailBG.color = Color.white; }
    }

    public void ShowError()
    {
        if (spriteError != null)
        {
            tailBG.sprite = spriteError;
            Color c = errorFlashTint;  // overlay a red hue...
            c.a = errorFlashAlpha;     // ...at the explicit flash opacity (0.3) instead of full white
            tailBG.color = c;
        }
    }

    public void HideContent()
    {
        if (textComp != null) textComp.gameObject.SetActive(false);
        if (imageComp != null) imageComp.gameObject.SetActive(false);
    }

    public void SetBGAlpha(float alpha) => tailBG.color = new Color(1f, 1f, 1f, alpha);

    // Hides content + restores default sprite, but does NOT change alpha (for use before a fade)
    public void PrepareEmpty()
    {
        HideContent();
        if (spritePlaceholder != null) tailBG.sprite = spritePlaceholder;
    }

    // Full reset to the unfilled placeholder state
    public void ShowEmpty()
    {
        PrepareEmpty();
        tailBG.color = new Color(1f, 1f, 1f, placeholderAlpha);
    }

    public void Init(SnakeTail mapMaker, int index)
    {
        manager = mapMaker;
        myIndex = index;
    }

    // Flies a throwaway copy of this segment's visuals to the bar along a bowed line, shrinking to nothing.
    // The real segment is emptied immediately so the worm looks drained.
    //   targetPos  : flight end (progress bar world anchor).
    //   speed      : constant flight speed in world units/sec → duration = distance / speed.
    //   arcBulge   : bow height in world units at mid-flight (0 = straight line).
    //   bulgeAngle : bow direction in degrees, measured from the flight direction (90 = perpendicular/left).
    public IEnumerator LaunchToProgressBar(Vector3 targetPos, float speed, float arcBulge, float bulgeAngle)
    {
        Vector3 startPos   = transform.position;
        Vector3 startScale = transform.localScale;

        GameObject flyObj = new GameObject("FlySegment");
        flyObj.transform.position   = startPos;
        flyObj.transform.localScale = startScale;

        CloneRenderer(tailBG, flyObj.transform, 999);
        if (imageComp != null && imageComp.gameObject.activeSelf && imageComp.sprite != null)
            CloneRenderer(imageComp, flyObj.transform, 1000);
        if (textComp != null && textComp.gameObject.activeSelf && !string.IsNullOrEmpty(textComp.text))
            CloneText(textComp, flyObj.transform, 1001);

        ShowEmpty();

        // Flight time follows from the distance and the requested constant speed.
        float distance       = Vector3.Distance(startPos, targetPos);
        float flightDuration = speed > 0f ? distance / speed : 0f;

        // Bow offset direction: flight direction rotated by bulgeAngle.
        Vector3 dir = (targetPos - startPos).normalized;
        float rad = bulgeAngle * Mathf.Deg2Rad;
        Vector3 bulgeDir = new Vector3(dir.x * Mathf.Cos(rad) - dir.y * Mathf.Sin(rad),
                                       dir.x * Mathf.Sin(rad) + dir.y * Mathf.Cos(rad), 0f);

        // Particle trail rides the flight on its own transform (kept off flyObj so the shrink can't
        // scale the squares). Its duration is tied to this flight's length.
        if (launchTrail != null)
        {
            launchTrail.transform.SetParent(null, false);
            launchTrail.transform.position = startPos;
            launchTrail.gameObject.SetActive(true);
            launchTrail.Clear();
            var main = launchTrail.main;
            main.duration = Mathf.Max(0.01f, flightDuration);
            launchTrail.Play();
        }

        float z = startPos.z;
        float elapsed = 0f;
        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flightDuration);
            Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
            pos += bulgeDir * (arcBulge * Mathf.Sin(t * Mathf.PI)); // 0 at both ends, peak mid-flight
            pos.z = z;
            flyObj.transform.position   = pos;
            flyObj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            if (launchTrail != null) launchTrail.transform.position = pos;
            yield return null;
        }

        // Stop emitting; live world-space particles finish in place. Stop Action = Disable sleeps the system.
        if (launchTrail != null)
            launchTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Destroy(flyObj);
    }

    // Spawns a SpriteRenderer at the source's world transform, then parents it under the flying
    // copy (keeping its world placement) so it moves and shrinks together with the segment.
    private static void CloneRenderer(SpriteRenderer src, Transform flyRoot, int sortingOrder)
    {
        var go = new GameObject(src.name + "_fly");
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        go.transform.localScale = src.transform.lossyScale;
        go.transform.SetParent(flyRoot, true); // worldPositionStays: keep the on-screen layout

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = src.sprite;
        sr.color        = src.color;
        sr.sortingOrder = sortingOrder;
    }

    // Same idea as CloneRenderer, but for the world-space label. Autosizing is switched on so the
    // text re-fits its box as the copy shrinks, staying crisp instead of pixel-scaling down.
    private static void CloneText(TextMeshPro src, Transform flyRoot, int sortingOrder)
    {
        var go = new GameObject(src.name + "_fly");
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        go.transform.localScale = src.transform.lossyScale;
        go.transform.SetParent(flyRoot, true);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font               = src.font;
        tmp.fontSharedMaterial = src.fontSharedMaterial; // preserve outline / face look
        tmp.text               = src.text;               // already RTL-fixed on the source
        tmp.color              = src.color;
        tmp.alignment          = src.alignment;
        tmp.fontStyle          = src.fontStyle;
        tmp.rectTransform.sizeDelta = src.rectTransform.sizeDelta;

        tmp.fontSizeMax      = src.fontSize;
        tmp.fontSizeMin      = 0f;
        tmp.enableAutoSizing = true; // shrink text with tail segment

        if (tmp.renderer != null) tmp.renderer.sortingOrder = sortingOrder;
    }

    void LateUpdate()
    {
        if (manager == null || manager.positions.Count <= myIndex + 1) return;

        Vector3 targetPos = manager.positions[myIndex];
        Vector3 previousPos = manager.positions[myIndex + 1];

        transform.position = Vector3.Lerp(previousPos, targetPos, manager.CurrentLerpT);

        if (manager.rotateSegments)
        {
            Vector3 direction = targetPos - previousPos;
            if (direction != Vector3.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }
    }
}
