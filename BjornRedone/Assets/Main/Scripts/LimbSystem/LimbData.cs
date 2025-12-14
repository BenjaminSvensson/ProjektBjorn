using UnityEngine;

// Create assets from this (e.g., "Goblin Arm", "Skeleton Leg")
// Right-click in Project view -> Create -> Roguelike/Limb Data
[CreateAssetMenu(fileName = "NewLimbData", menuName = "Roguelike/Limb Data")]
public class LimbData : ScriptableObject
{
    [Tooltip("What type of limb is this?")]
    public LimbType limbType;

    [Tooltip("The prefab to spawn (must have WorldLimb.cs on it)")]
    public GameObject visualPrefab; // Kept as visualPrefab per your request

    [Header("--- Limb-Specific Stats ---")]
    public float maxHealth = 20f;

    [Header("Leg Stats")]
    [Tooltip("Bonus added to player's base move speed. ONLY APPLIES TO LEGS.")]
    public float moveSpeedBonus = 0f;
    
    [Header("Arm Audio")]
    [Tooltip("List of sounds to play randomly on punch impact.")]
    public AudioClip[] punchSounds; 
    [Tooltip("The pitch of the punch sound. Higher = faster/lighter, Lower = slower/heavier.")]
    [Range(0.1f, 3f)]
    public float punchPitch = 1.0f;
    [Tooltip("The volume of the punch sound.")]
    [Range(0.1f, 1f)]
    public float punchVolume = 1.0f;

    [Header("Arm Stats")]
    [Tooltip("Bonus added to player's base attack damage. ONLY APPLIES TO ARMS.")]
    public float attackDamageBonus = 0f;
    [Tooltip("How long the punch animation lasts (in seconds).")]
    public float punchDuration = 0.2f;
    [Tooltip("How long to wait between punches (in seconds).")]
    public float attackCooldown = 0.5f;
    [Tooltip("How far the punch reaches from the arm. ONLY APPLIES TO ARMS.")]
    public float attackReach = 0.5f;
    [Tooltip("The radius of the punch impact. ONLY APPLIES TO ARMS.")]
    public float impactSize = 0.3f;
    [Tooltip("The force applied to enemies when hit. ONLY APPLIES TO ARMS.")]
    public float knockbackForce = 5f;

    [Header("Head Stats")]
    [Tooltip("Bonus added to player's max health (mostly for Head/Torso).")]
    public float healthBonus = 0f;
}