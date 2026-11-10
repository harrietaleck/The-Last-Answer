using System.Collections.Generic;
using System.IO;
using TLA.Data.Cases;
using TLA.Systems.Cases;
using UnityEditor;
using UnityEngine;

namespace TLA.EditorTools.UI
{
    /// <summary>
    /// Menu tools for writing cases (see CASE_AUTHORING_GUIDE.md):
    ///   The Last Answer → Cases → New Case            a new CaseDefinition with its folders, added to the catalog (locked until ready)
    ///   The Last Answer → Cases → Validate All Cases  checks every case in the catalog with CaseValidator
    /// </summary>
    internal static class CaseAuthoringTools
    {
        [MenuItem("The Last Answer/Cases/New Case", priority = 40)]
        private static void NewCase()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CaseCatalog>(CaseDataBuilder.CatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("New Case", "No CaseCatalog found. Run The Last Answer → Environment → Rebuild All Locations first.", "OK");
                return;
            }

            int number = 1000;
            while (AssetDatabase.IsValidFolder($"{CaseDataBuilder.Folder}/Case{number}")) number++;
            string folder = $"{CaseDataBuilder.Folder}/Case{number}";
            foreach (string sub in new[] { "", "/Evidence", "/Facts", "/Statements", "/Suspects" })
                Directory.CreateDirectory(folder + sub);
            AssetDatabase.Refresh();

            var c = ScriptableObject.CreateInstance<CaseDefinition>();
            c.caseId = $"CASE_{number}";
            c.caseNumber = number.ToString();
            c.title = "Untitled Case";
            c.isAvailable = false; // stays "coming soon" in Case Files until you tick Is Available
            c.startScene = "PoliceStation";
            string path = $"{folder}/CASE_{number}.asset";
            AssetDatabase.CreateAsset(c, path);
            catalog.cases.Add(c);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            Debug.Log($"[Cases] Created {path}. Fill it in, put its evidence/facts/statements/suspects in the sub-folders, then run Validate All Cases.");
        }

        [MenuItem("The Last Answer/Cases/Validate All Cases", priority = 41)]
        private static void ValidateAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CaseCatalog>(CaseDataBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Cases] No CaseCatalog found.");
                return;
            }

            var problems = new List<string>();
            foreach (CaseDefinition c in catalog.cases) problems.AddRange(CaseValidator.Validate(c));
            if (problems.Count == 0)
                Debug.Log($"[Cases] All {catalog.cases.Count} cases are valid.");
            else
                foreach (string p in problems) Debug.LogError("[Cases] " + p);
        }
    }
}
