using System;
using System.IO;
using UnityEngine;

namespace TLA.Systems.Save
{
    /// <summary>
    /// Stores each key as a JSON file in a folder, by default:
    ///   Application.persistentDataPath/Saves/
    ///   (on Windows: %USERPROFILE%\AppData\LocalLow\&lt;Company&gt;\&lt;Product&gt;\Saves)
    ///
    /// SAFE WRITES: data is written to a temporary file first and then swapped in. If the game
    /// crashes or the PC loses power mid-save, the previous save file is still intact.
    /// The previous version is also kept as "&lt;key&gt;.json.bak".
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private const string Extension = ".json";

        private readonly string _folder;

        public FileSaveStorage(string folder)
        {
            _folder = folder;
        }

        public string Location => _folder;

        public bool Exists(string key)
        {
            return File.Exists(PathFor(key));
        }

        public void Write(string key, string content)
        {
            Directory.CreateDirectory(_folder);

            string path = PathFor(key);
            string tempPath = path + ".tmp";

            // Write and flush to disk before swapping, so a crash can't leave a half-written save in place.
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(path))
                File.Replace(tempPath, path, path + ".bak"); // swap in, keep old file as backup
            else
                File.Move(tempPath, path);
        }

        public bool TryRead(string key, out string content)
        {
            string path = PathFor(key);
            content = null;

            if (!File.Exists(path)) return false;

            try
            {
                content = File.ReadAllText(path);
                return true;
            }
            catch (Exception exception) // e.g. file locked by another program
            {
                Debug.LogError($"[Save] Could not read '{path}': {exception.Message}");
                return false;
            }
        }

        public bool TryReadBackup(string key, out string content)
        {
            string path = PathFor(key) + ".bak";
            content = null;
            if (!File.Exists(path)) return false;
            try
            {
                content = File.ReadAllText(path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save] Could not read backup '{path}': {exception.Message}");
                return false;
            }
        }

        public void Delete(string key)
        {
            string path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }

        private string PathFor(string key)
        {
            // Guard against keys like "../something" escaping the save folder.
            if (string.IsNullOrWhiteSpace(key) || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException($"Invalid save key '{key}'.", nameof(key));

            return Path.Combine(_folder, key + Extension);
        }
    }
}
