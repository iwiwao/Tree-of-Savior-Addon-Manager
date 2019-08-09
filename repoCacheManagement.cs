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
                        //repoParentData iToSRepo = await returnParentRepoData("https://raw.githubusercontent.com/Tree-of-Savior-Addon-Community/Addons/master/addons.json", progressMessages); // Not as up-to-date as JTosAddon
                        repoParentData iToSRepo = await returnParentRepoData("https://raw.githubusercontent.com/JTosAddon/Addons/itos/managers.json", progressMessages);
                        List<addonDataFromRepo> foo = await returnAddonData(iToSRepo, progressMessages);
                        addonCollection = foo;
                        break;
                    case 1:
                        progressMessages.Report(new taskProgressMsg { currentMsg = "Checking jToS Addons" });
                        repoParentData jToSRepo = await returnParentRepoData("https://raw.githubusercontent.com/JTosAddon/Addons/master/managers.json", progressMessages);
                        List<addonDataFromRepo> bar = await returnAddonData(jToSRepo, progressMessages);
                        addonCollection = bar;
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
                    webConnectorResponse.Dispose();
                    List<addonDataFromRepo> tempAddons = new List<addonDataFromRepo>();
                    tempAddons = JsonConvert.DeserializeObject<List<addonDataFromRepo>>(resultString);
                    //addons.Select(x => { x.authorRepo = repoURI; x.tagsFlat = string.Join(",", x.Tags); return x; }).ToList();
                    foreach (addonDataFromRepo q in tempAddons) {
                        DateTimeOffset releaseDate = DateTimeOffset.MinValue;
                        string repoPicURL = "";
                        atomDataResult f = atomData.FirstOrDefault(x => x.tag == q.ReleaseTag); // Attempt to match this addon to the atom data passed in.  Not certain this is the best filtering.
                        if (f != null) { releaseDate = f.updated; repoPicURL = f.repoPicURL; }
                        addons.Add(new addonDataFromRepo { authorRepo = repoURI, Description = q.Description, Extension = q.Extension, File = q.File, FileVersion = q.FileVersion, Name = q.Name, ReleaseTag = q.ReleaseTag, Tags = q.Tags, tagsFlat = string.Join(",", q.Tags), Unicode = q.Unicode, whichRepo = q.whichRepo, releaseDate = releaseDate, repoPicURL = repoPicURL });
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
                        ret.Add(new atomDataResult { title = q.Title, tag = tag, updated = q.Updated, repoPicURL = q.MediaThumbnail != null ? q.MediaThumbnail.Url : ""});
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
