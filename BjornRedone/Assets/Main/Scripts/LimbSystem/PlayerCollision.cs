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
                // --- MODIFIED: Check if the attach was successful ---
                bool attached = limbController.TryAttachLimb(worldLimb.GetLimbData());
                
                // Only destroy the pickup if we successfully attached it
                if (attached)
                {
                    Destroy(other.gameObject);
                }
                // --- END MODIFICATION ---
            }
        }
    }
}