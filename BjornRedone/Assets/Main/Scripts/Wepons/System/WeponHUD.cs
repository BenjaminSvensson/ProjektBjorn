using UnityEngine;
using UnityEngine.UI;

public class WeaponHUD : MonoBehaviour
{
    [Header("Slot 1 Visuals")]
    [Tooltip("The GameObject (e.g., a glow or highlighted frame) to show when Slot 1 is ACTIVE.")]
    [SerializeField] private GameObject slot1ActiveObject;
    [Tooltip("The GameObject (e.g., a dark frame) to show when Slot 1 is INACTIVE.")]
    [SerializeField] private GameObject slot1InactiveObject;
    [Tooltip("The UI element that displays the actual Weapon Icon (Pistol, Sword, etc.).")]
    [SerializeField] private Image slot1Icon; // --- CHANGED back to Image (Sprite)

    [Header("Slot 2 Visuals")]
    [Tooltip("The GameObject to show when Slot 2 is ACTIVE.")]
    [SerializeField] private GameObject slot2ActiveObject;
    [Tooltip("The GameObject to show when Slot 2 is INACTIVE.")]
    [SerializeField] private GameObject slot2InactiveObject;
    [Tooltip("The UI element that displays the actual Weapon Icon.")]
    [SerializeField] private Image slot2Icon; // --- CHANGED back to Image (Sprite)

    public void UpdateSlots(int activeIndex, WeaponData weapon1, WeaponData weapon2)
    {
        // 1. Toggle Slot 1 Visuals
        bool isSlot1Active = (activeIndex == 0);
        if (slot1ActiveObject) slot1ActiveObject.SetActive(isSlot1Active);
        if (slot1InactiveObject) slot1InactiveObject.SetActive(!isSlot1Active);

        // 2. Toggle Slot 2 Visuals
        bool isSlot2Active = (activeIndex == 1);
        if (slot2ActiveObject) slot2ActiveObject.SetActive(isSlot2Active);
        if (slot2InactiveObject) slot2InactiveObject.SetActive(!isSlot2Active);

        // 3. Update the Icons (The pictures of the weapons)
        UpdateIcon(slot1Icon, weapon1);
        UpdateIcon(slot2Icon, weapon2);
    }

    private void UpdateIcon(Image iconImage, WeaponData data)
    {
        if (iconImage == null) return;

        if (data != null && data.icon != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = true;
            iconImage.preserveAspect = true; // Keeps the weapon looking correct (not stretched)
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }
}