using UnityEngine;

public enum WeaponType
{
    Melee,
    Ranged
}

public enum MeleeAttackStyle
{
    Stab,
    Swing
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Info")]
    public string weaponName;
    public Sprite icon; 
    
    [Header("World Objects")]
    public GameObject pickupPrefab;
    
    [Header("Visuals")]
    public Sprite heldSprite;
    [Tooltip("Scale of the sprite when held (Default is 1, 1, 1).")]
    public Vector3 heldScale = Vector3.one; 
    [Tooltip("Rotation offset in degrees (Z-axis). Useful if your sprite is drawn pointing Up or Down.")]
    public float heldRotationOffset = 0f; // --- NEW ---

    [Header("Combat Type")]
    public WeaponType type;

    [Header("Melee Settings")]
    public MeleeAttackStyle attackStyle = MeleeAttackStyle.Stab;
    [Tooltip("Extra damage added to the punch.")]
    public float meleeDamageBonus = 5f;
    [Tooltip("Sounds to play when hitting an enemy with this weapon.")]
    public AudioClip[] meleeImpactSounds;

    [Header("Melee Stat Modifiers")]
    [Tooltip("Multiplier for attack speed. 1.0 = Normal. 0.5 = Slow/Heavy. 1.5 = Fast.")]
    public float attackSpeedMultiplier = 1.0f;
    [Tooltip("Multiplier for knockback force.")]
    public float knockbackMultiplier = 1.0f;
    [Tooltip("Arc angle for Swing attacks (e.g., 90 degrees).")]
    public float swingArc = 90f;

    [Header("Ranged Settings")]
    [Tooltip("The projectile prefab to spawn (Must have Projectile.cs).")]
    public GameObject projectilePrefab;
    public float fireRate = 0.2f;
    public float projectileSpeed = 15f;
    public float projectileDamage = 5f;
    [Tooltip("Random spread angle in degrees.")]
    public float spread = 5f;
    public int projectilesPerShot = 1;
    public AudioClip[] shootSounds; 
}