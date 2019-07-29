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
                if (System.IO.File.Exists("programSettings.json")) { tosAMProgramSettings = JsonConvert.DeserializeObject<programSettings>(System.IO.File.ReadAllText("programSettings.json")); } // If there is a saved settings file, load it.
                displayActiveGrid();
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
                List<addonDataFromRepo> iToSResult = await Task.Factory.StartNew(() => callParentUpdateCache(progressMessages, 0)); // iToS 
                iToSResult.Select(x => { x.whichRepo = "iToS"; return x; }).ToList();
                List<addonDataFromRepo> jToSResult = await Task.Factory.StartNew(() => callParentUpdateCache(progressMessages, 1)); // jToS
                jToSResult.Select(x => { x.whichRepo = "jToS"; return x; }).ToList();
                saveCacheDataToFile(iToSResult, jToSResult, true, progressMessages);
                listOfAllAddons.Clear(); listOfAllAddons = iToSResult.Concat(jToSResult).ToList();
                displayActiveGrid(); // Update the current grid.
            } catch (Exception ex) {
                Common.showError("Update Cache Error", ex);
            } finally {
                MenuItemUpdateCache.IsEnabled = true;
            }
        } // End MenuItemUpdateCache_Click

        private void MenuItemSelectToSDir_Click(object sender, RoutedEventArgs e) {
            // WPF needs an Directory slector.. :<
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
            if (e.Key == Key.Return) { displayActiveGrid(); }
        } // end filterTBKeyDownHandler
        #endregion

        #region "Cache functions"
        private List<addonDataFromRepo> callParentUpdateCache(IProgress<taskProgressMsg> progressMessages, int mode) { //Task<string>
            List<addonDataFromRepo> addons = new List<addonDataFromRepo>();
            try {
                if (mode == 0) {
                    progressMessages.Report(new taskProgressMsg { currentMsg = "Checking iToS Addons" });
                    repoParentData iToSRepo = returnParentRepoData("https://raw.githubusercontent.com/JTosAddon/Addons/itos/managers.json", progressMessages);
                    List<addonDataFromRepo> tmpAddonList = returnAddonData(iToSRepo, progressMessages);
                    addons = tmpAddonList.OrderBy(x => x.Name).ToList();
                } else if (mode == 1) {
                    progressMessages.Report(new taskProgressMsg { currentMsg = "Checking jToS Addons" });
                    repoParentData jToSRepo = returnParentRepoData("https://raw.githubusercontent.com/JTosAddon/Addons/master/managers.json", progressMessages);
                    List<addonDataFromRepo> tmpAddonList = returnAddonData(jToSRepo, progressMessages);
                    addons = tmpAddonList.OrderBy(x => x.Name).ToList();
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in callParentUpdateCache", showAsPopup = true, exceptionContent = ex });
            }
            progressMessages.Report(new taskProgressMsg { currentMsg = "Completed Cache Update" });
            return addons;
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

        private List<addonDataFromRepo> returnAddonData(repoParentData repo, IProgress<taskProgressMsg> progressMessages) {
            List<addonDataFromRepo> addonCollection = new List<addonDataFromRepo>();
            try {
                foreach (Source repoSource in repo.Sources) {
                    progressMessages.Report(new taskProgressMsg { currentMsg = $"Checking Addons at repo: {repoSource.Repo}" });
                    addonCollection = addonCollection.Concat(returnChildRepoData(repoSource.Repo, progressMessages)).ToList();
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnAddonData", showAsPopup = true, exceptionContent = ex });
            }
            return addonCollection;
        } // end returnAddonData

        private List<addonDataFromRepo> returnChildRepoData(string repoURI, IProgress<taskProgressMsg> progressMessages) {
            List<addonDataFromRepo> addons = new List<addonDataFromRepo>();
            try {
                System.Net.WebRequest request = System.Net.WebRequest.Create($"https://raw.githubusercontent.com/{repoURI}/master/addons.json");
                request.ContentType = "application/json; charset=utf-8";
                System.Net.WebResponse response = request.GetResponse();
                System.IO.Stream responseStream = response.GetResponseStream();
                System.IO.StreamReader responseReader = new System.IO.StreamReader(responseStream, System.Text.Encoding.UTF8);
                string responseText = responseReader.ReadToEnd();
                responseReader.Close(); responseStream.Close(); response.Close(); request = null;
                addons = JsonConvert.DeserializeObject<List<addonDataFromRepo>>(responseText);
                addons.Select(x => { x.authorRepo = repoURI; x.tagsFlat = string.Join(",", x.Tags); return x; }).ToList();
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in reutrnChildRepoData", showAsPopup = true, exceptionContent = ex });
            }
            return addons;
        } // end returnChildRepoData

        // Must be a better way to do this.
        private void saveCacheDataToFile(List<addonDataFromRepo> iTOSData, List<addonDataFromRepo> jTOSData, bool purgeFile, IProgress<taskProgressMsg> progressMessages) {
            try {
                List<addonDataFromRepo> localAddon = new List<addonDataFromRepo>();
                localAddon = iTOSData.Concat(jTOSData).ToList(); // Merge the two repos
                if (localAddon.Count == 0) { return; } // Should we allow a 0-length cache to act as a purge or force the user to manually delete the file?  Allowing 0-length would destroy the cache in event of temporary failure pulling remote lists.
                if (purgeFile) {
                    System.IO.File.WriteAllText("completeAddonList.json", JsonConvert.SerializeObject(localAddon));
                } else {
                    System.IO.File.AppendAllText("completeAddonList.json", JsonConvert.SerializeObject(localAddon));
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
                throw;
            }
        } // end saveInstallDataToFile
        #endregion

        #region "Tab Control"
        private void RepoTabs_SelectionChanged(object sender, RoutedEventArgs e) { //object sender, SelectionChangedEventArgs e
            Application.Current.Dispatcher.BeginInvoke((Action)delegate { displayActiveGrid(); }, DispatcherPriority.Render, null);
        } // end RepoTabs_SelectionChanged
        #endregion

        #region "WrapPanel Setup and Control"
        private void displayActiveGrid() {
            TabItem tab = repoTabs.SelectedItem as TabItem;
            string tabHeader = tab.Header.ToString();
            WrapPanel wp = null;
            List<addonDataFromRepo> filteredAddonList = string.IsNullOrEmpty(filterTB.Text) ? listOfAllAddons.Where(x => x.whichRepo == tabHeader).OrderBy(x => x.Name).ToList() : listOfAllAddons.Where(x => x.whichRepo == tabHeader && x.filterCheck.Contains(filterTB.Text.ToLower())).OrderBy(x => x.Name).ToList();
            if (tabHeader == "iToS") { wp = iToSWP; } else if (tabHeader == "jToS") { wp = jToSWP; }
            if (wp == null) { return; } // Wait, what?
            wp.Children.Clear();

            int i = 0;
            foreach (addonDataFromRepo a in filteredAddonList) {
                Canvas c = new Canvas() { Width = 350, Height = 150, Background = Brushes.White, Tag = i.ToString() };  // Setting the name to the index of the addon list, small cheat I'll use to pull data later.
                c.MouseDown += new MouseButtonEventHandler(mouseDoubleClickAction);
                Border b = new Border { BorderThickness = new Thickness(1, 1, 1, 1), BorderBrush = Brushes.Black, Margin = new Thickness(5) }; b.Child = c;
                b.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = new Color { A = 1, R = 0, G = 0, B = 0 }, Direction = 320, ShadowDepth = 7, Opacity = .25 };

                string[] aR = a.authorRepo.Split('/');
                c.Children.Add(new TextBlock { Text = $"{a.Name} by {aR[0]}", Padding = new Thickness(5, 5, 0, 0) });

                Label addonAuthorLabel = new Label { Padding = new Thickness(5, 5, 0, 0), FontSize = 12 }; Canvas.SetTop(addonAuthorLabel, 22);
                Hyperlink addonAuthor = new Hyperlink(new Run(a.authorRepo)) { NavigateUri = new Uri($"https://github.com/{a.authorRepo}") };
                addonAuthor.RequestNavigate += new RequestNavigateEventHandler(delegate (object sender, RequestNavigateEventArgs e) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)); e.Handled = true; });
                addonAuthorLabel.Content = addonAuthor;
                c.Children.Add(addonAuthorLabel);

                TextBlock addonVersion = new TextBlock { Text = a.FileVersion, FontSize = 10, Padding = new Thickness(5, 0, 0, 0) }; Canvas.SetTop(addonVersion, 43);
                c.Children.Add(addonVersion);

                string shortDesc = a.Description.Length > 125 ? a.Description.Substring(0, 125).Trim() + "..." : a.Description.Trim();
                TextBlock addonDesc = new TextBlock { Text = shortDesc, FontSize = 14, Padding = new Thickness(5, 0, 0, 0), TextWrapping = TextWrapping.Wrap, Width = 325 }; Canvas.SetTop(addonDesc, 63);
                c.Children.Add(addonDesc);

                installedAddons ia = listOfInstalledAddons.FirstOrDefault(x => x.addonName == a.Name && x.addonRepo == a.whichRepo); // Check if this addon is installed.
                if (ia != null) { // Addon is installed.  
                    TextBlock addonInstalledVersion = new TextBlock { Text = $"Installed: {ia.addonVersion} on {ia.installDate.ToShortDateString()}", FontSize = 10 }; Canvas.SetTop(addonInstalledVersion, 43); Canvas.SetLeft(addonInstalledVersion, 50);
                    c.Children.Add(addonInstalledVersion);
                    Version curVersion = new Version(); Version.TryParse(a.FileVersion.Replace("v", ""), out curVersion); // See if it's the version matches.
                    Version installedVersion = new Version(); Version.TryParse(ia.addonVersion.Replace("v", ""), out installedVersion);
                    c.Background = curVersion.CompareTo(installedVersion) > 0 ? Brushes.LightYellow : Brushes.LightGreen;
                }
                wp.Children.Add(b); // add the Border "parent" to the Wrap Panel.
                i++;
            }
        } // end displayActiveGrid

        private void mouseDoubleClickAction(object sender, MouseButtonEventArgs e) {
            try {
                if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) {
                    Canvas c = (Canvas)sender;
                    int listIndex = -1; int.TryParse(c.Tag.ToString(), out listIndex);
                    if (listIndex == -1) { return; }
                    TabItem tab = repoTabs.SelectedItem as TabItem;
                    addonDataFromRepo addon = listOfAllAddons.Where(x => x.whichRepo == tab.Header.ToString()).OrderBy(x => x.Name).ToList()[listIndex];
                    addonInfo addonInfoWin = new addonInfo { addonData = addon, installedAddonData = listOfInstalledAddons, rootDir = tosAMProgramSettings.tosRootDir, Owner = this };
                    addonInfoWin.ShowDialog();
                    // The popup window can update the installed Addon list, so we need to update our List<> and cache file, and then re-process the display.
                    listOfInstalledAddons = addonInfoWin.installedAddonData;
                    saveInstalledAddonDataToFile();
                    displayActiveGrid();
                    addonInfoWin = null; // WPF should clean up all resources, so this is probably pointless.
                }
            } catch (Exception ex) {
                Common.showError("Canvas DoubleClick", ex);
            }
        } // end mouseDoubleClickAction
        #endregion
    }
} // End Class
