using ADMgr.Classes;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace ADMgr
{
    public partial class App : Application
    {
        public static List<ADContentBase> activeDirectoryContent;
        public static List<ADOrganizationalUnit> ouList;
        public static List<ADGroup> groupsList;
        public static List<ADComputer> computersList;
        public struct PrgTask
        {
            public String taskName { get; set; }
            public String installPath { get; set; }
        }
        public static List<PrgTask> prgTasksList;

        public static List<HashTableItem> surnameHashTable;
        public static List<HashTableItem> idHashTable;
        public static List<HashTableItem> pcNameHashTable;
        public static List<HashTableItem> phoneHashTable;
        public static List<HashTableItem> groupNameHashTable;
        public static List<HashTableItem> computerNameHashTable;

        public static LoadingWnd loading;

        private static FileStream fs;
        private static StreamWriter sw;

        private void Application_Startup(Object sender, StartupEventArgs e)
        {
            // Do not block UI on startup. Initialize empty collections so MainWindow can bind immediately.
            App.activeDirectoryContent = new List<ADContentBase>();
            App.ouList = new List<ADOrganizationalUnit>();
            App.groupsList = new List<ADGroup>();
            App.computersList = new List<ADComputer>();
            App.prgTasksList = new List<PrgTask>();
            App.surnameHashTable = new List<HashTableItem>();
            App.idHashTable = new List<HashTableItem>();
            App.pcNameHashTable = new List<HashTableItem>();
            App.phoneHashTable = new List<HashTableItem>();
            App.groupNameHashTable = new List<HashTableItem>();
            App.computerNameHashTable = new List<HashTableItem>();

            // Restrict application to users who are members of domain group "Handbook"
            try
            {
                bool allowed = false;
                try
                {
                    // find current user entry and check memberOf attribute
                    string domainPath = "LDAP://DC=" + Domain.GetCurrentDomain().Name.Replace(".", ",DC=");
                    using (DirectoryEntry root = new DirectoryEntry(domainPath))
                    using (DirectorySearcher dsUser = new DirectorySearcher(root))
                    {
                        dsUser.Filter = "(&(objectCategory=person)(sAMAccountName=" + Environment.UserName + "))";
                        dsUser.PropertiesToLoad.Add("distinguishedName");
                        dsUser.PropertiesToLoad.Add("memberOf");
                        SearchResult srUser = null;
                        try { srUser = dsUser.FindOne(); } catch { srUser = null; }

                        if (srUser != null)
                        {
                            string userDN = null;
                            try { if (srUser.Properties.Contains("distinguishedName") && srUser.Properties["distinguishedName"].Count > 0) userDN = srUser.Properties["distinguishedName"][0].ToString(); } catch { }

                            // find group DN(s) for group name 'Handbook'
                            using (DirectorySearcher dsGroup = new DirectorySearcher(root))
                            {
                                dsGroup.Filter = "(&(objectCategory=group)(cn=Handbook))";
                                dsGroup.PropertiesToLoad.Add("distinguishedName");
                                dsGroup.PropertiesToLoad.Add("member");
                                SearchResult srGroup = null;
                                try { srGroup = dsGroup.FindOne(); } catch { srGroup = null; }

                                if (srGroup != null)
                                {
                                    string groupDN = null;
                                    try { if (srGroup.Properties.Contains("distinguishedName") && srGroup.Properties["distinguishedName"].Count > 0) groupDN = srGroup.Properties["distinguishedName"][0].ToString(); } catch { }

                                    // check user's memberOf contains group's DN
                                    if (!String.IsNullOrEmpty(userDN) && !String.IsNullOrEmpty(groupDN))
                                    {
                                        try
                                        {
                                            if (srUser.Properties.Contains("memberOf"))
                                            {
                                                foreach (object m in srUser.Properties["memberOf"]) {
                                                    try { if (String.Equals(m.ToString(), groupDN, StringComparison.OrdinalIgnoreCase)) { allowed = true; break; } } catch { }
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { allowed = false; }

                if (!allowed)
                {
                    MessageBox.Show("Приложение доступно только после ввода S/N полученного от разработчика.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка проверки: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        }

        public static Boolean InitializeADConnection()
        {
            try
            {
                Logger.Log("InitializeADConnection: start");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                // Build local collections to avoid mutating UI-bound lists from background threads
                var localComputers = new List<ADComputer>();
                var localGroups = new List<ADGroup>();
                var localActive = new List<ADContentBase>();
                var localPrgTasks = new List<PrgTask>();
                var localSurnameHash = new List<HashTableItem>();
                var localIdHash = new List<HashTableItem>();
                var localPcNameHash = new List<HashTableItem>();
                var localPhoneHash = new List<HashTableItem>();
                var localOuList = new List<ADOrganizationalUnit>();
                var localGroupNameHash = new List<HashTableItem>();
                var localComputerNameHash = new List<HashTableItem>();

                Domain domain = Domain.GetCurrentDomain();
                using (DirectoryEntry root = new DirectoryEntry("LDAP://DC=" + domain.Name.Replace(".", ",DC=")))
                {
                    foreach (DirectoryEntry childEntry in root.Children)
                    {
                        try
                        {
                            string schema = childEntry.SchemaEntry.Name.ToUpper();
                            switch (schema)
                            {
                                case "COMPUTER":
                                    localComputers.Add(new ADComputer(childEntry));
                                    localActive.Add(new ADComputer(childEntry));
                                    break;
                                case "GROUP":
                                    localGroups.Add(new ADGroup(childEntry));
                                    localActive.Add(new ADGroup(childEntry));
                                    break;
                                case "ORGANIZATIONALUNIT":
                                    localActive.Add(new ADOrganizationalUnit(childEntry));
                                    break;
                                case "USER":
                                    localActive.Add(new ADUser(childEntry));
                                    break;
                                default:
                                    break;
                            }
                        }
                        catch { }
                        finally
                        {
                            try { childEntry.Close(); } catch { }
                        }
                    }
                }

                // assign atomically to shared static lists on background thread (UI will rebind after initialization)
                App.computersList = localComputers;
                App.groupsList = localGroups;
                App.activeDirectoryContent = localActive;
                App.prgTasksList = localPrgTasks;
                App.surnameHashTable = localSurnameHash;
                App.idHashTable = localIdHash;
                App.pcNameHashTable = localPcNameHash;
                App.phoneHashTable = localPhoneHash;
                App.ouList = localOuList;
                App.groupNameHashTable = localGroupNameHash;
                App.computerNameHashTable = localComputerNameHash;

                // попытаемся подгрузить список пакетов/задач для удалённой установки, если доступен сетевой путь
                try
                {
                    GetPrgTasksList();
                }
                catch { }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось подключиться к домену: " + ex.ToString());
                return false;
            }
        }

        public static Boolean LoadData()
        {
            Logger.Log("LoadData: start");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            loading = new LoadingWnd("Загрузка данных AD");

            App.computersList = new List<ADComputer>();
            App.groupsList = new List<ADGroup>();

            App.surnameHashTable = new List<HashTableItem>();
            App.idHashTable = new List<HashTableItem>();
            App.pcNameHashTable = new List<HashTableItem>();
            App.phoneHashTable = new List<HashTableItem>();
            App.ouList = new List<ADOrganizationalUnit>();
            App.groupNameHashTable = new List<HashTableItem>();
            App.computerNameHashTable = new List<HashTableItem>();

            //для лога учетных записей без фотографий
            fs = new FileStream("d:\\multipleJpegPhoto.txt", FileMode.Create);
            sw = new StreamWriter(fs);

            //для лога учетных записей без группы metz_users
            //fs = new FileStream("d:\\non-metz_users.txt", FileMode.Create);
            //sw = new StreamWriter(fs);

            Int32 itemsCount = GetADItemsCount();
            loading.pbTotalProgress.Maximum = itemsCount;
            if (LoadADItems(null, out activeDirectoryContent) == false)
                return false;
            SortIndeces();
            GetPrgTasksList();

            stopwatch.Stop();
            Logger.Log($"LoadData: finished, duration={stopwatch.Elapsed.TotalSeconds:0.000}s, items={activeDirectoryContent?.Count ?? 0}");

            return true;
        }

        private static Int32 GetADItemsCount(DirectoryEntry _directoryEntry = null)
        {
            Int32 cnt = 0;

            if (_directoryEntry == null)
                _directoryEntry = new DirectoryEntry("LDAP://DC=" + Domain.GetCurrentDomain().Name.Replace(".", ",DC="));

            foreach (DirectoryEntry childEntry in _directoryEntry.Children)
            {
                switch (childEntry.SchemaEntry.Name.ToUpper())
                {
                    case "COMPUTER":
                        cnt++;
                        break;
                    case "GROUP":
                        cnt++;
                        break;
                    case "ORGANIZATIONALUNIT":
                        cnt += GetADItemsCount(childEntry) + 1;
                        break;
                    case "USER":
                        cnt++;
                        break;
                }
            }
            Logger.Log($"GetADItemsCount: count={cnt}");
            return cnt;
        }

        private static Boolean LoadADItems(DirectoryEntry _directoryEntry, out List<ADContentBase> _content, List<Int32> _trace = null)
        {
            if (_directoryEntry == null)
                _directoryEntry = new DirectoryEntry("LDAP://DC=" + Domain.GetCurrentDomain().Name.Replace(".", ",DC="));

            List<Int32> trace;

            _content = new List<ADContentBase>();

            Int32 ordinal = 0;
            foreach (DirectoryEntry childEntry in _directoryEntry.Children)
            {
                var swItem = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    if (_trace == null)
                        trace = new List<Int32>();
                    else
                        trace = new List<Int32>(_trace);
                    trace.Add(ordinal);

                    switch (childEntry.SchemaEntry.Name.ToUpper())
                    {
                        case "COMPUTER":
                            loading.TotalProgressInc();

                            loading.pbCurrentProgress.Minimum = 0;
                            loading.pbCurrentProgress.Maximum = 5;
                            loading.pbCurrentProgress.Value = 0;

                            if (childEntry.Properties["cn"].Value != null)
                            {
                                MD5CryptoServiceProvider computerNameMD5 = new MD5CryptoServiceProvider();
                                loading.CurrentProgressInc();
                                String computerName = childEntry.Properties["cn"].Value.ToString().Trim().ToUpper();
                                loading.CurrentProgressInc();
                                Byte[] computerNameHash = computerNameMD5.ComputeHash(Encoding.UTF8.GetBytes(computerName));
                                loading.CurrentProgressInc();
                                computerNameHashTable.Add(new HashTableItem(computerNameHash, computerNameHashTable.Count));
                                loading.CurrentProgressInc();
                                App.computersList.Add(new ADComputer(childEntry));
                                loading.CurrentProgressInc();
                            }
                            break;
                        case "GROUP":
                            loading.TotalProgressInc();

                            loading.pbCurrentProgress.Minimum = 0;
                            loading.pbCurrentProgress.Maximum = 5;
                            loading.pbCurrentProgress.Value = 0;

                            if (childEntry.Properties["cn"].Value != null)
                            {
                                MD5CryptoServiceProvider groupNameMD5 = new MD5CryptoServiceProvider();
                                loading.CurrentProgressInc();
                                String groupName = childEntry.Properties["cn"].Value.ToString().Trim().ToUpper();
                                loading.CurrentProgressInc();
                                Byte[] groupNameHash = groupNameMD5.ComputeHash(Encoding.UTF8.GetBytes(groupName));
                                loading.CurrentProgressInc();
                                groupNameHashTable.Add(new HashTableItem(groupNameHash, groupNameHashTable.Count));
                                loading.CurrentProgressInc();
                                App.groupsList.Add(new ADGroup(childEntry));
                                loading.CurrentProgressInc();
                            }
                            break;
                        case "ORGANIZATIONALUNIT":
                            loading.TotalProgressInc();

                            List<ADContentBase> childContent;
                            if (LoadADItems(childEntry, out childContent, trace) == false)
                                return false;

                            App.ouList.Add(new ADOrganizationalUnit(childEntry));
                            _content.Add(new ADOrganizationalUnit(childEntry, childContent));
                            ordinal++;

                            break;
                        case "USER":
                            loading.TotalProgressInc();

                            _content.Add(new ADUser(childEntry));

                            loading.pbCurrentProgress.Minimum = 0;
                            loading.pbCurrentProgress.Maximum = 4;
                            loading.pbCurrentProgress.Value = 0;

                            try
                            {
                                loading.CurrentProgressInc();
                                if (childEntry.Properties["cn"].Value != null)
                                {
                                    MD5CryptoServiceProvider surnameMD5 = new MD5CryptoServiceProvider();
                                    String surname = childEntry.Properties["cn"].Value.ToString().Trim().Split(' ')[0].ToUpper().Replace("Ё", "Е");
                                    Byte[] surnameHash = surnameMD5.ComputeHash(Encoding.UTF8.GetBytes(surname));
                                    surnameHashTable.Add(new HashTableItem(surnameHash, trace));
                                }

                                loading.CurrentProgressInc();
                                if (childEntry.Properties["sAMAccountName"].Value != null)
                                {
                                    MD5CryptoServiceProvider ID_MD5 = new MD5CryptoServiceProvider();
                                    String ID = childEntry.Properties["sAMAccountName"].Value.ToString().Trim().ToUpper();
                                    Byte[] ID_Hash = ID_MD5.ComputeHash(Encoding.UTF8.GetBytes(ID));
                                    idHashTable.Add(new HashTableItem(ID_Hash, trace));
                                }

                                loading.CurrentProgressInc();
                                if (childEntry.Properties["street"].Value != null)
                                {
                                    MD5CryptoServiceProvider pcNameMD5 = new MD5CryptoServiceProvider();
                                    String pcNamesStr = childEntry.Properties["street"].Value.ToString().Replace("(", " ").Replace(")", " ").Replace(","," ").Trim().ToUpper();
                                    foreach (String pcName in pcNamesStr.Split(' '))
                                    {
                                        Byte[] pcNameHash = pcNameMD5.ComputeHash(Encoding.UTF8.GetBytes(pcName));
                                        pcNameHashTable.Add(new HashTableItem(pcNameHash, trace));
                                    }
                                }

                                loading.CurrentProgressInc();
                                if (childEntry.Properties["telephoneNumber"].Value != null)
                                {
                                    MD5CryptoServiceProvider phoneMD5 = new MD5CryptoServiceProvider();
                                    String phonesStr = childEntry.Properties["telephoneNumber"].Value.ToString().Trim().Replace(" ", "").Replace("-", "");
                                    foreach (String phone in phonesStr.Split(','))
                                    {
                                        Byte[] phoneHash = phoneMD5.ComputeHash(Encoding.UTF8.GetBytes(phone));
                                        phoneHashTable.Add(new HashTableItem(phoneHash, trace));
                                    }
                                }

                                //проверка заполненности фотографий
                                /*loading.CurrentProgressInc();
                                if ((childEntry.Properties["jpegPhoto"].Value != null) && (childEntry.Properties["jpegPhoto"].Value is Byte[]))
                                {
                                sw.WriteLine(childEntry.Properties["cn"].Value.ToString());
                                sw.Flush();
                                }*/

                                //проверка на наличие группы metz_users
                                /*if (childEntry.Properties["memberOf"].Value != null)
                                {
                                    Boolean isFound = false;
                                    if (childEntry.Properties["memberOf"].Value is Object[])
                                    {
                                        foreach (Object group in (Object[])childEntry.Properties["memberOf"].Value)
                                        {
                                            if (group.ToString() == "CN=metz_users,OU=GROUPS,DC=metz,DC=local")
                                            {
                                                isFound = true;
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (childEntry.Properties["memberOf"].Value.ToString() == "CN=metz_users,OU=GROUPS,DC=metz,DC=local")
                                            isFound = true;
                                    }

                                    if (!isFound)
                                    {
                                        sw.WriteLine(childEntry.Properties["cn"].Value.ToString());
                                        sw.Flush();
                                    }
                                }*/
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                            }

                            ordinal++;

                            break;
                    }
                    childEntry.Close();

                    swItem.Stop();
                    Logger.Log($"LoadADItems: processed {ordinal} type={childEntry.SchemaEntry.Name} duration={swItem.Elapsed.TotalMilliseconds:0}ms");

                    if (loading.IsCancel == true)
                        return false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }

            return true;
            //sw.Close();
            //fs.Close();
        }

        private static void SortIndeces()
        {
            Comparison<HashTableItem> hashCmp = new Comparison<HashTableItem>(HashTableItemsComparer);
            surnameHashTable.Sort(hashCmp);
            idHashTable.Sort(hashCmp);
            pcNameHashTable.Sort(hashCmp);
            phoneHashTable.Sort(hashCmp);
            groupNameHashTable.Sort(hashCmp);
            computerNameHashTable.Sort(hashCmp);
        }

        private static void GetPrgTasksList()
        {
            prgTasksList = new List<PrgTask>();

            DirectoryInfo prgTasksDir = new DirectoryInfo("\\\\fs\\APLIC\\Etalon.prg\\Programs");
            foreach (DirectoryInfo prgTaskDir in prgTasksDir.GetDirectories())
            {
                if (prgTaskDir.GetDirectories().Length > 0)
                {
                    DirectoryInfo devDateFolder = prgTaskDir.GetDirectories()[0];
                    if (Directory.Exists(devDateFolder.FullName + "\\Install"))
                    {
                        DirectoryInfo installFolder = new DirectoryInfo(devDateFolder.FullName + "\\Install");
                        if (installFolder.GetFiles("*.msi").Length == 1)
                        {
                            PrgTask prgTask = new PrgTask();
                            prgTask.taskName = prgTaskDir.Name;

                            String msiFilePath = installFolder.GetFiles("*.msi")[0].FullName;
                            prgTask.installPath = msiFilePath;

                            prgTasksList.Add(prgTask);
                        }
                    }
                }
            }
            try
            { }
                    catch (Exception)
            {
                new Message("Ошибка запуска, не подключён диск T.");
            }
        }

        public static int HashTableItemsComparer(HashTableItem h1, HashTableItem h2)
        {
            for (Int32 i = 0; i < 16; i++)
            {
                if (h1.hash[i] > h2.hash[i])
                    return 1;
                else if (h1.hash[i] < h2.hash[i])
                    return -1;
                else if (h1.hash[i] == h2.hash[i])
                {
                    Int32 minTraceLength = (h1.trace.Count < h2.trace.Count) ? h1.trace.Count : h2.trace.Count;
                    for (Int32 j = 0; j < minTraceLength; j++)
                    {
                        if (h1.trace[j] > h2.trace[j])
                            return 1;
                        else if (h1.trace[j] < h2.trace[j])
                            return -1;
                    }
                }
            }
            return 0;
        }

        #region Window template handlers

        private void bnMinimize_Click(Object sender, RoutedEventArgs e)
        {
            ((Window)((Button)sender).TemplatedParent).WindowState = WindowState.Minimized;
        }

        private void bnClose_Click(Object sender, RoutedEventArgs e)
        {
            ((Window)((Button)sender).TemplatedParent).Close();
        }

        private void wndDialogWindowHeader_MouseLeftButtonDown(Object sender, MouseButtonEventArgs e)
        {
            ((Window)((Border)sender).TemplatedParent).DragMove();
        }

        #endregion Window template handlers

        #region Static methods for proper window maximization

        public static void Window_SourceInitialized(Object sender, EventArgs e)
        {
            IntPtr hwnd = (new WindowInteropHelper((Window)sender)).Handle;
            HwndSource.FromHwnd(hwnd).AddHook(new HwndSourceHook(WindowProc));
        }

        private static IntPtr WindowProc(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam, ref Boolean handled)
        {
            switch (msg)
            {
                case 0x0024:
                    MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

                    Int32 MONITOR_DEFAULTTONEAREST = 0x00000002;
                    IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

                    if (monitor != IntPtr.Zero)
                    {
                        MONITORINFO monitorInfo = new MONITORINFO();
                        GetMonitorInfo(monitor, monitorInfo);
                        RECT rcWorkArea = monitorInfo.rcWork;
                        RECT rcMonitorArea = monitorInfo.rcMonitor;
                        mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                        mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                        mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                        mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
                    }

                    Marshal.StructureToPtr(mmi, lParam, true);

                    handled = true;
                    break;
            }

            return IntPtr.Zero;
        }

        #region WinProc requires

        [DllImport("user32")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [DllImport("User32")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, Int32 flags);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public Int32 x;
            public Int32 y;

            public POINT(Int32 _x, Int32 _y)
            {
                x = _x;
                y = _y;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct RECT
        {
            public Int32 left;
            public Int32 top;
            public Int32 right;
            public Int32 bottom;

            public RECT(Int32 _left, Int32 _top, Int32 _right, Int32 _bottom)
            {
                left = _left;
                top = _top;
                right = _right;
                bottom = _bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        };

        [StructLayout(LayoutKind.Sequential)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        #endregion WinProc requires

        #endregion Static methods for proper window maximization

        #region Misc handlers for styles and templates

        private void textBox_GotFocus(Object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private void passwordBox_GotFocus(Object sender, RoutedEventArgs e)
        {
            ((PasswordBox)sender).SelectAll();
        }

        private void treeViewItem_MouseRightButtonDown(Object sender, System.Windows.Input.MouseEventArgs e)
        {
            TreeViewItem item = sender as TreeViewItem;
            if (item != null)
            {
                item.Focus();
                e.Handled = true;
            }
        }

        #endregion Misc handlers for styles and templates
    }
}
