using System;
using System.DirectoryServices;
using System.Xml;

namespace ADMgr.Classes
{
    public abstract class ADContentBase
    {
        public DirectoryEntry parentEntry;
        public DirectoryEntry entry;
        // cached simple attributes to allow background-thread safe searching without touching DirectoryEntry
        public System.Collections.Generic.Dictionary<string, string> cachedAttributes;
        public byte[] cachedPhoto;
        public String name
        {
            get
            {
                if (entry != null)
                    return entry.Name.Remove(0, 3);
                else
                    return "";
            }
            set { }
        }
        public String LDAP
        {
            get
            {
                if (entry != null)
                    return entry.Path;
                else
                    return "";
            }
        }
        public String clearLDAP
        {
            get
            {
                if (entry != null)
                    return entry.Path.Remove(0, 7);
                else
                    return "";
            }
        }

        public ADContentBase()
        {
            parentEntry = null;
            entry = null;
        }

        public ADContentBase(DirectoryEntry _entry)
        {
            parentEntry = _entry.Parent;
            entry = _entry;
            // populate simple cache of commonly used string attributes and photo bytes
            try
            {
                cachedAttributes = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] attrs = new string[] { "telephoneNumber", "sAMAccountName", "description", "displayName", "cn", "department", "userPrincipalName" };
                foreach (string a in attrs)
                {
                    try
                    {
                        var prop = entry.Properties[a];
                        if (prop != null && prop.Value != null)
                        {
                            if (prop.Value is System.Array)
                            {
                                object[] arr = (object[])prop.Value;
                                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                                for (int i = 0; i < arr.Length; i++)
                                {
                                    if (arr[i] != null)
                                    {
                                        if (sb.Length > 0) sb.Append(' ');
                                        sb.Append(arr[i].ToString());
                                    }
                                }
                                cachedAttributes[a] = sb.ToString();
                            }
                            else
                            {
                                cachedAttributes[a] = prop.Value.ToString();
                            }
                        }
                    }
                    catch { }
                }

                try
                {
                    var pp = entry.Properties["jpegPhoto"];
                    if (pp != null && pp.Value != null && pp.Value is byte[])
                        cachedPhoto = (byte[])pp.Value;
                }
                catch { }
            }
            catch { }
        }

        public void Commit()
        {
            entry.CommitChanges();
        }
    }
}