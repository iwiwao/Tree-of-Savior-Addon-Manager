using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;

namespace ToSAddonManager {
    class repoCacheManagement {
        internal string rootDir { get; set; }
        internal HttpClient webConnector { get; set; }

        internal async Task<(List<addonDataFromRepo>, List<addonDataFromRepoAPI>)> callParentUpdateCache(IProgress<taskProgressMsg> progressMessages, int mode) {
            List<addonDataFromRepo> addonCollection = new List<addonDataFromRepo>();
            List<addonDataFromRepoAPI> addonAPICollection = new List<addonDataFromRepoAPI>(); // Not currently using due to API call limits.
            try {
                switch (mode) {
                    case 0:
                        progressMessages.Report(new taskProgressMsg { currentMsg = "Checking iToS Addons" });
                        //repoParentData iToSRepo = await returnParentRepoData("https://raw.githubusercontent.com/Tree-of-Savior-Addon-Community/Addons/master/addons.json", progressMessages); // Not as up-to-date as JTosAddon
                        repoParentData iToSRepo = await returnParentRepoData("https://raw.githubusercontent.com/JTosAddon/Addons/itos/managers.json", progressMessages);
                        (List<addonDataFromRepo>, List<addonDataFromRepoAPI>) foo = await returnAddonData(iToSRepo, progressMessages);
                        addonCollection = foo.Item1; addonAPICollection = foo.Item2;
                        break;
                    case 1:
                        progressMessages.Report(new taskProgressMsg { currentMsg = "Checking jToS Addons" });
                        repoParentData jToSRepo = await returnParentRepoData("https://raw.githubusercontent.com/JTosAddon/Addons/master/managers.json", progressMessages);
                        (List<addonDataFromRepo>, List<addonDataFromRepoAPI>) bar = await returnAddonData(jToSRepo, progressMessages);
                        addonCollection = bar.Item1; addonAPICollection = bar.Item2;
                        break;
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in callParentUpdateCache", showAsPopup = true, exceptionContent = ex });
            }
            progressMessages.Report(new taskProgressMsg { currentMsg = "Completed Cache Update" });
            return (addonCollection, addonAPICollection);
        } // end callParentUpdateCache

        private async Task<repoParentData> returnParentRepoData(string uri, IProgress<taskProgressMsg> progressMessages) {
            repoParentData repo = new repoParentData();
            try {
                HttpResponseMessage webConnectorResponse = await webConnector.GetAsync(uri);
                if (webConnectorResponse.IsSuccessStatusCode) {
                    string resultString = await webConnectorResponse.Content.ReadAsStringAsync();
                    repo = JsonConvert.DeserializeObject<repoParentData>(resultString);
                } else {
                    progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnParentRepoData: ", showAsPopup = true, exceptionContent = new HttpRequestException() });
                }

                webConnectorResponse.Dispose();
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnParentRepoData: ", showAsPopup = true, exceptionContent = ex });
            }
            return repo;
        } // end returnParentRepoData

        private async Task<(List<addonDataFromRepo>, List<addonDataFromRepoAPI>)> returnAddonData(repoParentData repo, IProgress<taskProgressMsg> progressMessages) {
            List<addonDataFromRepo> addonCollection = new List<addonDataFromRepo>();
            List<addonDataFromRepoAPI> addonAPICollection = new List<addonDataFromRepoAPI>();
            try {
                foreach (Source repoSource in repo.Sources) {
                    progressMessages.Report(new taskProgressMsg { currentMsg = $"Checking Addons at repo: {repoSource.Repo}" });
                    (List<addonDataFromRepo>, List<addonDataFromRepoAPI>) foo = await returnChildRepoData(repoSource.Repo, progressMessages);
                    addonCollection = addonCollection.Concat(foo.Item1).ToList();
                    //addonAPICollection = addonAPICollection.Concat(foo.Item2).ToList();
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnAddonData", showAsPopup = true, exceptionContent = ex });
            }
            return (addonCollection, addonAPICollection);
        } // end returnAddonData

        private async Task<(List<addonDataFromRepo>, List<addonDataFromRepoAPI>)> returnChildRepoData(string repoURI, IProgress<taskProgressMsg> progressMessages) {
            List<addonDataFromRepo> addons = new List<addonDataFromRepo>();
            List<addonDataFromRepoAPI> addonsAPI = new List<addonDataFromRepoAPI>();
            try {
                HttpResponseMessage webConnectorResponse = await webConnector.GetAsync($"https://raw.githubusercontent.com/{repoURI}/master/addons.json");
                if (webConnectorResponse.IsSuccessStatusCode) {
                    string resultString = await webConnectorResponse.Content.ReadAsStringAsync();
                    webConnectorResponse.Dispose();
                    addons = JsonConvert.DeserializeObject<List<addonDataFromRepo>>(resultString);
                    addons.Select(x => { x.authorRepo = repoURI; x.tagsFlat = string.Join(",", x.Tags); return x; }).ToList();
                }
                webConnectorResponse.Dispose();

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
            return (addons, addonsAPI);
        } // end returnChildRepoData

        internal async Task<bool> checkAndInstallDependencies(IProgress<taskProgressMsg> progressMessages) {
            try {
                if (string.IsNullOrEmpty(rootDir)) { return false; }
                progressMessages.Report(new taskProgressMsg { currentMsg = "Downloading/Refreshing Dependencies" });
                string[] depFiles = { "acutil.lua", "json.lua", "cwapi.lua", "xmlSimple.lua" };

                foreach (string q in depFiles) {
                    // Currently, we will just overwrite existing files since there isn't recorded version information.
                    progressMessages.Report(new taskProgressMsg { currentMsg = $"Downloading {q}" });
                    HttpResponseMessage webConnectorResponse = await webConnector.GetAsync($"https://raw.githubusercontent.com/Tree-of-Savior-Addon-Community/AC-Util/master/src/{q}");
                    webConnectorResponse.EnsureSuccessStatusCode();
                    System.IO.FileStream fs = new System.IO.FileStream($"{rootDir}/release/lua/{q}", System.IO.FileMode.Create);
                    await webConnectorResponse.Content.CopyToAsync(fs); fs.Close(); fs.Dispose();
                    webConnectorResponse.Dispose();
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Dependency Download Error", exceptionContent = ex, showAsPopup = true });
            }
            return true;
        } // end checkAndInstallDependencies

        internal async Task<List<brokenAddons>> returnBrokenAddonData(IProgress<taskProgressMsg> progressMessages) {
            List<brokenAddons> listOfBrokenAddons = new List<brokenAddons>();
            try {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Checking Broken Addons" });
                HttpResponseMessage webConnectorResponse = await webConnector.GetAsync("https://raw.githubusercontent.com/JTosAddon/Addons/master/broken-addons.json");
                if (webConnectorResponse.IsSuccessStatusCode) {
                    BrokenAddonsData tmpList = new BrokenAddonsData();
                    string resultString = await webConnectorResponse.Content.ReadAsStringAsync();
                    webConnectorResponse.Dispose();
                    tmpList = JsonConvert.DeserializeObject<BrokenAddonsData>(resultString);
                    listOfBrokenAddons = tmpList.Addons;
                }
                webConnectorResponse.Dispose();
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnBrokenAddonData", showAsPopup = true, exceptionContent = ex });
            }
            return listOfBrokenAddons;
        } // end returnBrokenAddonData
    }
}
