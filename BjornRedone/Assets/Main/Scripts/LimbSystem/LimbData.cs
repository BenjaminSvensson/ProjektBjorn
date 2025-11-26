using UnityEngine;

// Create assets from this (e.g., "Goblin Arm", "Skeleton Leg")
// Right-click in Project view -> Create -> Rogalike/Limb Data
[CreateAssetMenu(fileName = "NewLimbData", menuName = "Roguelike/Limb Data")]
public class LimbData : ScriptableObject
{
    [Tooltip("What type of limb is this?")]
    public LimbType limbType;

    [Tooltip("The prefab to spawn (must have WorldLimb.cs on it)")]
    public GameObject visualPrefab;

    // We no longer need these!
    // [Tooltip("The sprite to show when this limb is a pickup on the ground")]
    // public Sprite pickupSprite;
    //
    // [Tooltip("The sprite to show when this limb breaks on the ground")]
    // public Sprite brokenLimbSprite;

    [Header("Stat Modifiers")]
    [Tooltip("Bonus added to player's base move speed")]
    public float moveSpeedBonus = 0f;

    [Tooltip("Bonus added to player's base attack damage")]
    public float attackDamageBonus = 0f;
    
    [Tooltip("Bonus added to player's base attack speed")]
    public float attackSpeedBonus = 0f;

    [Tooltip("Bonus added to player's max health (mostly for Head/Torso)")]
    public float healthBonus = 0f;

    // You can add more here later, like special abilities
    // public string specialAbilityID;
}