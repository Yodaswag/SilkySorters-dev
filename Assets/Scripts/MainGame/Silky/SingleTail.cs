using TMPro;
using UnityEngine;

public class SingleTail : MonoBehaviour
{
    public SpriteRenderer imageComp; //Image Component for answers with images
    public ImageScript imageScript;
    public SpriteRenderer tailBG;
    public TextMeshPro textComp; //Text Component for answers with text
    
    private SnakeTail manager; 
    private int myIndex;

    public void Init(SnakeTail mapMaker, int index)
    {
        manager = mapMaker;
        myIndex = index;
    }

    void LateUpdate()
    {
        // Safety check
        if (manager == null || manager.positions.Count <= myIndex + 1) return;

        // Get the two points this specific tail segment is traveling between
        // Note: myIndex + 1 is the "target" (closer to head) and myIndex + 2 is "previous"
        // depending on how you index. Standard logic:
        // Position 0 = Head. Position 1 = First Tail Target. Position 2 = First Tail Start.
        Vector3 targetPos = manager.positions[myIndex];
        Vector3 previousPos = manager.positions[myIndex + 1];

        // Move smoothly using the Manager's global timing
        transform.position = Vector3.Lerp(previousPos, targetPos, manager.CurrentLerpT);

        // Optional: Rotate to face movement direction
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