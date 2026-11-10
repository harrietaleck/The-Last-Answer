namespace TLA.Systems.Investigation
{
    /// <summary>
    /// A service whose state belongs to the case being investigated. The GameManager calls
    /// <see cref="BeginCase"/> on every one of them when a new investigation starts, so adding a
    /// case-level system never means editing the GameManager's new-game code.
    /// </summary>
    public interface ICaseScopedService
    {
        void BeginCase();
    }
}
