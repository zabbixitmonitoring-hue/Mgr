using ADMgr.Classes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Media;

namespace ADMgr
{

    public class Person
    {
        public string Name { get; set; }
        public string Surname { get; set; }
    }



    public partial class MainWindow : Window
    {

        private const int SearchTopN = 10;

        

        List<Person> SouthPark = new List<Person>() {
            new Person() { Name = "Eric", Surname="Cartman" },
            new Person() { Name = "Stan", Surname="Marsh" },
            new Person() { Name = "Kyle", Surname="Broflovski" },
            new Person() { Name = "Kenny", Surname="McCormick" },
            new Person() { Name = "Bebe", Surname="Stevens" },
            new Person() { Name = "Clyde", Surname="Donovan" },
            new Person() { Name = "Craig", Surname="Tucker" },
            new Person() { Name = "Jimmy", Surname="Vulmer" },
            new Person() { Name = "Pip", Surname="Pirrup" },
            new Person() { Name = "Token", Surname="Black" },
            new Person() { Name = "Tweek", Surname="Tweak" },
            new Person() { Name = "Wendy", Surname="Testaburger" },
            new Person() { Name = "Annie", Surname="Polk" },
            new Person() { Name = "Randy", Surname="Marsh" },
            new Person() { Name = "Sharon", Surname="Marsh" },
            new Person() { Name = "Shelley", Surname="Marsh" },
            new Person() { Name = "Marvin", Surname="Marsh" },
            new Person() { Name = "Jimbo", Surname="Kern" },
            new Person() { Name = "Gerald", Surname="Broflovski" },
            new Person() { Name = "Sheila", Surname="Broflovski" },
            new Person() { Name = "Ike", Surname="Broflovski" },
            new Person() { Name = "Kyle", Surname="Schwartz" },
            new Person() { Name = "Liane", Surname="Cartman" },
            new Person() { Name = "Stuart", Surname="McCormick" },
            new Person() { Name = "Carol", Surname="McCormick" },
            new Person() { Name = "Kevin", Surname="McCormick" },
            new Person() { Name = "Stephen", Surname="Stotch" },
            new Person() { Name = "Linda", Surname="Stotch" },
            new Person() { Name = "Richard", Surname="Tweak" }
        };

        [DllImport("ws2_32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        internal static extern IntPtr gethostbyname([In] string host);

        private ADOrganizationalUnit parentOU = null;
        private ServiceController ra_svc = null;
        private ManagementScope mgmtScope;
        private Int32 searchStartPos;
        private Byte[] activeSearchHash;
        // attributes to search in users for unified search (cached to avoid reallocation)
        private static readonly string[] userSearchAttributes = new string[] { "telephoneNumber", "sAMAccountName", "description", "displayName", "cn" };

        private struct HeaderedInfoSet
        {
            public String name { get; set; }
            public ObservableCollection<KeyValuePair<String, String>> properties { get; set; }
        }
        private ObservableCollection<KeyValuePair<String, String>> systemDetailsList;
        private ObservableCollection<KeyValuePair<String, String>> motherboardDetailsList;
        private ObservableCollection<KeyValuePair<String, String>> cpuDetailsList;
        private ObservableCollection<HeaderedInfoSet> videoCardDetailsList;
        private ObservableCollection<KeyValuePair<String, String>> soundDetailsList;
        private ObservableCollection<HeaderedInfoSet> ramDetailsList;
        private ObservableCollection<HeaderedInfoSet> hddDetailsList;
        private ObservableCollection<KeyValuePair<String, String>> cdromDetailsList;
        private class Volume
        {
            public Char letter { get; set; }
            public String label { get; set; }
            public String fileSystem { get; set; }
            public Int64 capacity { get; set; } // GB
            public Int64 free { get; set; } // GB

            public String DriveLetter { get { return letter.ToString() + ":"; } }
            public String DriveName { get { return label; } }
            public double TotalSizeGB { get { return (double)capacity; } }
            public double FreeSpaceGB { get { return (double)free; } }
            public double UsedSpaceGB { get { return Math.Max(0.0, TotalSizeGB - FreeSpaceGB); } }
            public double UsedPercentage { get { return TotalSizeGB > 0.0 ? (UsedSpaceGB * 100.0 / TotalSizeGB) : 0.0; } }
            public Brush StatusColor { get; set; }
            public SeriesCollection ChartSeries { get; set; }
            public String FileSystem { get { return fileSystem; } }
        }
        private ObservableCollection<Volume> volumesList;
        private ObservableCollection<String> filesList;

        private class RemotePrgTask : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public App.PrgTask prgTask { get; set; }
            private Boolean _isInstalled;
            public Boolean isInstalled
            {
                get { return _isInstalled; }
                set { _isInstalled = value; OnPropertyChanged("isInstalled"); }
            }

            public virtual void OnPropertyChanged(String _propName)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(_propName));
            }
        }
        private ObservableCollection<RemotePrgTask> remotePrgTasksList;
        private ObservableCollection<HeaderedInfoSet> printersList;
        private struct RemoteProcess
        {
            public Int32 id { get; set; }
            public String caption { get; set; }
            public String creationDate { get; set; }
            public String executablePath { get; set; }
            public String commandLine { get; set; }
        }
        private ObservableCollection<RemoteProcess> processesList;
        private ObservableCollection<String> bsodList;

        private delegate void PCMgmtDelegate(String _pcName);

        // incremental tree population to avoid UI freeze
        private DispatcherTimer populateTimer = null;
        private int populateIndex = 0;
        private List<ADContentBase> populateSource = null;
        private const int PopulateBatchSize = 10;
        private const int PopulateIntervalMs = 100;

        public MainWindow()
        {
            InitializeComponent();

            //if (App.loading.IsCancel == false)
            //    App.loading.Close();
            //else
            //{
            //    Loaded += new RoutedEventHandler((_s, _e) => { Close(); });
            //    return;
            //}

            SourceInitialized += App.Window_SourceInitialized;
            tbSearchAll.Focus();

            // handle ESC to clear search and collapse tree
            this.PreviewKeyDown += (s, e) =>
            {
                try
                {
                    if (e.Key == System.Windows.Input.Key.Escape)
                    {
                        ClearSearchAndCollapse();
                        e.Handled = true;
                    }
                }
                catch { }
            };

            Show();
        }

        private void Window_Loaded(Object sender, RoutedEventArgs e)
        {
            // show UI immediately; start AD connection in background and show FindLoading
            Title = "Подключение...";

            // bind to placeholders so UI doesn't access App lists while they are populated in background
            tvUsers.ItemsSource = new System.Collections.Generic.List<ADContentBase>();
            lbGroups.ItemsSource = new System.Collections.Generic.List<ADGroup>();
            lbComputers.ItemsSource = new System.Collections.Generic.List<ADComputer>();

            // disable search controls until AD connection completes
            try { tbSearchAll.IsEnabled = false; } catch { }
            try { bnSearchAll.IsEnabled = false; } catch { }

            // show loading indicator near search
            try { FindLoading.Visibility = Visibility.Visible; } catch { }

            Logger.Log("Window_Loaded: starting InitializeADConnection thread");
            // run AD initialization on a dedicated STA thread to avoid COM marshalling issues
            Thread initThread = new Thread(() =>
            {
                var swInit = System.Diagnostics.Stopwatch.StartNew();
                Logger.Log("InitializeADConnection thread: start");
                bool ok = App.InitializeADConnection();
                swInit.Stop();
                Logger.Log($"InitializeADConnection thread: finished, duration={swInit.Elapsed.TotalSeconds:0.000}s");

                Dispatcher.BeginInvoke((Action)(() =>
                {
                    Logger.Log("Dispatcher: processing InitializeADConnection completion");
                    if (ok)
                    {
                        Logger.Log("Dispatcher: scheduling AssignDataToUI at Background priority");
                        // schedule UI assignment at Background priority so initial rendering and animations aren't blocked
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => AssignDataToUI(ok)));
                    }
                    else
                    {
                        MessageBox.Show("Не удалось подключиться к домену. Приложение будет закрыто.");
                        Close();
                    }
                }));
            }) { IsBackground = true };
            try { initThread.SetApartmentState(ApartmentState.STA); } catch { }
            initThread.Start();

            systemDetailsList = new ObservableCollection<KeyValuePair<String, String>>();
            lbSystemDetails.ItemsSource = systemDetailsList;

            motherboardDetailsList = new ObservableCollection<KeyValuePair<String, String>>();
            lbMotherboard.ItemsSource = motherboardDetailsList;

            cpuDetailsList = new ObservableCollection<KeyValuePair<String, String>>();
            lbCPU.ItemsSource = cpuDetailsList;

            videoCardDetailsList = new ObservableCollection<HeaderedInfoSet>();
            tcVideoCard.ItemsSource = videoCardDetailsList;

            soundDetailsList = new ObservableCollection<KeyValuePair<String, String>>();
            lbSoundCard.ItemsSource = soundDetailsList;

            ramDetailsList = new ObservableCollection<HeaderedInfoSet>();
            tcRAM.ItemsSource = ramDetailsList;

            hddDetailsList = new ObservableCollection<HeaderedInfoSet>();
            tcHDD.ItemsSource = hddDetailsList;

            cdromDetailsList = new ObservableCollection<KeyValuePair<String, String>>();
            lbCDROM.ItemsSource = cdromDetailsList;

            volumesList = new ObservableCollection<Volume>();
            tcVolumes.ItemsSource = volumesList;

            filesList = new ObservableCollection<String>();
            lbFiles.ItemsSource = filesList;

            printersList = new ObservableCollection<HeaderedInfoSet>();
            tcPrinters.ItemsSource = printersList;

            processesList = new ObservableCollection<RemoteProcess>();
            lvProcesses.ItemsSource = processesList;

            remotePrgTasksList = new ObservableCollection<RemotePrgTask>();
            lbPrgTasks.ItemsSource = remotePrgTasksList;

            bsodList = new ObservableCollection<String>();
            lbBSOD.ItemsSource = bsodList;

            tcOveralData.SelectedIndex = 0;


        }

        // assign populated App.* lists to UI controls on UI thread (runs at Background priority)
        private void AssignDataToUI(bool ok)
        {
            Logger.Log("AssignDataToUI: start");
            try
            {
                // Do not hide FindLoading or enable search controls here.
                // Keep indicator visible until tree population finishes.
                try { Title = "" + Domain.GetCurrentDomain().Name; } catch { }

                Logger.Log("AssignDataToUI: building TreeNodeViewModel roots");
                try
                {
                    var roots = new System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel>();
                    if (App.activeDirectoryContent != null)
                    {
                        foreach (var item in App.activeDirectoryContent)
                            roots.Add(new TreeNodeViewModel(item));
                    }
                    tvUsers.ItemsSource = roots;
                }
                catch (Exception ex) { Logger.LogException(ex, "AssignDataToUI BuildRoots"); }

                Logger.Log("AssignDataToUI: assigning ItemsSource for lbGroups");
                try { lbGroups.ItemsSource = null; lbGroups.ItemsSource = App.groupsList; } catch (Exception ex) { Logger.LogException(ex, "Assign lbGroups"); }

                Logger.Log("AssignDataToUI: assigning ItemsSource for lbComputers");
                try { lbComputers.ItemsSource = null; lbComputers.ItemsSource = App.computersList; } catch (Exception ex) { Logger.LogException(ex, "Assign lbComputers"); }

                Logger.Log("AssignDataToUI: finished");

                // hide loading and enable search now that roots are assigned
                try { FindLoading.Visibility = Visibility.Collapsed; } catch { }
                try { tbSearchAll.IsEnabled = true; } catch { }
                try { bnSearchAll.IsEnabled = true; } catch { }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "AssignDataToUI");
            }
        }
        
        private void StartPopulateTreeInBatches(List<ADContentBase> items)
        {
            try
            {
                if (populateTimer != null)
                {
                    populateTimer.Stop();
                    populateTimer = null;
                }

                populateSource = items ?? new List<ADContentBase>();
                populateIndex = 0;
                tvUsers.Items.Clear();

                Logger.Log($"StartPopulateTreeInBatches: total={populateSource.Count}");

                populateTimer = new DispatcherTimer();
                populateTimer.Interval = TimeSpan.FromMilliseconds(PopulateIntervalMs);
                populateTimer.Tick += (s, e) =>
                {
                    // Stop timer while processing to avoid reentrancy
                    try { populateTimer.Stop(); } catch { }

                    // Schedule actual add at Background priority so rendering/animations have precedence
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        int added = 0;
                        while (populateIndex < populateSource.Count && added < PopulateBatchSize)
                        {
                            try
                            {
                                tvUsers.Items.Add(populateSource[populateIndex]);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogException(ex, "Populate add item");
                            }
                            populateIndex++;
                            added++;
                        }

                        Logger.Log($"PopulateTick: added={added}, index={populateIndex}/{populateSource.Count}");

                        if (populateIndex >= populateSource.Count)
                        {
                            try { populateTimer.Stop(); } catch { }
                            populateTimer = null;
                            populateSource = null;
                            Logger.Log("StartPopulateTreeInBatches: finished populating tvUsers");

                            // now that tree is fully populated, hide loading indicator and enable search controls
                            try { FindLoading.Visibility = Visibility.Collapsed; } catch { }
                            try { tbSearchAll.IsEnabled = true; } catch { }
                            try { bnSearchAll.IsEnabled = true; } catch { }
                            Logger.Log("StartPopulateTreeInBatches: hid FindLoading and enabled search controls");
                        }
                        else
                        {
                            // resume timer for next batch
                            try { populateTimer.Start(); } catch { }
                        }
                    }));
                };

                populateTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "StartPopulateTreeInBatches");
            }
        }


        private void themes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string theme = e.AddedItems[0].ToString();

                // Window Level
                // this.ApplyTheme(theme);

                // Application Level
                // Application.Current.ApplyTheme(theme);
            }
        }

        private DispatcherTimer dispatcherTimer = null;

        private void tvUsers_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (tvUsers.SelectedItem != null)
            {
                if (dispatcherTimer != null)
                {
                    dispatcherTimer.Stop();
                    dispatcherTimer = null;
                }

                // ensure details panel is visible when an item is selected
                try { dpFull.Visibility = Visibility.Visible; } catch { }

                ResetInfo();

                ADContentBase selectedData = null;
                if (tvUsers.SelectedItem is TreeNodeViewModel tnv)
                    selectedData = tnv.Data;
                else if (tvUsers.SelectedItem is ADContentBase adc)
                    selectedData = adc;

                if (selectedData == null)
                    return;

                foreach (PropertyValueCollection property in selectedData.entry.Properties)
                {
                    if ((property.PropertyName == "objectSid") && (property.Value is Array))
                    {
                        Byte[] SID_array = (Byte[])property.Value;

                        UInt32 sid1 = (UInt32)(SID_array[12] | SID_array[13] << 8 | SID_array[14] << 16 | SID_array[15] << 24);
                        UInt32 sid2 = (UInt32)(SID_array[16] | SID_array[17] << 8 | SID_array[18] << 16 | SID_array[19] << 24);
                        UInt32 sid3 = (UInt32)(SID_array[20] | SID_array[21] << 8 | SID_array[22] << 16 | SID_array[23] << 24);
                        UInt32 sid4 = (UInt32)(SID_array[24] | SID_array[25] << 8 | SID_array[26] << 16 | SID_array[27] << 24);
                        String SID_str = "S-" + SID_array[0].ToString() + "-" + SID_array[1].ToString() + "-" + SID_array[8].ToString() + "-" + sid1 + "-" + sid2 + "-" + sid3 + "-" + sid4;
                        property.Value = SID_str;
                    }
                    lvProperties.Items.Add(property);
                }

                if (selectedData is ADUser)
                {
                    ADUser user = (ADUser)selectedData;

                    DirectoryEntry parentOUEntry = user.parentEntry;
                    parentOU = new ADOrganizationalUnit(parentOUEntry);
                    tbADPath.Text = parentOU.clearLDAP;

                    if ((user.entry.Properties["jpegPhoto"].Value != null) && (user.entry.Properties["jpegPhoto"].Value is Byte[]))
                    {
                        try
                        {
                            BitmapImage photo = new BitmapImage();
                            photo.CacheOption = BitmapCacheOption.OnLoad;
                            photo.BeginInit();
                            photo.StreamSource = new MemoryStream((Byte[])user.entry.Properties["jpegPhoto"].Value);
                            photo.EndInit();
                            imPhoto.Source = photo;
                        }
                        catch
                        {
                           new Message("Не удалось загрузить фотографию");
                        }
                    }

                    if (user.entry.Properties["cn"].Value != null)
                        tbFullName.Text = user.entry.Properties["cn"].Value.ToString();

                    if (user.entry.Properties["sAMAccountName"].Value != null)
                        tbID.Text = "Таб. № " + user.entry.Properties["sAMAccountName"].Value.ToString();

                    if (user.entry.Properties["telephoneNumber"].Value != null)
                        tbPhone.Text ="Тел. " + user.entry.Properties["telephoneNumber"].Value.ToString();

                    // additional attributes: drink and mobile
                    try
                    {
                        if (user.entry.Properties["drink"].Value != null)
                            tbdrink.Text = "ДР: " + user.entry.Properties["drink"].Value.ToString();
                    }
                    catch { }

                    try
                    {
                        if (user.entry.Properties["mobile"].Value != null)
                            tbmobile.Text = "Моб. " + user.entry.Properties["mobile"].Value.ToString();
                    }
                    catch { }

                    if (user.entry.Properties["department"].Value != null)
                        tbDepartment.Text = user.entry.Properties["department"].Value.ToString();

                    if (user.entry.Properties["title"].Value != null)
                        tbPost.Text = user.entry.Properties["title"].Value.ToString();

                    if (user.entry.Properties["physicalDeliveryOfficeName"].Value != null)
                        tbAddress.Text = user.entry.Properties["physicalDeliveryOfficeName"].Value.ToString();

                    if (user.entry.Properties["description"].Value != null)
                    {
                        Boolean isFound = false;

                        String pcNamesStr = user.entry.Properties["description"].Value.ToString().Replace(",", " ").Replace("(", " ").Replace(")", " ").Replace("  ", " ").Trim().ToUpper();
                        String[] pcNames = pcNamesStr.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (String pcName in pcNames)
                        {
                            mgmtScope = null;
                            ra_svc = null;

                            stLoading.Visibility = Visibility.Visible;
                            stSucceeded.Visibility = Visibility.Collapsed;
                            stFailed.Visibility = Visibility.Collapsed;

                            if (gethostbyname(pcName) != IntPtr.Zero)
                            {
                                try
                                {
                                    Ping ping = new Ping();
                                    IPStatus status = ping.Send(pcName, 700).Status;
                                    if ((status != IPStatus.BadDestination) && (status != IPStatus.DestinationHostUnreachable))
                                    {
                                        if ((dispatcherTimer != null) && (dispatcherTimer.IsEnabled))
                                            dispatcherTimer.Stop();
                                        else
                                            dispatcherTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(1500) };

                                        dispatcherTimer.Tick += new EventHandler((_s, _e) =>
                                        {
                                            try
                                            {
                                                status = ping.Send(pcName, 1000).Status;
                                                if (status == IPStatus.Success)
                                                    ThreadPool.QueueUserWorkItem(new WaitCallback(EnableAvailablePCTools), pcName);
                                                else
                                                    ThreadPool.QueueUserWorkItem(new WaitCallback(EnableUnavailablePCTools), pcName);
                                            }
                                            catch (Exception ex)
                                            {
                                                MessageBox.Show(ex.ToString(), "Ошибка во время операции PING");
                                                Dispatcher.BeginInvoke(new PCMgmtDelegate(EnableUnavailablePCTools), pcName);
                                                ((DispatcherTimer)_s).Stop();
                                            }
                                        });
                                        dispatcherTimer.Start();

                                        GetNetworkInfo(pcName);
                                        if (status != IPStatus.TimedOut)
                                        {
                                            mgmtScope = new ManagementScope("\\\\" + pcName + "\\root\\cimv2");
                                            mgmtScope.Connect();

                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetSystemInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetMotherboardInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetVideoCardInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetCPUInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetSoundCardInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetRAMInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetHDDInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetCDROMInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetVolumesInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetPrintersInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetProcessesInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetPrgTasksInfo), pcName);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(GetBSODData), pcName);
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetSystemInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetMotherboardInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetVideoCardInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetCPUInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetSoundCardInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetRAMInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetHDDInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetCDROMInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetVolumesInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetPrintersInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetProcessesInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetPrgTasksInfo), pcName); })).Start();
                                            new Thread(new ThreadStart(() => { Dispatcher.BeginInvoke(DispatcherPriority.Background, new PCMgmtDelegate(GetBSODData), pcName); })).Start();
                                        }

                                        isFound = true;
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(ex.ToString(), "Проверка доступа к ПК");
                                }
                            }
                        }
                        if (!isFound)
                        {
                            Dispatcher.BeginInvoke(new PCMgmtDelegate(EnableUnavailablePCTools), pcNames[0]);
                        }
                    }

                    if (user.entry.Properties["memberOf"].Value != null)
                    {
                        icADGroups.Items.Clear();
                        if (user.entry.Properties["memberOf"].Value is Array)
                        {
                            foreach (String groupName in (Object[])user.entry.Properties["memberOf"].Value)
                            {
                                icADGroups.Items.Add(groupName.Split(',')[0].Remove(0, 3));
                            }
                        }
                        else
                            icADGroups.Items.Add(user.entry.Properties["memberOf"].Value.ToString().Split(',')[0].Remove(0, 3));
                    }
                }
            }
        }

        private void ResetInfo()
        {
            tcOveralData.SelectedIndex = 0;

            imPhoto.Source = null;
            tbPost.Text = "";
            tbWorkstation.Text = "";
            tbADPath.Text = "";
            tbFullName.Text = "";
            tbID.Text = "";
            tbPhone.Text = "";
            tbdrink.Text = "";
            tbmobile.Text = "";
            tbDepartment.Text = "";
            tbPost.Text = "";
            tbAddress.Text = "";
            icADGroups.Items.Clear();
            stLoading.Visibility = Visibility.Collapsed;
            stSucceeded.Visibility = Visibility.Collapsed;
            stFailed.Visibility = Visibility.Collapsed;

           // tiNetwork.Visibility = Visibility.Collapsed;
            tbIP.Text = "";
            //tbMAC.Text = "";

            tiSystemInfo.Visibility = Visibility.Collapsed;
            systemDetailsList.Clear();
            lbSystemDetails.Tag = null;

            tiHardwareInfo.Visibility = Visibility.Collapsed;
            motherboardDetailsList.Clear();
            lbMotherboard.Tag = null;
            cpuDetailsList.Clear();
            lbCPU.Tag = null;
            videoCardDetailsList.Clear();
            tcVideoCard.Tag = null;
            soundDetailsList.Clear();
            lbSoundCard.Tag = null;
            ramDetailsList.Clear();
            tcRAM.Tag = null;
            hddDetailsList.Clear();
            tcHDD.Tag = null;
            cdromDetailsList.Clear();
            lbCDROM.Tag = null;

            tiVolumes.Visibility = Visibility.Collapsed;
            volumesList.Clear();
            tcVolumes.Tag = null;
            filesList.Clear();
            lbFiles.Tag = null;

            tiProcesses.Visibility = Visibility.Collapsed;
            processesList.Clear();
            lvProcesses.Tag = null;

            tiPrgTasksInstall.Visibility = Visibility.Collapsed;
            remotePrgTasksList.Clear();
            lbPrgTasks.Tag = null;

            tiPrinters.Visibility = Visibility.Collapsed;
            printersList.Clear();
            tcPrinters.Tag = null;

            tiBSODs.Visibility = Visibility.Collapsed;
            bsodList.Clear();
            lbBSOD.Tag = null;

            tiRemoteCmd.Visibility = Visibility.Collapsed;
            bnSendRemoteCmd.Tag = null;

            tiMisc.Visibility = Visibility.Collapsed;
            bnGPUpdate.Tag = null;
            bnRemotePrgLaunch.Tag = null;
            bnSpoolerRestart.Tag = null;
            bnTimeSync.Tag = null;
            bnShutdownRemotePC.Tag = null;

            bnPingWorkstation.Visibility = Visibility.Collapsed;
            bnInstallRAdmin.Visibility = Visibility.Collapsed;
            bnTurnOnRAdmin.Visibility = Visibility.Collapsed;
            bnConnectViaRAdmin.Visibility = Visibility.Collapsed;
            bnConnectViewRAdmin.Visibility = Visibility.Collapsed;
            bnConnectFileRAdmin.Visibility = Visibility.Collapsed;
            bnConnectViaRDP.Visibility = Visibility.Collapsed;
            bnCompMgmt.Visibility = Visibility.Collapsed;
            UserPhotoBorder.Visibility = Visibility.Collapsed;
            lvProperties.Items.Clear();
        }

        // Unified search: поиск по всем полям
        private void bnSearchAll_Click(object sender, RoutedEventArgs e)
        {
            DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnClearSearchAll_Click(object sender, RoutedEventArgs e)
        {
            ClearSearchAndCollapse();
        }

        // Clear search textbox, close any search result windows, clear selection and collapse all tree nodes
        private void ClearSearchAndCollapse()
        {
            try { tbSearchAll.Text = ""; } catch { }

            try { CloseSearchResultsWindows(); } catch { }

            try { ClearTreeViewSelection(tvUsers); } catch { }

            try { CollapseAllTreeViewItems(tvUsers); } catch { }

            // clear details panel and collapse full details dock
            try { ResetInfo(); } catch { }
            try { dpFull.Visibility = Visibility.Collapsed; } catch { }

            try { FindLoading.Visibility = Visibility.Collapsed; } catch { }
            try { tbSearchAll.IsEnabled = true; } catch { }
            try { bnSearchAll.IsEnabled = true; } catch { }

            try { tbSearchAll.Focus(); } catch { }
        }

        private void CloseSearchResultsWindows()
        {
            var windows = new List<Window>();
            foreach (Window w in Application.Current.Windows)
            {
                if (w == null) continue;
                if (w is SearchResultsWindow || w.Title == "Результаты поиска")
                    windows.Add(w);
            }
            foreach (var w in windows)
            {
                try { w.Close(); } catch { }
            }
        }

        private void CollapseAllTreeViewItems(TreeView tree)
        {
            if (tree == null) return;

            foreach (object top in tree.Items)
            {
                TreeViewItem tvi = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(top);
                if (tvi == null)
                {
                    tree.UpdateLayout();
                    tvi = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(top);
                }
                if (tvi == null) continue;
                CollapseContainerRecursive(tvi);
            }
        }

        private void CollapseContainerRecursive(TreeViewItem container)
        {
            try
            {
                // collapse current
                if (container.IsExpanded)
                    container.IsExpanded = false;

                // recurse to children
                foreach (object child in container.Items)
                {
                    TreeViewItem tvi = (TreeViewItem)container.ItemContainerGenerator.ContainerFromItem(child);
                    if (tvi == null)
                    {
                        container.UpdateLayout();
                        tvi = (TreeViewItem)container.ItemContainerGenerator.ContainerFromItem(child);
                    }
                    if (tvi == null) continue;
                    CollapseContainerRecursive(tvi);
                }
            }
            catch { }
        }

        private void tbSearchAll_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                DoUnifiedSearch(tbSearchAll.Text);
        }

        private void DoUnifiedSearch(string query)
        {
            if (String.IsNullOrWhiteSpace(query))
                return;

            string q = query.Trim();

            // show loading indicator near search and disable controls
            try { FindLoading.Visibility = Visibility.Visible; } catch { }
            try { tbSearchAll.IsEnabled = false; } catch { }
            try { bnSearchAll.IsEnabled = false; } catch { }

            Logger.Log($"DoUnifiedSearch: starting search for '{q}'");
            // run LDAP and in-memory search on a dedicated STA thread to avoid DirectoryServices marshalling issues
            Thread searchThread = new Thread(() =>
            {
                var swSearch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Log("Search thread: start");
                List<SearchResultInfo> results = new List<SearchResultInfo>();

                // try LDAP search first (top N)
                try
                {
                    string domainPath = "LDAP://DC=" + Domain.GetCurrentDomain().Name.Replace(".", ",DC=");
                    using (DirectoryEntry root = new DirectoryEntry(domainPath))
                    using (DirectorySearcher ds = new DirectorySearcher(root))
                    {
                        string esc = LdapEscape(q);
                        ds.Filter = "(&(objectCategory=person)(|(telephoneNumber=*" + esc + "*)(sAMAccountName=*" + esc + "*)(description=*" + esc + "*)(displayName=*" + esc + "*)(cn=*" + esc + "*)))";
                        ds.PropertiesToLoad.Clear();
                        ds.PropertiesToLoad.Add("cn");
                        ds.PropertiesToLoad.Add("distinguishedName");
                        ds.PropertiesToLoad.Add("department");
                        ds.PropertiesToLoad.Add("telephoneNumber");
                        ds.PropertiesToLoad.Add("userPrincipalName");
                        ds.PropertiesToLoad.Add("description");
                        ds.PropertiesToLoad.Add("jpegPhoto");
                        ds.SizeLimit = SearchTopN;

                        SearchResultCollection src = null;
                        try { src = ds.FindAll(); }
                        catch { src = null; }

                        if (src != null)
                        {
                            foreach (SearchResult sr in src)
                            {
                                try
                                {
                                    DirectoryEntry de = null;
                                    try { de = sr.GetDirectoryEntry(); } catch { }

                                    string cn = null;
                                    try { if (sr.Properties.Contains("cn") && sr.Properties["cn"].Count > 0) cn = sr.Properties["cn"][0].ToString(); }
                                    catch { }

                                    var info = new SearchResultInfo() { DisplayName = cn ?? (de != null ? de.Name : null), EntryPath = de != null ? de.Path : null };

                                    // read extra attributes if present
                                    try { if (sr.Properties.Contains("department") && sr.Properties["department"].Count > 0) info.Department = sr.Properties["department"][0].ToString(); } catch { }
                                    try { if (sr.Properties.Contains("telephoneNumber") && sr.Properties["telephoneNumber"].Count > 0) info.TelephoneNumber = sr.Properties["telephoneNumber"][0].ToString(); } catch { }
                                    try { if (sr.Properties.Contains("userPrincipalName") && sr.Properties["userPrincipalName"].Count > 0) info.UserPrincipalName = sr.Properties["userPrincipalName"][0].ToString(); } catch { }
                                    try { if (sr.Properties.Contains("description") && sr.Properties["description"].Count > 0) info.Description = sr.Properties["description"][0].ToString(); } catch { }
                                    try { if (sr.Properties.Contains("jpegPhoto") && sr.Properties["jpegPhoto"].Count > 0) info.PhotoBytes = sr.Properties["jpegPhoto"][0] as byte[]; } catch { }

                                    results.Add(info);
                                }
                                catch { continue; }
                                if (results.Count >= SearchTopN) break;
                            }
                            try { src.Dispose(); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("LDAP search failed: " + ex.ToString());
                }

                // if no LDAP results - search in-memory tree
                if (results.Count == 0)
                {
                    try
                    {
                        foreach (ADContentBase item in App.activeDirectoryContent)
                        {
                            List<ADContentBase> path = new List<ADContentBase>();
                            FindPaths(item, q, path, results);
                            if (results.Count >= SearchTopN) break;
                        }
                    }
                    catch { }
                }

                // update UI
                swSearch.Stop();
                Logger.Log($"Search thread: finished, duration={swSearch.Elapsed.TotalSeconds:0.000}s, results={results.Count}");
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    Logger.Log("Dispatcher: processing search completion");
                    try { FindLoading.Visibility = Visibility.Collapsed; } catch { }
                    try { tbSearchAll.IsEnabled = true; } catch { }
                    try { bnSearchAll.IsEnabled = true; } catch { }

                    if (results.Count == 0)
                    {
                        MessageBox.Show("Ничего не найдено.", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // show results in separate XAML window
                    var dlg = new SearchResultsWindow(results) { Owner = this };
                    dlg.ShowDialog();
                }));
            }) { IsBackground = true };
            try { searchThread.SetApartmentState(ApartmentState.STA); } catch { }
            searchThread.Start();
        }
        private bool FindPath(ADContentBase current, string q, List<ADContentBase> path, out List<ADContentBase> resultPath)
        {
            resultPath = null;
            if (current == null)
                return false;

            path.Add(current);

                try
                {
                    // Search only in user objects to avoid matching computers/groups by description etc.
                    if (current is ADUser)
                    {
                        try
                        {
                            // use cached attributes populated at creation time to avoid touching DirectoryEntry from background thread
                            if (current.cachedAttributes != null)
                            {
                                foreach (string attr in userSearchAttributes)
                                {
                                    try
                                    {
                                        string val;
                                        if (!current.cachedAttributes.TryGetValue(attr, out val) || String.IsNullOrEmpty(val))
                                            continue;

                                        if (val.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            resultPath = new List<ADContentBase>(path);
                                            return true;
                                        }
                                    }
                                    catch { continue; }
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }

            ADOrganizationalUnit ou = current as ADOrganizationalUnit;
            if (ou != null && ou.content != null)
            {
                foreach (ADContentBase child in ou.content)
                {
                    if (FindPath(child, q, path, out resultPath))
                        return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        // collect multiple matching paths (up to SearchTopN) from in-memory tree
        private void FindPaths(ADContentBase current, string q, List<ADContentBase> path, List<SearchResultInfo> results)
        {
            if (current == null || results.Count >= SearchTopN)
                return;

            path.Add(current);

            try
            {
                if (current is ADUser)
                {
                    try
                    {
                        if (current.cachedAttributes != null)
                        {
                            foreach (string attr in userSearchAttributes)
                            {
                                try
                                {
                                    string val;
                                    if (!current.cachedAttributes.TryGetValue(attr, out val) || String.IsNullOrEmpty(val))
                                        continue;

                                    if (val.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        var info = new SearchResultInfo()
                                        {
                                            DisplayName = current.cachedAttributes.ContainsKey("cn") ? current.cachedAttributes["cn"] : current.name,
                                            EntryPath = current.LDAP,
                                            PathInTree = new List<ADContentBase>(path),
                                            Department = current.cachedAttributes.ContainsKey("department") ? current.cachedAttributes["department"] : null,
                                            TelephoneNumber = current.cachedAttributes.ContainsKey("telephoneNumber") ? current.cachedAttributes["telephoneNumber"] : null,
                                            UserPrincipalName = current.cachedAttributes.ContainsKey("userPrincipalName") ? current.cachedAttributes["userPrincipalName"] : null,
                                            Description = current.cachedAttributes.ContainsKey("description") ? current.cachedAttributes["description"] : null,
                                            PhotoBytes = current.cachedPhoto
                                        };
                                        results.Add(info);
                                        if (results.Count >= SearchTopN) break;
                                        goto CONTINUE_CHILDREN_CHECK;
                                    }
                                }
                                catch { continue; }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

        CONTINUE_CHILDREN_CHECK:
            ADOrganizationalUnit ou = current as ADOrganizationalUnit;
            if (ou != null && ou.content != null)
            {
                foreach (ADContentBase child in ou.content)
                {
                    if (results.Count >= SearchTopN) break;
                    FindPaths(child, q, path, results);
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        private void ShowSearchResultsDialog(List<SearchResultInfo> results)
        {
            Window dlg = new Window() { Title = "Результаты поиска", Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize };
            StackPanel sp = new StackPanel() { Margin = new Thickness(8) };
            ListBox lb = new ListBox() { Width = 560, Height = 300 };

            foreach (var r in results)
            {
                Grid g = new Grid() { Margin = new Thickness(4) };
                g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(72) });
                g.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                Image img = new Image() { Width = 64, Height = 64, Margin = new Thickness(4) };
                if (r.PhotoBytes != null && r.PhotoBytes.Length > 0)
                {
                    try
                    {
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = new System.IO.MemoryStream(r.PhotoBytes);
                        bmp.EndInit();
                        img.Source = bmp;
                    }
                    catch { }
                }
                Grid.SetColumn(img, 0);
                g.Children.Add(img);

                StackPanel info = new StackPanel() { Orientation = Orientation.Vertical };
                TextBlock tbName = new TextBlock() { Text = r.DisplayName ?? "(без имени)", FontWeight = FontWeights.Bold };
                info.Children.Add(tbName);
                if (!String.IsNullOrEmpty(r.Department)) info.Children.Add(new TextBlock() { Text = "Отдел: " + r.Department });
                if (!String.IsNullOrEmpty(r.TelephoneNumber)) info.Children.Add(new TextBlock() { Text = "Телефон: " + r.TelephoneNumber });
                if (!String.IsNullOrEmpty(r.UserPrincipalName)) info.Children.Add(new TextBlock() { Text = "UPN: " + r.UserPrincipalName });
                if (!String.IsNullOrEmpty(r.Description)) info.Children.Add(new TextBlock() { Text = "Описание: " + r.Description, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });

                // note about loaded status
                if (!String.IsNullOrEmpty(r.EntryPath))
                    info.Children.Add(new TextBlock() { Text = "(найден в AD)", FontStyle = FontStyles.Italic, Foreground = SystemColors.GrayTextBrush });
                else
                    info.Children.Add(new TextBlock() { Text = "(найден в AD, путь отсутствует)", FontStyle = FontStyles.Italic, Foreground = SystemColors.GrayTextBrush });

                Grid.SetColumn(info, 1);
                g.Children.Add(info);

                ListBoxItem it = new ListBoxItem() { Content = g, Tag = r };
                lb.Items.Add(it);
            }

            sp.Children.Add(lb);
            DockPanel dock = new DockPanel() { Margin = new Thickness(0, 8, 0, 0) };
            Button ok = new Button() { Content = "Выбрать", Width = 90, Margin = new Thickness(4) };
            Button cancel = new Button() { Content = "Отмена", Width = 90, Margin = new Thickness(4) };
            dock.Children.Add(ok);
            dock.Children.Add(cancel);
            DockPanel.SetDock(ok, Dock.Left);
            DockPanel.SetDock(cancel, Dock.Right);
            sp.Children.Add(dock);

            dlg.Content = sp;

            ok.Click += (s, e) =>
            {
                if (lb.SelectedItem == null)
                {
                    MessageBox.Show("Выберите элемент.", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sel = ((ListBoxItem)lb.SelectedItem).Tag as SearchResultInfo;
                if (sel != null)
                {
                    // try to find path in loaded tree now on UI thread
                    if (!String.IsNullOrEmpty(sel.EntryPath))
                    {
                        List<ADContentBase> resultPath = null;
                        try
                        {
                            foreach (ADContentBase item in App.activeDirectoryContent)
                            {
                                List<ADContentBase> path = new List<ADContentBase>();
                                if (TryFindPathToEntry(item, sel.EntryPath, path, out resultPath))
                                    break;
                            }
                        }
                        catch { resultPath = null; }

                        if (resultPath != null && resultPath.Count > 0)
                        {
                            SelectPathInTreeView(resultPath);
                            dlg.DialogResult = true;
                            dlg.Close();
                            return;
                        }
                        else
                        {
                            MessageBox.Show("Объект найден в AD, но не загружен в дереве. Попробуйте обновить содержимое.", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Объект найден в AD, но отсутствует путь.", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
            };

            cancel.Click += (s, e) => { dlg.DialogResult = false; dlg.Close(); };

            dlg.ShowDialog();
        }

        // Try to find path to DirectoryEntry with given path (entry.Path) starting from current node
        public bool TryFindPathToEntry(ADContentBase current, string targetEntryPath, List<ADContentBase> path, out List<ADContentBase> resultPath)
        {
            resultPath = null;
            if (current == null)
                return false;

            path.Add(current);

            try
            {
                if (current.entry != null)
                {
                    try
                    {
                        if (String.Equals(current.entry.Path, targetEntryPath, StringComparison.OrdinalIgnoreCase))
                        {
                            resultPath = new List<ADContentBase>(path);
                            return true;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            ADOrganizationalUnit ou = current as ADOrganizationalUnit;
            if (ou != null)
            {
                try
                {
                    // access children (lazy load) and search recursively
                    foreach (ADContentBase child in ou.content)
                    {
                        if (TryFindPathToEntry(child, targetEntryPath, path, out resultPath))
                            return true;
                    }
                }
                catch { }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        // Escape user input for LDAP filter
        private string LdapEscape(string input)
        {
            if (String.IsNullOrEmpty(input))
                return input;
            return input.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("\0", "\\00").Replace("(", "\\28").Replace(")", "\\29");
        }

        public void SelectPathInTreeView(List<ADContentBase> path)
        {
            if (path == null || path.Count == 0)
                return;

            // find corresponding TreeNodeViewModel root matching path[0]
            TreeNodeViewModel rootVm = null;
            foreach (object o in tvUsers.Items)
            {
                if (o is TreeNodeViewModel vm && vm.Data != null && vm.Data.LDAP == path[0].LDAP)
                {
                    rootVm = vm;
                    break;
                }
            }
            if (rootVm == null)
                return;

            // get container for root
            TreeViewItem currentContainer = (TreeViewItem)tvUsers.ItemContainerGenerator.ContainerFromItem(rootVm);
            if (currentContainer == null)
            {
                tvUsers.UpdateLayout();
                currentContainer = (TreeViewItem)tvUsers.ItemContainerGenerator.ContainerFromItem(rootVm);
            }
            if (currentContainer == null) return;

            // iterate down the path, expanding nodes as needed
            TreeNodeViewModel currentVm = rootVm;
            currentContainer.IsExpanded = true;

            for (int i = 1; i < path.Count; i++)
            {
                // ensure children loaded for currentVm
                if (currentVm.HasDummyChild)
                    currentVm.IsExpanded = true; // will load children

                // find child vm matching path[i]
                TreeNodeViewModel childVm = null;
                foreach (var c in currentVm.Children)
                {
                    if (c != null && c.Data != null && c.Data.LDAP == path[i].LDAP)
                    {
                        childVm = c;
                        break;
                    }
                }
                if (childVm == null) return;

                // get child container
                TreeViewItem childContainer = (TreeViewItem)currentContainer.ItemContainerGenerator.ContainerFromItem(childVm);
                if (childContainer == null)
                {
                    currentContainer.UpdateLayout();
                    childContainer = (TreeViewItem)currentContainer.ItemContainerGenerator.ContainerFromItem(childVm);
                }
                if (childContainer == null) return;

                childContainer.IsExpanded = true;
                currentContainer = childContainer;
                currentVm = childVm;
            }

            // select last container
            currentContainer.IsSelected = true;
            currentContainer.BringIntoView();
        }

        private void ClearTreeViewSelection(TreeView tree)
        {
            // try to find selected item and clear
            foreach (object top in tree.Items)
            {
                TreeViewItem tvi = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(top);
                if (tvi == null)
                {
                    tree.UpdateLayout();
                    tvi = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(top);
                }
                if (tvi == null) continue;
                ClearSelectionInContainer(tvi);
            }
        }

        private void ClearSelectionInContainer(TreeViewItem container)
        {
            if (container.IsSelected)
                container.IsSelected = false;

            if (!container.IsExpanded)
                return;

            foreach (object child in container.Items)
            {
                TreeViewItem tvi = (TreeViewItem)container.ItemContainerGenerator.ContainerFromItem(child);
                if (tvi == null)
                {
                    container.UpdateLayout();
                    tvi = (TreeViewItem)container.ItemContainerGenerator.ContainerFromItem(child);
                }
                if (tvi == null) continue;
                ClearSelectionInContainer(tvi);
            }
        }

        void EnableAvailablePCTools(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                tbWorkstation.Text = "ПК: " + pcName;

                //if (mgmtScope != null)
                {
                    stLoading.Visibility = Visibility.Collapsed;
                    stSucceeded.Visibility = Visibility.Visible;
                    stFailed.Visibility = Visibility.Collapsed;
                    UserPhotoBorder.Visibility = Visibility.Visible;
                    bnPingWorkstation.Visibility = Visibility.Visible;
                    bnPingWorkstation.Tag = pcName;

                    SelectQuery dskQuery = new SelectQuery("Win32_Service", "Name LIKE \"r_server\" OR Name LIKE \"RServer3\"");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject serviceDetails in mgmtSrchr.Get())
                        {
                            if ((serviceDetails.Properties["Name"].Value.ToString() == "r_server") || (serviceDetails.Properties["Name"].Value.ToString() == "RServer3"))
                            {
                                String ver = (serviceDetails.Properties["Name"].Value.ToString() == "r_server") ? "v2.1" : "v3.4";

                                bnTurnOnRAdmin.Content = "Включить RAdmin " + ver;
                                bnConnectViaRAdmin.Content = "RA Управление " + ver;
                                bnConnectViewRAdmin.Content = "RA Просмотр " + ver;
                                bnConnectFileRAdmin.Content = "RA Файл " + ver;

                                if (serviceDetails.Properties["State"].Value.ToString() != "Running")
                                {
                                    bnTurnOnRAdmin.Visibility = Visibility.Visible;
                                    bnConnectViaRAdmin.Visibility = Visibility.Collapsed;
                                    bnConnectViewRAdmin.Visibility = Visibility.Collapsed;
                                    bnConnectFileRAdmin.Visibility = Visibility.Collapsed;
                                }
                                else
                                {
                                    bnTurnOnRAdmin.Visibility = Visibility.Collapsed;
                                    bnConnectViaRAdmin.Visibility = Visibility.Visible;
                                    bnConnectViewRAdmin.Visibility = Visibility.Visible;
                                    bnConnectFileRAdmin.Visibility = Visibility.Visible;
                                }

                                try
                                {
                                    ServiceController[] services = ServiceController.GetServices(pcName.ToString());
                                    foreach (ServiceController service in services)
                                        if ((service.ServiceName == "r_server") || (service.ServiceName == "RServer3"))
                                            ra_svc = service;
                                    bnConnectViaRAdmin.Tag = pcName;
                                    bnConnectViewRAdmin.Tag = pcName;
                                    bnConnectFileRAdmin.Tag = pcName;
                                }
                                catch
                                {
                                    ra_svc = null;
                                    bnConnectViaRAdmin.Tag = null;
                                    bnConnectViewRAdmin.Visibility = Visibility.Collapsed;
                                    bnConnectFileRAdmin.Visibility = Visibility.Collapsed;
                                }
                            }
                        }

                        if (ra_svc == null)
                        {
                            bnInstallRAdmin.Visibility = Visibility.Visible;
                            bnTurnOnRAdmin.Visibility = Visibility.Collapsed;
                            bnConnectViaRAdmin.Visibility = Visibility.Collapsed;
                            bnConnectViewRAdmin.Visibility = Visibility.Collapsed;
                            bnConnectFileRAdmin.Visibility = Visibility.Collapsed;
                        }
                    }
                    catch
                    {
                        bnInstallRAdmin.Visibility = Visibility.Collapsed;
                        bnTurnOnRAdmin.Visibility = Visibility.Collapsed;
                        bnConnectViaRAdmin.Visibility = Visibility.Collapsed;
                        bnConnectViewRAdmin.Visibility = Visibility.Collapsed;
                        bnConnectFileRAdmin.Visibility = Visibility.Collapsed;
                    }

                    bnConnectViaRAdmin.Visibility = Visibility.Visible;
                    bnConnectViewRAdmin.Visibility = Visibility.Visible;
                    bnConnectFileRAdmin.Visibility = Visibility.Visible;
                    bnConnectViaRAdmin.Tag = pcName;
                    bnConnectViewRAdmin.Tag = pcName;
                    bnConnectFileRAdmin.Tag = pcName;

                    bnConnectViaRDP.Visibility = Visibility.Visible;
                    bnConnectViaRDP.Tag = pcName;

                    bnCompMgmt.Visibility = Visibility.Visible;
                    bnCompMgmt.Tag = pcName;

                    bnSendRemoteCmd.Tag = pcName;
                    tiRemoteCmd.Visibility = Visibility.Visible;

                    bnGPUpdate.Tag = pcName;
                    bnRemotePrgLaunch.Tag = pcName;
                    bnTimeSync.Tag = pcName;
                    bnSpoolerRestart.Tag = pcName;
                    bnShutdownRemotePC.Tag = pcName;
                    tiMisc.Visibility = Visibility.Visible;
                }
            }), _pcName);
        }

        void GetNetworkInfo(Object _pcName)
        {
            tbIP.Text = "";
            //tbMAC.Text = "";

            foreach (IPAddress ipAddress in Dns.GetHostEntry(_pcName.ToString()).AddressList)
            {
                tbIP.Text += "IP: " + (tbIP.Text != "" ? "\n" : "") + ipAddress.ToString();
            }

            if (mgmtScope != null)
            {
                SelectQuery dskQuery = new SelectQuery("Win32_NetworkAdapter", "PhysicalAdapter = true");
                try
                {
                    ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Получение MAC-адреса");
                }
            }

           // tiNetwork.Visibility = Visibility.Collapsed;
           // tiNetwork.UpdateLayout();
        }

        void GetSystemInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                systemDetailsList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_OperatingSystem");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject systemDetails in mgmtSrchr.Get())
                        {
                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Название", systemDetails.Properties["Caption"].Value != null ? systemDetails.Properties["Caption"].Value.ToString() : "")); }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Сервис-пак", systemDetails.Properties["CSDVersion"].Value != null ? systemDetails.Properties["CSDVersion"].Value.ToString() : "")); }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Разрядность", systemDetails.Properties["OSArchitecture"].Value != null ? systemDetails.Properties["OSArchitecture"].Value.ToString() : "")); }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Номер сборки", systemDetails.Properties["BuildNumber"].Value != null ? systemDetails.Properties["BuildNumber"].Value.ToString() : "")); }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Установлена на диске", systemDetails.Properties["SystemDrive"].Value != null ? systemDetails.Properties["SystemDrive"].Value.ToString() : "")); }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Описание компьютера", systemDetails.Properties["Description"].Value != null ? systemDetails.Properties["Description"].Value.ToString() : "")); }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Количество пользователей", systemDetails.Properties["NumberOfUsers"].Value != null ? systemDetails.Properties["NumberOfUsers"].Value.ToString() : "")); }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Зарегистрированный пользователь", systemDetails.Properties["RegisteredUser"].Value != null ? systemDetails.Properties["RegisteredUser"].Value.ToString() : "")); }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Организация", systemDetails.Properties["Organization"].Value != null ? systemDetails.Properties["Organization"].Value.ToString() : "")); }
                            catch { }

                            try
                            {
                                if (systemDetails.Properties["InstallDate"].Value != null)
                                {
                                    String currentDateTime = systemDetails.Properties["InstallDate"].Value.ToString();
                                    String normalizeDateTime = currentDateTime.Substring(6, 2) + "." + currentDateTime.Substring(4, 2) + "." + currentDateTime.Substring(0, 4) + " " +
                                                               currentDateTime.Substring(8, 2) + ":" + currentDateTime.Substring(10, 2) + ":" + currentDateTime.Substring(12, 2);
                                    systemDetailsList.Add(new KeyValuePair<String, String>("Дата установки", normalizeDateTime));
                                }
                            }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Код продукта", systemDetails.Properties["SerialNumber"].Value != null ? systemDetails.Properties["SerialNumber"].Value.ToString() : "")); }
                            catch { }

                            try
                            {
                                if (systemDetails.Properties["LocalDateTime"].Value != null)
                                {
                                    String currentDateTime = systemDetails.Properties["LocalDateTime"].Value.ToString();
                                    String normalizeDateTime = currentDateTime.Substring(6, 2) + "." + currentDateTime.Substring(4, 2) + "." + currentDateTime.Substring(0, 4) + " " +
                                                               currentDateTime.Substring(8, 2) + ":" + currentDateTime.Substring(10, 2) + ":" + currentDateTime.Substring(12, 2);
                                    systemDetailsList.Add(new KeyValuePair<String, String>("Локальное время", normalizeDateTime));
                                }
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о системе");
                    }

                    dskQuery = new SelectQuery("Win32_ComputerSystem");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject systemDetails in mgmtSrchr.Get())
                        {
                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Рабочая группа", systemDetails.Properties["Domain"].Value != null ? systemDetails.Properties["Domain"].Value.ToString() : "")); }
                            catch { }

                            try { systemDetailsList.Add(new KeyValuePair<String, String>("Пользователь", systemDetails.Properties["UserName"].Value != null ? systemDetails.Properties["UserName"].Value.ToString() : "")); }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о системе");
                    }

                    if (systemDetailsList.Count > 0)
                    {
                        lbSystemDetails.Tag = pcName;
                        tiSystemInfo.Visibility = Visibility.Visible;
                        tiSystemInfo.UpdateLayout();
                    }
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void GetMotherboardInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                motherboardDetailsList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_BaseBoard");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject motherboardDetails in mgmtSrchr.Get())
                        {
                            try { motherboardDetailsList.Add(new KeyValuePair<String, String>("Производитель", motherboardDetails.Properties["Manufacturer"].Value != null ? motherboardDetails.Properties["Manufacturer"].Value.ToString() : "")); }
                            catch { }

                            try { motherboardDetailsList.Add(new KeyValuePair<String, String>("Название", motherboardDetails.Properties["Product"].Value != null ? motherboardDetails.Properties["Product"].Value.ToString() : "")); }
                            catch { }

                            try { motherboardDetailsList.Add(new KeyValuePair<String, String>("Версия", motherboardDetails.Properties["Version"].Value != null ? motherboardDetails.Properties["Version"].Value.ToString() : "")); }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о материнской плате");
                    }

                    if (motherboardDetailsList.Count > 0)
                    {
                        tiHardwareInfo.Visibility = Visibility.Visible;
                        tiHardwareInfo.UpdateLayout();
                        tiMotherboard.Visibility = Visibility.Visible;
                        tiMotherboard.UpdateLayout();
                    }
                    else
                        tiMotherboard.Visibility = Visibility.Collapsed;
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void GetCPUInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                cpuDetailsList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_Processor");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject cpuDetails in mgmtSrchr.Get())
                        {
                            try { cpuDetailsList.Add(new KeyValuePair<String, String>("Название", cpuDetails.Properties["Name"].Value != null ? cpuDetails.Properties["Name"].Value.ToString() : "")); }
                            catch { }

                            try { cpuDetailsList.Add(new KeyValuePair<String, String>("Описание", cpuDetails.Properties["Caption"].Value != null ? cpuDetails.Properties["Caption"].Value.ToString() : "")); }
                            catch { }

                            try { cpuDetailsList.Add(new KeyValuePair<String, String>("Разъем", cpuDetails.Properties["SocketDesignation"].Value != null ? cpuDetails.Properties["SocketDesignation"].Value.ToString() : "")); }
                            catch { }

                            try { cpuDetailsList.Add(new KeyValuePair<String, String>("Текущая частота (МГц)", cpuDetails.Properties["CurrentClockSpeed"].Value != null ? cpuDetails.Properties["CurrentClockSpeed"].Value.ToString() : "")); }
                            catch { }

                            try { cpuDetailsList.Add(new KeyValuePair<String, String>("Максимальная частота (МГц)", cpuDetails.Properties["MaxClockSpeed"].Value != null ? cpuDetails.Properties["MaxClockSpeed"].Value.ToString() : "")); }
                            catch { }

                            try { cpuDetailsList.Add(new KeyValuePair<String, String>("Размер L2-кэша (байт)", cpuDetails.Properties["L2CacheSize"].Value != null ? cpuDetails.Properties["L2CacheSize"].Value.ToString() : "")); }
                            catch { }

                            try { cpuDetailsList.Add(new KeyValuePair<String, String>("Размер L3-кэша (байт)", cpuDetails.Properties["L3CacheSize"].Value != null ? cpuDetails.Properties["L3CacheSize"].Value.ToString() : "")); }
                            catch { }

                            try { cpuDetailsList.Add(new KeyValuePair<String, String>("Количество ядер", cpuDetails.Properties["NumberOfCores"].Value != null ? cpuDetails.Properties["NumberOfCores"].Value.ToString() : "")); }
                            catch { }

                            try { cpuDetailsList.Add(new KeyValuePair<String, String>("Количество потоков", cpuDetails.Properties["NumberOfLogicalProcessors"].Value != null ? cpuDetails.Properties["NumberOfLogicalProcessors"].Value.ToString() : "")); }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о процессоре");
                    }

                    if (cpuDetailsList.Count > 0)
                    {
                        tiHardwareInfo.Visibility = Visibility.Visible;
                        tiHardwareInfo.UpdateLayout();
                        tiCPU.Visibility = Visibility.Visible;
                        tiCPU.UpdateLayout();
                    }
                    else
                        tiCPU.Visibility = Visibility.Collapsed;
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void GetVideoCardInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                videoCardDetailsList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_VideoController");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject videoCardDetails in mgmtSrchr.Get())
                        {
                            HeaderedInfoSet videoCard = new HeaderedInfoSet();
                            videoCard.properties = new ObservableCollection<KeyValuePair<String, String>>();

                            try { videoCard.name = videoCardDetails.Properties["Name"].Value != null ? videoCardDetails.Properties["Name"].Value.ToString() : ""; }
                            catch { }

                            try { videoCard.properties.Add(new KeyValuePair<String, String>("Текущее разрешение (высота)", videoCardDetails.Properties["CurrentVerticalResolution"].Value != null ? videoCardDetails.Properties["CurrentVerticalResolution"].Value.ToString() : "")); }
                            catch { }

                            try { videoCard.properties.Add(new KeyValuePair<String, String>("Текущее разрешение (ширина)", videoCardDetails.Properties["CurrentHorizontalResolution"].Value != null ? videoCardDetails.Properties["CurrentHorizontalResolution"].Value.ToString() : "")); }
                            catch { }

                            try
                            {
                                if (videoCardDetails.Properties["AdapterRAM"].Value != null)
                                {
                                    UInt64 videoRAM = UInt64.Parse(videoCardDetails.Properties["AdapterRAM"].Value.ToString());
                                    videoCard.properties.Add(new KeyValuePair<String, String>("Видеопамять (Мб)", (videoRAM >> 20).ToString()));
                                }
                            }
                            catch { }

                            videoCardDetailsList.Add(videoCard);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о видеокарте");
                    }

                    if (videoCardDetailsList.Count > 0)
                    {
                        tiHardwareInfo.Visibility = Visibility.Visible;
                        tiHardwareInfo.UpdateLayout();
                        tiVideoCard.Visibility = Visibility.Visible;
                        tiVideoCard.UpdateLayout();
                    }
                    else
                        tiVideoCard.Visibility = Visibility.Collapsed;
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void GetSoundCardInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                soundDetailsList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_SoundDevice");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject soundDetails in mgmtSrchr.Get())
                        {
                            try { soundDetailsList.Add(new KeyValuePair<String, String>("Название устройства", soundDetails.Properties["Caption"].Value != null ? soundDetails.Properties["Caption"].Value.ToString() : "")); }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о звуковой карте");
                    }

                    if (soundDetailsList.Count > 0)
                    {
                        tiHardwareInfo.Visibility = Visibility.Visible;
                        tiHardwareInfo.UpdateLayout();
                        tiSoundCard.Visibility = Visibility.Visible;
                        tiSoundCard.UpdateLayout();
                    }
                    else
                        tiSoundCard.Visibility = Visibility.Collapsed;
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void GetRAMInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                ramDetailsList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_PhysicalMemory");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject ramDetails in mgmtSrchr.Get())
                        {
                            HeaderedInfoSet ram = new HeaderedInfoSet();
                            ram.properties = new ObservableCollection<KeyValuePair<String, String>>();

                            ram.name = ramDetails.Properties["Manufacturer"].Value != null ? ramDetails.Properties["Manufacturer"].Value.ToString() : "RAM";

                            try
                            {
                                if (ramDetails.Properties["Capacity"].Value != null)
                                {
                                    UInt64 ramSize = UInt64.Parse(ramDetails.Properties["Capacity"].Value.ToString());
                                    ram.properties.Add(new KeyValuePair<String, String>("Размер (Мб)", (ramSize >> 20).ToString()));
                                }
                            }
                            catch { }

                            try { ram.properties.Add(new KeyValuePair<String, String>("Частота (Мгц)", ramDetails.Properties["Speed"].Value != null ? ramDetails.Properties["Speed"].Value.ToString() : "")); }
                            catch { }

                            try
                            {
                                if (ramDetails.Properties["MemoryType"].Value != null)
                                {
                                    switch (UInt16.Parse(ramDetails.Properties["MemoryType"].Value.ToString()))
                                    {
                                        case 0:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "Unknown"));
                                            break;
                                        case 1:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "Other"));
                                            break;
                                        case 2:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "DRAM"));
                                            break;
                                        case 3:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "Synchronous DRAM"));
                                            break;
                                        case 4:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "Cache DRAM"));
                                            break;
                                        case 5:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "EDO"));
                                            break;
                                        case 6:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "EDRAM"));
                                            break;
                                        case 7:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "VRAM"));
                                            break;
                                        case 8:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "SRAM"));
                                            break;
                                        case 9:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "RAM"));
                                            break;
                                        case 10:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "ROM"));
                                            break;
                                        case 11:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "Flash"));
                                            break;
                                        case 12:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "EEPROM"));
                                            break;
                                        case 13:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "FEPROM"));
                                            break;
                                        case 14:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "EPROM"));
                                            break;
                                        case 15:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "CDRAM"));
                                            break;
                                        case 16:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "3DRAM"));
                                            break;
                                        case 17:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "SDRAM"));
                                            break;
                                        case 18:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "SGRAM"));
                                            break;
                                        case 19:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "RDRAM"));
                                            break;
                                        case 20:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "DDR"));
                                            break;
                                        case 21:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "DDR2"));
                                            break;
                                        case 22:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "DDR2"));
                                            break;
                                        case 23:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "DDR2 FB-DIMM"));
                                            break;
                                        case 24:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "DDR2—FB-DIMM"));
                                            break;
                                        case 25:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "DDR3"));
                                            break;
                                        case 26:
                                            ram.properties.Add(new KeyValuePair<String, String>("Тип памяти", "FBD2"));
                                            break;
                                    }
                                }
                            }
                            catch { }

                            ramDetailsList.Add(ram);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения об оперативной памяти");
                    }

                    if (ramDetailsList.Count > 0)
                    {
                        tiHardwareInfo.Visibility = Visibility.Visible;
                        tiHardwareInfo.UpdateLayout();
                        tiRAM.Visibility = Visibility.Visible;
                        tiRAM.UpdateLayout();
                    }
                    else
                        tiRAM.Visibility = Visibility.Collapsed;
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void GetHDDInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                hddDetailsList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_DiskDrive");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject hddDetails in mgmtSrchr.Get())
                        {
                            HeaderedInfoSet hdd = new HeaderedInfoSet();
                            hdd.properties = new ObservableCollection<KeyValuePair<String, String>>();

                            try { hdd.name = hddDetails.Properties["Caption"].Value != null ? hddDetails.Properties["Caption"].Value.ToString() : ""; }
                            catch { }

                            try
                            {
                                if (hddDetails.Properties["Size"].Value != null)
                                {
                                    UInt64 hddSize = UInt64.Parse(hddDetails.Properties["Size"].Value.ToString());
                                    hdd.properties.Add(new KeyValuePair<String, String>("Размер (Гб)", (hddSize >> 30).ToString()));
                                }
                            }
                            catch { }

                            try { hdd.properties.Add(new KeyValuePair<String, String>("Интерфейс подключения", hddDetails.Properties["InterfaceType"].Value != null ? hddDetails.Properties["InterfaceType"].Value.ToString() : "")); }
                            catch { }

                            hddDetailsList.Add(hdd);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о жёстком диске");
                    }

                    if (hddDetailsList.Count > 0)
                    {
                        tiHardwareInfo.Visibility = Visibility.Visible;
                        tiHardwareInfo.UpdateLayout();
                        tiHDD.Visibility = Visibility.Visible;
                        tiHDD.UpdateLayout();
                    }
                    else
                        tiHDD.Visibility = Visibility.Collapsed;
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void GetCDROMInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                cdromDetailsList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_CDROMDrive");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject cdromDetails in mgmtSrchr.Get())
                        {
                            try { cdromDetailsList.Add(new KeyValuePair<String, String>("Название", cdromDetails.Properties["Name"].Value != null ? cdromDetails.Properties["Name"].Value.ToString() : "")); }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о CD-ROM");
                    }

                    if (cdromDetailsList.Count > 0)
                    {
                        tiHardwareInfo.Visibility = Visibility.Visible;
                        tiHardwareInfo.UpdateLayout();
                        tiCDROM.Visibility = Visibility.Visible;
                        tiCDROM.UpdateLayout();
                    }
                    else
                        tiCDROM.Visibility = Visibility.Collapsed;
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private String currentDir;
        void GetVolumesInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                volumesList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_LogicalDisk", "DriveType = 3");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (var volumeDetail in mgmtSrchr.Get())
                        {
                            Volume volume = new Volume();
                            String devId = volumeDetail.GetPropertyValue("DeviceID").ToString();
                            if ((devId != null) && (devId != ""))
                            {
                                volume.letter = devId[0];

                                try
                                {
                                    if (volumeDetail.Properties["VolumeName"].Value != null)
                                    {
                                        volume.label = volumeDetail.Properties["VolumeName"].Value.ToString();
                                        if (volume.label == "")
                                            volume.label = "нет метки";
                                    }
                                }
                                catch { }

                                try { volume.fileSystem = volumeDetail.Properties["FileSystem"].Value != null ? volumeDetail.Properties["FileSystem"].Value.ToString() : ""; }
                                catch { }

                                try
                                {
                                    if (volumeDetail.Properties["Size"].Value != null)
                                    {
                                        Int64 capacity = Int64.Parse(volumeDetail.Properties["Size"].Value.ToString());
                                        volume.capacity = capacity >> 30;
                                    }
                                }
                                catch { }

                                try
                                {
                                    if (volumeDetail.Properties["FreeSpace"].Value != null)
                                    {
                                        Int64 free = Int64.Parse(volumeDetail.Properties["FreeSpace"].Value.ToString());
                                        volume.free = free >> 30;
                                    }
                                }
                                catch { }

                                // prepare chart series and status color
                                try
                                {
                                    double total = (double)volume.capacity;
                                    double freeGb = (double)volume.free;
                                    double usedGb = Math.Max(0.0, total - freeGb);

                                    var series = new SeriesCollection();
                                    series.Add(new PieSeries { Title = "Used", Values = new ChartValues<double> { usedGb }, Fill = new SolidColorBrush(Colors.OrangeRed), DataLabels = false, PushOut = 4 });
                                    series.Add(new PieSeries { Title = "Free", Values = new ChartValues<double> { freeGb }, Fill = new SolidColorBrush(Colors.LightGray), DataLabels = false });
                                    volume.ChartSeries = series;

                                    if (total <= 0)
                                        volume.StatusColor = Brushes.Gray;
                                    else if ((usedGb * 100.0 / total) > 90.0)
                                        volume.StatusColor = Brushes.Red;
                                    else if ((usedGb * 100.0 / total) > 75.0)
                                        volume.StatusColor = Brushes.Orange;
                                    else
                                        volume.StatusColor = Brushes.Green;
                                }
                                catch { }

                                volumesList.Add(volume);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о разделах жёсткого диска");
                    }

                    if (volumesList.Count > 0)
                    {
                        tcVolumes.Tag = pcName;
                        tcVolumes.SelectedIndex = 0;
                        tiVolumes.Visibility = Visibility.Visible;
                        tiVolumes.UpdateLayout();
                    }
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void tcVolumes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tcVolumes.Tag != null)
            {
                if (tcVolumes.SelectedItem != null)
                {
                    currentDir = "\\\\" + tcVolumes.Tag.ToString() + "\\" + tcVolumes.SelectedValue.ToString() + "$";
                    LoadDir(currentDir);
                    e.Handled = true;
                }
            }
        }

        private void LoadDir(String _dirPath)
        {
            filesList.Clear();

            try
            {
                DirectoryInfo currentDirInfo = new DirectoryInfo(_dirPath);
                if (_dirPath[_dirPath.Length - 1] != '$')
                    filesList.Add("..");
                foreach (DirectoryInfo childDirInfo in currentDirInfo.GetDirectories())
                    filesList.Add("[" + childDirInfo.Name + "]");
                foreach (FileInfo childFileInfo in currentDirInfo.GetFiles())
                    filesList.Add(" " + childFileInfo.Name + " ");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Загрузка списка файлов");
            }
        }

        private void lbFiles_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lbFiles.SelectedItem != null)
            {
                String selectedItem = lbFiles.SelectedItem.ToString();
                if (selectedItem != "..")
                    currentDir += "\\" + selectedItem.Remove(selectedItem.Length - 1, 1).Remove(0, 1);
                else
                {
                    String[] pathParts = currentDir.Split('\\');
                    currentDir = "\\";
                    for (Int32 i = 0; i < pathParts.Length - 1; i++)
                    {
                        currentDir += "\\" + pathParts[i];
                    }
                }
                LoadDir(currentDir);
            }

            e.Handled = true;
        }

        private void bnOpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (currentDir != "")
            {
                Process explorerProc = new Process();
                explorerProc.StartInfo.FileName = "explorer.exe";
                explorerProc.StartInfo.Arguments = "\"" + currentDir + "\"";
                explorerProc.Start();
            }
        }

        private delegate void UpdateProgressDelegate(Double _progress);
        private void UpdateProgress(Double _progress)
        {
            pbFileCopying.Value = _progress;
            pbFileCopying.UpdateLayout();
        }

        private delegate void HideProgressDelegate();
        private void HideProgress()
        {
            dpFileCopyProgress.Visibility = Visibility.Collapsed;
            bnUploadFile.Visibility = Visibility.Visible;
        }

        Boolean cancelFlag;

        private void bnUploadFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog();
            openFileDlg.Title = "Выберите файл для загрузки на удаленный ПК";
            if (openFileDlg.ShowDialog() == true)
            {
                FileStream srcFileStream = null;
                FileStream destFileStream = null;

                String srcFilePath = openFileDlg.FileName;
                String destFIlePath = currentDir + "\\" + Path.GetFileName(srcFilePath);

                try
                {
                    cancelFlag = false;

                    tbCopyingFileName.Text = srcFilePath;
                    dpFileCopyProgress.Visibility = Visibility.Visible;
                    bnUploadFile.Visibility = Visibility.Collapsed;

                    Byte[] buffer = new Byte[1024];

                    srcFileStream = new FileStream(srcFilePath, FileMode.Open);
                    destFileStream = new FileStream(destFIlePath, FileMode.Create);

                    Int64 fileSize = srcFileStream.Length;
                    Int64 totalBytes = 0;
                    Int32 currentBlockSize = 0;

                    new Thread(new ThreadStart(() =>
                        {
                            while ((currentBlockSize = srcFileStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                totalBytes += currentBlockSize;
                                Double percentage = (Double)totalBytes * 100.0 / fileSize;
                                destFileStream.Write(buffer, 0, currentBlockSize);

                                this.Dispatcher.BeginInvoke(new UpdateProgressDelegate(UpdateProgress), percentage);

                                if (cancelFlag == true)
                                {
                                    srcFileStream.Close();
                                    destFileStream.Close();

                                    if (File.Exists(destFIlePath))
                                        File.Delete(destFIlePath);

                                    new Message("Операции копирования отменена!");

                                    break;
                                }
                            }

                            if (cancelFlag == false)
                                new Messagegood("Файл успешно скопирован!");

                            this.Dispatcher.BeginInvoke(new HideProgressDelegate(HideProgress));

                            srcFileStream.Close();
                            destFileStream.Close();

                            cancelFlag = false;
                        })).Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Ошибка копирования файла");

                    srcFileStream.Close();
                    destFileStream.Close();

                    if (File.Exists(destFIlePath))
                        File.Delete(destFIlePath);
                }
            }
        }

        private void bnAbortFileCopying_Click(object sender, RoutedEventArgs e)
        {
            cancelFlag = true;
        }

        private void GetProcessesInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                processesList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_Process");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject processDetails in mgmtSrchr.Get())
                        {
                            RemoteProcess remoteProcess = new RemoteProcess();
                            remoteProcess.id = Int32.Parse(processDetails.Properties["ProcessId"].Value.ToString());
                            remoteProcess.caption = processDetails.Properties["Caption"].Value != null ? processDetails.Properties["Caption"].Value.ToString() : "";
                            if (processDetails.Properties["CreationDate"].Value != null)
                            {
                                String creationDateTime = processDetails.Properties["CreationDate"].Value.ToString();
                                String normalizeDateTime = creationDateTime.Substring(6, 2) + "." + creationDateTime.Substring(4, 2) + "." + creationDateTime.Substring(0, 4) + " " +
                                    creationDateTime.Substring(8, 2) + ":" + creationDateTime.Substring(10, 2) + ":" + creationDateTime.Substring(12, 2);
                                remoteProcess.creationDate = normalizeDateTime;
                            }
                            remoteProcess.executablePath = processDetails.Properties["ExecutablePath"].Value != null ? processDetails.Properties["ExecutablePath"].Value.ToString() : "";
                            remoteProcess.commandLine = processDetails.Properties["ExecutablePath"].Value != null ? processDetails.Properties["CommandLine"].Value.ToString() : "";

                            if (processesList.Count > 1)
                            {
                                if (processesList[processesList.Count - 1].caption.CompareTo(remoteProcess.caption) < 0)
                                    processesList.Insert(processesList.Count - 2, remoteProcess);
                                else
                                    processesList.Add(remoteProcess);
                            }
                            else
                                processesList.Add(remoteProcess);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о процессах");
                    }

                    if (processesList.Count > 0)
                    {
                        lvProcesses.Tag = pcName;
                        tiProcesses.Visibility = Visibility.Visible;
                        tiProcesses.UpdateLayout();
                    }
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void bnProcessKill_Click(object sender, RoutedEventArgs e)
        {
            if (lvProcesses.Tag != null)
            {
                if (MessageBox.Show("Завершить процесс?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        Process taskkillProc = new Process();
                        taskkillProc.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\psexec.exe";
                        taskkillProc.StartInfo.Arguments = "\\\\" + lvProcesses.Tag.ToString() + " taskkill /f /pid " + ((Button)sender).Tag.ToString();
                        taskkillProc.StartInfo.CreateNoWindow = true;
                        taskkillProc.Start();
                        taskkillProc.WaitForExit();

                        new Messagegood("Процесс завершен!");

                        GetProcessesInfo(lvProcesses.Tag.ToString());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Ошибка завершения процесса.");
                    }
                }
            }
        }

        private void bnRefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            if (lvProcesses.Tag != null)
            {
                GetProcessesInfo(lvProcesses.Tag.ToString());
            }
        }

        private void GetPrgTasksInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                remotePrgTasksList.Clear();

                if (Directory.Exists("\\\\" + _pcName + "\\d$"))
                {
                    foreach (App.PrgTask prgTask in App.prgTasksList)
                    {
                        DirectoryInfo remoteDiskD = new DirectoryInfo("\\\\" + pcName + "\\d$");
                        RemotePrgTask remotePrgTask = new RemotePrgTask();
                        remotePrgTask.prgTask = prgTask;
                        if (remoteDiskD.GetDirectories(prgTask.taskName).Length > 0)
                            remotePrgTask.isInstalled = true;
                        else
                            remotePrgTask.isInstalled = false;
                        remotePrgTasksList.Add(remotePrgTask);
                    }
                }

                if (remotePrgTasksList.Count > 0)
                {
                    lbPrgTasks.Tag = pcName;
                    tiPrgTasksInstall.Visibility = Visibility.Visible;
                    tiPrgTasksInstall.UpdateLayout();
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void bnInstallPrgTask_Click(object sender, RoutedEventArgs e)
        {
            String pcName = lbPrgTasks.Tag.ToString();

            RemotePrgTask remotePrgTask = (RemotePrgTask)((Button)sender).Tag;
            File.Copy(remotePrgTask.prgTask.installPath, Path.Combine("\\\\" + pcName + "\\c$", Path.GetFileName(remotePrgTask.prgTask.installPath)), true);

            Process p = new Process();
            p.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\PsExec.exe";
            p.StartInfo.Arguments = "\\\\" + pcName + " -i msiexec /q /i c:\\" + Path.GetFileName(remotePrgTask.prgTask.installPath);
            p.Start();

            p.WaitForExit();

            File.Delete(Path.Combine("\\\\" + pcName + "\\c$", Path.GetFileName(remotePrgTask.prgTask.installPath)));

            if (p.ExitCode == 0)
            {
                new Messagegood("Задача " + remotePrgTask.prgTask.taskName + " установлена.");
                remotePrgTask.isInstalled = true;
            }
            else
            {
                new Message("Ошибка при установке задачи " + remotePrgTask.prgTask.taskName + " (Ошибка №" + p.ExitCode.ToString("X8") + ")");
            }
        }

        private void bnUninstallPrgTask_Click(object sender, RoutedEventArgs e)
        {
            String pcName = lbPrgTasks.Tag.ToString();

            RemotePrgTask remotePrgTask = (RemotePrgTask)((Button)sender).Tag;
            File.Copy(remotePrgTask.prgTask.installPath, Path.Combine("\\\\" + pcName + "\\c$", Path.GetFileName(remotePrgTask.prgTask.installPath)), true);

            Process p = new Process();
            p.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\PsExec.exe";
            p.StartInfo.Arguments = "\\\\" + pcName + " -i msiexec /q /x c:\\" + Path.GetFileName(remotePrgTask.prgTask.installPath);
            p.Start();

            p.WaitForExit();

            File.Delete(Path.Combine("\\\\" + pcName + "\\c$", Path.GetFileName(remotePrgTask.prgTask.installPath)));

            if (p.ExitCode == 0)
            {
                new Messagegood("Задача " + remotePrgTask.prgTask.taskName + " удалена.");
                remotePrgTask.isInstalled = false;
            }
            else if (p.ExitCode == 0x0645)
            {
                new Message("Задача " + remotePrgTask.prgTask.taskName + " не установлена. Удаление не возможно.");
            }
            else
            {
                new Message("Ошибка при удалении задачи " + remotePrgTask.prgTask.taskName + " (Ошибка №" + p.ExitCode.ToString("X8") + ")");
            }
        }

        private void bnRefreshPrgTasks_Click(object sender, RoutedEventArgs e)
        {
            if (lbPrgTasks.Tag != null)
            {
                GetPrgTasksInfo(lbPrgTasks.Tag.ToString());
            }
        }

        private void GetPrintersInfo(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                printersList.Clear();

                if (mgmtScope != null)
                {
                    SelectQuery dskQuery = new SelectQuery("Win32_Printer");
                    try
                    {
                        ManagementObjectSearcher mgmtSrchr = new ManagementObjectSearcher(mgmtScope, dskQuery);
                        foreach (ManagementBaseObject printerDetails in mgmtSrchr.Get())
                        {
                            Boolean isShared = false;
                            HeaderedInfoSet printer = new HeaderedInfoSet();
                            printer.properties = new ObservableCollection<KeyValuePair<String, String>>();

                            printer.name = printerDetails.Properties["Name"].Value != null ? printerDetails.Properties["Name"].Value.ToString() : "";

                            try { printer.properties.Add(new KeyValuePair<String, String>("Драйвер", printerDetails.Properties["DriverName"].Value != null ? printerDetails.Properties["DriverName"].Value.ToString() : "")); }
                            catch { }

                            try { printer.properties.Add(new KeyValuePair<String, String>("Размещение", printerDetails.Properties["Location"].Value != null ? printerDetails.Properties["Location"].Value.ToString() : "")); }
                            catch { }

                            try { printer.properties.Add(new KeyValuePair<String, String>("Сетевой", printerDetails.Properties["Network"].Value != null ? printerDetails.Properties["Network"].Value.ToString() : "")); }
                            catch { }

                            try { printer.properties.Add(new KeyValuePair<String, String>("Порт", printerDetails.Properties["PortName"].Value != null ? printerDetails.Properties["PortName"].Value.ToString() : "")); }
                            catch { }

                            try
                            {
                                if (printerDetails.Properties["PrinterStatus"].Value != null)
                                {
                                    switch (printerDetails.Properties["PrinterStatus"].Value.ToString())
                                    {
                                        case "1":
                                            printer.properties.Add(new KeyValuePair<String, String>("Статус", "Другой"));
                                            break;
                                        case "2":
                                            printer.properties.Add(new KeyValuePair<String, String>("Статус", "Неизвестно"));
                                            break;
                                        case "3":
                                            printer.properties.Add(new KeyValuePair<String, String>("Статус", "Готов"));
                                            break;
                                        case "4":
                                            printer.properties.Add(new KeyValuePair<String, String>("Статус", "Печатает"));
                                            break;
                                        case "5":
                                            printer.properties.Add(new KeyValuePair<String, String>("Статус", "Прогревается"));
                                            break;
                                        case "6":
                                            printer.properties.Add(new KeyValuePair<String, String>("Статус", "Остановка печати"));
                                            break;
                                        case "7":
                                            printer.properties.Add(new KeyValuePair<String, String>("Статус", "Автономная работа"));
                                            break;
                                    }
                                }
                            }
                            catch { }

                            try
                            {
                                if (printerDetails.Properties["Shared"].Value != null)
                                    isShared = Boolean.Parse(printerDetails.Properties["Shared"].Value.ToString());
                            }
                            catch { }

                            try { printer.properties.Add(new KeyValuePair<String, String>("Имя для общего доступа", printerDetails.Properties["ShareName"].Value != null ? printerDetails.Properties["ShareName"].Value.ToString() : "")); }
                            catch { }

                            printersList.Add(printer);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Сведения о принтерах");
                    }

                    if (printersList.Count > 0)
                    {
                        tcPrinters.Tag = pcName;
                        tiPrinters.Visibility = Visibility.Visible;
                        tiPrinters.UpdateLayout();
                    }
                }
            }), DispatcherPriority.Background, _pcName);
        }

        void GetBSODData(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                bsodList.Clear();
                if (Directory.Exists("\\\\" + pcName + "\\admin$\\minidump"))
                {
                    DirectoryInfo minidumpDirInfo = new DirectoryInfo("\\\\" + _pcName + "\\admin$\\minidump");
                    foreach (FileInfo dumpFileInfo in minidumpDirInfo.GetFiles("*.dmp"))
                    {
                        bsodList.Add(dumpFileInfo.Name);
                    }
                }
                if (File.Exists("\\\\" + pcName + "\\admin$\\memory.dmp"))
                    bsodList.Add("memory.dmp");

                if (bsodList.Count > 0)
                {
                    lbBSOD.Tag = pcName;
                    tiBSODs.Visibility = Visibility.Visible;
                    tiBSODs.UpdateLayout();
                }
            }), DispatcherPriority.Background, _pcName);
        }

        private void bnViewBSOD_Click(object sender, RoutedEventArgs e)
        {
            if ((lbBSOD.Items.Count > 0) && (lbBSOD.Tag != null))
            {
                if (File.Exists(Environment.CurrentDirectory + "\\misc\\bsv\\remotepc.ini") && File.Exists(Environment.CurrentDirectory + "\\misc\\bsv\\BlueScreenView.exe"))
                {
                    FileStream remotePC_FS = new FileStream(Environment.CurrentDirectory + "\\misc\\bsv\\remotepc.ini", FileMode.Create);
                    StreamWriter sw = new StreamWriter(remotePC_FS);
                    sw.AutoFlush = true;
                    sw.Write(lbBSOD.Tag.ToString());

                    System.Threading.Thread.Sleep(1000);

                    sw.Close();
                    Process blueScreenView = new Process();
                    blueScreenView.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\bsv\\BlueScreenView.exe";
                    blueScreenView.StartInfo.WorkingDirectory = Environment.CurrentDirectory + "\\misc\\bsv";
                    blueScreenView.Start();
                }
                else
                    new Message("Программа BlueScreenView.exe или ее компонент не доступны.");
            }
        }

        void EnableUnavailablePCTools(Object _pcName)
        {
            Dispatcher.BeginInvoke(new PCMgmtDelegate((pcName) =>
            {
                tbWorkstation.Text = "ПК: " + pcName;

                stLoading.Visibility = Visibility.Collapsed;
                stSucceeded.Visibility = Visibility.Collapsed;
                stFailed.Visibility = Visibility.Visible;
                UserPhotoBorder.Visibility = Visibility.Visible;
                bnPingWorkstation.Visibility = Visibility.Visible;
                bnInstallRAdmin.Visibility = Visibility.Collapsed;
                bnConnectViaRAdmin.Visibility = Visibility.Collapsed;
                bnConnectViewRAdmin.Visibility = Visibility.Collapsed;
                bnConnectFileRAdmin.Visibility = Visibility.Collapsed;
                bnConnectViaRDP.Visibility = Visibility.Collapsed;
                bnCompMgmt.Visibility = Visibility.Collapsed;
            }), DispatcherPriority.Background, _pcName);
        }

        private Boolean CompareHashes(Byte[] _h1, Byte[] _h2)
        {
            if ((_h1 == null) || (_h2 == null))
                return false;

            for (Int32 i = 0; i < _h1.Length; i++)
            {
                if (_h1[i] != _h2[i])
                    return false;
            }
            return true;
        }

        private void tbSearchBySurname_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnSearchBySurname_Click(object sender, RoutedEventArgs e)
        {
            DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnClearSearchBySurname_Click(object sender, RoutedEventArgs e)
        {
            tbSearchAll.Text = "";
            tbSearchAll.Focus();
            activeSearchHash = new Byte[16];
        }

        private void tbSearchByID_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnSearchByID_Click(object sender, RoutedEventArgs e)
        {
            DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnClearSearchByID_Click(object sender, RoutedEventArgs e)
        {
            tbSearchAll.Text = "";
            tbSearchAll.Focus();
            activeSearchHash = new Byte[16];
        }

        private void tbSearchByPcName_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnSearchByPcName_Click(object sender, RoutedEventArgs e)
        {
            DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnClearSearchByPcName_Click(object sender, RoutedEventArgs e)
        {
            tbSearchAll.Text = "";
            tbSearchAll.Focus();
            activeSearchHash = new Byte[16];
        }

        private void tbSearchByPhone_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnSearchByPhone_Click(object sender, RoutedEventArgs e)
        {
            DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnClearSearchByPhone_Click(object sender, RoutedEventArgs e)
        {
            tbSearchAll.Text = "";
            tbSearchAll.Focus();
            activeSearchHash = new Byte[16];
        }

        private void tbSearchByIP_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnSearchByIP_Click(object sender, RoutedEventArgs e)
        {
            DoUnifiedSearch(tbSearchAll.Text);
        }

        private void bnClearSearchByIP_Click(object sender, RoutedEventArgs e)
        {
            tbSearchAll.Text = "";
            tbSearchAll.Focus();
            activeSearchHash = new Byte[16];
        }


        private void bnPingWorkstation_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag != null)
            {
                Process pingProcess = new Process();
                pingProcess.StartInfo.FileName = Environment.SystemDirectory + "\\ping.exe";
                pingProcess.StartInfo.Arguments = ((Button)sender).Tag.ToString() + " -t";
                pingProcess.Start();
            }
        }

        private void bnInstallRAdmin_Click(object sender, RoutedEventArgs e)
        {

        }

        private void bnTurnOnRAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (ra_svc != null)
            {
                try
                {
                    ra_svc.Start();
                    ra_svc.WaitForStatus(ServiceControllerStatus.Running);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    return;
                }

                bnTurnOnRAdmin.Visibility = Visibility.Collapsed;
                bnConnectViaRAdmin.Visibility = Visibility.Visible;
                bnConnectViewRAdmin.Visibility = Visibility.Visible;
                bnConnectFileRAdmin.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Служба RAdmin не найдена на целевом ПК!");
            }
        }

        private void bnConnectViaRAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag != null)
            {
                if (File.Exists("c:\\Program Files (x86)\\Radmin Viewer 3\\radmin.exe"))
                {
                    Process pingProcess = new Process();
                    pingProcess.StartInfo.FileName = "c:\\Program Files (x86)\\Radmin Viewer 3\\radmin.exe";
                    pingProcess.StartInfo.Arguments = "/connect:" + ((Button)sender).Tag.ToString() + ":4098";
                    pingProcess.Start();
                }
                else
                    MessageBox.Show("Программа RAdmin не установлена в системе!");
            }
        }

        private void bnConnectViewRAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag != null)
            {
                if (File.Exists("c:\\Program Files (x86)\\Radmin Viewer 3\\radmin.exe"))
                {
                    Process pingProcess = new Process();
                    pingProcess.StartInfo.FileName = "c:\\Program Files (x86)\\Radmin Viewer 3\\radmin.exe";
                    pingProcess.StartInfo.Arguments = "/connect:" + ((Button)sender).Tag.ToString() + ":4098 /noinput";
                    pingProcess.Start();
                }
                else
                    MessageBox.Show("Программа RAdmin не установлена в системе!");
            }
        }

        private void bnConnectFileRAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag != null)
            {
                if (File.Exists("c:\\Program Files (x86)\\Radmin Viewer 3\\radmin.exe"))
                {
                    Process pingProcess = new Process();
                    pingProcess.StartInfo.FileName = "c:\\Program Files (x86)\\Radmin Viewer 3\\radmin.exe";
                    pingProcess.StartInfo.Arguments = "/connect:" + ((Button)sender).Tag.ToString() + ":4098 /file";
                    pingProcess.Start();
                }
                else
                    MessageBox.Show("Программа RAdmin не установлена в системе!");
            }
        }

        private void bnConnectViaRDP_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag != null)
            {
                Process pingProcess = new Process();
                pingProcess.StartInfo.FileName = "c:\\windows\\system32\\mstsc.exe";
                pingProcess.StartInfo.Arguments = "/v:" + ((Button)sender).Tag.ToString();
                pingProcess.Start();
            }
        }

        private void bnCompMgmt_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag != null)
            {
                Process compMgmtProcess = new Process();
                compMgmtProcess.StartInfo.FileName = "c:\\windows\\system32\\mmc.exe";
                compMgmtProcess.StartInfo.Arguments = "c:\\windows\\system32\\compmgmt.msc /computer:\\\\" + ((Button)sender).Tag.ToString();
                compMgmtProcess.Start();
            }

        }

        private void tbRemoteCmd_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                bnSendRemoteCmd_Click(null, null);
        }

        private void bnSendRemoteCmd_Click(object sender, RoutedEventArgs e)
        {
            if (bnSendRemoteCmd.Tag != null)
            {
                Process pcExecProc = new Process();
                pcExecProc.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\psexec.exe";
                pcExecProc.StartInfo.Arguments = "\\\\" + bnSendRemoteCmd.Tag.ToString() + " " + tbRemoteCmd.Text.Replace("\\", "\\\\");
                pcExecProc.StartInfo.UseShellExecute = false;
                pcExecProc.StartInfo.CreateNoWindow = false;
                pcExecProc.StartInfo.RedirectStandardOutput = true;
                pcExecProc.StartInfo.RedirectStandardError = true;
                pcExecProc.Start();
                pcExecProc.WaitForExit();
                tbOutput.Text = pcExecProc.StandardOutput.ReadToEnd();
                tbOutput.Text += "\n\n" + pcExecProc.StandardError.ReadToEnd();
            }
        }

        private void bnGPUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (bnGPUpdate.Tag != null)
            {
                Process gpupdateProc = new Process();
                gpupdateProc.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\psexec.exe";
                gpupdateProc.StartInfo.Arguments = "\\\\" + bnTimeSync.Tag.ToString() + " gpupdate /force";
                gpupdateProc.StartInfo.CreateNoWindow = true;
                try
                {
                    gpupdateProc.Start();
                    gpupdateProc.WaitForExit();
                    MessageBox.Show("Групповые политики обновлены успешно!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Ошибка при обновление групповых политик");
                }
            }
        }

        private void bnRemotePrgLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (bnRemotePrgLaunch.Tag != null)
            {
                Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog();
                openFileDlg.Title = "Выберите файл для запуска на удаленном ПК";
                if (openFileDlg.ShowDialog() == true)
                {
                    String filePath = openFileDlg.FileName;
                    if (filePath[0] != '\\')
                        filePath = "\\\\" + Dns.GetHostName() + "\\" + filePath.Replace(":", "$");

                    Process remotePrgProc = new Process();
                    remotePrgProc.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\psexec.exe";
                    remotePrgProc.StartInfo.Arguments = "-i \\\\" + bnRemotePrgLaunch.Tag.ToString() + " \"" + filePath + "\"";

                    try
                    {
                        remotePrgProc.Start();
                        MessageBox.Show("Программа запущена!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Ошибка при запуске программы");
                    }
                }
            }
        }

        private void bnTimeSync_Click(object sender, RoutedEventArgs e)
        {
            if (bnTimeSync.Tag != null)
            {
                Process timeSyncProc = new Process();
                timeSyncProc.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\psexec.exe";
                timeSyncProc.StartInfo.Arguments = "\\\\" + bnTimeSync.Tag.ToString() + " net time \\\\172.16.1.184 /set /y";
                timeSyncProc.StartInfo.CreateNoWindow = true;
                try
                {
                    timeSyncProc.Start();
                    timeSyncProc.WaitForExit();
                    MessageBox.Show("Синхронизация прошла успешно!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Ошибка при синхронизации");
                }
            }
        }

        private void bnSpoolerRestart_Click(object sender, RoutedEventArgs e)
        {
            if (bnSpoolerRestart.Tag != null)
            {
                Process spoolerStopProc = new Process();
                spoolerStopProc.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\psexec.exe";
                spoolerStopProc.StartInfo.Arguments = "\\\\" + bnSpoolerRestart.Tag.ToString() + " net stop spooler";
                spoolerStopProc.StartInfo.CreateNoWindow = true;
                Process spoolerStartProc = new Process();
                spoolerStartProc.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\psexec.exe";
                spoolerStartProc.StartInfo.Arguments = "\\\\" + bnSpoolerRestart.Tag.ToString() + " net start spooler";
                spoolerStartProc.StartInfo.CreateNoWindow = true;
                try
                {
                    spoolerStopProc.Start();
                    spoolerStopProc.WaitForExit();
                    spoolerStartProc.Start();
                    spoolerStartProc.WaitForExit();
                    MessageBox.Show("Служба печати перезапущена!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Ошибка при перезапуске службы печати");
                }
            }
        }

        private void bnShutdownRemotePC_Click(object sender, RoutedEventArgs e)
        {
            if (bnShutdownRemotePC.Tag != null)
            {
                Process timeSyncProc = new Process();
                timeSyncProc.StartInfo.FileName = Environment.CurrentDirectory + "\\misc\\psexec.exe";
                timeSyncProc.StartInfo.Arguments = "\\\\" + bnTimeSync.Tag.ToString() + " shutdown -s -t 0";
                timeSyncProc.StartInfo.CreateNoWindow = true;
                try
                {
                    timeSyncProc.Start();
                    timeSyncProc.WaitForExit();
                    MessageBox.Show("Компьютер выключен!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Ошибка при выключении ПК");
                }
            }
        }

        #region Для вкладки "Группы"

        private void tbSearchGroupByName_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                bnSearchGroupByName_Click(null, null);
        }

        private void bnSearchGroupByName_Click(object sender, RoutedEventArgs e)
        {
            if (tbSearchGroupByName.Text.Trim() != "")
            {
                MD5CryptoServiceProvider groupNameMD5 = new MD5CryptoServiceProvider();
                String groupName = tbSearchGroupByName.Text.Trim().ToUpper();
                Byte[] groupName_Hash = groupNameMD5.ComputeHash(Encoding.UTF8.GetBytes(groupName));

                foreach (HashTableItem hashTableItem in App.groupNameHashTable)
                {
                    if (CompareHashes(groupName_Hash, hashTableItem.hash) == true)
                    {
                        lbGroups.SelectedIndex = hashTableItem.trace[0];
                        lbGroups.ScrollIntoView(lbGroups.SelectedItem);
                        return;
                    }
                }

                MessageBox.Show("Группа не найдена.");
            }
        }

        private void lbGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lvProperties.Items.Clear();
            foreach (PropertyValueCollection property in ((ADContentBase)lbGroups.SelectedItem).entry.Properties)
            {
                if ((property.PropertyName == "objectSid") && (property.Value is Array))
                {
                    Byte[] SID_array = (Byte[])property.Value;

                    UInt32 sid1 = (UInt32)(SID_array[12] | SID_array[13] << 8 | SID_array[14] << 16 | SID_array[15] << 24);
                    UInt32 sid2 = (UInt32)(SID_array[16] | SID_array[17] << 8 | SID_array[18] << 16 | SID_array[19] << 24);
                    UInt32 sid3 = (UInt32)(SID_array[20] | SID_array[21] << 8 | SID_array[22] << 16 | SID_array[23] << 24);
                    UInt32 sid4 = (UInt32)(SID_array[24] | SID_array[25] << 8 | SID_array[26] << 16 | SID_array[27] << 24);
                    String SID_str = "S-" + SID_array[0].ToString() + "-" + SID_array[1].ToString() + "-" + SID_array[8].ToString() + "-" + sid1 + "-" + sid2 + "-" + sid3 + "-" + sid4;
                    property.Value = SID_str;
                }
                lvProperties.Items.Add(property);
            }
        }

        #endregion Для вкладки "Группы"

        #region Для вкладки "Компьютеры"

        private void tbSearchComputerByName_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                bnSearchComputerByName_Click(null, null);
        }

        private void bnSearchComputerByName_Click(object sender, RoutedEventArgs e)
        {
            if (tbSearchComputerByName.Text.Trim() != "")
            {
                MD5CryptoServiceProvider computerNameMD5 = new MD5CryptoServiceProvider();
                String computerName = tbSearchComputerByName.Text.Trim().ToUpper();
                Byte[] computerName_Hash = computerNameMD5.ComputeHash(Encoding.UTF8.GetBytes(computerName));

                foreach (HashTableItem hashTableItem in App.computerNameHashTable)
                {
                    if (CompareHashes(computerName_Hash, hashTableItem.hash) == true)
                    {
                        lbComputers.SelectedIndex = hashTableItem.trace[0];
                        lbComputers.ScrollIntoView(lbComputers.SelectedItem);
                        return;
                    }
                }

                MessageBox.Show("Компьютер не найден.");
            }
        }

        private void lbComputers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lvProperties.Items.Clear();
            foreach (PropertyValueCollection property in ((ADContentBase)lbComputers.SelectedItem).entry.Properties)
            {
                if ((property.PropertyName == "objectSid") && (property.Value is Array))
                {
                    Byte[] SID_array = (Byte[])property.Value;

                    UInt32 sid1 = (UInt32)(SID_array[12] | SID_array[13] << 8 | SID_array[14] << 16 | SID_array[15] << 24);
                    UInt32 sid2 = (UInt32)(SID_array[16] | SID_array[17] << 8 | SID_array[18] << 16 | SID_array[19] << 24);
                    UInt32 sid3 = (UInt32)(SID_array[20] | SID_array[21] << 8 | SID_array[22] << 16 | SID_array[23] << 24);
                    UInt32 sid4 = (UInt32)(SID_array[24] | SID_array[25] << 8 | SID_array[26] << 16 | SID_array[27] << 24);
                    String SID_str = "S-" + SID_array[0].ToString() + "-" + SID_array[1].ToString() + "-" + SID_array[8].ToString() + "-" + sid1 + "-" + sid2 + "-" + sid3 + "-" + sid4;
                    property.Value = SID_str;
                }
                lvProperties.Items.Add(property);
            }
        }

        #endregion Для вкладки "Компьютеры"

        private void bnToggleProperties_Click(object sender, RoutedEventArgs e)
        {
            if (lvProperties.Visibility == Visibility.Visible)
            {
                lvProperties.Visibility = Visibility.Collapsed;
                bnToggleProperties.Content = "<";
                bnToggleProperties.ToolTip = "Отобразить дополнительные свойства объекта";
            }
            else
            {
                lvProperties.Visibility = Visibility.Visible;
                bnToggleProperties.Content = ">";
                bnToggleProperties.ToolTip = "Скрыть дополнительные свойства объекта";
            }
        }

        private void bnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        
    }
}