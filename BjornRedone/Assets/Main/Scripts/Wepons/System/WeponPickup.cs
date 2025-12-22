using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour, IInteractable
{
    [Header("Weapon Data")]
    public WeaponData weaponData;

    [Header("Physics")]
    [SerializeField] private float groundFriction = 5f;
    [SerializeField] private float rotationSpeed = 200f;

    private Rigidbody2D rb;
    private bool isFlying = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = groundFriction; 
    }

    void FixedUpdate()
    {
        // Stop "flying" state when slow enough
        if (isFlying && rb.linearVelocity.sqrMagnitude < 1f)
        {
            isFlying = false;
        }
    }

    public void InitializeDrop(Vector2 direction, float force = 5f)
    {
        if (rb != null)
        {
            rb.AddForce(direction * force, ForceMode2D.Impulse);
            rb.angularVelocity = Random.Range(-rotationSpeed, rotationSpeed);
            isFlying = true; 
        }
    }

    // --- IInteractable Implementation ---

    public string GetInteractionPrompt()
    {
        return weaponData != null ? $"Pick up {weaponData.weaponName}" : "Pick up Weapon";
    }

    public void Interact(GameObject interactor)
    {
        if (isFlying) return; // Can't grab while it's flying through the air

        WeaponSystem weaponSystem = interactor.GetComponent<WeaponSystem>();
        if (weaponSystem != null)
        {
            if (weaponSystem.TryPickupWeapon(weaponData))
            {
                Destroy(gameObject);
            }
        }
    }

    // Explicit implementation for the Interface property
    Transform IInteractable.transform => transform;
}