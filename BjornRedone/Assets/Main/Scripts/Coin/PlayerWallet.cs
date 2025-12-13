using UnityEngine;
using UnityEngine.Events;

public class PlayerWallet : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private int currentCoins = 0;

    // Event other scripts (like UI) can listen to
    public UnityEvent<int> OnCoinsChanged;

    public void AddCoins(int amount)
    {
        currentCoins += amount;
        
        // Notify UI
        OnCoinsChanged?.Invoke(currentCoins);
        
        Debug.Log($"Collected {amount} coins. Total: {currentCoins}");
    }

    public int GetCoins()
    {
        return currentCoins;
    }
}