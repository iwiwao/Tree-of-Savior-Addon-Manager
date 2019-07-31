using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Newtonsoft.Json;
using System.Net.Http;

namespace ToSAddonManager {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        internal List<addonDataFromRepo> listOfAllAddons = new List<addonDataFromRepo>(); // sigh..
        //internal List<addonDataFromRepoAPI> listofAllAddonsAPI = new List<addonDataFromRepoAPI>();
        internal List<installedAddons> listOfInstalledAddons = new List<installedAddons>();
        internal programSettings tosAMProgramSettings = new programSettings(); // Just storing the ToS directory for now.
        static internal readonly HttpClient webConnector = new HttpClient();

        public MainWindow() {
            InitializeComponent();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            try {
                Version semanticVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string semanticVersionStr = $"v{semanticVersion.Major}.{semanticVersion.Minor}.{semanticVersion.Build}";
                this.Title = $"Iwiwao's ToS Addon Manager {semanticVersionStr}";

                // Set initial HttpClient values as needed to connect to Github API.
                System.Net.Http.Headers.ProductHeaderValue header = new System.Net.Http.Headers.ProductHeaderValue("IwiwaoToSAddonManager", semanticVersionStr);
                System.Net.Http.Headers.ProductInfoHeaderValue userAgent = new System.Net.Http.Headers.ProductInfoHeaderValue(header);
                webConnector.DefaultRequestHeaders.UserAgent.Add(userAgent);

                if (System.IO.File.Exists("installedAddons.json")) { listOfInstalledAddons = JsonConvert.DeserializeObject<List<installedAddons>>(System.IO.File.ReadAllText("installedAddons.json")); } // If there is saved installed addon list, load it.
                if (System.IO.File.Exists("completeAddonList.json")) { listOfAllAddons = JsonConvert.DeserializeObject<List<addonDataFromRepo>>(System.IO.File.ReadAllText("completeAddonList.json")); } // If there is cache data, load it.
                //if (System.IO.File.Exists("completeAddonListAPI.json")) { listofAllAddonsAPI = JsonConvert.DeserializeObject<List<addonDataFromRepoAPI>>(System.IO.File.ReadAllText("completeAddonListAPI.json")); }
                if (System.IO.File.Exists("programSettings.json")) { tosAMProgramSettings = JsonConvert.DeserializeObject<programSettings>(System.IO.File.ReadAllText("programSettings.json")); } // If there is a saved settings file, load it.
                displayActiveGrid("iToS"); displayActiveGrid("jToS");
                if (tosAMProgramSettings.checkForUpdates) { AllowAutoCheck.IsChecked = true; checkForUpdates(null, null); }
            } catch (Exception ex) {
                Common.showError("Program Load Error", ex);
            }
        } // end MainWindow_Loaded

        private void updateForTaskProgress(taskProgressMsg progress) {
            statusBar1TextBlock.Text = progress.currentMsg;
            if (progress.showAsPopup) { Common.showError("Error", progress.exceptionContent); }
        } // end updateForTaskProgress

        #region "Menu and TB Items"
        private void exitButtonClicked(object sender, RoutedEventArgs e) {
            System.Windows.Application.Current.Shutdown();
        } // End exitButtonClicked

        private async void MenuItemUpdateCache_Click(object sender, RoutedEventArgs e) {
            try {
                if (string.IsNullOrEmpty(tosAMProgramSettings.tosRootDir) || !System.IO.Directory.Exists(tosAMProgramSettings.tosRootDir)) { MessageBox.Show("Please set a valid ToS Program directory (Required for dependancy download)"); return; }
                MenuItemUpdateCache.IsEnabled = false;
                statusBar1TextBlock.Text = "Started Cache Update";
                Progress<taskProgressMsg> progressMessages = new Progress<taskProgressMsg>(updateForTaskProgress); // Will contain the progress messages from each function.

                repoCacheManagement rCM = new repoCacheManagement() { rootDir = tosAMProgramSettings.tosRootDir, webConnector = webConnector };

                (List<addonDataFromRepo>, List<addonDataFromRepoAPI>) iToSCollections = await rCM.callParentUpdateCache(progressMessages, 0); // iToS 
                iToSCollections.Item1.Select(x => { x.whichRepo = "iToS"; return x; }).ToList();

                (List<addonDataFromRepo>, List<addonDataFromRepoAPI>) jToSCollections = await rCM.callParentUpdateCache(progressMessages, 1); // jToS
                jToSCollections.Item1.Select(x => { x.whichRepo = "jToS"; return x; }).ToList();
                
                await rCM.callParentUpdateCache(progressMessages, 2); // Dependencies - does not care about return values.

                listOfAllAddons.Clear(); listOfAllAddons = iToSCollections.Item1.Concat(jToSCollections.Item1).ToList();
                //listofAllAddonsAPI.Clear(); listofAllAddonsAPI = iToSCollections.Item2.Concat(jToSCollections.Item2).ToList();

                saveCacheDataToFile(true, progressMessages);

                displayActiveGrid("iToS"); displayActiveGrid("jToS");  // Update the tabs.
            } catch (Exception ex) {
                Common.showError("Update Cache Error", ex);
            } finally {
                MenuItemUpdateCache.IsEnabled = true;
            }
        } // End MenuItemUpdateCache_Click

        private void MenuItemSelectToSDir_Click(object sender, RoutedEventArgs e) {
            // WPF needs an Directory selector.. :<
            Microsoft.Win32.OpenFileDialog fd = new Microsoft.Win32.OpenFileDialog { FileName = "Selet your ToS directory", Title = "Select your ToS directroy and hit OK", ValidateNames = false, CheckFileExists = false, CheckPathExists = true };
            if (fd.ShowDialog() == true) {
                string fullPath = System.IO.Path.GetDirectoryName(fd.FileName);
                if (System.IO.Directory.Exists(fullPath + "/addons") && System.IO.Directory.Exists(fullPath + "/data")) {
                    tosAMProgramSettings.tosRootDir = fullPath; System.IO.File.WriteAllText("programSettings.json", JsonConvert.SerializeObject(tosAMProgramSettings));
                } else {
                    MessageBox.Show("Tree of Savior directory selection was not valid");
                }
            }
        } // end MenuItemSelectToSDir_Click

        private void filterTBKeyDownHandler(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return) { displayActiveGrid("iToS"); displayActiveGrid("jToS"); }
        } // end filterTBKeyDownHandler

        private async void checkForUpdates(object sender, RoutedEventArgs e) {
            try {
                Version semanticVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string semanticVersionStr = $"{semanticVersion.Major}.{semanticVersion.Minor}.{semanticVersion.Build}";
                HttpResponseMessage webConnectorResponse = await webConnector.GetAsync("https://api.github.com/repos/iwiwao/Tree-of-Savior-Addon-Manager/releases/latest");
                webConnectorResponse.EnsureSuccessStatusCode();
                string resultString = await webConnectorResponse.Content.ReadAsStringAsync();
                addonDataFromRepoAPI ToSProgramInfo = JsonConvert.DeserializeObject<addonDataFromRepoAPI>(resultString);
                webConnectorResponse.Dispose();
                Version availableVersion = Version.Parse(ToSProgramInfo.Name.Replace("v", ""));
                string rootMsg = $"Current Version: {semanticVersionStr}{Environment.NewLine}Latest Version: {ToSProgramInfo.Name}{Environment.NewLine}{Environment.NewLine}";
                if (semanticVersion < availableVersion) {
                    MessageBoxResult mbr = MessageBox.Show($"{rootMsg}Would you like to open a webbrowser to download the latest version?{Environment.NewLine}{Environment.NewLine}Changes:{Environment.NewLine}{ToSProgramInfo.Body}", "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                    if (mbr == MessageBoxResult.Yes) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/iwiwao/Tree-of-Savior-Addon-Manager/releases")); }
                } else {
                    if (sender != null) { MessageBox.Show($"{rootMsg}Looks like you are up to date.", "All Good", MessageBoxButton.OK, MessageBoxImage.Information); } // Sender will be null when called from program launch, where we do not want to show this box.
                }
            } catch (Exception ex) {
                Common.showError("Check for Updates Error", ex);
            }
        } // end checkForUpdates

        private void allowAutomaticUpdatesCheckChanged(object sender, RoutedEventArgs e) {
            try {
                tosAMProgramSettings.checkForUpdates = AllowAutoCheck.IsChecked;
                System.IO.File.WriteAllText("programSettings.json", JsonConvert.SerializeObject(tosAMProgramSettings));
            } catch (Exception ex) {
                Common.showError("Allow Automatic Update Check Changed Error", ex);
            }
        }
        #endregion

        private void saveInstalledAddonDataToFile() {
            try {
                if (listOfInstalledAddons.Count > 0) {
                    System.IO.File.WriteAllText("installedAddons.json", JsonConvert.SerializeObject(listOfInstalledAddons));
                } else {
                    System.IO.File.Delete("installedAddons.json");
                }
            } catch (Exception ex) {
                Common.showError("Save Installed Addon Data To File Error", ex);
            }
        } // end saveInstallDataToFile

        private void saveCacheDataToFile(bool purgeFile, IProgress<taskProgressMsg> progressMessages) {
            try {
                if (purgeFile) {
                    System.IO.File.WriteAllText("completeAddonList.json", JsonConvert.SerializeObject(listOfAllAddons));
                    //System.IO.File.WriteAllText("completeAddonListAPI.json", JsonConvert.SerializeObject(listofAllAddonsAPI));
                } else {
                    System.IO.File.AppendAllText("completeAddonList.json", JsonConvert.SerializeObject(listOfAllAddons));
                    //System.IO.File.AppendAllText("completeAddonListAPI.json", JsonConvert.SerializeObject(listofAllAddonsAPI));
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in saveCacheDataToFile", showAsPopup = true, exceptionContent = ex });
            }
        } // end saveCacheDataToFile

        #region "WrapPanel Setup and Control"
        private void displayActiveGrid(string selectedTab) {
            ItemsControl ic = null;
            // Do we bother creating the filtered list, or just filter the main list in the following loop?
            List<addonDataFromRepo> filteredAddonList = string.IsNullOrEmpty(filterTB.Text) ? listOfAllAddons.Where(x => x.whichRepo == selectedTab).OrderBy(x => x.Name).ToList() : listOfAllAddons.Where(x => x.whichRepo == selectedTab && x.filterCheck.Contains(filterTB.Text.ToLower())).OrderBy(x => x.Name).ToList();
            if (selectedTab == "iToS") { ic = iToSIC; } else if (selectedTab == "jToS") { ic = jToSIC; }
            if (ic == null) { return; } // Wait, what?

            List<addonDisplayData> addonDisplayList = new List<addonDisplayData>();
            foreach (addonDataFromRepo a in filteredAddonList) {
                addonDisplayData q = new addonDisplayData() { name = a.Name, availableVersion = a.FileVersion, description = a.Description, installStatusColor = Brushes.White, whichRepo = a.whichRepo };
                string[] aR = a.authorRepo.Split('/'); q.author = $"by {aR[0]}";
                q.authorRepoUri = new Hyperlink(new Run(a.authorRepo)) { NavigateUri = new Uri($"https://github.com/{a.authorRepo}") };
                q.authorRepoUri.RequestNavigate += new RequestNavigateEventHandler(delegate (object sender, RequestNavigateEventArgs e) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)); e.Handled = true; });

                installedAddons ia = listOfInstalledAddons.FirstOrDefault(x => x.addonName == a.Name && x.addonRepo == a.whichRepo); // Check if this addon is installed.
                if (ia != null) { // Addon is installed.  
                    q.installedVersion = $"Installed: {ia.addonVersion} on {ia.installDate.ToShortDateString()}";
                    Version curVersion = new Version(); Version.TryParse(a.FileVersion.Replace("v", ""), out curVersion); // See if it's the version matches.
                    Version installedVersion = new Version(); Version.TryParse(ia.addonVersion.Replace("v", ""), out installedVersion);
                    q.installStatusColor = curVersion.CompareTo(installedVersion) > 0 ? Brushes.LightYellow : Brushes.LightGreen;
                }
                addonDisplayList.Add(q);
            }
            ic.ItemsSource = addonDisplayList;
        } // end displayActiveGrid

        private void mouseDoubleClickAction(object sender, MouseButtonEventArgs e) {
            try {
                if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) {
                    if (string.IsNullOrEmpty(tosAMProgramSettings.tosRootDir) || !System.IO.Directory.Exists(tosAMProgramSettings.tosRootDir)) { MessageBox.Show("Please set a valid ToS Program directory"); return; }
                    Border c = (Border)sender;
                    addonDisplayData addon = (addonDisplayData)c.DataContext;
                    addonDataFromRepo selectedAddon = listOfAllAddons.FirstOrDefault(x => x.whichRepo == addon.whichRepo && x.Name == addon.name);
                    if (selectedAddon != null) {
                        addonInfo addonInfoWin = new addonInfo { addonData = selectedAddon, installedAddonData = listOfInstalledAddons, rootDir = tosAMProgramSettings.tosRootDir, webConnector = webConnector, Owner = this };
                        addonInfoWin.ShowDialog();
                        // The popup window can update the installed Addon list, so we need to update our List<> and cache file, and then re-process the display.
                        listOfInstalledAddons = addonInfoWin.installedAddonData;
                        saveInstalledAddonDataToFile();
                        displayActiveGrid("iToS"); displayActiveGrid("jToS");
                        addonInfoWin = null; // WPF should clean up all resources, so this is probably pointless.
                    }
                }
            } catch (Exception ex) {
                Common.showError("Canvas DoubleClick", ex);
            }
        } // end mouseDoubleClickAction
        #endregion
    }
} // End Class
