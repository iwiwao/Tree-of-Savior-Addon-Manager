using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;
using System.Xml;

namespace ToSAddonManager {
    class repoCacheManagement {
        internal string rootDir { get; set; }
        internal HttpClient webConnector { get; set; }

        internal async Task<List<addonDataFromRepo>> callParentUpdateCache(IProgress<taskProgressMsg> progressMessages, int mode) {
            List<addonDataFromRepo> addonCollection = new List<addonDataFromRepo>();
            try {
                switch (mode) {
                    case 0:
                        progressMessages.Report(new taskProgressMsg { currentMsg = "Checking iToS Addons" });
                        repoParentData iToSRepo = await returnParentRepoData("https://raw.githubusercontent.com/JTosAddon/Addons/itos/managers.json", progressMessages);
                        addonCollection = await returnAddonData(iToSRepo, progressMessages);
                        break;
                    case 1:
                        progressMessages.Report(new taskProgressMsg { currentMsg = "Checking jToS Addons" });
                        repoParentData jToSRepo = await returnParentRepoData("https://raw.githubusercontent.com/JTosAddon/Addons/master/managers.json", progressMessages);
                        addonCollection = await returnAddonData(jToSRepo, progressMessages);
                        break;
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in callParentUpdateCache", showAsPopup = true, exceptionContent = ex });
            }
            progressMessages.Report(new taskProgressMsg { currentMsg = "Completed Cache Update" });
            return addonCollection;
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

        private async Task<List<addonDataFromRepo>> returnAddonData(repoParentData repo, IProgress<taskProgressMsg> progressMessages) {
            List<addonDataFromRepo> addonCollection = new List<addonDataFromRepo>();
            try {
                foreach (Source repoSource in repo.Sources) {
                    progressMessages.Report(new taskProgressMsg { currentMsg = $"Checking Addons at repo: {repoSource.Repo}" });
                    // Pull Atom data before hitting repo, so we can attempt to match Atom(XML) data with the release(JSON) data from Github.
                    List<atomDataResult> atomData = returnAtomData(repoSource.Repo, progressMessages);
                    List<addonDataFromRepo> foo = await returnChildRepoData(repoSource.Repo, atomData, progressMessages);
                    addonCollection = addonCollection.Concat(foo).ToList();
                }
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnAddonData", showAsPopup = true, exceptionContent = ex });
            }
            return addonCollection;
        } // end returnAddonData

        private async Task<List<addonDataFromRepo>> returnChildRepoData(string repoURI, List<atomDataResult> atomData, IProgress<taskProgressMsg> progressMessages) {
            List<addonDataFromRepo> addons = new List<addonDataFromRepo>();
            try {
                HttpResponseMessage webConnectorResponse = await webConnector.GetAsync($"https://raw.githubusercontent.com/{repoURI}/master/addons.json");
                if (webConnectorResponse.IsSuccessStatusCode) {
                    string resultString = await webConnectorResponse.Content.ReadAsStringAsync();
                    addons = JsonConvert.DeserializeObject<List<addonDataFromRepo>>(resultString);
                    for (int i = 0; i < addons.Count; i++) {
                        addons[i].authorRepo = repoURI;
                        addons[i].tagsFlat = string.Join(",", addons[i].Tags);
                        atomDataResult f = atomData.FirstOrDefault(x => x.tag == addons[i].ReleaseTag); // Attempt to match this addon to the atom data passed in.  Not certain this is the best filtering.
                        if (f != null) { addons[i].releaseDate = f.updated; addons[i].repoPicURL = f.repoPicURL; } else { addons[i].releaseDate = DateTimeOffset.MinValue; addons[i].repoPicURL = ""; }
                    }
                }
                webConnectorResponse.Dispose();
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in reutrnChildRepoData", showAsPopup = true, exceptionContent = ex });
            }
            return addons;
        } // end returnChildRepoData

        internal async Task<bool> checkAndInstallDependencies(IProgress<taskProgressMsg> progressMessages) {
            try {
                // Currently, leaving this as a self-contained function.  May merge it with the jToS addon download since the dependancy list comes from that repo data, so we could avoid the second hit to Github.
                if (string.IsNullOrEmpty(rootDir)) { return false; } 
                progressMessages.Report(new taskProgressMsg { currentMsg = "Downloading/Refreshing Dependencies" });
                repoParentData repoSource = new repoParentData();
                HttpResponseMessage webConnectorResponse = await webConnector.GetAsync("https://raw.githubusercontent.com/JTosAddon/Addons/master/managers.json");
                if (webConnectorResponse.IsSuccessStatusCode) {
                    string resultString = await webConnectorResponse.Content.ReadAsStringAsync();
                    repoSource = JsonConvert.DeserializeObject<repoParentData>(resultString);
                } else {
                    progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnParentRepoData: ", showAsPopup = true, exceptionContent = new HttpRequestException() });
                }
                if (repoSource.Dependencies != null && repoSource.Dependencies.Count > 0) {
                    foreach (Dependency q in repoSource.Dependencies) {
                        // Currently, we will just overwrite existing files since there isn't recorded version information.
                        string depFilename = System.IO.Path.GetFileName(q.Url.AbsolutePath);
                        progressMessages.Report(new taskProgressMsg { currentMsg = $"Downloading {depFilename}" });
                        webConnectorResponse = await webConnector.GetAsync(q.Url);
                        if (webConnectorResponse.IsSuccessStatusCode) {
                            System.IO.FileStream fs = new System.IO.FileStream($"{rootDir}/release/lua/{depFilename}", System.IO.FileMode.Create);
                            await webConnectorResponse.Content.CopyToAsync(fs); fs.Close(); fs.Dispose();
                        }
                    }
                }
                webConnectorResponse.Dispose();
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
                    string resultString = await webConnectorResponse.Content.ReadAsStringAsync();
                    webConnectorResponse.Dispose();
                    listOfBrokenAddons = JsonConvert.DeserializeObject<BrokenAddonsData>(resultString).Addons;
                }
                webConnectorResponse.Dispose();
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnBrokenAddonData", showAsPopup = true, exceptionContent = ex });
            }
            return listOfBrokenAddons;
        } // end returnBrokenAddonData

        internal async Task<List<addonInstallerOverride>> returnAddonInstallerOverride(IProgress<taskProgressMsg> progressMessages) {
            List<addonInstallerOverride> listOfAddonOverrides = new List<addonInstallerOverride>();
            try {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Checking Addon Overrides" });
                HttpResponseMessage webConnectorResponse = await webConnector.GetAsync("https://raw.githubusercontent.com/iwiwao/ToSAMOverrides/master/addonOverrides.json");
                if (webConnectorResponse.IsSuccessStatusCode) {
                    string resultString = await webConnectorResponse.Content.ReadAsStringAsync();
                    webConnectorResponse.Dispose();
                    listOfAddonOverrides = JsonConvert.DeserializeObject<List<addonInstallerOverride>>(resultString);
                }
                webConnectorResponse.Dispose();
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = "Error in returnAddonInstallerOverride", showAsPopup = true, exceptionContent = ex });
            }
            return listOfAddonOverrides;
        }

        internal List<atomDataResult> returnAtomData(string repo, IProgress<taskProgressMsg> progressMessages) {
            List<atomDataResult> ret = new List<atomDataResult>();
            try {
                bool continueLoop = true; // Limited to 10 feed items per result.  Need to append "?after={tagName}" when we pull additional pages.
                string after = "";
                do {
                    XmlDocument xmlD = new XmlDocument();
                    xmlD.Load($"https://github.com/{repo}/releases.atom{after}");
                    string jsonText = JsonConvert.SerializeXmlNode(xmlD);
                    if (!jsonText.Contains("entry")) { break; }  // I mean, there is probably a smarter way to do this.  
                    atomConversion atomConv = JsonConvert.DeserializeObject<atomConversion>(jsonText);
                    foreach (Entry q in atomConv.Feed.Entry) {
                        if (q == null) { continue; }
                        string[] idSPlit = q.Id.Split('/'); // The last element (splitting on /) of the ID will have the tag we need for the ?after= string.  Example: <id>tag:github.com,2008:Repository/71538918/zoomyplus_tbl</id>
                        string tag = idSPlit[idSPlit.Count() - 1];
                        after = $"?after={tag}";
                        ret.Add(new atomDataResult { title = q.Title, tag = tag, updated = q.Updated, repoPicURL = q.MediaThumbnail != null ? q.MediaThumbnail.Url : "" });
                    }
                    if (atomConv.Feed.Entry.Count < 10) { continueLoop = false; }
                } while (continueLoop);
            } catch (Exception ex) {
                progressMessages.Report(new taskProgressMsg { currentMsg = $"Error in returnAtomData - {repo}", showAsPopup = true, exceptionContent = ex });
            }
            return ret;
        } // end returnAtomData
    }
}
