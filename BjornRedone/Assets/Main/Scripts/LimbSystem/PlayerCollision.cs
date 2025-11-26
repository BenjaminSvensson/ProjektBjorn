using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    private PlayerLimbController limbController;

    void Start()
    {
        limbController = GetComponent<PlayerLimbController>();
        if (limbController == null)
        {
            Debug.LogError("PlayerCollision script requires a PlayerLimbController on the same GameObject.");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if we collided with a limb pickup
        WorldLimb worldLimb = other.GetComponent<WorldLimb>();
        if (worldLimb != null && worldLimb.CanPickup())
        {
            // Try to attach this limb
            if (limbController != null)
            {
                limbController.TryAttachLimb(worldLimb.GetLimbData());
                
                // Destroy the pickup object after collecting it
                Destroy(other.gameObject);
            }
        }
    }
}