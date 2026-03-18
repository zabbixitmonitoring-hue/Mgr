using System.Collections.Generic;

namespace ADMgr
{
    public class SearchResultInfo
    {
        public string DisplayName { get; set; }
        public string EntryPath { get; set; }
        public List<Classes.ADContentBase> PathInTree { get; set; }
        public bool IsInTree { get { return PathInTree != null && PathInTree.Count > 0; } }

        public string Department { get; set; }
        public string TelephoneNumber { get; set; }
        public string UserPrincipalName { get; set; }
        public string Description { get; set; }
        public byte[] PhotoBytes { get; set; }
    }
}
