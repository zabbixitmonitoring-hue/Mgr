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
    public class ADComputer : ADContentBase
    {
        public ADComputer(DirectoryEntry _entry)
            : base(_entry)
        {
        }

        public ADComputer(String _name, ADOrganizationalUnit _parentOU)
        {
            name = _name;

            try
            {
                entry = _parentOU.entry.Children.Find("CN=" + _name, "computer");
            }
            catch (COMException)
            {
                entry = _parentOU.entry.Children.Add("CN=" + _name, "computer");
                entry.CommitChanges();
            }
        }
    }
}
