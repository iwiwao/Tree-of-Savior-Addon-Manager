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

namespace ToSAddonManager {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        internal List<addonDataFromRepo> listOfAllAddons = new List<addonDataFromRepo>(); // sigh..
        //internal List<addonDataFromRepoAPI> listofAllAddonsAPI = new List<addonDataFromRepoAPI>();
        internal List<installedAddons> listOfInstalledAddons = new List<installedAddons>();
        internal programSettings tosAMProgramSettings = new programSettings(); // Just storing the ToS directory for now.

        public MainWindow() {
            InitializeComponent();
        }

        private void updateForTaskProgress(taskProgressMsg progress) {
            statusBar1TextBlock.Text = progress.currentMsg;
            if (progress.showAsPopup) { Common.showError("Error", progress.exceptionContent); }
        } // end updateForTaskProgress

        private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            try {
                if (System.IO.File.Exists("installedAddons.json")) { listOfInstalledAddons = JsonConvert.DeserializeObject<List<installedAddons>>(System.IO.File.ReadAllText("installedAddons.json")); } // If there is saved installed addon list, load it.
                if (System.IO.File.Exists("completeAddonList.json")) { listOfAllAddons = JsonConvert.DeserializeObject<List<addonDataFromRepo>>(System.IO.File.ReadAllText("completeAddonList.json")); } // If there is cache data, load it.
                //if (System.IO.File.Exists("completeAddonListAPI.json")) { listofAllAddonsAPI = JsonConvert.DeserializeObject<List<addonDataFromRepoAPI>>(System.IO.File.ReadAllText("completeAddonListAPI.json")); }
                if (System.IO.File.Exists("programSettings.json")) { tosAMProgramSettings = JsonConvert.DeserializeObject<programSettings>(System.IO.File.ReadAllText("programSettings.json")); } // If there is a saved settings file, load it.
                displayActiveGrid("iToS"); displayActiveGrid("jToS");
            } catch (Exception ex) {
                Common.showError("Program Load Error", ex);
            }
        } // end MainWindow_Loaded

        #region "Menu and TB Items"
        private void exitButtonClicked(object sender, RoutedEventArgs e) {
            System.Windows.Application.Current.Shutdown();
        } // End exitButtonClicked

        private async void MenuItemUpdateCache_Click(object sender, RoutedEventArgs e) {
            try {
                MenuItemUpdateCache.IsEnabled = false;
                statusBar1TextBlock.Text = "Started Cache Update";
                Progress<taskProgressMsg> progressMessages = new Progress<taskProgressMsg>(updateForTaskProgress); // Will contain the progress messages from each function.
                List<addonDataFromRepo> iToSAddonCollection = new List<addonDataFromRepo>();
                List<addonDataFromRepoAPI> iToSAddonAPICollection = new List<addonDataFromRepoAPI>(); // Would be used for Github API data, but rate limiting makes this less than useful.
                List<addonDataFromRepo> jToSAddonCollection = new List<addonDataFromRepo>();
                List<addonDataFromRepoAPI> jToSAddonAPICollection = new List<addonDataFromRepoAPI>();

                await Task.Factory.StartNew(() => callParentUpdateCache(progressMessages, 0, ref iToSAddonCollection, ref iToSAddonAPICollection)); // iToS 
                iToSAddonCollection.Select(x => { x.whichRepo = "iToS"; return x; }).ToList();

                await Task.Factory.StartNew(() => callParentUpdateCache(progressMessages, 1, ref jToSAddonCollection, ref jToSAddonAPICollection)); // jToS
                jToSAddonCollection.Select(x => { x.whichRepo = "jToS"; return x; }).ToList();

                listOfAllAddons.Clear(); listOfAllAddons = iToSAddonCollection.Concat(jToSAddonCollection).ToList();
                //listofAllAddonsAPI.Clear(); listofAllAddonsAPI = iToSAddonAPICollection.Concat(jToSAddonAPICollection).ToList();

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
        #endregion

        #region "Cache functions"
        private void callParentUpdateCache(IProgress<taskProgressMsg> progressMessages, int mode, ref List<addonDataFromRepo> addonCollection, ref List<addonDataFromRepoAPI> addonAPICollection) { //Task<string>
            try {
                if (mode == 0) {
                    progressMessages.Report(new taskProgressMsg { currentMsg = "Checking iToS Addons" });
                    repoParentData iToSRepo = returnParentRepoData("https://raw.githubusercontent.com/Tree-of-Savior-Addon-Community/Addons/master/addons.json", progressMessages);
                    returnAddonData(iToSRepo, ref addonCollection, ref addonAPICollection, progressMessages);
                } else if (mode == 1) {
                    progressMessages.Report(new taskProgressMsg { currentMsg = "Checking jToS Addons" });
                    repoParentData jToSRepo = returnParentRepoData("https://raw.githubusercontent.com/JTosAddon/Addons/master/managers.json", progressMessages);
                    returnAddonData(jToSRepo, ref addonCollection, ref addonAPICollection, progressMessages);
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in callParentUpdateCache", showAsPopup = true, exceptionContent = ex });
            }
            progressMessages.Report(new taskProgressMsg { currentMsg = "Completed Cache Update" });
        } // end callParentUpdateCache

        private repoParentData returnParentRepoData(string uri, IProgress<taskProgressMsg> progressMessages) {
            repoParentData repo = new repoParentData();
            try {
                System.Net.WebRequest request = System.Net.WebRequest.Create(uri);
                request.ContentType = "application/json; charset=utf-8";
                System.Net.WebResponse response = request.GetResponse();
                System.IO.Stream responseStream = response.GetResponseStream();
                System.IO.StreamReader responseReader = new System.IO.StreamReader(responseStream, System.Text.Encoding.UTF8);
                string responseText = responseReader.ReadToEnd();
                responseReader.Close(); responseStream.Close(); response.Close(); request = null;
                repo = JsonConvert.DeserializeObject<repoParentData>(responseText);
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnParentRepoData: ", showAsPopup = true, exceptionContent = ex });
            }
            return repo;
        } // end returnParentRepoData

        private void returnAddonData(repoParentData repo, ref List<addonDataFromRepo> addonCollection, ref List<addonDataFromRepoAPI> addonAPICollection, IProgress<taskProgressMsg> progressMessages) {
            try {
                foreach (Source repoSource in repo.Sources) {
                    progressMessages.Report(new taskProgressMsg { currentMsg = $"Checking Addons at repo: {repoSource.Repo}" });
                    List<addonDataFromRepo> addons = new List<addonDataFromRepo>();
                    List<addonDataFromRepoAPI> addonsAPI = new List<addonDataFromRepoAPI>();
                    returnChildRepoData(repoSource.Repo, ref addons, ref addonsAPI, progressMessages);
                    addonCollection = addonCollection.Concat(addons).ToList();
                    //addonAPICollection = addonAPICollection.Concat(addonsAPI).ToList();
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnAddonData", showAsPopup = true, exceptionContent = ex });
            }
        } // end returnAddonData

        private void returnChildRepoData(string repoURI, ref List<addonDataFromRepo> addons, ref List<addonDataFromRepoAPI> addonsAPI, IProgress<taskProgressMsg> progressMessages) {
            try {
                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create($"https://raw.githubusercontent.com/{repoURI}/master/addons.json");
                System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound) {
                    System.IO.Stream responseStream = response.GetResponseStream();
                    System.IO.StreamReader responseReader = new System.IO.StreamReader(responseStream, System.Text.Encoding.UTF8);
                    string responseText = responseReader.ReadToEnd();
                    responseReader.Close(); responseStream.Close(); response.Close(); request = null;
                    addons = JsonConvert.DeserializeObject<List<addonDataFromRepo>>(responseText);
                    addons.Select(x => { x.authorRepo = repoURI; x.tagsFlat = string.Join(",", x.Tags); return x; }).ToList();
                }

                // Pull additional data from github API. -- Not really possible with unauthententiced requests due to rate limit of 60 requests per hour.
                //System.Net.HttpWebRequest request1 = (System.Net.HttpWebRequest)System.Net.WebRequest.Create($"https://api.github.com/repos/{repoURI}/releases");
                //request1.ContentType = "application/json; charset=utf-8";
                //request1.Accept = "application/vnd.github.v3+json";
                //System.Net.WebResponse response1 = request1.GetResponse();
                //System.IO.Stream responseStream1 = response1.GetResponseStream();
                //System.IO.StreamReader responseReader1 = new System.IO.StreamReader(responseStream1, System.Text.Encoding.UTF8);
                //string responseText1 = responseReader1.ReadToEnd();
                //responseReader1.Close(); responseStream1.Close(); response1.Close(); request1 = null;
                //addonsAPI = JsonConvert.DeserializeObject<List<addonDataFromRepoAPI>>(responseText);
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in reutrnChildRepoData", showAsPopup = true, exceptionContent = ex });
            }
            //return addons;
        } // end returnChildRepoData

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
        #endregion

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
                        addonInfo addonInfoWin = new addonInfo { addonData = selectedAddon, installedAddonData = listOfInstalledAddons, rootDir = tosAMProgramSettings.tosRootDir, Owner = this };
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
