namespace TLA.Gameplay.Interaction
{
    /// <summary>
    /// Anything the player can look at and use with the Interact key (E): evidence, doors,
    /// drawers, phones, light switches...
    ///
    /// The <see cref="PlayerInteractor"/> finds objects implementing this interface by raycasting
    /// from the camera. The object (or one of its parents) also needs a non-trigger Collider.
    ///
    /// Most objects should inherit from <see cref="Interactable"/>, which implements this
    /// interface with a prompt field, an on/off switch, focus callbacks and an Inspector event.
    /// Implement the interface directly only when inheriting isn't possible.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Short verb shown on screen, e.g. "Open", "Inspect", "Pick up". The key name is added automatically.</summary>
        string Prompt { get; }

        /// <summary>When false, the object is ignored: no prompt is shown and E does nothing.</summary>
        bool CanInteract { get; }

        /// <summary>Called when the player presses Interact while looking at this object.</summary>
        void Interact(PlayerInteractor interactor);
    }
}
