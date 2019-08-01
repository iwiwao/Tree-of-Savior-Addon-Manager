using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;

namespace ToSAddonManager {
    class AddonManagement {
        internal List<installedAddons> installedAddonData { get; set; }
        internal addonDataFromRepo addonData { get; set; }
        internal string rootDir { get; set; }

        internal async Task<bool> downloadAndSaveAddon(IProgress<taskProgressMsg> progressMessages, HttpClient webConnector) {
            try {
                // Download file, save it, create addon directory.
                progressMessages.Report(new taskProgressMsg { currentMsg = "Connecting to Repo..." });
                HttpResponseMessage webConnectorRespoonse = await webConnector.GetAsync($"https://github.com/{addonData.authorRepo}/releases/download/{addonData.ReleaseTag}/{addonData.File}-{addonData.FileVersion}.{addonData.Extension}");
                System.IO.FileStream fs = new System.IO.FileStream($"{rootDir}/data/_{addonData.File}-{addonData.Unicode}-{addonData.FileVersion}.{addonData.Extension}", System.IO.FileMode.Create);
                progressMessages.Report(new taskProgressMsg { currentMsg = "Saving addon..." });
                await webConnectorRespoonse.Content.CopyToAsync(fs); fs.Close(); fs.Dispose();
                webConnectorRespoonse.Dispose();
                progressMessages.Report(new taskProgressMsg { currentMsg = "Addon saved..." });
                if (!System.IO.Directory.Exists($"{rootDir}/addons/{addonData.File}")) { System.IO.Directory.CreateDirectory($"{rootDir}/addons/{addonData.File}"); }
                return true;
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Download and Save Addon Error", exceptionContent = ex, showAsPopup = true });
                return false;
            }
        } // end downloadAndSaveAddon

        internal bool deleteInstalledAddon(bool purgeDirectory) {
            try {
                string fullAddonFile = $"_{addonData.File}-{addonData.Unicode}-{addonData.FileVersion}.{addonData.Extension}";
                if (purgeDirectory && System.IO.Directory.Exists($"{rootDir}/addons/{addonData.File}")) { System.IO.Directory.Delete($"{rootDir}/addons/{addonData.File}", true); }
                if (System.IO.File.Exists($"{rootDir}/data/{fullAddonFile}")) { System.IO.File.Delete($"{rootDir}/data/{fullAddonFile}"); }
                return true;
            } catch (Exception ex) {
                Common.showError("Delete Installed Addon Button Error", ex);
                return false;
            }
        } // end deleteInstalledAddon

        internal bool updateInstalledAddonList(int action) {
            try {
                if (action == 0) { // Add
                    installedAddonData.Add(new installedAddons { addonAuthorRepo = addonData.authorRepo, addonFilename = addonData.File, addonName = addonData.Name, addonRepo = addonData.whichRepo, addonVersion = addonData.FileVersion, installDate = DateTime.Now });
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
    }
}
