using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using System.Collections.Generic;

public class DealerShopManager : MonoBehaviour
{
    // --- SINGLETON SETUP ---
    public static DealerShopManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Hide UI on start
        gameObject.SetActive(false); 
        
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(CloseShopAndFinish);
        }
    }

    [Header("Shop Configuration")]
    public List<ShopItemData> allPossibleItems;
    
    [Header("UI Assignments")]
    public Button[] uiSlots; 
    public Image[] itemIcons; 
    public TextMeshProUGUI[] priceTexts; 
    public Button exitButton;

    // Internal State
    private GameObject currentDealerObject;
    private PlayerWallet playerWallet;      
    private List<ShopItemData> currentItemsInShop = new List<ShopItemData>();
    private List<ShopItemData> purchasedItems = new List<ShopItemData>();

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
            uiSlots[i].gameObject.SetActive(false); 

            if (i < currentItemsInShop.Count)
            {
                int index = i;
                ShopItemData item = currentItemsInShop[i];
                
                // Icon Setup
                if (i < itemIcons.Length && itemIcons[i] != null)
                {
                    itemIcons[i].sprite = item.icon;
                    itemIcons[i].preserveAspect = true; 
                    Color c = itemIcons[i].color;
                    itemIcons[i].color = new Color(c.r, c.g, c.b, 1f); 
                }

                // Price Setup
                if (i < priceTexts.Length && priceTexts[i] != null)
                    priceTexts[i].text = item.price.ToString();

                // Button Setup
                Button slotBtn = uiSlots[i];
                slotBtn.interactable = true;
                slotBtn.onClick.RemoveAllListeners();
                slotBtn.onClick.AddListener(() => TryBuyItem(index));
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
                priceTexts[index].text = "Sold";

            // If we bought the LAST item, close and destroy automatically
            if (purchasedItems.Count == currentItemsInShop.Count)
            {
                CloseShopAndFinish();
            }
        }
    }

    public void CloseShopAndFinish()
    {
        // 1. Spawn everything we bought
        if (currentDealerObject != null)
        {
            foreach (var item in purchasedItems)
            {
                if (item.itemPrefab != null)
                    Instantiate(item.itemPrefab, currentDealerObject.transform.position, Quaternion.identity);
            }

            // 2. CHECK: Did we buy everything?
            bool allSold = (purchasedItems.Count >= currentItemsInShop.Count);

            if (allSold)
            {
                // We cleared the shop -> Destroy the dealer
                Destroy(currentDealerObject);
            }
            else
            {
                // We left some items behind -> Dealer stays alive
                Debug.Log("Exiting shop. Dealer stays because items remain.");
            }
        }
        
        // 3. Close the UI
        gameObject.SetActive(false);
    }
}