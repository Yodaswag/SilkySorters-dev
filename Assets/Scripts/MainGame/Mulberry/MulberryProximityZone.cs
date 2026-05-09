using UnityEngine;

public class MulberryProximityZone : MonoBehaviour
{
    private OrderItem orderItem;

    private void Awake()
    {
        orderItem = GetComponentInParent<OrderItem>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (orderItem != null && IsMainPlayer(other))
        {
            orderItem.SetPlayerInProximity(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (orderItem != null && IsMainPlayer(other))
        {
            orderItem.SetPlayerInProximity(false);
        }
    }

    private static bool IsMainPlayer(Collider2D other)
    {
        SnakeGrow snakeGrow = other.GetComponentInParent<SnakeGrow>();
        return snakeGrow != null && snakeGrow.gameManager != null;
    }
}
