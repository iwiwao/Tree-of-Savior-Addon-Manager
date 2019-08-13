using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Newtonsoft.Json;
using System.Net.Http;

namespace ToSAddonManager {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        internal List<addonDataFromRepo> listOfAllAddons = new List<addonDataFromRepo>(); // sigh..
        internal List<installedAddons> listOfInstalledAddons = new List<installedAddons>();
        internal List<brokenAddons> listOfBrokenAddons = new List<brokenAddons>();
        internal List<addonInstallerOverride> listOfAddonOverrides = new List<addonInstallerOverride>();
        internal programSettings tosAMProgramSettings = new programSettings();
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
                if (System.IO.File.Exists("programSettings.json")) { tosAMProgramSettings = JsonConvert.DeserializeObject<programSettings>(System.IO.File.ReadAllText("programSettings.json")); } // If there is a saved settings file, load it.
                if (System.IO.File.Exists("brokenAddonList.json")) { listOfBrokenAddons = JsonConvert.DeserializeObject<List<brokenAddons>>(System.IO.File.ReadAllText("brokenAddonList.json")); } // You get the idea...
                if (System.IO.File.Exists("addonOverrides.json")) { listOfAddonOverrides = JsonConvert.DeserializeObject<List<addonInstallerOverride>>(System.IO.File.ReadAllText("addonOverrides.json")); }
                displayActiveGrid();
                if (tosAMProgramSettings.checkForUpdates) { AllowAutoCheck.IsChecked = true; checkForUpdates(null, null); }
            } catch (Exception ex) {
                Common.showError("Program Load Error", ex);
            }
        } // end MainWindow_Loaded

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            try {
                System.IO.File.WriteAllText("programSettings.json", JsonConvert.SerializeObject(tosAMProgramSettings));
            } catch (Exception ex) {
                Common.showError("MainWindow Closing Error", ex);
            }
        } // end MainWindow_Closing

        private void updateForTaskProgress(taskProgressMsg progress) {
            statusBar1TextBlock.Text = progress.currentMsg;
            if (progress.showAsPopup) { Common.showError(progress.currentMsg, progress.exceptionContent); }
        } // end updateForTaskProgress

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
                } else {
                    System.IO.File.AppendAllText("completeAddonList.json", JsonConvert.SerializeObject(listOfAllAddons));
                }
                System.IO.File.WriteAllText("brokenAddonList.json", JsonConvert.SerializeObject(listOfBrokenAddons));
                System.IO.File.WriteAllText("addonOverrides.json", JsonConvert.SerializeObject(listOfAddonOverrides));
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in saveCacheDataToFile", showAsPopup = true, exceptionContent = ex });
            }
        } // end saveCacheDataToFile

        #region "Menu and TB Items"
        private void exitButtonClicked(object sender, RoutedEventArgs e) {
            System.Windows.Application.Current.Shutdown();
        } // End exitButtonClicked

        private async void MenuItemUpdateCache_Click(object sender, RoutedEventArgs e) {
            try {
                MenuItemUpdateCache.IsEnabled = false;
                statusBar1TextBlock.Text = "Started Cache Update";
                Progress<taskProgressMsg> progressMessages = new Progress<taskProgressMsg>(updateForTaskProgress); // Will contain the progress messages from each function.

                if (System.IO.File.Exists("completeAddonList.json")) { tosAMProgramSettings.previousUpdateDateStampUTC = new System.IO.FileInfo("completeAddonList.json").LastWriteTimeUtc; } else { tosAMProgramSettings.previousUpdateDateStampUTC = DateTime.MinValue; } // Store previous update time for "What's new" option.

                repoCacheManagement rCM = new repoCacheManagement() { rootDir = tosAMProgramSettings.tosRootDir, webConnector = webConnector };

                List<addonDataFromRepo> iToSCollections = await rCM.callParentUpdateCache(progressMessages, 0); // iToS 
                iToSCollections.Select(x => { x.whichRepo = "iToS"; return x; }).ToList();

                List<addonDataFromRepo> jToSCollections = await rCM.callParentUpdateCache(progressMessages, 1); // jToS
                jToSCollections.Select(x => { x.whichRepo = "jToS"; return x; }).ToList();

                listOfBrokenAddons = await rCM.returnBrokenAddonData(progressMessages); // Download list of broken addons.
                listOfAddonOverrides = await rCM.returnAddonInstallerOverride(progressMessages); // Return manually-maintained list of addon overrides.

                listOfAllAddons.Clear(); listOfAllAddons = iToSCollections.Concat(jToSCollections).ToList();

                saveCacheDataToFile(true, progressMessages);
                statusBar1TextBlock.Text = "Completed all Cache Update functions.";

                displayActiveGrid(); // Update the active tab.
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
                    if (Common.checkForToSDirectory(tosAMProgramSettings.tosRootDir)) { tosAMProgramSettings.tosRootDir = fullPath; }
                } else {
                    MessageBox.Show("Tree of Savior directory selection was not valid");
                }
            }
        } // end MenuItemSelectToSDir_Click

        private async void MenuItemUpdateDeps_Click(object sender, RoutedEventArgs e) {
            try {
                if (Common.checkForToSDirectory(tosAMProgramSettings.tosRootDir) == false) { MessageBox.Show("Please set a valid ToS Program directory."); return; }
                statusBar1TextBlock.Text = "Starting Dependency download functions.";
                Progress<taskProgressMsg> progressMessages = new Progress<taskProgressMsg>(updateForTaskProgress); // Will contain the progress messages from each function.
                repoCacheManagement rCM = new repoCacheManagement() { rootDir = tosAMProgramSettings.tosRootDir, webConnector = webConnector };
                await rCM.checkAndInstallDependencies(progressMessages); // Dependencies - does not care about return values.
                statusBar1TextBlock.Text = "Completed Dependency download functions.";
            } catch (Exception ex) {
                Common.showError("Update Dependency Error", ex);
            }
        } // end MenuItemUpdateDeps_Click

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
            } catch (Exception ex) {
                Common.showError("Allow Automatic Update Check Changed Error", ex);
            }
        }

        private void FindExistingAddons_Click(object sender, RoutedEventArgs e) {
            try {
                if (Common.checkForToSDirectory(tosAMProgramSettings.tosRootDir) == false) { MessageBox.Show("Please set a valid ToS Program directory."); return; }
                MessageBoxResult mb = MessageBox.Show("This will attempt to find addons that were previously installed manually or through another manager and update the installed addon list.  Proceed?", "Find Existing Addons", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (mb == MessageBoxResult.Yes) {
                    string[] fileList = System.IO.Directory.GetFiles($"{tosAMProgramSettings.tosRootDir}/data/", "_*.ipf");
                    AddonManagement am = new AddonManagement();
                    int z = 0;
                    foreach (string q in fileList) {
                        System.IO.FileInfo fI = new System.IO.FileInfo(q);
                        string[] fileNameSplit = fI.Name.Split('-'); // 0 will be root of the filename, with _ at the start.  1 will be the unicode char.  2 will be version + ".ipf"
                        string fN = fileNameSplit[0].Replace("_", "");
                        string fV = fileNameSplit[2].Replace(".ipf", "");
                        if (listOfInstalledAddons.FirstOrDefault(x => x.addonFilename == fN && x.addonVersion == fV) == null) { // Addon is not installed from this manager.
                            addonDataFromRepo foundAddon = listOfAllAddons.FirstOrDefault(i => i.File == fN && i.Unicode == fileNameSplit[1] && i.FileVersion == fV);
                            if (foundAddon != null) {
                                am.installedAddonData = listOfInstalledAddons; am.addonData = foundAddon;
                                am.updateInstalledAddonList(0);
                                listOfInstalledAddons = am.installedAddonData;
                            }
                        }
                        z++;
                    }
                    if (z > 0) {
                        saveInstalledAddonDataToFile();
                        displayActiveGrid();
                        MessageBox.Show($"Found {z} existing addons and added them to the list of installed addons", "Found Addons", MessageBoxButton.OK, MessageBoxImage.Information);
                    } else {
                        MessageBox.Show("We did not find any additional addons", "No Addons Discovered", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            } catch (Exception ex) {
                Common.showError("Allow Automatic Update Check Changed Error", ex);
            }
        }

        private void filterTBKeyDownHandler(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return) { displayActiveGrid(); }
        } // end filterTBKeyDownHandler

        private void FilterGroupCheckChanged(object sender, RoutedEventArgs e) {
            displayActiveGrid();
        } // end FilterGroupCheckChanged
        #endregion

        #region "WrapPanel Setup and Control"
        private void displayActiveGrid() {
            try {
                ItemsControl ic = null;
                // Determine which tab is currently active, so we don't update the background tab(s).
                if (repoTabs == null) { return; } // At program load (before anything shows up) this will not be set, so do not attempt to process just yet.
                string selectedTab = "";
                switch (repoTabs.SelectedIndex) {
                    case 1: ic = jToSIC; selectedTab = "jToS"; break;
                    default: ic = iToSIC; selectedTab = "iToS"; break;
                }
                if (ic == null) { return; } // Wait, what?

                List<addonDataFromRepo> filteredAddonList = returnFilteredList(selectedTab); // Return filtered and sorted list.
                List<addonDisplayData> addonDisplayList = new List<addonDisplayData>();
                foreach (addonDataFromRepo a in filteredAddonList) {
                    if (filterGroupWhatsNew.IsChecked == true && a.releaseDate < tosAMProgramSettings.previousUpdateDateStampUTC) { continue; } // Only displaying addons that have been updated/added since the last cache update.
                    installedAddons ia = listOfInstalledAddons.FirstOrDefault(x => x.addonName == a.Name && x.addonRepo == a.whichRepo); // Check if this addon is installed.
                    if (ia == null && (filterGroupInstalled.IsChecked == true || filterGroupUpdatable.IsChecked == true)) { continue; } // Only displaying installed or updatable addons.

                    addonDisplayData q = new addonDisplayData() { name = a.Name, description = a.Description, installStatusColor = Brushes.White, whichRepo = a.whichRepo, allowInstall = Visibility.Visible, allowDelete = Visibility.Hidden };

                    string releaseDate = "during the big bang."; // Try to display the release date with the 'available version' tag.
                    if (a.releaseDate != DateTimeOffset.MinValue) { releaseDate = $"on {a.releaseDate.ToLocalTime().ToString("MM/dd/yyyy")}"; }
                    q.availableVersion = $"{a.FileVersion} released {releaseDate}";
                    if (ia != null) { // Addon is installed.
                        q.allowInstall = Visibility.Hidden;
                        q.allowDelete = Visibility.Visible;
                        q.installedVersion = $"Installed: {ia.addonVersion} on {ia.installDate.ToShortDateString()}";
                        Version curVersion = new Version(); Version.TryParse(a.FileVersion.Replace("v", ""), out curVersion); // See if it's the version matches.
                        Version installedVersion = new Version(); Version.TryParse(ia.addonVersion.Replace("v", ""), out installedVersion);
                        int verComp = curVersion.CompareTo(installedVersion);
                        if (verComp == 0 && filterGroupUpdatable.IsChecked == true) { // Only displaying updatable addons.
                            continue;
                        } else if (verComp > 0) { // Addon is updatable.
                            q.installStatusColor = Brushes.Yellow;
                            q.allowInstall = Visibility.Visible;
                        } else {
                            q.installStatusColor = Brushes.LightGreen;
                        }
                    }
                    string[] aR = a.authorRepo.Split('/'); q.author = $"by {aR[0]}";
                    q.authorRepoUri = new Hyperlink(new Run(a.authorRepo)) { NavigateUri = new Uri($"https://github.com/{a.authorRepo}") };
                    q.authorRepoUri.RequestNavigate += new RequestNavigateEventHandler(delegate (object sender, RequestNavigateEventArgs e) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)); e.Handled = true; });
                    // No matter what, if this addon was in the broken list, color the background red.  Still allow the user to download, I guess.
                    brokenAddons broken = listOfBrokenAddons.FirstOrDefault(x => x.File == a.File && x.Version == a.FileVersion.Replace("v", "") && x.Author == aR[0]);
                    if (broken != null) { q.installStatusColor = Brushes.Salmon; }
                    addonDisplayList.Add(q);
                }
                ic.ItemsSource = addonDisplayList;
            } catch (Exception ex) {
                Common.showError("displayActiveGrid Error", ex);
            }
        } // end displayActiveGrid

        private List<addonDataFromRepo> returnFilteredList(string selectedTab) {
            List<addonDataFromRepo> ret = new List<addonDataFromRepo>();
            try {
                // Certainly there is a better way to do this .OrderBy code.
                switch (true) {
                    case bool _ when sortGroupAuthorName.IsChecked == true: ret = string.IsNullOrEmpty(filterTB.Text) ? listOfAllAddons.Where(x => x.whichRepo == selectedTab).OrderBy(x => x.authorRepo).ToList() : listOfAllAddons.Where(x => x.whichRepo == selectedTab && x.filterCheck.Contains(filterTB.Text.ToLower())).OrderBy(x => x.authorRepo).ToList(); break;
                    case bool _ when sortGroupUpdatedDate.IsChecked == true: ret = string.IsNullOrEmpty(filterTB.Text) ? listOfAllAddons.Where(x => x.whichRepo == selectedTab).OrderByDescending(x => x.releaseDate).ToList() : listOfAllAddons.Where(x => x.whichRepo == selectedTab && x.filterCheck.Contains(filterTB.Text.ToLower())).OrderByDescending(x => x.releaseDate).ToList(); break;
                    default: ret = string.IsNullOrEmpty(filterTB.Text) ? listOfAllAddons.Where(x => x.whichRepo == selectedTab).OrderBy(x => x.Name).ToList() : listOfAllAddons.Where(x => x.whichRepo == selectedTab && x.filterCheck.Contains(filterTB.Text.ToLower())).OrderBy(x => x.Name).ToList(); break;
                }
            } catch (Exception ex) {
                Common.showError("returnFilteredList Error", ex);
            }
            return ret;
        } // end returnFilteredList

        private void mouseClickInfoAction(object sender, MouseButtonEventArgs e) {
            try {
                if (e.ChangedButton == MouseButton.Left) {
                    if (Common.checkForToSDirectory(tosAMProgramSettings.tosRootDir) == false) { MessageBox.Show("Please set a valid ToS Program directory."); return; }
                    Image i = (Image)sender;
                    addonDisplayData addon = (addonDisplayData)i.DataContext;
                    addonDataFromRepo selectedAddon = listOfAllAddons.FirstOrDefault(x => x.whichRepo == addon.whichRepo && x.Name == addon.name);
                    if (selectedAddon != null) {
                        addonInfo addonInfoWin = new addonInfo { addonData = selectedAddon, installedAddonData = listOfInstalledAddons, rootDir = tosAMProgramSettings.tosRootDir, webConnector = webConnector, Owner = this };
                        addonInfoWin.ShowDialog();
                        // The popup window can update the installed Addon list, so we need to update our List<> and cache file, and then re-process the display.
                        listOfInstalledAddons = addonInfoWin.installedAddonData;
                        saveInstalledAddonDataToFile(); // Writes JSON file.
                        addonInfoWin = null; // WPF should clean up all resources, so this is probably pointless.
                    }
                }
            } catch (Exception ex) {
                Common.showError("Mouse Click Info Action Error", ex);
            } finally {
                displayActiveGrid();
            }
        } // end mouseClickInfoAction

        private async void mouseClickInstallAction(object sender, MouseButtonEventArgs e) {
            try {
                if (e.ChangedButton == MouseButton.Left) {
                    if (Common.checkForToSDirectory(tosAMProgramSettings.tosRootDir) == false) { MessageBox.Show("Please set a valid ToS Program directory."); return; }
                    Image i = (Image)sender;
                    addonDisplayData addon = (addonDisplayData)i.DataContext;
                    addonDataFromRepo selectedAddon = listOfAllAddons.FirstOrDefault(x => x.whichRepo == addon.whichRepo && x.Name == addon.name);
                    if (selectedAddon != null) {
                        AddonManagement am = new AddonManagement() { addonData = selectedAddon, installedAddonData = listOfInstalledAddons, rootDir = tosAMProgramSettings.tosRootDir, addonInstallerOverrides = listOfAddonOverrides };
                        installedAddons iA = listOfInstalledAddons.FirstOrDefault(x => x.addonRepo == addon.whichRepo && x.addonName == addon.name);
                        bool allowContinue = false;
                        if (iA != null) { // Allow Update from here as well.
                            if (MessageBox.Show("Update Addon?", "Update", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                                allowContinue = true;
                                bool updateListResultBool = am.updateInstalledAddonList(1); // Remove the addon from the installed list.
                                if (!updateListResultBool) { MessageBox.Show("Apparently, there was an error while attempting to update the installed addon list.. :<"); return; }
                            }
                        } else {
                            if (MessageBox.Show("Install Addon?", "Install", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { allowContinue = true; }
                        }
                        if (allowContinue) {
                            Progress<taskProgressMsg> progressMessages = new Progress<taskProgressMsg>(updateForTaskProgress);
                            bool downloadResultBool = await am.downloadAndSaveAddon(progressMessages, webConnector);
                            if (!downloadResultBool) { MessageBox.Show("Apparently, there was an error while attempting to download the addon.. :<"); return; }
                            statusBar1TextBlock.Text = "Updating installed addon list...";
                            bool updateListResultBool = am.updateInstalledAddonList(0);
                            if (!updateListResultBool) { MessageBox.Show("Apparently, there was an error while attempting to update the installed addon list.. :<"); return; }
                            listOfInstalledAddons = am.installedAddonData;
                            saveInstalledAddonDataToFile();
                            statusBar1TextBlock.Text = "Install Complete";
                        }
                    }
                }
            } catch (Exception ex) {
                Common.showError("Mouse Click Install Action Error", ex);
            } finally {
                displayActiveGrid();
            }
        } // end mouseClickInstallAction

        private void mouseClickUninstallAction(object sender, MouseButtonEventArgs e) {
            try {
                if (e.ChangedButton == MouseButton.Left) {
                    if (Common.checkForToSDirectory(tosAMProgramSettings.tosRootDir) == false) { MessageBox.Show("Please set a valid ToS Program directory."); return; }
                    if (Common.checkForToSProcess()) { MessageBox.Show("Cannot uninstall addons while ToS is running.", "ToS Running", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
                    Image i = (Image)sender;
                    addonDisplayData addon = (addonDisplayData)i.DataContext;
                    addonDataFromRepo selectedAddon = listOfAllAddons.FirstOrDefault(x => x.whichRepo == addon.whichRepo && x.Name == addon.name);
                    if (selectedAddon != null) {
                        MessageBoxResult mbr = MessageBox.Show("Delete Addon?", "Uninstall", MessageBoxButton.YesNo);
                        if (mbr == MessageBoxResult.Yes) {
                            MessageBoxResult mb = MessageBox.Show($"Remove associated addon directory?{Environment.NewLine}Addon-specific settings are stored here, so if you plan on reinstalling, select 'No'", "Addon directory", MessageBoxButton.YesNo);
                            AddonManagement am = new AddonManagement() { addonData = selectedAddon, installedAddonData = listOfInstalledAddons, rootDir = tosAMProgramSettings.tosRootDir, addonInstallerOverrides = listOfAddonOverrides };
                            bool deleteAddonResultBool = am.deleteInstalledAddon(mb == MessageBoxResult.Yes ? true : false);
                            if (!deleteAddonResultBool) { MessageBox.Show("Apparently, there was an error while attempting to delete the addon.. :<"); return; }
                            bool updateListResultBool = am.updateInstalledAddonList(1);
                            if (!updateListResultBool) { MessageBox.Show("Apparently, there was an error while attempting to update the installed addon list.. :<"); return; }
                            listOfInstalledAddons = am.installedAddonData;
                            saveInstalledAddonDataToFile();
                            statusBar1TextBlock.Text = "Uninstall Complete";
                        }
                    }
                }
            } catch (Exception ex) {
                Common.showError("Mouse Click Install Action Error", ex);
            } finally {
                displayActiveGrid();
            }
        } // end mouseClickUninstallAction

        private void RepoTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            Dispatcher.BeginInvoke((Action)(() => displayActiveGrid()));
        }
        #endregion
    }
} // End Class
