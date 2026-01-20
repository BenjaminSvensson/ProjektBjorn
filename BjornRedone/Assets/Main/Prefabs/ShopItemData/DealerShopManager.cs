using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using System.Collections.Generic;

public class DealerShopManager : MonoBehaviour
{
    // --- SINGLETON SETUP START ---
    public static DealerShopManager Instance { get; private set; }

    private void Awake()
    {
        // If there is already a manager, destroy this new one (safety check)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // AUTOMATICALLY HIDE UI ON START
        // This keeps your game clean, but allows the script to exist.
        // Make sure the main GameObject is ENABLED in the Inspector!
        gameObject.SetActive(false); // We hide it instantly here
        
        // Setup Exit Button
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(CloseShopAndFinish);
        }
    }
    // --- SINGLETON SETUP END ---

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
        // Show the UI
        gameObject.SetActive(true);

        if (playerWallet == null)
            playerWallet = FindObjectOfType<PlayerWallet>();

        currentDealerObject = dealer;
        purchasedItems.Clear();
        currentItemsInShop.Clear();

        GenerateRandomItems();
        UpdateUI();
    }

    // ... (The rest of your script: GenerateRandomItems, UpdateUI, TryBuyItem, CloseShopAndFinish)
    // ... PASTE THE REST OF THE FUNCTIONS HERE FROM THE PREVIOUS SCRIPT ...
    
    // For completeness, here are the functions so you don't lose them:
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
                
                if (i < itemIcons.Length && itemIcons[i] != null)
                {
                    itemIcons[i].sprite = item.icon;
                    itemIcons[i].preserveAspect = true; 
                    Color c = itemIcons[i].color;
                    itemIcons[i].color = new Color(c.r, c.g, c.b, 1f); 
                }

                if (i < priceTexts.Length && priceTexts[i] != null)
                    priceTexts[i].text = item.price.ToString();

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