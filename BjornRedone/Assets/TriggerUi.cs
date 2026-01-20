using UnityEngine;

public class TriggerUi : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Drag the 'DealerUi' object here, even if it is disabled.")]
    public DealerShopManager shopManager; 

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Check for Player
        if (other.CompareTag("Player"))
        {
            // 2. Check if we assigned the shop in the inspector
            if (shopManager != null)
            {
                shopManager.OpenShop(this.gameObject);
            }
            else
            {
                Debug.LogError("Shop Manager is not assigned on " + gameObject.name);
            }
        }
    }
}