using UnityEngine;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(PlayerLimbController))]
public class WeaponSystem : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private WeaponData[] weaponSlots = new WeaponData[2]; 
    [SerializeField] private int activeSlotIndex = 0;

    [Header("Ammo")]
    [SerializeField] private int totalReserveAmmo = 24; 
    [SerializeField] private int maxReserveAmmo = 99;
    
    private int[] slotAmmoCounts = new int[2]; 
    
    private bool isReloading = false;
    private float reloadTimer = 0f;

    [Header("Throwing")]
    [SerializeField] private float throwForce = 15f; 

    [Header("References")]
    [SerializeField] private WeaponHUD weaponHUD; 
    [SerializeField] private SpriteRenderer heldWeaponRenderer;
    [SerializeField] private AudioSource audioSource; 

    [Header("Main Hand Grip")]
    [SerializeField] private Vector3 rightHandGripOffset = new Vector3(0.3f, 0f, 0f);
    [SerializeField] private Vector3 leftHandGripOffset = new Vector3(0.3f, 0f, 0f);

    [Header("Off-Hand Grip")]
    [SerializeField] private Vector3 secondaryGripOffset = new Vector3(-0.3f, 0f, 0f);

    private PlayerLimbController limbController;
    private bool isHoldingWithRightHand = false; 
    private Camera cam;

    private float[] slotCooldowns = new float[2]; 
    private GameObject currentEquippedInstance; 
    private Transform currentMuzzleSocket;

    void Awake()
    {
        limbController = GetComponent<PlayerLimbController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        
        for(int i=0; i<2; i++) slotAmmoCounts[i] = 0;
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
        HandleReloadLogic();
    }

    void LateUpdate()
    {
        UpdateWeaponTransform();
    }

    private void HandleReloadLogic()
    {
        if (isReloading)
        {
            reloadTimer -= Time.deltaTime;
            if (reloadTimer <= 0)
            {
                FinishReload();
            }
        }
    }

    public void StartReload()
    {
        WeaponData activeWeapon = GetActiveWeapon();
        if (activeWeapon == null || activeWeapon.type != WeaponType.Ranged) return;
        if (isReloading) return;
        if (slotAmmoCounts[activeSlotIndex] >= activeWeapon.magazineSize) return; 
        if (totalReserveAmmo <= 0) return; 

        isReloading = true;
        reloadTimer = activeWeapon.reloadTime;

        if (audioSource != null && activeWeapon.reloadSound != null)
        {
            audioSource.PlayOneShot(activeWeapon.reloadSound);
        }
        
        // Update HUD to reflect maybe a "Reloading..." state if you wanted, 
        // for now we just keep showing numbers
        UpdateAmmoUI();
    }

    private void FinishReload()
    {
        isReloading = false;
        WeaponData activeWeapon = GetActiveWeapon();
        if (activeWeapon == null) return;

        int spaceInMag = activeWeapon.magazineSize - slotAmmoCounts[activeSlotIndex];
        int amountToLoad = Mathf.Min(spaceInMag, totalReserveAmmo);

        slotAmmoCounts[activeSlotIndex] += amountToLoad;
        totalReserveAmmo -= amountToLoad;

        UpdateAmmoUI();
    }

    public void AddReserveAmmo(int amount)
    {
        totalReserveAmmo = Mathf.Min(totalReserveAmmo + amount, maxReserveAmmo);
        UpdateAmmoUI();
    }

    public void ConsumeAmmo(int amount = 1)
    {
        if (activeSlotIndex >= 0 && activeSlotIndex < slotAmmoCounts.Length)
        {
            slotAmmoCounts[activeSlotIndex] = Mathf.Max(0, slotAmmoCounts[activeSlotIndex] - amount);
            UpdateAmmoUI();
        }
    }

    private void UpdateAmmoUI()
    {
        if (weaponHUD == null) return;

        WeaponData activeWeapon = GetActiveWeapon();
        if (activeWeapon != null && activeWeapon.type == WeaponType.Ranged)
        {
            weaponHUD.UpdateAmmo(
                slotAmmoCounts[activeSlotIndex], 
                activeWeapon.magazineSize, 
                totalReserveAmmo, 
                true
            );
        }
        else
        {
            weaponHUD.UpdateAmmo(0, 0, totalReserveAmmo, false);
        }
    }

    public Vector2 GetFirePoint()
    {
        if (currentMuzzleSocket != null) return currentMuzzleSocket.position;
        if (heldWeaponRenderer != null && GetActiveWeapon() != null)
            return heldWeaponRenderer.transform.TransformPoint(GetActiveWeapon().muzzleOffset);
        return transform.position;
    }

    private void UpdateCooldowns()
    {
        for (int i = 0; i < slotCooldowns.Length; i++)
        {
            if (slotCooldowns[i] > 0) slotCooldowns[i] -= Time.deltaTime;
        }
    }

    public float GetCurrentWeaponCooldown() { return activeSlotIndex >= 0 ? slotCooldowns[activeSlotIndex] : 0f; }
    public void SetCurrentWeaponCooldown(float time) { if (activeSlotIndex >= 0) slotCooldowns[activeSlotIndex] = time; }
    public int GetCurrentClipAmmo() { return activeSlotIndex >= 0 ? slotAmmoCounts[activeSlotIndex] : 0; }
    public int GetTotalReserveAmmo() { return totalReserveAmmo; }
    public bool IsReloading() { return isReloading; }

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

        if (Keyboard.current.qKey.wasPressedThisFrame) ThrowActiveWeapon();
        if (Keyboard.current.rKey.wasPressedThisFrame) StartReload();
    }

    private void ThrowActiveWeapon()
    {
        if (!IsHoldingWeapon()) return;
        isReloading = false; 
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 throwDir = (mouseWorldPos - (Vector2)transform.position).normalized;
        DropWeapon(activeSlotIndex, throwDir, throwForce);
    }

    private void SetActiveSlot(int index)
    {
        if (index < 0 || index >= weaponSlots.Length) return;
        if (activeSlotIndex == index) return;
        isReloading = false;
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

    public bool HasAnyWeapon() { return weaponSlots[0] != null || weaponSlots[1] != null; }
    public bool IsHoldingWeapon() { return weaponSlots[activeSlotIndex] != null; }
    public WeaponData GetActiveWeapon() { return weaponSlots[activeSlotIndex]; }
    public bool IsHoldingWithRightHand() { return isHoldingWithRightHand; }

    public bool TryPickupWeapon(WeaponData newData)
    {
        if (newData == null) return false;
        if (limbController != null && !limbController.CanAttack()) return false;

        if (weaponSlots[activeSlotIndex] != null) DropWeapon(activeSlotIndex);

        weaponSlots[activeSlotIndex] = newData;
        slotCooldowns[activeSlotIndex] = 0f; 
        slotAmmoCounts[activeSlotIndex] = newData.magazineSize; 
        isReloading = false;

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
        slotAmmoCounts[slotIndex] = 0; 

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

        UpdateState();
    }

    private void UpdateState()
    {
        if (weaponHUD != null)
        {
            weaponHUD.UpdateSlots(activeSlotIndex, weaponSlots[0], weaponSlots[1]);
            UpdateAmmoUI(); // --- NEW: Update Ammo Text when switching state
        }
        
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
                if (activeWeapon.equippedPrefab != null)
                {
                    heldWeaponRenderer.enabled = false; 
                    currentEquippedInstance = Instantiate(activeWeapon.equippedPrefab, heldWeaponRenderer.transform);
                    currentEquippedInstance.transform.localPosition = Vector3.zero;
                    currentEquippedInstance.transform.localRotation = Quaternion.identity;
                    currentEquippedInstance.transform.localScale = Vector3.one;

                    Transform socket = currentEquippedInstance.transform.Find(activeWeapon.muzzleSocketName);
                    if (socket != null) currentMuzzleSocket = socket;
                    else currentMuzzleSocket = currentEquippedInstance.transform.GetComponentInChildren<Transform>().Find(activeWeapon.muzzleSocketName);
                }
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
                baseRotation *= Quaternion.Euler(0, 180, 0);

            Quaternion finalWeaponRotation = baseRotation;
            if (activeWeapon != null && activeWeapon.heldRotationOffset != 0f)
                finalWeaponRotation *= Quaternion.Euler(0, 0, activeWeapon.heldRotationOffset);
            
            heldWeaponRenderer.transform.SetPositionAndRotation(targetPos, finalWeaponRotation);
            
            if (activeWeapon != null)
            {
                heldWeaponRenderer.transform.localScale = activeWeapon.heldScale;
                if (isHoldingWithRightHand && limbController.GetArmData(true) != null)
                    SnapOffHand(limbController.GetLeftArmSlot(), heldWeaponRenderer.transform, baseRotation);
                else if (!isHoldingWithRightHand && limbController.GetArmData(false) != null)
                    SnapOffHand(limbController.GetRightArmSlot(), heldWeaponRenderer.transform, baseRotation);
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