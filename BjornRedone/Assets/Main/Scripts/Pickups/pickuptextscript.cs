using UnityEngine;

public class pickuptextscript : MonoBehaviour
{

    [SerializeField] private string bonusText;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Your existing pickup logic is already here
        // (stats, destroy, etc.)

        FindFirstObjectByType<Bonus_script>().ShowBonus(bonusText);

    }
}
