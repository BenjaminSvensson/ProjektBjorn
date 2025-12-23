using UnityEngine;

/// <summary>
/// This script goes on a child GameObject of the Player.
/// It should have a Collider2D.
/// It detects collision with pickups (limbs, coins, weapons).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerCollision : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private PlayerLimbController limbController;
    [SerializeField] private WeaponSystem weaponSystem;

    void Awake()
    {
        if (limbController == null) limbController = GetComponentInParent<PlayerLimbController>();
        if (weaponSystem == null) weaponSystem = GetComponentInParent<WeaponSystem>();
        
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
        // 1. Check for LIMBS
        WorldLimb worldLimb = otherObj.GetComponent<WorldLimb>();
        if (worldLimb != null && worldLimb.CanPickup())
        {
            if (limbController != null)
            {
                bool attached = limbController.TryAttachLimb(worldLimb.GetLimbData(), worldLimb.IsShowingDamaged());
                if (attached) Destroy(otherObj);
            }
            return; // Handled
        }

        // 2. Check for WEAPONS
        WeaponPickup weaponPickup = otherObj.GetComponent<WeaponPickup>();
        if (weaponPickup != null && weaponPickup.CanPickup())
        {
            if (weaponSystem != null)
            {
                // --- UPDATED: Pass the persistent ammo count from the pickup ---
                bool pickedUp = weaponSystem.TryPickupWeapon(weaponPickup.GetWeaponData(), weaponPickup.currentAmmoCount);
                if (pickedUp)
                {
                    Destroy(otherObj);
                }
            }
            return; // Handled
        }
    }
}