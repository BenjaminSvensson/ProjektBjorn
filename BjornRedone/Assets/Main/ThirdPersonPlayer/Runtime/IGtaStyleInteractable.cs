namespace Bjorn.ThirdPerson
{
    public interface IGtaStyleInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(GtaStylePlayerInteractor interactor);
        void Interact(GtaStylePlayerInteractor interactor);
    }
}
