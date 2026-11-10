using UnityEngine;
using UnityEngine.Events;

namespace TLA.Gameplay.Interaction
{
    /// <summary>
    /// Base class for interactive objects. Two ways to use it:
    ///
    ///  1. NO CODE: add this component to an object with a collider, type a Prompt, and hook up
    ///     "On Interacted" in the Inspector (e.g. play a sound, enable a light).
    ///  2. CODE: create a subclass (see DoorInteractable) and override <see cref="OnInteract"/>,
    ///     and optionally <see cref="Prompt"/>, <see cref="CanInteract"/>, OnFocusGained/Lost.
    ///
    /// Gameplay rules (e.g. "this evidence is now discovered") belong in the game systems. A
    /// subclass should call those systems, not store the rules itself.
    /// </summary>
    public class Interactable : MonoBehaviour, IInteractable
    {
        [Tooltip("Verb shown on screen, e.g. \"Inspect\". Becomes \"[E] Inspect\".")]
        [SerializeField] private string prompt = "Interact";

        [Tooltip("Untick to temporarily disable interaction (no prompt, E does nothing).")]
        [SerializeField] private bool interactionEnabled = true;

        [Tooltip("Inspector hook, called after every successful interaction.")]
        [SerializeField] private UnityEvent onInteracted = new UnityEvent();

        public virtual string Prompt => prompt;

        /// <summary>Sound played after a successful interaction. Override (e.g. doors play their own; None = silent).</summary>
        protected virtual TLA.Data.Audio.SoundCue InteractionSound => TLA.Data.Audio.SoundCue.Interact;

        public virtual bool CanInteract => interactionEnabled && isActiveAndEnabled;

        public UnityEvent OnInteracted => onInteracted;

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract) return;

            OnInteract(interactor);
            PlaySound(InteractionSound, transform.position);
            onInteracted.Invoke();
        }

        public void SetInteractionEnabled(bool value)
        {
            interactionEnabled = value;
        }

        /// <summary>Plays a sound effect through the AudioManager (does nothing if the game systems aren't running).</summary>
        protected static void PlaySound(TLA.Data.Audio.SoundCue cue, Vector3? position = null)
        {
            if (cue != TLA.Data.Audio.SoundCue.None && TLA.Core.Services.Services.TryGet(out TLA.Systems.Audio.AudioManager audio))
                audio.Play(cue, position);
        }

        /// <summary>Change the prompt at runtime, e.g. "Open" -> "Close".</summary>
        protected void SetPrompt(string value)
        {
            prompt = value;
        }

        /// <summary>Override to do something when used. The base version does nothing.</summary>
        protected virtual void OnInteract(PlayerInteractor interactor)
        {
        }

        /// <summary>Called when the player starts looking at this object (e.g. to highlight it).</summary>
        public virtual void OnFocusGained()
        {
        }

        /// <summary>Called when the player looks away or moves out of range.</summary>
        public virtual void OnFocusLost()
        {
        }
    }
}
