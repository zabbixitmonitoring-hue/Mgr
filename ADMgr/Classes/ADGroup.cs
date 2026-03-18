using System;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Xml;

namespace ADMgr.Classes
{
    public class ADGroup : ADContentBase
    {
        public ADGroup(DirectoryEntry _entry)
            : base(_entry)
        {
        }

        public ADGroup(String _name, ADOrganizationalUnit _parentOU)
        {
            name = _name;

            try
            {
                entry = _parentOU.entry.Children.Find("CN=" + _name, "group");
            }
            catch (COMException)
            {
                entry = _parentOU.entry.Children.Add("CN=" + _name, "group");
                entry.CommitChanges();
            }
            AssignWithGroup(_parentOU.name, _parentOU.clearLDAP);

            NTFSWork(_parentOU.name);
        }

        private void AssignWithGroup(String _groupName, String _groupLDAP)
        {
            DirectoryEntry group = new DirectoryEntry("LDAP://CN=" + _groupName + "," + _groupLDAP);
            group.Properties["member"].Add(entry.Path.Remove(0, 7));
            group.CommitChanges();
            group.Dispose();
        }

        private void NTFSWork(String _groupName)
        {
            if (!Directory.Exists("\\\\srv-fs-01\\Backup\\" + _groupName))
                Directory.CreateDirectory("\\\\srv-fs-01\\Backup\\" + _groupName);

            DirectorySecurity securityRight = Directory.GetAccessControl("\\\\srv-fs-01\\Backup\\" + _groupName);

            securityRight.AddAccessRule(new FileSystemAccessRule(
                Domain.GetCurrentDomain().Name,
                FileSystemRights.ReadAndExecute | FileSystemRights.Write | FileSystemRights.DeleteSubdirectoriesAndFiles,
                AccessControlType.Allow));
            securityRight.AddAccessRule(new FileSystemAccessRule(
                Domain.GetCurrentDomain().Name,
                FileSystemRights.Modify | FileSystemRights.DeleteSubdirectoriesAndFiles,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow));

            Directory.SetAccessControl("\\\\srv-fs-01\\Backup\\" + _groupName, securityRight);
        }
    }
}
