using UnityEngine;

public class Strength_pills : MonoBehaviour
{
    public float addstrength = 0.25f;
    private Multipliers multipliers;
    void Start()
    {
        multipliers = GetComponent<Multipliers>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Multipliers playerMultipliers = collision.GetComponent<Multipliers>();
            if (playerMultipliers != null)
            {
                playerMultipliers.strength += addstrength; // increase player's speed
            }

            Destroy(gameObject); // destroy the pill
        }
    }
}
