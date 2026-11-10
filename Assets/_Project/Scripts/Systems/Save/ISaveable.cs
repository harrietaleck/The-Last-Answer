using Newtonsoft.Json.Linq;

namespace TLA.Systems.Save
{
    /// <summary>
    /// Implement this on any system whose state must survive saving and loading
    /// (later: case progress, discovered evidence, notebook...). Register it with
    /// <see cref="SaveService.Register"/> and the save system includes it automatically.
    ///
    /// Example:
    /// <code>
    ///   public string SaveKey => "investigation";
    ///   public JToken CaptureState() => JToken.FromObject(_state);           // object -> JSON
    ///   public void RestoreState(JToken data) => _state = data.ToObject&lt;InvestigationState&gt;();
    /// </code>
    ///
    /// Save plain data (IDs, numbers, strings, lists), never references to Unity objects.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Unique, stable name of this section in the save file. Never change it after release.</summary>
        string SaveKey { get; }

        JToken CaptureState();

        void RestoreState(JToken state);
    }
}
