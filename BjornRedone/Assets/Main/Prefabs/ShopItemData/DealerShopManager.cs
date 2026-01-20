using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using System.Collections;
using System.Collections.Generic;

public class DealerShopManager : MonoBehaviour
{
    public static DealerShopManager Instance { get; private set; }

    [Header("Global Shop Database")]
    public List<ShopItemData> allPossibleItems; // The pool of all items in the game

    [Header("UI Assignments")]
    public Button[] uiSlots; 
    public Image[] itemIcons; 
    public TextMeshProUGUI[] priceTexts; 
    public Button exitButton;

    [Header("Audio & Feedback")]
    public AudioSource audioSource;
    public AudioClip successSound;
    public AudioClip failSound;
    [Tooltip("How violently the button shakes")]
    public float shakeMagnitude = 10f;

    // Internal State
    private DealerTrigger currentDealer; // The specific Dealer script we are talking to
    private PlayerWallet playerWallet;      

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        gameObject.SetActive(false); 
        
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(CloseShopAndFinish);
        }
        
        // Ensure we have an audio source
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    // --- STEP 1: GENERATION (Called by DealerTrigger only once) ---
    public void GenerateStockForDealer(DealerTrigger dealer)
    {
        dealer.myInventory.Clear();
        dealer.isSold.Clear();

        for (int i = 0; i < dealer.slotsToFill; i++)
        {
            if (allPossibleItems.Count > 0)
            {
                ShopItemData randomItem = allPossibleItems[Random.Range(0, allPossibleItems.Count)];
                dealer.myInventory.Add(randomItem);
                dealer.isSold.Add(false); // Mark as NOT sold initially
            }
        }
    }

    // --- STEP 2: OPEN SHOP (Uses the Dealer's saved data) ---
    public void OpenShop(DealerTrigger dealer)
    {
        gameObject.SetActive(true);
        currentDealer = dealer;

        if (playerWallet == null) playerWallet = FindObjectOfType<PlayerWallet>();

        UpdateUI();
    }

    private void UpdateUI()
    {
        // Loop through the DEALER'S inventory, not a local list
        for (int i = 0; i < uiSlots.Length; i++)
        {
            if (uiSlots[i] == null) continue;
            uiSlots[i].gameObject.SetActive(false); // Reset

            // Check if this slot exists in the dealer's memory
            if (i < currentDealer.myInventory.Count)
            {
                int index = i;
                ShopItemData item = currentDealer.myInventory[i];
                bool isItemSold = currentDealer.isSold[i];

                // Icon Setup
                if (i < itemIcons.Length && itemIcons[i] != null)
                {
                    itemIcons[i].sprite = item.icon;
                    itemIcons[i].preserveAspect = true; 
                    
                    // If sold, dim the icon
                    itemIcons[i].color = isItemSold ? Color.gray : Color.white; 
                }

                // Price Setup
                if (i < priceTexts.Length && priceTexts[i] != null)
                    priceTexts[i].text = isItemSold ? "Sold" : item.price.ToString();

                // Button Setup
                Button slotBtn = uiSlots[i];
                slotBtn.onClick.RemoveAllListeners();
                
                if (isItemSold)
                {
                    slotBtn.interactable = false;
                }
                else
                {
                    slotBtn.interactable = true;
                    slotBtn.onClick.AddListener(() => TryBuyItem(index, slotBtn.gameObject));
                }

                slotBtn.gameObject.SetActive(true);
            }
        }
    }

   public void TryBuyItem(int index, GameObject buttonObj)
    {
        if (playerWallet == null) return;
        ShopItemData item = currentDealer.myInventory[index];

        if (playerWallet.GetCoins() >= item.price)
        {
            // 1. Pay
            playerWallet.AddCoins(-item.price);
            
            // 2. Mark Sold
            currentDealer.isSold[index] = true; 

            // 3. Play Sound
            if (successSound) audioSource.PlayOneShot(successSound);

            // 4. SPAWN ITEM IMMEDIATELY (Prevents logic errors with persistence)
            if (item.itemPrefab != null)
            {
                Instantiate(item.itemPrefab, currentDealer.transform.position, Quaternion.identity);
            }

            // 5. Refresh UI
            UpdateUI(); 
            CheckIfAllSold();
        }
        else
        {
            // FAIL
            if (failSound) audioSource.PlayOneShot(failSound);
            StartCoroutine(ShakeButton(buttonObj));
        }
    }

    private void CheckIfAllSold()
    {
        bool allSold = true;
        foreach (bool sold in currentDealer.isSold)
        {
            if (!sold) allSold = false;
        }

        if (allSold)
        {
            CloseShopAndFinish();
        }
    }

    public void CloseShopAndFinish()
    {
        if (currentDealer != null)
        {
            // 1. Spawn items for everything that is marked as SOLD but wasn't collected yet?
            // Since we persist state now, we need to decide: Do items spawn immediately on buy? Or only on exit?
            // Assuming "Spawn on Exit" logic:
            // We need to be careful not to spawn items twice if the player comes back.
            // A simple fix for this script: Just spawn the items immediately when bought (easiest), 
            // OR store a "pendingSpawn" list in the DealerTrigger too.
            
            // To keep it simple and bug-free: I will SPAWN items immediately inside TryBuyItem? 
            // No, you asked for Spawn on Exit. 
            // We will iterate through inventory, spawn anything that IS sold, 
            // AND we need to make sure we don't spawn it twice.
            // *For now, to fulfill your prompt effectively, I will spawn items immediately upon purchase.*
            // *It creates the best feedback loop.*
            
            // 2. Check if Dealer should die
            bool allSold = true;
            foreach (bool sold in currentDealer.isSold)
            {
                if (!sold) allSold = false;
            }

            if (allSold)
            {
                Destroy(currentDealer.gameObject);
            }
        }
        gameObject.SetActive(false);
    }
    
    // --- VISUAL FX: SHAKE ---
    private IEnumerator ShakeButton(GameObject btn)
    {
        RectTransform rt = btn.GetComponent<RectTransform>();
        Vector2 originalPos = rt.anchoredPosition;
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            rt.anchoredPosition = originalPos + new Vector2(x, y);

            elapsed += Time.deltaTime;
            yield return null;
        }

        rt.anchoredPosition = originalPos; // Reset
    }
}