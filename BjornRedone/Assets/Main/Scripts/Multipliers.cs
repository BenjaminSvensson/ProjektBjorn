using UnityEngine;

public class Multipliers : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float speed = 1f;
    public float maxspeedmultiplier = 5f;
    public float strength = 1f;
    public float maxstreangthmultiplier = 10f;
    public float health = 1f;

    private void FixedUpdate()
    {
        if(speed > maxspeedmultiplier)
        {
            speed = maxspeedmultiplier;
        }
        if (strength > maxstreangthmultiplier)
        {
            strength = maxstreangthmultiplier;
        }

    }
}