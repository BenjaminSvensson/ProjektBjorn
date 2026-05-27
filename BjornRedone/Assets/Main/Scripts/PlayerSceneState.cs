using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerSceneState
{
    [System.Serializable]
    public struct LimbSlotState
    {
        public LimbSlot slot;
        public LimbData data;
        public bool isDamaged;
    }

    [System.Serializable]
    public struct WeaponSlotState
    {
        public WeaponData data;
        public int ammo;
        public float cooldown;
    }

    public class PlayerState
    {
        public bool hasState;
        public float torsoHealth;
        public float maxTorsoHealth;
        public float speedMultiplier = 1f;
        public float strengthMultiplier = 1f;
        public float healthMultiplier = 1f;
        public LimbSlotState[] limbs = new LimbSlotState[0];
        public WeaponSlotState[] weapons = new WeaponSlotState[0];
        public int activeWeaponSlot;
        public int reserveAmmo;
        public int coins;
    }

    private static PlayerState savedState;

    public static bool HasSavedState => savedState != null && savedState.hasState;

    public static bool SaveCurrentPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return false;

        return SaveFrom(player);
    }

    public static bool SaveFrom(GameObject player)
    {
        if (player == null) return false;

        PlayerLimbController limbs = player.GetComponent<PlayerLimbController>();
        if (limbs == null || !limbs.enabled) return false;

        PlayerState state = limbs.CaptureSceneState();

        Multipliers multipliers = player.GetComponent<Multipliers>();
        if (multipliers != null)
        {
            state.speedMultiplier = multipliers.speed;
            state.strengthMultiplier = multipliers.strength;
            state.healthMultiplier = multipliers.health;
        }

        WeaponSystem weapons = player.GetComponent<WeaponSystem>();
        if (weapons != null)
        {
            weapons.CaptureSceneState(state);
        }

        PlayerWallet wallet = player.GetComponent<PlayerWallet>();
        if (wallet != null)
        {
            state.coins = wallet.GetCoins();
        }

        state.hasState = true;
        savedState = state;
        return true;
    }

    public static void RestoreTo(GameObject player)
    {
        if (!HasSavedState || player == null) return;

        Multipliers multipliers = player.GetComponent<Multipliers>();
        if (multipliers != null)
        {
            multipliers.speed = savedState.speedMultiplier;
            multipliers.strength = savedState.strengthMultiplier;
            multipliers.health = savedState.healthMultiplier;
        }

        PlayerLimbController limbs = player.GetComponent<PlayerLimbController>();
        if (limbs != null)
        {
            limbs.RestoreSceneState(savedState);
        }

        WeaponSystem weapons = player.GetComponent<WeaponSystem>();
        if (weapons != null)
        {
            weapons.RestoreSceneState(savedState);
        }

        PlayerWallet wallet = player.GetComponent<PlayerWallet>();
        if (wallet != null)
        {
            wallet.SetCoins(savedState.coins);
        }
    }

    public static void LoadSceneWithPlayerState(string sceneName)
    {
        if (!SaveCurrentPlayer())
        {
            Clear();
        }

        SceneManager.LoadScene(sceneName);
    }

    public static void Clear()
    {
        savedState = null;
    }
}
