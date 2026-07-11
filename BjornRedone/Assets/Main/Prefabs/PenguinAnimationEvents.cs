using UnityEngine;

public class PenguinAnimationEvents : MonoBehaviour
{
    // Drag your Main Penguin Parent object here in the Inspector!
    public PenguinEnemyAI mainAI; 

    // CALL THIS FUNCTION from the Animation Event at Frame 8 and 26
    public void PlayFootstep()
    {
        if (mainAI != null)
        {
            mainAI.PlayFootstepSound();
        }
        else
        {
            // Auto-find if you forgot to drag it
            mainAI = GetComponentInParent<PenguinEnemyAI>();
            if(mainAI) mainAI.PlayFootstepSound();
        }
    }
}