using UnityEngine;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(PlayerLimbController))]
public class WeaponSystem : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private WeaponData[] weaponSlots = new WeaponData[2]; 
    [SerializeField] private int activeSlotIndex = 0;

    [Header("Throwing")]
    [SerializeField] private float throwForce = 15f; 

    [Header("References")]
    [SerializeField] private WeaponHUD weaponHUD; 
    [SerializeField] private SpriteRenderer heldWeaponRenderer;

    [Header("Main Hand Grip")]
    [SerializeField] private Vector3 rightHandGripOffset = new Vector3(0.3f, 0f, 0f);
    [SerializeField] private Vector3 leftHandGripOffset = new Vector3(0.3f, 0f, 0f);

    [Header("Off-Hand Grip")]
    [Tooltip("Distance of the off-hand relative to the weapon pivot, aligned with the aim direction.")]
    [SerializeField] private Vector3 secondaryGripOffset = new Vector3(-0.3f, 0f, 0f);

    private PlayerLimbController limbController;
    private bool isHoldingWithRightHand = false; 
    private Camera cam;

    private float[] slotCooldowns = new float[2]; 
    
    // --- NEW: Instantiated Weapon Logic ---
    private GameObject currentEquippedInstance; 
    private Transform currentMuzzleSocket;

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

    // --- NEW: GetFirePoint Logic ---
    public Vector2 GetFirePoint()
    {
        // 1. Try to use the socket from the instantiated prefab
        if (currentMuzzleSocket != null)
        {
            return currentMuzzleSocket.position;
        }

        // 2. Fallback to Sprite/Offset calculation
        if (heldWeaponRenderer != null)
        {
            WeaponData weapon = GetActiveWeapon();
            if (weapon != null)
            {
                return heldWeaponRenderer.transform.TransformPoint(weapon.muzzleOffset);
            }
        }

        return transform.position;
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
        
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetActiveSlot(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetActiveSlot(1);
        
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (scroll > 0) SetActiveSlot(0);
            if (scroll < 0) SetActiveSlot(1);
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            ThrowActiveWeapon();
        }
    }

    private void ThrowActiveWeapon()
    {
        if (!IsHoldingWeapon()) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);
        Vector2 throwDir = (mouseWorldPos - (Vector2)transform.position).normalized;

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
                Vector2 finalDir = dropDir.HasValue ? dropDir.Value : Random.insideUnitCircle.normalized;
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
        
        // --- NEW: Cleanup old instantiated weapon ---
        if (currentEquippedInstance != null)
        {
            Destroy(currentEquippedInstance);
            currentEquippedInstance = null;
            currentMuzzleSocket = null;
        }

        WeaponData activeWeapon = weaponSlots[activeSlotIndex];

        if (heldWeaponRenderer != null)
        {
            if (activeWeapon != null)
            {
                // A. PREFAB MODE
                if (activeWeapon.equippedPrefab != null)
                {
                    // Disable sprite renderer, we are using a real object
                    heldWeaponRenderer.enabled = false; 

                    // Instantiate as child of the renderer transform (which acts as the pivot)
                    currentEquippedInstance = Instantiate(activeWeapon.equippedPrefab, heldWeaponRenderer.transform);
                    currentEquippedInstance.transform.localPosition = Vector3.zero;
                    currentEquippedInstance.transform.localRotation = Quaternion.identity;
                    
                    // REVERTED: Force scale to One. 
                    // Using the prefab's original scale caused issues with imported models (often 0.01 scale).
                    // Use 'heldScale' in WeaponData to resize the weapon if needed.
                    currentEquippedInstance.transform.localScale = Vector3.one;

                    // Find Muzzle
                    Transform socket = currentEquippedInstance.transform.Find(activeWeapon.muzzleSocketName);
                    if (socket != null)
                    {
                        currentMuzzleSocket = socket;
                    }
                    else
                    {
                        // Search recursively if not immediate child
                        currentMuzzleSocket = currentEquippedInstance.transform.GetComponentInChildren<Transform>().Find(activeWeapon.muzzleSocketName);
                    }
                }
                // B. SPRITE MODE (Fallback)
                else
                {
                    heldWeaponRenderer.sprite = activeWeapon.heldSprite;
                    heldWeaponRenderer.enabled = true;
                }
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
        if (heldWeaponRenderer == null || limbController == null) return;

        Transform mainAnchor = null;
        Vector3 gripOffset = Vector3.zero;
        WeaponData activeWeapon = weaponSlots[activeSlotIndex];

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

        if (mainAnchor != null)
        {
            Vector3 targetPos = mainAnchor.TransformPoint(gripOffset);
            
            Quaternion baseRotation = mainAnchor.rotation * Quaternion.Euler(0, 0, 180f);

            if (limbController.GetVisualsHolder() != null && limbController.GetVisualsHolder().localScale.x < 0)
            {
                baseRotation *= Quaternion.Euler(0, 180, 0);
            }

            Quaternion finalWeaponRotation = baseRotation;

            if (activeWeapon != null && activeWeapon.heldRotationOffset != 0f)
            {
                finalWeaponRotation *= Quaternion.Euler(0, 0, activeWeapon.heldRotationOffset);
            }
            
            heldWeaponRenderer.transform.SetPositionAndRotation(targetPos, finalWeaponRotation);
            
            if (activeWeapon != null)
            {
                heldWeaponRenderer.transform.localScale = activeWeapon.heldScale;

                if (isHoldingWithRightHand && limbController.GetArmData(true) != null)
                {
                    Transform offHand = limbController.GetLeftArmSlot();
                    SnapOffHand(offHand, heldWeaponRenderer.transform, baseRotation);
                }
                else if (!isHoldingWithRightHand && limbController.GetArmData(false) != null)
                {
                    Transform offHand = limbController.GetRightArmSlot();
                    SnapOffHand(offHand, heldWeaponRenderer.transform, baseRotation);
                }
            }
            else
            {
                heldWeaponRenderer.transform.localScale = Vector3.one;
            }
        }
    }

    private void SnapOffHand(Transform hand, Transform weaponTransform, Quaternion baseRotation)
    {
        if (hand == null) return;
        
        Vector3 worldOffset = baseRotation * secondaryGripOffset;
        Vector3 targetPos = weaponTransform.position + worldOffset;
        Quaternion targetRot = baseRotation * Quaternion.Euler(0, 0, 180f);

        hand.SetPositionAndRotation(targetPos, targetRot);
    }
}