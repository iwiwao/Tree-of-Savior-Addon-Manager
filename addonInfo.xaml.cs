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
using System.Net.Http;

namespace ToSAddonManager {
    /// <summary>
    /// Interaction logic for addonInfo.xaml
    /// </summary>
    public partial class addonInfo : Window {
        internal List<installedAddons> installedAddonData { get; set; }
        internal addonDataFromRepo addonData { get; set; }
        internal string rootDir { get; set; }
        internal HttpClient webConnector { get; set; }

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
                AddonManagement am = new AddonManagement() { addonData = addonData, installedAddonData = installedAddonData, rootDir = rootDir };
                // Which action to perform?
                if (actionButton.Content.ToString() == "Install") {
                    Progress<taskProgressMsg> progressMessages = new Progress<taskProgressMsg>(updateForTaskProgress); // Will contain the progress messages from each function.
                    bool downloadResultBool = await am.downloadAndSaveAddon(progressMessages, webConnector);
                    if (!downloadResultBool) { MessageBox.Show("Apparently, there was an error while attempting to download the addon.. :<"); return; }
                    statusBar1TextBlock.Text = "Updating installed addon list...";
                    bool updateListResultBool = am.updateInstalledAddonList(0);
                    if (!updateListResultBool) { MessageBox.Show("Apparently, there was an error while attempting to update the installed addon list.. :<"); return; }
                    actionButton.Content = "Uninstall"; actionButton.Background = Brushes.LightSalmon;
                    statusBar1TextBlock.Text = "Install Complete";
                } else if (actionButton.Content.ToString() == "Uninstall") {
                    if (Common.checkForToSProcess()) { MessageBox.Show("Cannot uninstall addons while ToS is running.", "ToS Running", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
                    MessageBoxResult mb = MessageBox.Show($"Remove associated addon directory?{Environment.NewLine}Addon-specific settings are stored here, so if you plan on reinstalling, select 'No'", "Addon directory", MessageBoxButton.YesNoCancel);
                    if (mb == MessageBoxResult.Cancel) { return; }
                    bool deleteAddonResultBool = am.deleteInstalledAddon(mb == MessageBoxResult.Yes ? true : false);
                    if (!deleteAddonResultBool) { MessageBox.Show("Apparently, there was an error while attempting to delete the addon.. :<"); return; }
                    bool updateListResultBool = am.updateInstalledAddonList(1);
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
                AddonManagement am = new AddonManagement() { addonData = addonData, installedAddonData = installedAddonData, rootDir = rootDir };
                // Going to just use the same functionality as the install button (for now)
                Progress<taskProgressMsg> progressMessages = new Progress<taskProgressMsg>(updateForTaskProgress);
                bool downloadResultBool = await Task.Run(() => am.downloadAndSaveAddon(progressMessages, webConnector));
                if (!downloadResultBool) { MessageBox.Show("Apparently, there was an error while attempting to download the addon.. :<"); return; }
                statusBar1TextBlock.Text = "Updating installed addon list...";
                bool updateListResultBoolStage1 = am.updateInstalledAddonList(1); // Remove old record.
                bool updateListResultBoolStage2 = am.updateInstalledAddonList(0);
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

        private void updateForTaskProgress(taskProgressMsg progress) {
            statusBar1TextBlock.Text = progress.currentMsg;
            if (progress.showAsPopup) { Common.showError("Error", progress.exceptionContent); }
        } // end updateForTaskProgress
    }
}
