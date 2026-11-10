namespace TLA.Systems.Save
{
    /// <summary>
    /// Where save data physically lives. The save system only talks to this interface, so the
    /// storage can be swapped without changing any game code: files on disk today, possibly
    /// Steam Cloud later, and an in-memory fake in unit tests.
    ///
    /// A "key" is a simple name such as "save_0" or "settings" (no folders, no extension).
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>Human-readable location, shown in the debug overlay (e.g. the save folder path).</summary>
        string Location { get; }

        bool Exists(string key);

        void Write(string key, string content);

        bool TryRead(string key, out string content);

        void Delete(string key);
    }
}
