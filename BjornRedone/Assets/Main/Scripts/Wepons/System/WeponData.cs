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
    
    [Header("Visuals - Prefab Mode (Recommended for precise Muzzles)")]
    [Tooltip("Assign a Prefab here to spawn it in the hand. This allows you to place an empty GameObject inside the prefab to act as the Muzzle.")]
    public GameObject equippedPrefab;
    [Tooltip("The name of the child GameObject in the Equipped Prefab to use as the fire point.")]
    public string muzzleSocketName = "Muzzle";

    [Header("Visuals - Sprite Mode (Simple)")]
    [Tooltip("Used if Equipped Prefab is null.")]
    public Sprite heldSprite;
    [Tooltip("Scale of the sprite/prefab when held.")]
    public Vector3 heldScale = Vector3.one; 
    [Tooltip("Rotation offset in degrees (Z-axis). Useful if your sprite is drawn pointing Up or Down.")]
    public float heldRotationOffset = 0f;
    
    [Tooltip("Fallback offset if using Sprite mode (or if Socket is missing). X = forward, Y = up/down.")]
    public Vector2 muzzleOffset = new Vector2(0.5f, 0f); 

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