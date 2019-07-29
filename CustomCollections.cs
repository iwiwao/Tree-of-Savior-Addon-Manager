using System;
using System.Collections.Generic;

namespace ToSAddonManager {
    internal class taskProgressMsg {
        internal string currentMsg { get; set; }
        internal bool showAsPopup { get; set; }
        internal Exception exceptionContent { get; set; }
    }

    // Parent Repo Structure
    public class repoParentData {
        public List<Dependency> Dependencies { get; set; }
        public List<Source> Sources { get; set; }
    }

    public class Dependency {
        public Uri Url { get; set; }
    }

    public class Source {
        public string Repo { get; set; }
    }

    public class programSettings {
        public string tosRootDir { get; set; }
    }

    // Addon Repo Data Structure
    public class addonDataFromRepo {
        public string Name { get; set; }
        public string File { get; set; }
        public string Extension { get; set; }
        public string FileVersion { get; set; }
        public string ReleaseTag { get; set; }
        public string Unicode { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        public string tagsFlat { get; set; }
        public string authorRepo { get; set; }
        public string whichRepo { get; set; }
        public string filterCheck { get { return string.Concat(Name, File, Description, tagsFlat, authorRepo).ToLower(); } }
    }

    public class installedAddons {
        public string addonName { get; set; }
        public string addonVersion { get; set; }
        public string addonRepo { get; set; }
        public string addonAuthorRepo { get; set; }
        public DateTime installDate { get; set; }
    }
}
