using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour
{
    [Header("Weapon Data")]
    public WeaponData weaponData;

    [Header("State")]
    [Tooltip("Current rounds in the mag. -1 means 'Uninitialized' (will default to full on start).")]
    public int currentAmmoCount = -1; 

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

    void Start()
    {
        // If placed in the scene manually (not dropped), ensure it has full ammo
        if (!isFlying && currentAmmoCount < 0 && weaponData != null)
        {
            currentAmmoCount = weaponData.magazineSize;
        }
    }

    void FixedUpdate()
    {
        if (isFlying && rb.linearVelocity.sqrMagnitude < 1f)
        {
            isFlying = false;
        }
    }

    /// <summary>
    /// Called when the player drops this weapon.
    /// </summary>
    /// <param name="direction">Throw direction</param>
    /// <param name="force">Throw force</param>
    /// <param name="ammo">The ammo currently in the magazine. If -1, defaults to max.</param>
    public void InitializeDrop(Vector2 direction, float force = 5f, int ammo = -1)
    {
        if (weaponData != null)
        {
            // If ammo is provided (-1 is default/invalid), use it. Otherwise max.
            currentAmmoCount = (ammo >= 0) ? ammo : weaponData.magazineSize;
        }

        if (rb != null)
        {
            rb.AddForce(direction * force, ForceMode2D.Impulse);
            rb.angularVelocity = Random.Range(-rotationSpeed, rotationSpeed);
            isFlying = true; 
        }
    }

    public bool CanPickup()
    {
        return !isFlying;
    }

    public WeaponData GetWeaponData()
    {
        return weaponData;
    }
}