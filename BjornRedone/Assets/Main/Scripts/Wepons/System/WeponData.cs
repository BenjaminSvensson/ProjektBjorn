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
    public float heldRotationOffset = 0f;
    public Vector2 muzzleOffset = new Vector2(0.5f, 0f); 

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
    public GameObject projectilePrefab;
    public float fireRate = 0.2f; // Time between individual shots
    public float projectileSpeed = 15f;
    public float projectileDamage = 5f;
    public float spread = 5f;
    public int projectilesPerShot = 1;
    public AudioClip[] shootSounds; 
    
    [Header("Ammo & Reloading")]
    [Tooltip("How many bullets this weapon holds in a clip.")]
    public int magazineSize = 6; // --- NEW ---
    [Tooltip("Time in seconds to reload.")]
    public float reloadTime = 1.5f; // --- NEW ---
    [Tooltip("Sound played when reload starts.")]
    public AudioClip reloadSound; // --- NEW ---
    [Tooltip("Sound played if attempting to fire while Empty or Reloading.")]
    public AudioClip emptyClickSound; 
}