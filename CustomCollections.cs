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

    // Addon Repo Data Structure - Github API JSON
    public partial class addonDataFromRepoAPI {
        public Uri Url { get; set; }
        public Uri AssetsUrl { get; set; }
        public string UploadUrl { get; set; }
        public Uri HtmlUrl { get; set; }
        public long Id { get; set; }
        public string NodeId { get; set; }
        public string TagName { get; set; }
        public string TargetCommitish { get; set; }
        public string Name { get; set; }
        public bool Draft { get; set; }
        public Author Author { get; set; }
        public bool Prerelease { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public List<Asset> Assets { get; set; }
        public Uri TarballUrl { get; set; }
        public Uri ZipballUrl { get; set; }
        public string Body { get; set; }
    }

    public partial class Asset {
        public Uri Url { get; set; }
        public long Id { get; set; }
        public string NodeId { get; set; }
        public string Name { get; set; }
        public object Label { get; set; }
        public Author Uploader { get; set; }
        public string ContentType { get; set; }
        public string State { get; set; }
        public long Size { get; set; }
        public long DownloadCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public Uri BrowserDownloadUrl { get; set; }
    }

    public partial class Author {
        public string Login { get; set; }
        public long Id { get; set; }
        public string NodeId { get; set; }
        public Uri AvatarUrl { get; set; }
        public string GravatarId { get; set; }
        public Uri Url { get; set; }
        public Uri HtmlUrl { get; set; }
        public Uri FollowersUrl { get; set; }
        public string FollowingUrl { get; set; }
        public string GistsUrl { get; set; }
        public string StarredUrl { get; set; }
        public Uri SubscriptionsUrl { get; set; }
        public Uri OrganizationsUrl { get; set; }
        public Uri ReposUrl { get; set; }
        public string EventsUrl { get; set; }
        public Uri ReceivedEventsUrl { get; set; }
        public string Type { get; set; }
        public bool SiteAdmin { get; set; }
    }

    // Broken Addon Structure
    public partial class BrokenAddonsData {
        public long Tosversion { get; set; }
        public List<brokenAddons> Addons { get; set; }
    }

    public partial class brokenAddons {
        public string File { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
    }

    // Addon Repo Data from addons.json
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

    public class programSettings {
        public string tosRootDir { get; set; }
        public bool checkForUpdates { get; set; }
    }

    public class installedAddons {
        public string addonName { get; set; }
        public string addonFilename { get; set; }
        public string addonVersion { get; set; }
        public string addonRepo { get; set; }
        public string addonAuthorRepo { get; set; }
        public DateTime installDate { get; set; }
    }

    public class addonDisplayData {
        public string name { get; set; }
        public string author { get; set; }
        public string whichRepo { get; set; }
        public System.Windows.Documents.Hyperlink authorRepoUri { get; set; }
        public string installedVersion { get; set; }
        public string availableVersion { get; set; }
        public string description { get; set; }
        public System.Windows.Media.SolidColorBrush installStatusColor { get; set; }
        public System.Windows.Visibility allowInstall { get; set; }
        public System.Windows.Visibility allowDelete { get; set; }
    }
}
