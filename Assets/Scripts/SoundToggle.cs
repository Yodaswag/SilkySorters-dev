using UnityEngine;
using UnityEngine.UI;

public class SoundToggle : MonoBehaviour
{
    [SerializeField] Image icon;          // button's icon Image
    [SerializeField] Sprite soundOnIcon;
    [SerializeField] Sprite soundOffIcon;

    void Awake()
    {
        Refresh();                        // sync icon to current global state
    }

    public void Toggle()                  // ← wire to Button OnClick
    {
        if (AudioListener.volume > 0f)
        {
            AudioListener.volume = 0f;
        }
        else
        {
            AudioListener.volume = 1f;
        }

        Refresh();
    }

    void Refresh()
    {
        if (AudioListener.volume > 0f)
        {
            icon.sprite = soundOnIcon;
        }
        else
        {
            icon.sprite = soundOffIcon;
        }
    }
}