using UnityEngine;

/// <summary>
/// This script goes on a child GameObject of the Player.
/// It should have a Collider2D set to "Is Trigger = true"
/// and is responsible for detecting limb pickups.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerCollision : MonoBehaviour
{
    [Header("Required Reference")]
    [SerializeField] private PlayerLimbController limbController;

    void Awake()
    {
        if (limbController == null)
        {
            // If not assigned, try to find it on the parent
            limbController = GetComponentInParent<PlayerLimbController>();
        }
        if (limbController == null)
        {
            Debug.LogError("PlayerCollision could not find the PlayerLimbController!");
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
                // --- MODIFIED: Check if the limb is damaged and pass it ---
                bool isDamaged = worldLimb.IsShowingDamaged();
                bool attached = limbController.TryAttachLimb(worldLimb.GetLimbData(), isDamaged);
                
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