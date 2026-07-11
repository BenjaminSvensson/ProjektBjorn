using UnityEngine;
using System.Collections.Generic;

public class DealerTrigger : MonoBehaviour
{
    [Header("Dealer Settings")]
    [Tooltip("How many items does this specific dealer sell?")]
    public int slotsToFill = 3;

    // --- MEMORY ---
    // These lists save the state of this specific dealer
    [HideInInspector]
    public List<ShopItemData> myInventory = new List<ShopItemData>(); 
    [HideInInspector]
    public List<bool> isSold = new List<bool>();
    
    private bool hasGeneratedItems = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Check if the Shop Manager exists
            if (DealerShopManager.Instance != null)
            {
                // 1. If this is our first time meeting, generate items
                if (!hasGeneratedItems)
                {
                    DealerShopManager.Instance.GenerateStockForDealer(this);
                    hasGeneratedItems = true;
                }

                // 2. Open the shop passing THIS script as the reference
                DealerShopManager.Instance.OpenShop(this);
            }
            else
            {
                Debug.LogError("DealerShopManager not found! Make sure DealerUi is enabled/present in the scene.");
            }
        }
    }
}