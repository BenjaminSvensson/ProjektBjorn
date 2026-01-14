using UnityEngine;

public class Speedpill : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float addspeed = 0.25f;
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
                playerMultipliers.speed += addspeed; // increase player's speed
            }

            Destroy(gameObject); // destroy the pill
        }
    }
}
