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

    // Addon Repo Data Structure - Github API JSON (Only used for update checker now)
    public partial class addonDataFromRepoAPI {
        public string Name { get; set; }
        public string Body { get; set; }
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
        public DateTimeOffset releaseDate { get; set; }
        public string repoPicURL { get; set; }
        public string filterCheck { get { return string.Concat(Name, File, Description, tagsFlat, authorRepo).ToLower(); } }
    }

    public class programSettings {
        public string tosRootDir { get; set; }
        public bool checkForUpdates { get; set; }
        public DateTime previousUpdateDateStampUTC { get; set; }
    }

    public class installedAddons {
        public string addonName { get; set; }
        public string addonFilename { get; set; }
        public string addonVersion { get; set; }
        public string addonRepo { get; set; }
        public string addonAuthorRepo { get; set; }
        public DateTime installDate { get; set; }
    }

    public class addonInstallerOverride {
        public string filename { get; set; }
        public string fileVersion { get; set; }
        public string whichRepo { get; set; }
        public string addonDirectoryOverride { get; set; }
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

    #region "XML ATOM -> JSON"
    internal class atomDataResult {
        internal string title { get; set; }
        internal string tag { get; set; }
        internal string repoPicURL { get; set; }
        internal DateTimeOffset updated { get; set; }
    }

    public partial class atomConversion {
        public Feed Feed { get; set; }
    }

    public partial class Feed {
        public string Id { get; set; }
        public string Title { get; set; }
        public DateTimeOffset Updated { get; set; }

        [Newtonsoft.Json.JsonConverter(typeof(SingleOrArrayConverter<Entry>))]
        public List<Entry> Entry { get; set; }
    }

    public partial class Entry {
        public string Id { get; set; }
        public DateTimeOffset Updated { get; set; }
        public string Title { get; set; }
        [Newtonsoft.Json.JsonProperty("media:thumbnail")]
        public MediaThumbnail MediaThumbnail { get; set; }
    }

    public partial class MediaThumbnail {
        [Newtonsoft.Json.JsonProperty("@url")]
        public string Url { get; set; }
    }

    class SingleOrArrayConverter<T> : Newtonsoft.Json.JsonConverter {
        public override bool CanConvert(Type objectType) {
            return (objectType == typeof(List<T>));
        }
        public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer) {
            Newtonsoft.Json.Linq.JToken token = Newtonsoft.Json.Linq.JToken.Load(reader);
            if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array) { return token.ToObject<List<T>>(); }
            return new List<T> { token.ToObject<T>() };
        }
        public override bool CanWrite {
            get { return false; }
        }
        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer) {
            throw new NotImplementedException();
        }
    }
    #endregion
}
