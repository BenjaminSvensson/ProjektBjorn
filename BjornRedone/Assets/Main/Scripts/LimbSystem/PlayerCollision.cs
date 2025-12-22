using UnityEngine;

/// <summary>
/// Handles collision with auto-pickup items (Limbs, Coins).
/// Weapons are handled via Interaction now.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerCollision : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private PlayerLimbController limbController;

    void Awake()
    {
        if (limbController == null) limbController = GetComponentInParent<PlayerLimbController>();
        
        if (limbController == null) Debug.LogError("PlayerCollision: Missing PlayerLimbController!");
    }

    // Handle Trigger pickups
    void OnTriggerEnter2D(Collider2D other)
    {
        HandleCollision(other.gameObject);
    }

    // Handle Physical pickups
    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject otherObj)
    {
        // 1. Check for LIMBS (Still Auto-Pickup for fluidity)
        WorldLimb worldLimb = otherObj.GetComponent<WorldLimb>();
        if (worldLimb != null && worldLimb.CanPickup())
        {
            if (limbController != null)
            {
                bool attached = limbController.TryAttachLimb(worldLimb.GetLimbData(), worldLimb.IsShowingDamaged());
                if (attached) Destroy(otherObj);
            }
            return;
        }

        // 2. Check for Coins (Still Auto-Pickup)
        CoinPickup coin = otherObj.GetComponent<CoinPickup>();
        if (coin != null)
        {
            // Coin logic handles itself via OnTriggerEnter usually, 
            // but if you have collision logic there, it goes here.
            return;
        }

        // REMOVED: WeaponPickup logic. Weapons are now "Interact Only".
    }
}