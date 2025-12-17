using UnityEngine;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(PlayerLimbController))]
public class WeaponSystem : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private WeaponData[] weaponSlots = new WeaponData[2]; 
    [SerializeField] private int activeSlotIndex = 0;

    [Header("Throwing")]
    [SerializeField] private float throwForce = 15f; // Stronger force for Q throw

    [Header("References")]
    [SerializeField] private WeaponHUD weaponHUD; 
    [SerializeField] private SpriteRenderer heldWeaponRenderer;

    [Header("Main Hand Grip")]
    [SerializeField] private Vector3 rightHandGripOffset = new Vector3(0.3f, 0f, 0f);
    [SerializeField] private Vector3 leftHandGripOffset = new Vector3(0.3f, 0f, 0f);

    [Header("Off-Hand Grip")]
    [SerializeField] private Vector3 secondaryGripOffset = new Vector3(-0.3f, 0f, 0f);

    private PlayerLimbController limbController;
    private bool isHoldingWithRightHand = false; 
    private Camera cam;

    private float[] slotCooldowns = new float[2]; 

    void Awake()
    {
        limbController = GetComponent<PlayerLimbController>();
        cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
    }

    void Start()
    {
        UpdateState();
    }

    void Update()
    {
        HandleInput();
        CheckArmStatus();
        UpdateCooldowns(); 
    }

    void LateUpdate()
    {
        UpdateWeaponTransform();
    }

    private void UpdateCooldowns()
    {
        for (int i = 0; i < slotCooldowns.Length; i++)
        {
            if (slotCooldowns[i] > 0)
            {
                slotCooldowns[i] -= Time.deltaTime;
            }
        }
    }

    public float GetCurrentWeaponCooldown()
    {
        if (activeSlotIndex >= 0 && activeSlotIndex < slotCooldowns.Length)
            return slotCooldowns[activeSlotIndex];
        return 0f;
    }

    public void SetCurrentWeaponCooldown(float time)
    {
        if (activeSlotIndex >= 0 && activeSlotIndex < slotCooldowns.Length)
            slotCooldowns[activeSlotIndex] = time;
    }

    private void HandleInput()
    {
        if (Keyboard.current == null) return;
        
        // Slot Switching
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetActiveSlot(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetActiveSlot(1);
        
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (scroll > 0) SetActiveSlot(0);
            if (scroll < 0) SetActiveSlot(1);
        }

        // Throw Weapon Input
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            ThrowActiveWeapon();
        }
    }

    private void ThrowActiveWeapon()
    {
        if (!IsHoldingWeapon()) return;

        // Calculate direction towards mouse (The player's "Aim/Facing" direction)
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);
        Vector2 throwDir = (mouseWorldPos - (Vector2)transform.position).normalized;

        // Pass the explicit throw force
        DropWeapon(activeSlotIndex, throwDir, throwForce);
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
        slotCooldowns[activeSlotIndex] = 0f; 

        UpdateState();
        return true;
    }

    /// <summary>
    /// Drops the weapon in the specified slot.
    /// </summary>
    public void DropWeapon(int slotIndex, Vector2? dropDir = null, float force = 5f)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;
        
        WeaponData weaponToDrop = weaponSlots[slotIndex];
        if (weaponToDrop == null) return;

        weaponSlots[slotIndex] = null;
        slotCooldowns[slotIndex] = 0f; 

        if (weaponToDrop.pickupPrefab != null)
        {
            GameObject drop = Instantiate(weaponToDrop.pickupPrefab, transform.position, Quaternion.identity);
            WeaponPickup pickupScript = drop.GetComponent<WeaponPickup>();
            if (pickupScript != null)
            {
                // Use provided direction OR random direction if none provided
                Vector2 finalDir = dropDir.HasValue ? dropDir.Value : Random.insideUnitCircle.normalized;
                
                // Pass the force to the pickup script
                pickupScript.InitializeDrop(finalDir, force);
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
            Vector3 targetPos = mainAnchor.TransformPoint(gripOffset);
            Quaternion finalRotation = mainAnchor.rotation * Quaternion.Euler(0, 0, 180f);

            // FLIP FIX: If player is facing left, flip 180 on Y axis
            if (limbController.GetVisualsHolder() != null && limbController.GetVisualsHolder().localScale.x < 0)
            {
                finalRotation *= Quaternion.Euler(0, 180, 0);
            }
            
            heldWeaponRenderer.transform.SetPositionAndRotation(targetPos, finalRotation);
            
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
        
        Vector3 compensatedOffset = new Vector3(
            secondaryGripOffset.x / weaponScale.x,
            secondaryGripOffset.y / weaponScale.y,
            secondaryGripOffset.z / weaponScale.z
        );

        Vector3 targetPos = weaponTransform.TransformPoint(compensatedOffset);
        Quaternion targetRot = weaponTransform.rotation * Quaternion.Euler(0, 0, 180f);

        hand.SetPositionAndRotation(targetPos, targetRot);
    }
}