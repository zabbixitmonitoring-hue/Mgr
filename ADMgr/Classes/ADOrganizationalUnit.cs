using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Xml;

namespace ADMgr.Classes
{
    public class ADOrganizationalUnit : ADContentBase
    {
        private DirectoryEntry group;
        private ADOrganizationalUnit parentOU;
        private List<ADContentBase> _content;
        public List<ADContentBase> content
        {
            get
            {
                if (_content == null)
                {
                    _content = new List<ADContentBase>();
                    try
                    {
                        foreach (DirectoryEntry childEntry in entry.Children)
                        {
                            try
                            {
                                switch (childEntry.SchemaEntry.Name.ToUpper())
                                {
                                    case "COMPUTER":
                                        _content.Add(new ADComputer(childEntry));
                                        break;
                                    case "GROUP":
                                        _content.Add(new ADGroup(childEntry));
                                        break;
                                    case "ORGANIZATIONALUNIT":
                                        // create OU wrapper without preloading its children (lazy)
                                        _content.Add(new ADOrganizationalUnit(childEntry));
                                        break;
                                    case "USER":
                                        _content.Add(new ADUser(childEntry));
                                        break;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                return _content;
            }
            set { _content = value; }
        }
        new public String name
        {
            get
            {
                if (entry != null)
                {
                    // Prefer description, then street, then ou, then fallback to AD name
                    try
                    {
                        if ((entry.Properties["description"].Value != null) && (entry.Properties["description"].Value.ToString() != ""))
                            return entry.Properties["description"].Value.ToString();
                    }
                    catch { }

                    try
                    {
                        if ((entry.Properties["street"].Value != null) && (entry.Properties["street"].Value.ToString() != ""))
                            return entry.Properties["street"].Value.ToString();
                    }
                    catch { }

                    try
                    {
                        if ((entry.Properties["ou"].Value != null) && (entry.Properties["ou"].Value.ToString() != ""))
                            return entry.Properties["ou"].Value.ToString();
                    }
                    catch { }

                    // fallback: AD name without prefix (CN= or OU=)
                    try
                    {
                        return entry.Name.Remove(0, 3);
                    }
                    catch { return ""; }
                }
                else
                    return "";
            }
        }
        public String fullPath
        {
            get
            {
                if (parentOU != null)
                    return parentOU.fullPath + " \\ " + name;
                else
                    return name;
            }
        }

        public ADOrganizationalUnit(DirectoryEntry _entry)
            : base(_entry)
        {
            // avoid recursive parent construction to prevent deep/expensive traversal at startup
            parentOU = null;
        }

        public ADOrganizationalUnit(DirectoryEntry _entry, List<ADContentBase> _content)
            : this(_entry)
        {
            content = _content;
        }

        public ADOrganizationalUnit(ADOrganizationalUnit _parentOU, String _name)
        {
            parentOU = _parentOU;
            try
            {
                entry = _parentOU.entry.Children.Find("OU=" + _name, "organizationalUnit");
            }
            catch (COMException)
            {
                entry = _parentOU.entry.Children.Add("OU=" + _name, "organizationalUnit");
                entry.CommitChanges();
            }

            try
            {
                group = entry.Children.Find("CN=" + _name, "group");
            }
            catch (COMException)
            {
                group = entry.Children.Add("CN=" + _name, "group");
                group.Properties["sAMAccountName"].Value = _name;
                group.Properties["groupType"].Value = -0x7ffffffe;
                group.CommitChanges();
            }
            AssignWithGroup(_parentOU.name, _parentOU.clearLDAP);

            NTFSWork();
        }

        private void AssignWithGroup(String _parentGroupName, String _parentGroupLDAP)
        {
            DirectoryEntry parentGroup = new DirectoryEntry("LDAP://CN=" + _parentGroupName + "," + _parentGroupLDAP);
            if (!parentGroup.Properties["member"].Contains(group.Path.Remove(0, 7)))
            {
                parentGroup.Properties["member"].Add(group.Path.Remove(0, 7));
                parentGroup.CommitChanges();
                parentGroup.Dispose();
            }
        }

        private void NTFSWork()
        {
            if (!Directory.Exists("\\\\srv-fs-01\\D$\\Profiles\\" + name))
                Directory.CreateDirectory("\\\\srv-fs-01\\D$\\Profiles\\" + name);

            DirectorySecurity securityRight = Directory.GetAccessControl("\\\\srv-fs-01\\D$\\Profiles\\" + name);

            securityRight.AddAccessRule(new FileSystemAccessRule(
                Domain.GetCurrentDomain().Name + "\\" + name,
                FileSystemRights.ListDirectory | FileSystemRights.ReadData,
                AccessControlType.Allow));

            Directory.SetAccessControl("\\\\srv-fs-01\\D$\\Profiles\\" + name, securityRight);
        }
    }
}