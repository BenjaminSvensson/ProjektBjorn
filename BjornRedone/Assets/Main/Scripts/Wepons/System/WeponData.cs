using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Info")]
    public string weaponName;
    public Sprite icon; // Shown in UI
    
    [Header("World Objects")]
    [Tooltip("The prefab spawned in the world when this weapon is dropped.")]
    public GameObject pickupPrefab;
    
    [Header("Visuals")]
    [Tooltip("The sprite shown in the player's hand.")]
    public Sprite heldSprite;
}