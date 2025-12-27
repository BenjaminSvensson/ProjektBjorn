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
    
    [Header("Visuals - Prefab Mode")]
    public GameObject equippedPrefab;
    public string muzzleSocketName = "Muzzle";

    [Header("Visuals - Sprite Mode")]
    public Sprite heldSprite;
    public Vector3 heldScale = Vector3.one; 
    [Tooltip("Rotation offset in degrees (Z-axis).")]
    public float heldRotationOffset = 0f;
    [Tooltip("Position offset relative to the hand pivot (Local Space). X = Along Arm, Y = Perpendicular.")]
    public Vector3 heldPositionOffset = Vector3.zero;
    
    [Tooltip("Fallback offset if using Sprite mode. X = forward, Y = up/down.")]
    public Vector2 muzzleOffset = new Vector2(0.5f, 0f); 

    [Header("Durability / Breaking")] // --- NEW ---
    [Tooltip("If true, the weapon is destroyed after hitting an enemy (e.g., Bottle).")]
    public bool breaksOnMeleeHit = false;
    [Tooltip("If true, the weapon is destroyed after hitting an enemy/wall when thrown.")]
    public bool breaksOnThrowHit = false;
    [Tooltip("The visual prefab to spawn when broken (must have BreakableEffect script).")]
    public GameObject brokenPrefab;

    [Header("Combat Type")]
    public WeaponType type;

    [Header("Melee Settings")]
    public MeleeAttackStyle attackStyle = MeleeAttackStyle.Stab;
    public float meleeDamageBonus = 5f;
    public AudioClip[] meleeImpactSounds;

    [Header("Melee Stat Modifiers")]
    public float attackSpeedMultiplier = 1.0f;
    public float knockbackMultiplier = 1.0f;
    public float swingArc = 90f;

    [Header("Ranged Settings")]
    [Tooltip("If true, holding the mouse button will fire continuously (Automatic). If false, you must click for each shot (Semi-Auto).")]
    public bool allowHoldToFire = true; 
    public GameObject projectilePrefab;
    public float fireRate = 0.2f; 
    public float projectileSpeed = 15f;
    public float projectileDamage = 5f;
    public float spread = 5f;
    [Tooltip("How much the camera shakes when firing this weapon.")]
    public float screenShakeAmount = 0.1f;
    public int projectilesPerShot = 1;
    public AudioClip[] shootSounds; 
    
    [Header("Ammo & Reloading")]
    public int magazineSize = 6; 
    public float reloadTime = 1.5f; 
    public AudioClip reloadSound; 
    public AudioClip emptyClickSound; 
}