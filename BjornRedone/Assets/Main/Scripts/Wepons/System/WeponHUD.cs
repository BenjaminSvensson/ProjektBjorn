using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required for TextMeshPro

public class WeaponHUD : MonoBehaviour
{
    [Header("Weapon Slots")]
    [SerializeField] private Image slot1Icon;
    [SerializeField] private Image slot2Icon;
    [SerializeField] private GameObject slot1Highlight;
    [SerializeField] private GameObject slot2Highlight;
    [SerializeField] private Color filledSlotColor = Color.white;

    [Header("Ammo Display")]
    [Tooltip("Text showing 'Current/Max' (e.g. 6/6)")]
    [SerializeField] private TextMeshProUGUI clipText;
    [Tooltip("Text showing total reserve ammo")]
    [SerializeField] private TextMeshProUGUI reserveText;
    [SerializeField] private GameObject ammoPanel; // Optional: To hide entire ammo section for melee

    public void UpdateSlots(int activeIndex, WeaponData w1, WeaponData w2)
    {
        // Update Icons
        UpdateIcon(slot1Icon, w1);
        UpdateIcon(slot2Icon, w2);

        // Update Highlight
        if (slot1Highlight) slot1Highlight.SetActive(activeIndex == 0);
        if (slot2Highlight) slot2Highlight.SetActive(activeIndex == 1);
    }

    private void UpdateIcon(Image img, WeaponData data)
    {
        if (img == null) return;

        img.preserveAspect = true;

        if (data != null && data.icon != null)
        {
            img.sprite = data.icon;
            img.color = filledSlotColor;
            img.enabled = true; // Show icon
        }
        else
        {
            img.sprite = null;
            img.enabled = false; // Hide icon completely
        }
    }

    public void UpdateAmmo(int currentClip, int maxClip, int reserve, bool isRanged)
    {
        if (ammoPanel) ammoPanel.SetActive(isRanged);

        if (isRanged)
        {
            if (clipText) clipText.text = $"{currentClip}/{maxClip}";
            if (reserveText) reserveText.text = $"{reserve}";
            
            // Optional: Enable texts if panel isn't used
            if (clipText) clipText.gameObject.SetActive(true);
            if (reserveText) reserveText.gameObject.SetActive(true);
        }
        else
        {
            // Hide texts for melee
            if (clipText) clipText.gameObject.SetActive(false);
            if (reserveText) reserveText.gameObject.SetActive(false);
        }
    }
}