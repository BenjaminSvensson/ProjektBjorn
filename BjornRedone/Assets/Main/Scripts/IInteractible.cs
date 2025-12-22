using UnityEngine;

public interface IInteractable
{
    // Returns the text to display (e.g. "Pick Up Shotgun")
    string GetInteractionPrompt();

    // The action to perform. 'interactor' is usually the Player GameObject.
    void Interact(GameObject interactor);

    // Used to calculate distance
    Transform transform { get; }
}