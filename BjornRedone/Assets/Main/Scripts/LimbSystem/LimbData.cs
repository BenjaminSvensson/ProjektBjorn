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

    [Header("--- Limb-Specific Stats ---")]

    [Header("Leg Stats")]
    [Tooltip("Bonus added to player's base move speed. ONLY APPLIES TO LEGS.")]
    public float moveSpeedBonus = 0f;
    
    [Header("Arm Stats")]
    [Tooltip("Bonus added to player's base attack damage. ONLY APPLIES TO ARMS.")]
    public float attackDamageBonus = 0f;
    [Tooltip("Base attack speed. (e.g., 2.0 = 2 attacks per second). ONLY APPLIES TO ARMS.")]
    public float attackSpeed = 1.0f; 
    [Tooltip("How far the punch reaches from the arm. ONLY APPLIES TO ARMS.")]
    public float attackReach = 0.5f;
    [Tooltip("The radius of the punch impact. ONLY APPLIES TO ARMS.")]
    public float impactSize = 0.3f;

    [Header("Head Stats")]
    [Tooltip("Bonus added to player's max health (mostly for Head/Torso).")]
    public float healthBonus = 0f;

    // We can remove these, as they are now limb-specific
    // public float moveSpeedBonus = 0f;
    // public float attackDamageBonus = 0f;
    // public float attackSpeedBonus = 0f;
}