using UnityEngine;

public class TriggerUi : MonoBehaviour
{
    // No public variable needed anymore!
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Use the Singleton Instance to find the shop automatically
            if (DealerShopManager.Instance != null)
            {
                DealerShopManager.Instance.OpenShop(this.gameObject);
            }
            else
            {
                Debug.LogError("Could not find DealerShopManager! Make sure DealerUi is in the scene and ENABLED.");
            }
        }
    }
}