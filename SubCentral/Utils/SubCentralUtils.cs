﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;
using SubCentral.Localizations;
using SubCentral.Enums;
using SubCentral.Structs;
using SubCentral.Settings;
using SubCentral.Settings.Data;
using NLog;


namespace SubCentral.Utils {
    public static class SubCentralUtils {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static readonly string SettingsFileName = "SubCentral.xml";
        public static string LogFileName = "SubCentral.log";
        public static string OldLogFileName = "SubCentral.log.bak";

        public static Dictionary<string, string> SubsLanguages {
            get {
                return _subsLanguages;
            }
            set {
                _subsLanguages = value;
            }
        }
        private static Dictionary<string, string> _subsLanguages = null;

        public static List<string> SubsDownloaderNames {
            get {
                return _subsDownloaderNames;
            }
            set {
                _subsDownloaderNames = value;
            }
        }
        private static List<string> _subsDownloaderNames = null;

        public static List<SettingsFolder> AllFolders {
            get {
                if (_allFolders == null) 
                    _allFolders = getAllFolders();
                return _allFolders;
            }
        }
        private static List<SettingsFolder> _allFolders = null;

        public static List<SettingsGroup> getAllProviderGroups() {
            List<SettingsGroup> result = new List<SettingsGroup>();

            // default groups
            SettingsGroup newSettingsGroup = null;

            newSettingsGroup = new SettingsGroup() {
                Title = Localization.AllProviders,
                Providers = getAllProvidersAsEnabledOrDisabled(true),
                Enabled = Settings.SettingsManager.Properties.GeneralSettings.AllProvidersEnabled,
                DefaultForMovies = Settings.SettingsManager.Properties.GeneralSettings.AllProvidersForMovies,
                DefaultForTVShows = Settings.SettingsManager.Properties.GeneralSettings.AllProvidersForTVShows
            };
            result.Add(newSettingsGroup);

            newSettingsGroup = new SettingsGroup() {
                Title = Localization.AllEnabledProviders,
                Providers = getAllProviders(),
                Enabled = Settings.SettingsManager.Properties.GeneralSettings.EnabledProvidersEnabled,
                DefaultForMovies = Settings.SettingsManager.Properties.GeneralSettings.EnabledProvidersForMovies,
                DefaultForTVShows = Settings.SettingsManager.Properties.GeneralSettings.EnabledProvidersForTVShows
            };
            result.Add(newSettingsGroup);

            result.AddRange(Settings.SettingsManager.Properties.GeneralSettings.Groups);

            if (!groupsHaveDefaultForMovies(result)) {
                result[0].DefaultForMovies = true;
            }

            if (!groupsHaveDefaultForTVShows(result)) {
                result[0].DefaultForTVShows = true;
            }

            return result;
        }

        public static List<SettingsGroup> getEnabledProviderGroups() {
            List<SettingsGroup> result = getAllProviderGroups();
            List<SettingsGroup> toRemove = new List<SettingsGroup>();

            foreach (SettingsGroup settingsGroup in result) {
                if (!settingsGroup.Enabled)
                    toRemove.Add(settingsGroup);
            }

            foreach (SettingsGroup settingsGroup in toRemove) {
                result.Remove(settingsGroup);
            }

            return result;
        }

        public static List<SettingsProvider> getEnabledProvidersFromGroup(SettingsGroup settingsGroup) {
            List<SettingsProvider> result = new List<SettingsProvider>();
            List<SettingsProvider> toRemove = new List<SettingsProvider>();

            if (settingsGroup == null || settingsGroup.Providers == null || settingsGroup.Providers.Count == 0) return result;

            foreach (SettingsProvider settingsProvider in settingsGroup.Providers) {
                if (settingsProvider.Enabled)
                    result.Add(settingsProvider);
            }

            foreach (SettingsProvider settingsProvider in result) {
                if (!SubsDownloaderNames.Contains(settingsProvider.ID))
                    toRemove.Add(settingsProvider);
            }
            foreach (SettingsProvider settingsProvider in toRemove) {
                result.Remove(settingsProvider);
            }
            
            return result;
        }

        public static SettingsGroup getDefaultGroupForSearchType(SubtitlesSearchType searchType) {
            SettingsGroup result = null;

            List<SettingsGroup> allProviderGroups = getAllProviderGroups();

            if (allProviderGroups == null || allProviderGroups.Count == 0) return result;

            foreach (SettingsGroup settingsGroup in allProviderGroups) {
                switch (searchType) {
                    case SubtitlesSearchType.IMDb:
                    case SubtitlesSearchType.MOVIE:
                        if (settingsGroup.Enabled && settingsGroup.DefaultForMovies && groupHasEnabledProviders(settingsGroup))
                            return settingsGroup;
                        break;
                    case SubtitlesSearchType.TVSHOW:
                        if (settingsGroup.Enabled && settingsGroup.DefaultForTVShows && groupHasEnabledProviders(settingsGroup))
                            return settingsGroup;
                        break;
                }
            }
            return result;
        }
        
        private static bool groupsHaveDefaultForMovies(List<SettingsGroup> settingsGroups) {
            if (settingsGroups == null || settingsGroups.Count == 0) return false;

            foreach (SettingsGroup settingsGroup in settingsGroups) {
                if (settingsGroup.DefaultForMovies) {
                    return true;
                }
            }

            return false;
        }

        private static bool groupsHaveDefaultForTVShows(List<SettingsGroup> settingsGroups) {
            if (settingsGroups == null || settingsGroups.Count == 0) return false;

            foreach (SettingsGroup settingsGroup in settingsGroups) {
                if (settingsGroup.DefaultForTVShows) {
                    return true;
                }
            }

            return false;
        }

        public static bool groupHasEnabledProviders(SettingsGroup settingsGroup) {
            return getEnabledProvidersFromGroup(settingsGroup).Count > 0;
        }

        public static List<SettingsProvider> getAllProviders() {
            List<SettingsProvider> result = new List<SettingsProvider>();
            List<SettingsProvider> toRemove = new List<SettingsProvider>();

            result.AddRange(Settings.SettingsManager.Properties.GeneralSettings.Providers);

            foreach (SettingsProvider settingsProvider in result) {
                if (!SubsDownloaderNames.Contains(settingsProvider.ID))
                    toRemove.Add(settingsProvider);
            }

            foreach (SettingsProvider settingsProvider in toRemove) {
                result.Remove(settingsProvider);
            }

            foreach (string provider in SubsDownloaderNames) {
                bool found = false;
                foreach (SettingsProvider settingsProvider in result) {
                    if (settingsProvider.ID == provider)
                        found = true;
                }
                if (!found) {
                    SettingsProvider newSettingsProvider = new SettingsProvider() {
                        ID = provider,
                        Title = provider,
                        Enabled = true // enabled by default
                    };

                    result.Add(newSettingsProvider);
                }
            }

            Settings.SettingsManager.Properties.GeneralSettings.Providers.Clear();
            Settings.SettingsManager.Properties.GeneralSettings.Providers.AddRange(result);

            return result;
        }

        public static List<SettingsProvider> getAllProvidersAsEnabledOrDisabled(bool enabled) {
            List<SettingsProvider> result = new List<SettingsProvider>();

            foreach (SettingsProvider settingsProvider in getAllProviders()) {
                SettingsProvider newSettingsProvider = new SettingsProvider() {
                    ID = settingsProvider.ID,
                    Title = settingsProvider.Title,
                    Enabled = enabled // enabled by default
                };
                result.Add(newSettingsProvider);
            }

            return result;
        }

        public static List<SettingsProvider> getEnabledProviders() {
            List<SettingsProvider> result = getAllProviders();
            List<SettingsProvider> toRemove = new List<SettingsProvider>();

            foreach (SettingsProvider settingsProvider in result) {
                if (!settingsProvider.Enabled)
                    toRemove.Add(settingsProvider);
            }

            foreach (SettingsProvider settingsProvider in toRemove) {
                result.Remove(settingsProvider);
            }

            return result;
        }

        public static List<string> getProviderIDs(List<SettingsProvider> settingsProviders) {
            List<string> result = new List<string>();

            if (settingsProviders == null || settingsProviders.Count == 0) return result;

            foreach (SettingsProvider settingsProvider in settingsProviders) {
                result.Add(settingsProvider.ID);
            }
            
            return result;
        }

        public static Dictionary<string, string> getProviderIDsAndTitles(List<SettingsProvider> settingsProviders) {
            Dictionary<string, string> result = new Dictionary<string, string>();

            if (settingsProviders == null || settingsProviders.Count == 0) return result;

            foreach (SettingsProvider settingsProvider in settingsProviders) {
                result.Add(settingsProvider.ID, settingsProvider.Title);
            }

            return result;
        }

        public static List<SettingsLanguage> getAllLanguages() {
            List<SettingsLanguage> result = new List<SettingsLanguage>();
            List<SettingsLanguage> toRemove = new List<SettingsLanguage>();

            result.AddRange(Settings.SettingsManager.Properties.LanguageSettings.Languages);

            foreach (SettingsLanguage settingsLanguage in result) {
                if (!SubsLanguages.ContainsKey(settingsLanguage.LanguageCode))
                    toRemove.Add(settingsLanguage);
            }

            foreach (SettingsLanguage settingsLanguage in toRemove) {
                result.Remove(settingsLanguage);
            }

            foreach (KeyValuePair<string, string> kvp in SubsLanguages) {
                bool found = false;
                foreach (SettingsLanguage settingsLanguage in result) {
                    if (settingsLanguage.LanguageCode == kvp.Key)
                        found = true;
                }
                if (!found) {
                    SettingsLanguage newSettingsLanguage = new SettingsLanguage() {
                        LanguageCode = kvp.Key,
                        LanguageName = kvp.Value,
                        Enabled = false // enabled by default
                    };

                    result.Add(newSettingsLanguage);
                }
            }

            if (!hasEnabledLanguage(result)) {
                enableDefaultLanguage(result);
            }

            Settings.SettingsManager.Properties.LanguageSettings.Languages.Clear();
            Settings.SettingsManager.Properties.LanguageSettings.Languages.AddRange(result);

            return result;
        }

        public static bool hasEnabledLanguage(List<SettingsLanguage> languages) {
            if (languages == null || languages.Count == 0 ) return false;

            foreach (SettingsLanguage settingsLanguage in languages) {
                if (settingsLanguage.Enabled)
                    return true;
            }
            return false;
        }

        public static bool enableDefaultLanguage(List<SettingsLanguage> languages) {
            if (languages == null || languages.Count == 0) return false;

            foreach (SettingsLanguage settingsLanguage in languages) {
                if (settingsLanguage.LanguageName == getUILanguage()) {
                    settingsLanguage.Enabled = true;
                    return true;
                }
            }
            return false;
        }

        public static string getUILanguage() {
            string result = string.Empty;

            try {
                result = GUILocalizeStrings.CurrentLanguage();
            }
            catch {
                try {
                    result = CultureInfo.CurrentUICulture.Name.Substring(0, 2);
                    CultureInfo ci = CultureInfo.GetCultureInfo(result);
                    result = ci.EnglishName;
                }
                catch {
                    result = string.Empty;
                }
            }

            if (!SubsLanguages.ContainsValue(result) || string.IsNullOrEmpty(result))
                return "English";
            
            return result;
        }

        public static List<string> getSelectedLanguageNames() {
            List<string> result = new List<string>();
            List<SettingsLanguage> allLanguages = getAllLanguages();

            foreach (SettingsLanguage settingsLanguage in allLanguages) {
                if (settingsLanguage.Enabled)
                    result.Add(settingsLanguage.LanguageName);
            }

            return result;
        }

        public static List<string> getSelectedLanguageCodes() {
            List<string> result = new List<string>();
            List<SettingsLanguage> allLanguages = getAllLanguages();

            foreach (SettingsLanguage settingsLanguage in allLanguages) {
                if (settingsLanguage.Enabled)
                    result.Add(settingsLanguage.LanguageCode);
            }
            
            return result;
        }

        public static int getLanguagePriorityByCode(string languageCode) {
            int result = int.MaxValue;

            List<SettingsLanguage> allLanguages = SubCentralUtils.getAllLanguages();

            if (allLanguages == null || allLanguages.Count == 0) return result;

            for (int i = 0; i < allLanguages.Count; i++) {
                SettingsLanguage settingsLanguage = allLanguages[i];
                if (settingsLanguage.LanguageCode.Equals(languageCode))
                    return i + 1;
            }
            return result;
        } 

        public static List<MultiSelectionItem> getLanguageNamesForMultiSelection() {
            List<MultiSelectionItem> result = new List<MultiSelectionItem>();
            List<SettingsLanguage> allLanguages = getAllLanguages();

            foreach (SettingsLanguage settingsLanguage in allLanguages) {
                MultiSelectionItem multiSelectionItem = new MultiSelectionItem();
                multiSelectionItem.ItemID = settingsLanguage.LanguageCode;
                multiSelectionItem.ItemTitle = settingsLanguage.LanguageName;
                multiSelectionItem.Selected = settingsLanguage.Enabled;

                result.Add(multiSelectionItem);
            }

            return result;
        }

        public static void setLanguageNamesFromMultiSelection(List<MultiSelectionItem> selectedLanguages) {
            if (selectedLanguages == null || selectedLanguages.Count == 0) return;

            List<SettingsLanguage> allLanguages = getAllLanguages();

            foreach (MultiSelectionItem multiSelectionItem in selectedLanguages) {
                foreach (SettingsLanguage settingsLanguage in Settings.SettingsManager.Properties.LanguageSettings.Languages) {
                    if (settingsLanguage.LanguageCode == multiSelectionItem.ItemID) {
                        if (multiSelectionItem.Selected)
                            settingsLanguage.Enabled = true;
                        else
                            settingsLanguage.Enabled = false;
                    }
                }
            }
        }

        public static int getSelectedLanguagesCountFromMultiSelection(List<MultiSelectionItem> selectedLanguages) {
            if (selectedLanguages == null) return 0;

            int result = 0;

            foreach (MultiSelectionItem multiSelectionItem in selectedLanguages) {
                if (multiSelectionItem.Selected) {
                    result++;
                }
            }
            
            return result;
        }

        private static List<SettingsFolder> getAllFolders() {
            List<SettingsFolder> result = new List<SettingsFolder>();
            List<SettingsFolder> toRemove = new List<SettingsFolder>();
            List<string> subtitlesPathsMP = new List<string>();
            int index;

            MediaPortal.Profile.Settings mpSettings = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml"));
            string subtitlesPathsSetting = mpSettings.GetValueAsString("subtitles", "paths", @".\");
            foreach (string subtitlesPath in subtitlesPathsSetting.Split(new string[] { "," }, StringSplitOptions.None)) {
                string subtitlesPathToAdd = subtitlesPath.Trim();
                if (pathNameIsValid(subtitlesPathToAdd))
                    subtitlesPathsMP.Add(ensureBackSlash(subtitlesPathToAdd));
            }

            foreach (string subtitlesPath in subtitlesPathsMP) {
                if (containsPath(Settings.SettingsManager.Properties.FolderSettings.Folders, subtitlesPath, out index)) {
                    SettingsFolder settingsFolder = Settings.SettingsManager.Properties.FolderSettings.Folders[index];
                    SettingsFolder newSettingsFolder = new SettingsFolder() {
                        Folder = settingsFolder.Folder,
                        Enabled = settingsFolder.Enabled,
                        //Existing = pathExists(settingsFolder.Folder),
                        //Writable = pathIsWritable(settingsFolder.Folder),
                        DefaultForMovies = settingsFolder.DefaultForMovies,
                        DefaultForTVShows = settingsFolder.DefaultForTVShows
                    };

                    result.Add(newSettingsFolder);
                }
                else {
                    SettingsFolder newSettingsFolder = new SettingsFolder() {
                        Folder = subtitlesPath,
                        Enabled = true,
                        //Existing = pathExists(subtitlesPath),
                        //Writable = pathIsWritable(subtitlesPath),
                        DefaultForMovies = false,
                        DefaultForTVShows = false
                    };

                    result.Add(newSettingsFolder);
                }
            }

            // ensure path .\ if empty - default
            if (result.Count == 0) {
                SettingsFolder newSettingsFolder = new SettingsFolder() {
                    Folder = @".\",
                    Enabled = true,
                    DefaultForMovies = true,
                    DefaultForTVShows = true
                };

                result.Insert(0, newSettingsFolder);
            }

            Settings.SettingsManager.Properties.FolderSettings.Folders.Clear();
            Settings.SettingsManager.Properties.FolderSettings.Folders.AddRange(result);

            return result;
        }

        public static List<FolderSelectionItem> getEnabledAndValidFoldersForMedia(FileInfo fileInfo, bool includeReadOnly) {
            List<FolderSelectionItem> result = new List<FolderSelectionItem>();
            List<SettingsFolder> allFolders = AllFolders;
            List<SettingsFolder> toRemove = new List<SettingsFolder>();

            // remove not enabled and if fileinfo is null all relative paths
            foreach (SettingsFolder settingsFolder in allFolders) {
                if (!settingsFolder.Enabled) {
                    toRemove.Add(settingsFolder);
                }
                else {
                    if (fileInfo == null && !Path.IsPathRooted(settingsFolder.Folder)) {
                        toRemove.Add(settingsFolder);
                    }
                }
                //if (!settingsFolder.Enabled || !pathExists(settingsFolder.Folder) || !pathIsWritable(settingsFolder.Folder)) {
                //    toRemove.Add(settingsFolder);
                //}
            }

            foreach (SettingsFolder settingsFolder in allFolders) {
                if (toRemove.Contains(settingsFolder)) continue;
                string folder = settingsFolder.Folder;
                if (fileInfo != null && !Path.IsPathRooted(settingsFolder.Folder)) {
                    folder = ResolveRelativePath(settingsFolder.Folder, Path.GetDirectoryName(fileInfo.FullName));
                    //folder = ResolveRelativePathEx(settingsFolder.Folder, Path.GetDirectoryName(fileInfo.FullName));
                }
                if (folder != null) {
                    FolderSelectionItem newFolderSelectionItem = new FolderSelectionItem() {
                        FolderName = folder,
                        //Existing = pathExists(folder),
                        //Writable = pathIsWritable(folder),
                        FolderErrorInfo = getFolderErrorInfo(folder),
                        OriginalFolderName = settingsFolder.Folder,
                        WasRelative = !Path.IsPathRooted(settingsFolder.Folder),
                        DefaultForMovies = settingsFolder.DefaultForMovies,
                        DefaultForTVShows = settingsFolder.DefaultForTVShows
                    };

                    if (!includeReadOnly) {
                        if (newFolderSelectionItem.FolderErrorInfo == FolderErrorInfo.ReadOnly)
                            continue;
                    }

                    result.Add(newFolderSelectionItem);
                }
            }

            return result;
        }

        public static FolderErrorInfo getFolderErrorInfo(string path) {
            FolderErrorInfo result = FolderErrorInfo.OK;

            if (!pathExists(path))
                result = FolderErrorInfo.NonExistant;
            else if (!pathIsWritable(path))
                result = FolderErrorInfo.ReadOnly;

            int iUncPathDepth = uncPathDepth(path);
            if (result == FolderErrorInfo.NonExistant && (!pathDriveIsReady(path) || !uncHostIsAlive(path) || (iUncPathDepth > 0 && iUncPathDepth < 3)))
                result = FolderErrorInfo.ReadOnly;

            return result;
        }

        public static bool uncHostIsAlive(string path) {
            if (string.IsNullOrEmpty(path) || !new Uri(path).IsUnc) return true;

            Uri uri = new Uri(path);
            string hostName = uri.Host;
            bool isAlive = isMachineReachable(hostName);
            if (!isAlive) return false;
            
            return true;
        }

        public static bool pathExists(string path) {
            if (string.IsNullOrEmpty(path)) return false;

            if (!Path.IsPathRooted(path)) return true;

            if (!uncHostIsAlive(path)) return false;
          
            return Directory.Exists(path);
        }

        public static bool pathIsWritable(string path) {
            if (string.IsNullOrEmpty(path)) return false;

            if (!Path.IsPathRooted(path)) return true;

            string fileName = String.Concat(path, Path.DirectorySeparatorChar, Path.GetRandomFileName());
            FileInfo fileInfo = new FileInfo(fileName);

            FileStream stream = null;
            try {
                stream = fileInfo.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            }
            catch {
                return false;
            }
            finally {
                if (stream != null) {
                    try {
                        stream.Close();
                        fileInfo.Delete();
                    }
                    catch {
                    }
                }
            }
            return true;
        }

        public static bool pathDriveIsReady(string path) {
            if (string.IsNullOrEmpty(path) || new Uri(path).IsUnc) return true;

            DriveInfo di = new DriveInfo(path[0].ToString());
            if (!di.IsReady) return false;

            return true;
        }

        public static bool pathIsDrive(string path) {
            if (string.IsNullOrEmpty(path)) return false;

            path = ensureBackSlash(path);
            if (path.Length == 3 && path.EndsWith(@":\")) return true;
            return false;
        }

        public static int uncPathDepth(string path) {
            if (string.IsNullOrEmpty(path) || !new Uri(path).IsUnc) return 0;

            path = ensureBackSlash(path);
            path = path.Substring(0, path.Length - 1);

            string[] check = path.Substring(2).Split(new char[] { Path.DirectorySeparatorChar });
            return check.Length;
        }

        private static bool foldersHaveDefaultForMovies(List<SettingsFolder> settingsFolders) {
            if (settingsFolders == null || settingsFolders.Count == 0) return false;

            foreach (SettingsFolder settingsFolder in settingsFolders) {
                if (settingsFolder.DefaultForMovies) {
                    return true;
                }
            }

            return false;
        }

        private static bool foldersHaveDefaultForTVShows(List<SettingsFolder> settingsFolders) {
            if (settingsFolders == null || settingsFolders.Count == 0) return false;

            foreach (SettingsFolder settingsFolder in settingsFolders) {
                if (settingsFolder.DefaultForTVShows) {
                    return true;
                }
            }

            return false;
        }

        public static bool containsPath(List<SettingsFolder> settingsFolders, string path, out int index) {
            index = -1;

            if (settingsFolders == null || settingsFolders.Count == 0) return false;

            int tempIndex = 0;
            foreach (SettingsFolder settingsFolder in settingsFolders) {
                if (settingsFolder.Folder.Equals(path)) {
                    index = tempIndex;
                    return true;
                }
                tempIndex++;
            }

            return false;
        }

        public static bool fileNameIsValid(string fileName) {
            if (string.IsNullOrEmpty(fileName)) return false;

            foreach (char lDisallowed in System.IO.Path.GetInvalidFileNameChars()) {
                if (fileName.Contains(lDisallowed.ToString()))
                    return false;
            }
            foreach (char lDisallowed in System.IO.Path.GetInvalidPathChars()) {
                if (fileName.Contains(lDisallowed.ToString()))
                    return false;
            }
            return true;
        }

        public static bool pathNameIsValid(string path) {
            if (string.IsNullOrEmpty(path)) return false;

            foreach (char lDisallowed in System.IO.Path.GetInvalidPathChars()) {
                if (path.Contains(lDisallowed.ToString()))
                    return false;
            }
            return true;
        }

        public static string ensureBackSlash(string path) {
            if (string.IsNullOrEmpty(path)) return null;

            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
                return path;
            else
                return path + Path.DirectorySeparatorChar.ToString();
        }

        public static string ResolveRelativePath(string relativePath, string referencePath) {
            if (string.IsNullOrEmpty(referencePath)) {
                throw new ArgumentNullException("basePath");
            }

            if (string.IsNullOrEmpty(relativePath)) {
                throw new ArgumentNullException("relativePath");
            }

            var result = referencePath;

            if (Path.IsPathRooted(relativePath)) {
                return relativePath;
            }

            if (relativePath.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                relativePath = relativePath.Substring(0, relativePath.Length - 1);
            }

            if (relativePath == ".") {
                return referencePath;
            }

            if (relativePath.StartsWith(@".\")) {
                relativePath = relativePath.Substring(2);
            }

            relativePath = relativePath.Replace(@"\.\", @"\");
            if (!relativePath.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                relativePath = relativePath + Path.DirectorySeparatorChar.ToString();
            }

            while (!string.IsNullOrEmpty(relativePath)) {
                int lengthOfOperation = relativePath.IndexOf(Path.DirectorySeparatorChar.ToString()) + 1;
                var operation = relativePath.Substring(0, lengthOfOperation - 1);
                relativePath = relativePath.Remove(0, lengthOfOperation);

                if (operation == @"..") {
                    Uri uri = new Uri(Path.Combine(result, operation));
                    if (uri.IsUnc && Path.GetDirectoryName(result) == null) {
                        result = uri.LocalPath;
                    }
                    else {
                        result = Path.GetDirectoryName(result);
                    }
                }
                else {
                    result = Path.Combine(result, operation);
                }

                if (result == null) return result;
            }

            if (uncPathDepth(result) == 1)
                return null;
            return result;
        }

        public static bool isMachineReachable(string hostName) {
            System.Net.IPHostEntry host = System.Net.Dns.GetHostEntry(hostName);

            string wqlTemplate = "SELECT StatusCode FROM Win32_PingStatus WHERE Address = '{0}'";

            System.Management.ManagementObjectSearcher query = new System.Management.ManagementObjectSearcher();

            query.Query = new System.Management.ObjectQuery(String.Format(wqlTemplate, host.AddressList[0]));

            query.Scope = new System.Management.ManagementScope("//localhost/root/cimv2");

            System.Management.ManagementObjectCollection pings = query.Get();

            foreach (System.Management.ManagementObject ping in pings) {
                if (Convert.ToInt32(ping.GetPropertyValue("StatusCode")) == 0)
                    return true;
            }

            return false;
        }

        public static SubtitlesSearchType getSubtitlesSearchTypeFromMediaDetail(BasicMediaDetail basicMediaDetail) {
            SubtitlesSearchType result = SubtitlesSearchType.NONE;

            bool useImdbMovieQuery = !(string.IsNullOrEmpty((basicMediaDetail.ImdbID)));
            bool useTitle = !(string.IsNullOrEmpty((basicMediaDetail.Title)));
            bool useMovieQuery = useTitle && !(string.IsNullOrEmpty((basicMediaDetail.YearStr)));
            bool useEpisodeQuery = useTitle && !(string.IsNullOrEmpty((basicMediaDetail.SeasonStr))) && !(string.IsNullOrEmpty((basicMediaDetail.EpisodeStr)));

            if (useEpisodeQuery) {
                result = SubtitlesSearchType.TVSHOW;
            }
            if (useImdbMovieQuery){
                result = SubtitlesSearchType.IMDb;
            }
            else if (useMovieQuery) {
                result = SubtitlesSearchType.MOVIE;
            }

            return result;
        }

        public static bool canSearchMediaDetailWithType(BasicMediaDetail basicMediaDetail, SubtitlesSearchType subtitlesSearchType) {
            bool useImdbMovieQuery = !(string.IsNullOrEmpty((basicMediaDetail.ImdbID)));
            bool useTitle = !(string.IsNullOrEmpty((basicMediaDetail.Title)));
            bool useMovieQuery = useTitle && !(string.IsNullOrEmpty((basicMediaDetail.YearStr)));
            bool useEpisodeQuery = useTitle && !(string.IsNullOrEmpty((basicMediaDetail.SeasonStr))) && !(string.IsNullOrEmpty((basicMediaDetail.EpisodeStr)));

            switch (subtitlesSearchType) {
                case SubtitlesSearchType.IMDb:
                    return useImdbMovieQuery;
                case SubtitlesSearchType.TVSHOW:
                    return useEpisodeQuery;
                case SubtitlesSearchType.MOVIE:
                    return useMovieQuery;
                default:
                    return false;
            }
        }

        public static bool isImdbIdCorrect(string imdbId) {
            if (string.IsNullOrEmpty(imdbId)) return false;

            return imdbId.Length == 9 && Regex.Match(imdbId, @"tt\d{7}").Success;
        }

        public static bool isYearCorrect(string year) {
            if (string.IsNullOrEmpty(year)) return false;

            //bool result = year.Length == 4 && Regex.Match(year, @"\d{4}").Success;

            //if (result) {
            int intYear = -1;
            if (int.TryParse(year, out intYear)) {
                if (intYear < 1900 || intYear > yearToRange()) {
                    return false;
                }
            }
            else {
                return false;
            }

            return true;
        }

        public static int yearToRange() {
            //return System.DateTime.Now.Year + 100;
            // TODO MS
            return 2050;
        }

        public static bool isSeasonOrEpisodeCorrect(string seasonOrEpisode) {
            if (string.IsNullOrEmpty(seasonOrEpisode)) return false;

            //bool result = seasonOrEpisode.Length > 0 && seasonOrEpisode.Length < 4 && Regex.Match(seasonOrEpisode, @"\d{1,3}").Success;

            //if (result) {
            int intSeasonOrEpisode = -1;
            if (int.TryParse(seasonOrEpisode, out intSeasonOrEpisode)) {
                if (intSeasonOrEpisode < 1 || intSeasonOrEpisode > 999) {
                    return false;
                }
            }
            else {
                return false;
            }

            return true;
        }

        public static bool IsAssemblyAvailable(string name, Version ver) {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly a in assemblies)
                if (a.GetName().Name == name && a.GetName().Version >= ver)
                    return true;

            return false;
        }

    }
}
