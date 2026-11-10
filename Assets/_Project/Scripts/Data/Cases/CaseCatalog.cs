using System.Collections.Generic;
using UnityEngine;

namespace TLA.Data.Cases
{
    /// <summary>
    /// The list of every case in the game, in the order shown in Case Files.
    /// Referenced by GameConfig, so systems and menus can find cases (and save files can look
    /// a case up by its id).
    /// </summary>
    [CreateAssetMenu(fileName = "CaseCatalog", menuName = "The Last Answer/Cases/Case Catalog", order = 22)]
    public sealed class CaseCatalog : ScriptableObject
    {
        public List<CaseDefinition> cases = new List<CaseDefinition>();

        /// <summary>The first available case (used by "New Investigation").</summary>
        public CaseDefinition DefaultCase
        {
            get
            {
                foreach (CaseDefinition c in cases)
                    if (c != null && c.isAvailable) return c;
                return null;
            }
        }

        public CaseDefinition Find(string caseId)
        {
            foreach (CaseDefinition c in cases)
                if (c != null && c.caseId == caseId) return c;
            return null;
        }
    }
}
