using UnityEngine;

/// <summary>
/// סקריפט לניהול תצוגת תמונה עם תמיכה במסכה (mask) וזום
/// מאפשר הצגת תמונות בשני מצבים: Sliced ו-KeepRatio
/// </summary>
public class ImageScript : MonoBehaviour
{
    // --- רכיבים ---
    [SerializeField] SpriteRenderer spriteRenderer;  // הרכיב שמציג את התמונה
    [SerializeField] SpriteMask spriteMask;          // מסכה אופציונלית לחיתוך התמונה
    
    // --- משתני סקייל ---
    private Vector3 fitScale;      // הסקייל המחושב להתאמת התמונה לקונטיינר
    private Vector3 originalScale; // הסקייל המקורי של ה-SpriteRenderer (נשמר ב-Awake)
    private Vector3 maskFitScale;  // הסקייל המחושב למסכה
    
    [SerializeField] float zoomScale = 1.5f; // מכפיל הזום (כמה להגדיל בזום)
    
    bool isImage = false; // האם יש תמונה מוצגת כרגע
    
    /// <summary>
    /// נקרא בטעינת האובייקט - שומר את הסקייל המקורי
    /// </summary>
    private void Awake()
    {
        originalScale = spriteRenderer.transform.localScale;
        
        isImage = false;
    }

    /// <summary>
    /// מציג תמונה במצב Sliced
    /// מתאים למשל למצב בו יש מסכה עגולה
    /// שימו לב שהמשמעות - שנאבד מידע במתיחת
    /// </summary>
    /// <param name="sprite">הספרייט להצגה</param>
    public void SetImage_Sliced(Sprite sprite)
    {
        // הגדרת מצב ציור Sliced - מאפשר מתיחה חכמה של התמונה
        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
        spriteRenderer.sprite = sprite;
        spriteRenderer.size = new Vector2(1, 1); // גודל קבוע - המתיחה מתבצעת ע"י ה-Sliced
        spriteRenderer.enabled = true;
        
        // הפעלת המסכה אם קיימת
        if (spriteMask != null)
            spriteMask.enabled = true;
    }

    /// <summary>
    /// מציג תמונה תוך שמירה על יחס הגובה-רוחב המקורי
    /// התמונה תתאים לקונטיינר מבלי להיחתך או להיעוות
    /// </summary>
    /// <param name="image">הספרייט להצגה</param>
    public void SetImage_KeepRatio(Sprite image)
    {
        // איפוס למצב Simple והחזרת הסקייל המקורי
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        spriteRenderer.transform.localScale = originalScale;

        // קביעת גודל הקונטיינר (האזור שבו התמונה צריכה להיכנס)
        float containerWidth, containerHeight;
        containerWidth = originalScale.x;
        containerHeight = originalScale.y;
            
        // קבלת מידות התמונה המקורית
        float imageWidth = image.bounds.size.x;
        float imageHeight = image.bounds.size.y;

        // חישוב יחסי גובה-רוחב
        float imageAspect = imageWidth / imageHeight;      // יחס התמונה
        float containerAspect = containerWidth / containerHeight; // יחס הקונטיינר

        float scaleFactor;

        // בחירת מקדם הסקייל כך שהתמונה תיכנס בשלמותה
        // אם התמונה "רחבה" יותר מהקונטיינר - מתאימים לפי רוחב
        // אם התמונה "גבוהה" יותר מהקונטיינר - מתאימים לפי גובה
        if (imageAspect > containerAspect)
        {
            // התמונה רחבה יחסית - מגבילים לפי רוחב
            scaleFactor = containerWidth / imageWidth;
        }
        else
        {
            // התמונה גבוהה יחסית - מגבילים לפי גובה
            scaleFactor = containerHeight / imageHeight;
        }

        // שמירת והחלת הסקייל המחושב
        fitScale = new Vector3(scaleFactor, scaleFactor, 1f);
        spriteRenderer.transform.localScale = fitScale;
        spriteRenderer.sprite = image;

        // התאמת המסכה לגודל התמונה (אם קיימת)
        if (spriteMask != null)
        {
            // חישוב הגודל הסופי של התמונה
            float finalWidth = imageWidth * scaleFactor;
            float finalHeight = imageHeight * scaleFactor;
            
            // חישוב סקייל למסכה (90% מגודל התמונה - יוצר שוליים קטנים)
            float maskScaleX = finalWidth * 0.9f / spriteMask.sprite.bounds.size.x;
            float maskScaleY = finalHeight * 0.9f / spriteMask.sprite.bounds.size.y;

            maskFitScale = new Vector3(maskScaleX, maskScaleY, 1f);
            spriteMask.transform.localScale = maskFitScale;
        
            spriteMask.enabled = true;
        }
        
        isImage = true;
        spriteRenderer.enabled = true;
    }
    
    /// <summary>
    /// מסתיר את התמונה והמסכה
    /// </summary>
    public void HideImage()
    {
        spriteRenderer.enabled = false;
        spriteRenderer.sprite = null;
        if (spriteMask != null)
            spriteMask.enabled = false;
        isImage = false;
    }

    /// <summary>
    /// מגדיל את התמונה (זום פנימה)
    /// מכפיל את הסקייל הנוכחי ב-zoomScale
    /// </summary>
    public void ZoomIn()
    {
        // אם אין תמונה - לא עושים כלום
        if(!isImage)
            return;
            
        // הגדלת התמונה
        spriteRenderer.transform.localScale = fitScale * zoomScale;
        
        // הגדלת המסכה בהתאם (כדי שתמשיך לכסות את התמונה)
        if (spriteMask != null)
        {
            spriteMask.transform.localScale = maskFitScale * zoomScale;
        }
    }

    /// <summary>
    /// מחזיר את התמונה לגודל הרגיל (זום החוצה)
    /// </summary>
    public void ZoomOut()
    {      
        if (!isImage)
            return;
        
        // החזרת התמונה לסקייל המקורי המחושב
        spriteRenderer.transform.localScale = fitScale;

        // החזרת המסכה לגודל המקורי
        if (spriteMask)
        {
            spriteMask.transform.localScale = maskFitScale;
        }
    }
}