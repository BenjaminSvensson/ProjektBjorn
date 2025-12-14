using UnityEngine;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(PlayerLimbController))]
public class WeaponSystem : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private WeaponData[] weaponSlots = new WeaponData[2]; 
    [SerializeField] private int activeSlotIndex = 0;

    [Header("References")]
    [SerializeField] private WeaponHUD weaponHUD; 
    [SerializeField] private SpriteRenderer heldWeaponRenderer;

    [Header("Main Hand Grip")]
    [Tooltip("The local point on the Right Arm sprite where the weapon attaches.")]
    [SerializeField] private Vector3 rightHandGripOffset = new Vector3(0.3f, 0f, 0f);
    [Tooltip("The local point on the Left Arm sprite where the weapon attaches.")]
    [SerializeField] private Vector3 leftHandGripOffset = new Vector3(0.3f, 0f, 0f);

    [Header("Off-Hand Grip")]
    [Tooltip("Where the second hand should hold the weapon (Local to the Weapon Sprite).")]
    [SerializeField] private Vector3 secondaryGripOffset = new Vector3(-0.3f, 0f, 0f);

    private PlayerLimbController limbController;
    private bool isHoldingWithRightHand = false; 

    void Awake()
    {
        limbController = GetComponent<PlayerLimbController>();
    }

    void Start()
    {
        UpdateState();
    }

    void Update()
    {
        HandleInput();
        CheckArmStatus();
    }

    void LateUpdate()
    {
        UpdateWeaponTransform();
    }

    private void HandleInput()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetActiveSlot(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetActiveSlot(1);
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
        if (activeSlotIndex == index) return;
        activeSlotIndex = index;
        UpdateState();
    }

    private void CheckArmStatus()
    {
        if (limbController != null && !limbController.CanAttack())
        {
            if (HasAnyWeapon())
            {
                DropWeapon(0);
                DropWeapon(1);
            }
        }
    }

    public bool HasAnyWeapon()
    {
        return weaponSlots[0] != null || weaponSlots[1] != null;
    }
    
    public bool IsHoldingWeapon()
    {
        return weaponSlots[activeSlotIndex] != null;
    }

    public WeaponData GetActiveWeapon()
    {
        return weaponSlots[activeSlotIndex];
    }

    public bool IsHoldingWithRightHand()
    {
        return isHoldingWithRightHand;
    }

    public bool TryPickupWeapon(WeaponData newData)
    {
        if (newData == null) return false;
        if (limbController != null && !limbController.CanAttack()) return false;

        if (weaponSlots[activeSlotIndex] != null) DropWeapon(activeSlotIndex);

        weaponSlots[activeSlotIndex] = newData;
        UpdateState();
        return true;
    }

    public void DropWeapon(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;
        
        WeaponData weaponToDrop = weaponSlots[slotIndex];
        if (weaponToDrop == null) return;

        weaponSlots[slotIndex] = null;

        if (weaponToDrop.pickupPrefab != null)
        {
            GameObject drop = Instantiate(weaponToDrop.pickupPrefab, transform.position, Quaternion.identity);
            WeaponPickup pickupScript = drop.GetComponent<WeaponPickup>();
            if (pickupScript != null)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                pickupScript.InitializeDrop(randomDir);
            }
        }
        else
        {
            Debug.LogWarning($"Dropped weapon '{weaponToDrop.weaponName}' but no Pickup Prefab was assigned!");
        }

        UpdateState();
    }

    private void UpdateState()
    {
        if (weaponHUD != null) weaponHUD.UpdateSlots(activeSlotIndex, weaponSlots[0], weaponSlots[1]);
        
        WeaponData activeWeapon = weaponSlots[activeSlotIndex];
        if (heldWeaponRenderer != null)
        {
            if (activeWeapon != null && activeWeapon.heldSprite != null)
            {
                heldWeaponRenderer.sprite = activeWeapon.heldSprite;
                heldWeaponRenderer.enabled = true;
            }
            else
            {
                heldWeaponRenderer.sprite = null;
                heldWeaponRenderer.enabled = false;
            }
        }
    }

    private void UpdateWeaponTransform()
    {
        if (heldWeaponRenderer == null || !heldWeaponRenderer.enabled || limbController == null) return;

        Transform mainAnchor = null;
        Vector3 gripOffset = Vector3.zero;

        // 1. Determine Main Hand (WeaponSystem prioritizes Right)
        if (limbController.GetArmData(false) != null) // Right
        {
            mainAnchor = limbController.GetRightArmSlot();
            gripOffset = rightHandGripOffset;
            isHoldingWithRightHand = true;
        }
        else if (limbController.GetArmData(true) != null) // Left
        {
            mainAnchor = limbController.GetLeftArmSlot();
            gripOffset = leftHandGripOffset;
            isHoldingWithRightHand = false;
        }

        // 2. Position Weapon on Main Hand
        if (mainAnchor != null)
        {
            heldWeaponRenderer.transform.position = mainAnchor.TransformPoint(gripOffset);
            
            // Base Rotation: Match arm + 180 Z (to point barrel forward instead of backward)
            Quaternion finalRotation = mainAnchor.rotation * Quaternion.Euler(0, 0, 180f);

            // --- FLIP FIX: If player is facing left (flipped sprite), flip weapon Y to keep it upright ---
            // We check the scale of the visuals holder to determine facing direction.
            if (limbController.GetVisualsHolder() != null && limbController.GetVisualsHolder().localScale.x < 0)
            {
                // Changing rotation to Y-axis 180 flip based on request
                finalRotation *= Quaternion.Euler(0, 180, 0);
            }
            
            heldWeaponRenderer.transform.rotation = finalRotation;
            
            WeaponData activeWeapon = weaponSlots[activeSlotIndex];
            if (activeWeapon != null)
            {
                heldWeaponRenderer.transform.localScale = activeWeapon.heldScale;

                // 3. Position Off-Hand on Weapon
                if (isHoldingWithRightHand && limbController.GetArmData(true) != null)
                {
                    Transform offHand = limbController.GetLeftArmSlot();
                    SnapOffHand(offHand, heldWeaponRenderer.transform, activeWeapon.heldScale);
                }
                else if (!isHoldingWithRightHand && limbController.GetArmData(false) != null)
                {
                    Transform offHand = limbController.GetRightArmSlot();
                    SnapOffHand(offHand, heldWeaponRenderer.transform, activeWeapon.heldScale);
                }
            }
            else
            {
                heldWeaponRenderer.transform.localScale = Vector3.one;
            }
        }
    }

    private void SnapOffHand(Transform hand, Transform weaponTransform, Vector3 weaponScale)
    {
        if (hand == null) return;
        
        // Compensate for weapon scale
        Vector3 compensatedOffset = new Vector3(
            secondaryGripOffset.x / weaponScale.x,
            secondaryGripOffset.y / weaponScale.y,
            secondaryGripOffset.z / weaponScale.z
        );

        hand.position = weaponTransform.TransformPoint(compensatedOffset);
        
        // Rotation: Match weapon rotation (flipped 180Z because arms usually point down/in)
        hand.rotation = weaponTransform.rotation * Quaternion.Euler(0, 0, 180f);
    }
}