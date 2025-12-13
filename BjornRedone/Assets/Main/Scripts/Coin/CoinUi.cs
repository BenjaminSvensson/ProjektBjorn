using UnityEngine;
using TMPro; // Required for TextMeshPro

public class CoinUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI coinText;
    
    // We find this automatically if null
    [SerializeField] private PlayerWallet playerWallet;

    void Start()
    {
        if (coinText == null) coinText = GetComponent<TextMeshProUGUI>();
        
        if (playerWallet == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player) playerWallet = player.GetComponent<PlayerWallet>();
        }

        if (playerWallet != null)
        {
            // Subscribe to the event
            playerWallet.OnCoinsChanged.AddListener(UpdateText);
            
            // Initial update
            UpdateText(playerWallet.GetCoins());
        }
        else
        {
            Debug.LogError("CoinUI could not find PlayerWallet!");
        }
    }

    void OnDestroy()
    {
        // Clean up event subscription to prevent memory leaks
        if (playerWallet != null)
        {
            playerWallet.OnCoinsChanged.RemoveListener(UpdateText);
        }
    }

    private void UpdateText(int amount)
    {
        if (coinText != null)
        {
            coinText.text = $"{amount}";
        }
    }
}