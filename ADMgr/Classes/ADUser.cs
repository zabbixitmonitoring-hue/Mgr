using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Windows.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Xml;

namespace ADMgr.Classes
{
    public class ADUser : ADContentBase
    {
        private String ID;              //homeDirectory, sAMAccountName, userPrincipalName, 
        private String surname;         //givenName
        private String firstName;       //sn, initials
        private String patronymic;      //sn, initials
        private String fullName         //cn, displayName, name, distinguishedName
        {
            get
            {
                return surname + " " + firstName[0].ToString() + "." + patronymic[0].ToString() + ".";
            }
        }
        private String post;            //title
        private String departmentNameFull;
        private String departmentNameAbbr;
        private String company;         //company
        private String location;        //physicalDeliveryOffice
        private String phone;           //telephoneNumber
        private String computerName;    //description, userWorkstations

        public ADUser(DirectoryEntry _entry)
            : base(_entry)
        {
        }

        public ADUser(ADOrganizationalUnit _parentOU, String _ID, String _computerName, String _surname, String _firstName, String _patronymic,
                      String _post, String _department, String _company, String _location, String _phone, String _extPhone, String _mobile, String _email, List<ADGroup> _groups,
                      String _additionalInfo = "", String _password = "11111", String _photoFileName = "")
        {
            String fullName = _surname + " " + _firstName + " " + _patronymic;
            try
            {
                parentEntry = _parentOU.entry;
                entry = parentEntry.Children.Add("CN=" + fullName, "user");
                if (parentEntry.Properties["description"].Value != null)
                {
                    String department = parentEntry.Properties["description"].Value.ToString();
                    departmentNameFull = department.Split(',')[0].Trim();
                    if (department.Contains(","))
                        departmentNameAbbr = department.Split(',')[1].Trim();
                    else
                        departmentNameAbbr = departmentNameFull;
                }

                if (_photoFileName != "")
                {
                    FileStream photoFileStream = new FileStream(_photoFileName,
                    FileMode.Open, FileAccess.Read, FileShare.Read);
                    Byte[] photoBuffer = new Byte[photoFileStream.Length];
                    photoFileStream.Read(photoBuffer, 0, (Int32)
                    photoFileStream.Length);

                    entry.Properties["jpegPhoto"].Add(photoBuffer);
                }

                //entry.Properties["homeDrive"].Value = "i:";
                //entry.Properties["homeDirectory"].Value = "\\\\srv-fs-01\\Backup\\" + departmentNameAbbr + "\\" + _ID;
                entry.Properties["sAMAccountName"].Value = _ID;
                entry.Properties["userPrincipalName"].Value = _ID + "@metz.local";

                entry.Properties["givenName"].Value = _surname;

                entry.Properties["initials"].Value = _firstName[0] + "." + _patronymic[0] + ".";

                entry.Properties["sn"].Value = _firstName + " " + _patronymic;

                entry.Properties["cn"].Value = fullName;
                entry.Properties["displayName"].Value = fullName;

                entry.Properties["title"].Value = _post;
                entry.Properties["company"].Value = _company;
                entry.Properties["department"].Value = departmentNameFull;
                entry.Properties["physicalDeliveryOfficeName"].Value = _location;
                entry.Properties["telephoneNumber"].Value = _phone;
                if (_extPhone != "")
                entry.Properties["homePhone"].Value = _extPhone;
                if (_mobile != "")
                entry.Properties["mobile"].Value = _mobile;
                if (_email != "")
                entry.Properties["mail"].Value = _email;
                if (_additionalInfo != "")
                entry.Properties["additionalInformation"].Value = _additionalInfo;

                entry.Properties["description"].Value = _computerName;
                entry.Properties["userWorkstations"].Value = _computerName;
                
                entry.CommitChanges();

                entry.Invoke("SetPassword", new object[] { _password });
                entry.CommitChanges();

                //срок действия пароля не ограничен
                entry.Properties["userAccountControl"].Value = 0x10200;
                entry.CommitChanges();

                //связь с группами безопасности
                String[] groupLDAPs = new String[_groups.Count];
                for (Int32 i = 0; i < _groups.Count; i++)
                {
                    ADGroup group = _groups[i];
                    groupLDAPs[i] = group.clearLDAP;
                    AssignWithGroup(group);
                }
                entry.CommitChanges();

                //защита от случайного удаления
                IdentityReference everyone = new SecurityIdentifier(WellKnownSidType.WorldSid,
                    new SecurityIdentifier((Byte[])Domain.GetCurrentDomain().GetDirectoryEntry().Properties["objectSid"].Value, 0));
                entry.ObjectSecurity.AddAccessRule(new ActiveDirectoryAccessRule(everyone, ActiveDirectoryRights.Delete, AccessControlType.Deny));
                entry.ObjectSecurity.AddAccessRule(new ActiveDirectoryAccessRule(everyone, ActiveDirectoryRights.DeleteTree, AccessControlType.Deny));
                entry.CommitChanges();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }

            //NTFSWork(_parentOU.name);
        }

        private void AssignWithGroup(ADGroup _group)
        {
            DirectoryEntry group = new DirectoryEntry(_group.LDAP);
            group.Properties["member"].Add(entry.Path.Remove(0, 7));
            group.CommitChanges();
            group.Dispose();
        }

        private void NTFSWork(String _groupName)
        {
            if (!Directory.Exists("\\\\srv-fs-01\\Backup\\" + _groupName + "\\" + ID))
                Directory.CreateDirectory("\\\\srv-fs-01\\Backup\\" + _groupName + "\\" + ID);

            DirectorySecurity securityRight = Directory.GetAccessControl("\\\\srv-fs-01\\Backup\\" + _groupName + "\\" + fullName);

            securityRight.AddAccessRule(new FileSystemAccessRule(
                Domain.GetCurrentDomain().Name + "\\" + ID,
                FileSystemRights.ReadAndExecute | FileSystemRights.Write | FileSystemRights.DeleteSubdirectoriesAndFiles,
                AccessControlType.Allow));
            securityRight.AddAccessRule(new FileSystemAccessRule(
                Domain.GetCurrentDomain().Name + "\\" + ID,
                FileSystemRights.Modify | FileSystemRights.DeleteSubdirectoriesAndFiles,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow));

            Directory.SetAccessControl("\\\\srv-fs-01\\Backup\\" + _groupName + "\\" + fullName, securityRight);
        }

        /*private DirectorySecurity CreateNTFSRights()
        {
            DirectorySecurity securityRight = new DirectorySecurity();

            IdentityReference administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,
                new SecurityIdentifier((Byte[])Domain.GetCurrentDomain().GetDirectoryEntry().Properties["objectSid"].Value, 0));

            securityRight.AddAccessRule(new FileSystemAccessRule(
                administrators,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            securityRight.AddAccessRule(new FileSystemAccessRule(
                administrators,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow));

            securityRight.AddAccessRule(new FileSystemAccessRule(
                Domain.GetCurrentDomain().Name + "\\teacher",
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            securityRight.AddAccessRule(new FileSystemAccessRule(
                Domain.GetCurrentDomain().Name + "\\teacher",
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow));

            return securityRight;
        }*/
    }
}