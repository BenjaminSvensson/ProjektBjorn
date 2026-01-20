using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using System.Collections;
using System.Collections.Generic;

public class DealerShopManager : MonoBehaviour
{
    public static DealerShopManager Instance { get; private set; }

    [Header("Global Shop Database")]
    public List<ShopItemData> allPossibleItems; 

    [Header("UI Assignments")]
    public Button[] uiSlots; 
    public Image[] itemIcons; 
    public TextMeshProUGUI[] priceTexts; 
    public Button exitButton;

    [Header("Audio & Feedback")]
    public AudioSource audioSource;
    public AudioClip successSound;
    public AudioClip failSound;
    public float shakeMagnitude = 10f;

    // Internal State
    private DealerTrigger currentDealer; 
    private PlayerWallet playerWallet;      

    private void Awake()
    {
        // Singleton Setup
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        Instance = this;

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(CloseShopAndFinish);
        }
        
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // IMPORTANT: We disable the WHOLE GameObject here.
        // This ensures enemies see it as "inactive" and can move.
        // Because we do this at the end of Awake, the Instance is already set safely.
        gameObject.SetActive(false);
    }

    // --- STEP 1: GENERATION ---
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
                dealer.isSold.Add(false); 
            }
        }
    }

    // --- STEP 2: OPEN SHOP ---
    public void OpenShop(DealerTrigger dealer)
    {
        // 1. Enable the GameObject (Enemies will see this and FREEZE)
        gameObject.SetActive(true);
        
        currentDealer = dealer;

        if (playerWallet == null) playerWallet = FindObjectOfType<PlayerWallet>();

        UpdateUI();
    }

    private void UpdateUI()
    {
        for (int i = 0; i < uiSlots.Length; i++)
        {
            if (uiSlots[i] == null) continue;
            uiSlots[i].gameObject.SetActive(false); 

            if (i < currentDealer.myInventory.Count)
            {
                int index = i;
                ShopItemData item = currentDealer.myInventory[i];
                bool isItemSold = currentDealer.isSold[i];

                if (i < itemIcons.Length && itemIcons[i] != null)
                {
                    itemIcons[i].sprite = item.icon;
                    itemIcons[i].preserveAspect = true; 
                    itemIcons[i].color = isItemSold ? Color.gray : Color.white; 
                }

                if (i < priceTexts.Length && priceTexts[i] != null)
                    priceTexts[i].text = isItemSold ? "Sold" : item.price.ToString();

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
            playerWallet.AddCoins(-item.price);
            currentDealer.isSold[index] = true; 

            if (successSound) audioSource.PlayOneShot(successSound);

            UpdateUI(); 
            CheckIfAllSold();
        }
        else
        {
            if (failSound) audioSource.PlayOneShot(failSound);
            StartCoroutine(ShakeButton(buttonObj));
            Debug.Log("Cannot afford!");
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
        
        // IMPORTANT: Disable the GameObject.
        // Enemies will now see "activeInHierarchy == false" and UNFREEZE.
        gameObject.SetActive(false);
    }
    
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

        rt.anchoredPosition = originalPos; 
    }
}