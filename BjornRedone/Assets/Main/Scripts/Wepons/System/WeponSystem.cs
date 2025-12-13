using UnityEngine;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(PlayerLimbController))]
public class WeaponSystem : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private WeaponData[] weaponSlots = new WeaponData[2]; // Slot 0 and Slot 1
    [SerializeField] private int activeSlotIndex = 0;

    [Header("References")]
    [SerializeField] private WeaponHUD weaponHUD; // Assign this in Inspector!
    
    private PlayerLimbController limbController;

    void Awake()
    {
        limbController = GetComponent<PlayerLimbController>();
    }

    void Start()
    {
        // Force the UI to refresh immediately on start
        // This fixes the bug where both slots look active initially
        UpdateUI();
    }

    void Update()
    {
        HandleInput();
        CheckArmStatus();
    }

    private void HandleInput()
    {
        // Safety check for Keyboard
        if (Keyboard.current == null) return;

        // Keyboard: 1 and 2 to switch slots
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SetActiveSlot(0);
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SetActiveSlot(1);
        }

        // Optional: Scroll wheel to switch
        // Note: ReadValue returns a float, typically +/- 120 per notch
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (scroll > 0) SetActiveSlot(0);
            if (scroll < 0) SetActiveSlot(1);
        }
    }

    private void SetActiveSlot(int index)
    {
        if (index < 0 || index >= weaponSlots.Length) return;
        
        // Don't update if we are already on this slot (optimization)
        if (activeSlotIndex == index) return;

        activeSlotIndex = index;
        UpdateUI();
    }

    // Called automatically in Update
    private void CheckArmStatus()
    {
        // Rule: If player has NO arms, they cannot hold weapons.
        if (limbController != null && !limbController.CanAttack())
        {
            if (HasAnyWeapon())
            {
                // Force drop everything if we lost our arms
                DropWeapon(0);
                DropWeapon(1);
            }
        }
    }

    public bool HasAnyWeapon()
    {
        return weaponSlots[0] != null || weaponSlots[1] != null;
    }

    public bool TryPickupWeapon(WeaponData newData)
    {
        if (newData == null) return false;

        // 1. Check if we have arms to hold it
        if (limbController != null && !limbController.CanAttack())
        {
            Debug.Log("Cannot pick up weapon: No arms!");
            return false;
        }

        // 2. Logic: Pickup into CURRENT slot.
        // If current slot has a weapon, DROP it first (Swap).
        if (weaponSlots[activeSlotIndex] != null)
        {
            DropWeapon(activeSlotIndex);
        }

        // 3. Equip new weapon
        weaponSlots[activeSlotIndex] = newData;
        Debug.Log($"Picked up {newData.weaponName} into Slot {activeSlotIndex + 1}");
        
        UpdateUI();
        return true;
    }

    public void DropWeapon(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;
        
        WeaponData weaponToDrop = weaponSlots[slotIndex];
        if (weaponToDrop == null) return;

        // 1. Remove from inventory
        weaponSlots[slotIndex] = null;

        // 2. Spawn in world
        if (weaponToDrop.pickupPrefab != null)
        {
            GameObject drop = Instantiate(weaponToDrop.pickupPrefab, transform.position, Quaternion.identity);
            
            // Try to find the pickup script to initialize physics
            // Note: We use SendMessage or TryGetComponent depending on your setup
            // For now, let's assume standard instantiation is enough, or use WeaponPickup if available
            WeaponPickup pickupScript = drop.GetComponent<WeaponPickup>();
            if (pickupScript != null)
            {
                // Throw it in a random direction
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                pickupScript.InitializeDrop(randomDir);
            }
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (weaponHUD != null)
        {
            weaponHUD.UpdateSlots(activeSlotIndex, weaponSlots[0], weaponSlots[1]);
        }
        else
        {
            Debug.LogWarning("WeaponSystem: Weapon HUD reference is missing! Assign it in Inspector.");
        }
    }
}