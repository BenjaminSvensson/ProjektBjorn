using UnityEngine;

/// <summary>
/// A contract for any object in the game that can be interacted with
/// by the player (e.g., limbs, chests, doors, bushes).
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// The text to display on the prompt (e.g., "Pick up Limb", "Shake Bush").
    /// </summary>
    string InteractionPromptText { get; }

    /// <summary>
    /// This is the main function called when the player interacts with this object.
    /// </summary>
    /// <param name="player">A reference to the player's limb controller.</param>
    void Interact(PlayerLimbController player);
}