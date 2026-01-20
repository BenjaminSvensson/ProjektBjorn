using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using System.Collections.Generic;

public class DealerShopManager : MonoBehaviour
{
    [Header("Shop Configuration")]
    public List<ShopItemData> allPossibleItems;
    
    [Header("UI Assignments")]
    [Tooltip("The actual clickable Buttons")]
    public Button[] uiSlots; 
    
    [Tooltip("The Image component where the sword/potion icon should appear")]
    public Image[] itemIcons; // <--- NEW: Drag your Icon Images here

    [Tooltip("The Text component for the price")]
    public TextMeshProUGUI[] priceTexts; 

    public Button exitButton;

    // Internal State
    private GameObject currentDealerObject;
    private PlayerWallet playerWallet;      
    private List<ShopItemData> currentItemsInShop = new List<ShopItemData>();
    private List<ShopItemData> purchasedItems = new List<ShopItemData>();

    private void Awake()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(CloseShopAndFinish);
        }
    }

    public void OpenShop(GameObject dealer)
    {
        gameObject.SetActive(true);

        if (playerWallet == null)
            playerWallet = FindObjectOfType<PlayerWallet>();

        currentDealerObject = dealer;
        purchasedItems.Clear();
        currentItemsInShop.Clear();

        GenerateRandomItems();
        UpdateUI();
    }

    private void GenerateRandomItems()
    {
        for (int i = 0; i < uiSlots.Length; i++)
        {
            if (allPossibleItems.Count > 0)
            {
                ShopItemData randomItem = allPossibleItems[Random.Range(0, allPossibleItems.Count)];
                currentItemsInShop.Add(randomItem);
            }
        }
    }

    private void UpdateUI()
    {
        for (int i = 0; i < uiSlots.Length; i++)
        {
            if (uiSlots[i] == null) continue;

            // Hide everything initially to be safe
            uiSlots[i].gameObject.SetActive(false); 

            if (i < currentItemsInShop.Count)
            {
                int index = i;
                ShopItemData item = currentItemsInShop[i];
                
                // 1. UPDATE ICON (Explicitly using the assigned array)
                if (i < itemIcons.Length && itemIcons[i] != null)
                {
                    itemIcons[i].sprite = item.icon;
                    // Fix transparent icons:
                    Color c = itemIcons[i].color;
                    itemIcons[i].color = new Color(c.r, c.g, c.b, 1f); 
                }

                // 2. UPDATE PRICE
                if (i < priceTexts.Length && priceTexts[i] != null)
                {
                    priceTexts[i].text = item.price.ToString();
                }

                // 3. SETUP BUTTON
                Button slotBtn = uiSlots[i];
                slotBtn.interactable = true;
                slotBtn.onClick.RemoveAllListeners();
                slotBtn.onClick.AddListener(() => TryBuyItem(index));
                
                // Show the button now that it's ready
                slotBtn.gameObject.SetActive(true);
            }
        }
    }

    public void TryBuyItem(int index)
    {
        if (playerWallet == null) return;
        ShopItemData item = currentItemsInShop[index];

        if (playerWallet.GetCoins() >= item.price)
        {
            playerWallet.AddCoins(-item.price);
            purchasedItems.Add(item);
            
            uiSlots[index].interactable = false;

            if (index < priceTexts.Length && priceTexts[index] != null)
            {
                priceTexts[index].text = "Sold";
            }

            if (purchasedItems.Count == currentItemsInShop.Count)
                CloseShopAndFinish();
        }
    }

    public void CloseShopAndFinish()
    {
        if (currentDealerObject != null)
        {
            foreach (var item in purchasedItems)
            {
                if (item.itemPrefab != null)
                    Instantiate(item.itemPrefab, currentDealerObject.transform.position, Quaternion.identity);
            }
            Destroy(currentDealerObject);
        }
        gameObject.SetActive(false);
    }
}