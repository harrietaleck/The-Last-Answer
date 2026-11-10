namespace TLA.Core.Events
{
    /// <summary>
    /// Marker interface for anything that can be sent through the <see cref="EventBus"/>.
    ///
    /// Events should be small, immutable structs that describe something that has
    /// ALREADY happened (past tense names: "GameStateChanged", "EvidenceDiscovered").
    /// Using structs avoids garbage collection when events are published often.
    /// </summary>
    public interface IGameEvent
    {
    }
}
