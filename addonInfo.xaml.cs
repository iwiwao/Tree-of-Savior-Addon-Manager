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
using System.Windows.Shapes;

namespace ToSAddonManager {
    /// <summary>
    /// Interaction logic for addonInfo.xaml
    /// </summary>
    public partial class addonInfo : Window {
        internal List<installedAddons> installedAddonData { get; set; }
        internal addonDataFromRepo addonData { get; set; }
        internal string rootDir { get; set; }

        public addonInfo() {
            InitializeComponent();
        }

        private void closeButton_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private void AddonInfoWindow_Loaded(object sender, RoutedEventArgs e) {
            try {
                // Set basic addon info.
                nameValue.Text = addonData.Name;
                fileValue.Text = addonData.File;
                versionValue.Text = addonData.FileVersion;
                tagsValue.Text = addonData.tagsFlat;
                descriptionValue.Text = addonData.Description;
                descriptionValue.TextWrapping = TextWrapping.Wrap;
                // Determine if addon is currently installed, and update action button appropriately.
                installedAddons i = installedAddonData.FirstOrDefault(x => x.addonName == addonData.Name && x.addonRepo == addonData.whichRepo);
                if (i != null) {
                    // Addon was found.  Change Action Button to Uninstall, and check version and determine if update is available.
                    actionButton.Content = "Uninstall"; actionButton.Background = Brushes.LightSalmon;
                    Version curVersion = new Version(); Version.TryParse(addonData.FileVersion.Replace("v", ""), out curVersion);
                    Version installedVersion = new Version(); Version.TryParse(i.addonVersion.Replace("v", ""), out installedVersion);
                    if (curVersion.CompareTo(installedVersion) > 0) { updateButton.Visibility = Visibility.Visible; }
                }
                //}
            } catch (Exception ex) {
                Common.showError("Addon Info Window Error", ex);
            }
        } // end AddonInfoWindow_loaded

        private async void ActionButton_Click(object sender, RoutedEventArgs e) {
            try {
                actionButton.IsEnabled = false;
                closeButton.IsEnabled = false;
                updateButton.IsEnabled = false;
                // Which action to perform?
                if (actionButton.Content.ToString() == "Install") {
                    Progress<taskProgressMsg> progressMessages = new Progress<taskProgressMsg>(updateForTaskProgress); // Will contain the progress messages from each function.
                    Task<bool> downloadResultBool = await Task.Factory.StartNew(() => downloadAndSaveAddon(progressMessages));
                    if (!downloadResultBool.Result) { MessageBox.Show("Apparently, there was an error while attempting to download the addon.. :<"); return; }
                    statusBar1TextBlock.Text = "Updating installed addon list...";
                    bool updateListResultBool = updateInstalledAddonList(0);
                    if (!updateListResultBool) { MessageBox.Show("Apparently, there was an error while attempting to update the installed addon list.. :<"); return; }
                    actionButton.Content = "Uninstall"; actionButton.Background = Brushes.LightSalmon;
                    statusBar1TextBlock.Text = "Install Complete";
                } else if (actionButton.Content.ToString() == "Uninstall") {
                    MessageBoxResult mb = MessageBox.Show($"Remove associated addon directory?{Environment.NewLine}Addon-specific settings are stored here, so if you plan on reinstalling, select 'No'", "Addon directory", MessageBoxButton.YesNoCancel);
                    if (mb == MessageBoxResult.Cancel) { return; }
                    bool deleteAddonResultBool = deleteInstalledAddon(mb == MessageBoxResult.Yes ? true : false);
                    if (!deleteAddonResultBool) { MessageBox.Show("Apparently, there was an error while attempting to delete the addon.. :<"); return; }
                    bool updateListResultBool = updateInstalledAddonList(1);
                    if (!updateListResultBool) { MessageBox.Show("Apparently, there was an error while attempting to update the installed addon list.. :<"); return; }
                    actionButton.Content = "Install"; actionButton.Background = Brushes.LightGreen;
                    statusBar1TextBlock.Text = "Uninstall Complete";
                }
            } catch (Exception ex) {
                Common.showError("Action Button Error", ex);
            } finally {
                actionButton.IsEnabled = true;
                closeButton.IsEnabled = true;
                updateButton.IsEnabled = true;
            }
        } // end ActionButton_Click

        private async void UpdateButton_Click(object sender, RoutedEventArgs e) {
            try {
                actionButton.IsEnabled = false;
                closeButton.IsEnabled = false;
                updateButton.IsEnabled = false;
                // Going to just use the same functionality as the install button (for now)
                Progress<taskProgressMsg> progressMessages = new Progress<taskProgressMsg>(updateForTaskProgress);
                Task<bool> downloadResultBool = await Task.Factory.StartNew(() => downloadAndSaveAddon(progressMessages));
                if (!downloadResultBool.Result) { MessageBox.Show("Apparently, there was an error while attempting to download the addon.. :<"); return; }
                statusBar1TextBlock.Text = "Updating installed addon list...";
                bool updateListResultBoolStage1 = updateInstalledAddonList(1); // Remove old record.
                bool updateListResultBoolStage2 = updateInstalledAddonList(0);
                if (!updateListResultBoolStage1 || !updateListResultBoolStage2) { MessageBox.Show("Apparently, there was an error while attempting to update the installed addon list.. :<"); return; }
                actionButton.Content = "Uninstall"; actionButton.Background = Brushes.LightSalmon;
                statusBar1TextBlock.Text = "Updatge Complete";
                updateButton.Visibility = Visibility.Hidden;
            } catch (Exception ex) {
                Common.showError("Update Button Error", ex);
            } finally {
                actionButton.IsEnabled = true;
                closeButton.IsEnabled = true;
                updateButton.IsEnabled = true;
            }
        } // end Updatebutton_Click

        private async Task<bool> downloadAndSaveAddon(IProgress<taskProgressMsg> progressMessages) {
            try {
                // Download file, save it, create addon directory.

                progressMessages.Report(new taskProgressMsg { currentMsg = "Connecting to Repo..." });
                System.Net.Http.HttpClient wc1 = new System.Net.Http.HttpClient(); // Should only init this once per session, but I guess I'm a rebel..Or a bad coder..
                System.Net.Http.HttpResponseMessage wc1Result = await wc1.GetAsync($"https://github.com/{addonData.authorRepo}/releases/download/{addonData.ReleaseTag}/{addonData.File}-{addonData.FileVersion}.{addonData.Extension}");
                System.IO.FileStream fs = new System.IO.FileStream($"{rootDir}/data/_{addonData.File}-{addonData.Unicode}-{addonData.FileVersion}.{addonData.Extension}", System.IO.FileMode.Create);
                progressMessages.Report(new taskProgressMsg { currentMsg = "Saving addon..." });
                await wc1Result.Content.CopyToAsync(fs);
                fs.Close(); fs.Dispose();
                wc1Result.Dispose();
                wc1.Dispose();
                progressMessages.Report(new taskProgressMsg { currentMsg = "Addon saved..." });
                if (!System.IO.Directory.Exists($"{rootDir}/addons/{addonData.Name}")) { System.IO.Directory.CreateDirectory($"{rootDir}/addons/{addonData.Name}"); }
                return true;
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Download and Save Addon Error", exceptionContent = ex, showAsPopup = true });
                return false;
            }
        } // end downloadAndSaveAddon

        private bool deleteInstalledAddon(bool purgeDirectory) {
            try {
                string fullAddonFile = $"_{addonData.File}-{addonData.Unicode}-{addonData.FileVersion}.{addonData.Extension}";
                if (purgeDirectory && System.IO.Directory.Exists($"{rootDir}/addons/{addonData.Name}")) { System.IO.Directory.Delete($"{rootDir}/addons/{addonData.Name}", true); }
                if (System.IO.File.Exists($"{rootDir}/data/{fullAddonFile}")) { System.IO.File.Delete($"{rootDir}/data/{fullAddonFile}"); }
                return true;
            } catch (Exception ex) {

                Common.showError("Delete Installed Addon Button Error", ex);
                return false;
            }
        } // end deleteInstalledAddon

        private bool updateInstalledAddonList(int action) {
            try {
                if (action == 0) { // Add
                    installedAddonData.Add(new installedAddons { addonAuthorRepo = addonData.authorRepo, addonName = addonData.Name, addonRepo = addonData.whichRepo, addonVersion = addonData.FileVersion, installDate = DateTime.Now });
                } else if (action == 1) { // Remove
                    installedAddons q = installedAddonData.FirstOrDefault(x => x.addonName == addonData.Name && x.addonRepo == addonData.whichRepo); // Check only the name and repo - ignore the version in case we've come in with a new version and it doesn't match the installed version.
                    if (q != null) { installedAddonData.Remove(q); }
                }
                return true;
            } catch (Exception ex) {
                Common.showError("Update Installed Addon List Error", ex);
                return false;
            }
        } // end updateInstalledAddonList

        private void updateForTaskProgress(taskProgressMsg progress) {
            statusBar1TextBlock.Text = progress.currentMsg;
            if (progress.showAsPopup) { Common.showError("Error", progress.exceptionContent); }
        } // end updateForTaskProgress
    }
}
