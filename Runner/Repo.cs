// todo need a more general way to select
// - specific set of files
// - not get size 0 files
// have a funny issue where if there is no extenion, then the extension is set to "." in the RepoItem returned by methods.  Probably in the m_repoDictionary incorrectly.

// todo need an option where RunnerCatalog gets all files not looking at metrics, but others can use metrics

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace OxRunner
{
    public class RepoItem
    {
        public string GuidName;
        public string Extension;
        public string[] Monikers;
        public FileInfo FiRepoItem;

        private byte[] m_ByteArray;
        public byte[] ByteArray
        {
            get {
                if (m_ByteArray == null)
                    m_ByteArray = File.ReadAllBytes(FiRepoItem.FullName);
                return m_ByteArray;
            }
        }
    }

    public class Repo
    {
        public DirectoryInfo m_repoLocation;
        public FileInfo m_fiMonikerCatalog;
        public XElement m_metricsCatalog = null;
        public Dictionary<string, XElement> m_metricsDictionary = null;

        private bool m_ReadWrite;
        public bool ReadWrite
        {
            get
            {
                return m_ReadWrite;
            }
        }

        public Repo(DirectoryInfo repoLocation)
        {
            m_ReadWrite = false;
            LoadRepo(repoLocation);
        }

        public Repo(DirectoryInfo repoLocation, bool readWrite)
        {
            m_ReadWrite = readWrite;
            LoadRepo(repoLocation);
        }

        private void LoadRepo(DirectoryInfo repoLocation)
        {
            m_repoLocation = repoLocation;
            FileUtils.ThreadSafeCreateDirectory(m_repoLocation);

            m_fiMonikerCatalog = m_repoLocation.GetFiles("MonikerCatalog*").OrderBy(t => t.Name).LastOrDefault();
            if (m_fiMonikerCatalog == null)
            {
                DateTime n = DateTime.Now;
                var monikerName = string.Format("MonikerCatalog-{0:00}-{1:00}-{2:00}-{3:00}{4:00}{5:00}-{6:000}.txt", n.Year - 2000, n.Month, n.Date, n.Hour, n.Minute, n.Second, n.Millisecond);
                m_fiMonikerCatalog = new FileInfo(Path.Combine(m_repoLocation.FullName, monikerName));
            }
            FileUtils.ThreadSafeCreateEmptyTextFileIfNotExist(m_fiMonikerCatalog);

            using (StreamReader sr = new StreamReader(m_fiMonikerCatalog.FullName))
            {
                foreach (var line in Lines(sr))
                {
                    var spl = line.Split('|');
                    var repoItem = new InternalRepoItem();
                    var key = spl[0];
                    repoItem.Extension = spl[1];
                    if (spl[2].Contains(':'))
                        repoItem.Monikers = spl[2].Split(':');
                    else
                        repoItem.Monikers = new string[] { };
                    if (m_repoDictionary.ContainsKey(key))
                    {
                        var repoDictionaryEntry = m_repoDictionary[key];
                        var existingItemWithSameExtension = repoDictionaryEntry.FirstOrDefault(q => q.Extension == repoItem.Extension);
                        if (existingItemWithSameExtension != null)
                        {
                            // following should never get executed.
                            var newMonikers = existingItemWithSameExtension.Monikers.Concat(repoItem.Monikers).OrderBy(m => m).Distinct().ToArray();
                            existingItemWithSameExtension.Monikers = newMonikers;
                        }
                        else
                        {
                            var newInternalRepoItems = repoDictionaryEntry.Concat(new[] { repoItem }).ToArray();
                            m_repoDictionary[key] = newInternalRepoItems;
                        }
                    }
                    else
                        m_repoDictionary.Add(key, new[] { repoItem });
                }
            }
        }

        public bool Store(FileInfo file, string[] monikers)
        {
            if (m_ReadWrite == false)
                throw new Exception("Repo is opened for readonly access");

            foreach (var item in monikers)
            {
                if (item.Contains(':'))
                {
                    Console.WriteLine("Moniker {0} contains colon", item);
                    return false;
                }
                if (item.Contains('|'))
                {
                    Console.WriteLine("Moniker {0} contains pipe symbol", item);
                    return false;
                }
            }

            // Sometimes the file may just have been written, and dropbox may be accessing.
            // If dropbox is accessing, then get UnauthorizedAccessException, so wait a bit, try again.
            while (true)
            {
                try
                {
                    if (!file.Exists)
                    {
                        Console.WriteLine("File {0} does not exist", file.FullName);
                        return false;
                    }
                }
                catch (System.UnauthorizedAccessException)
                {
                    Console.WriteLine("======================================================================================== System.UnauthorizedAccessException");
                    System.Threading.Thread.Sleep(20);
                    continue;
                }
                break;
            }

            string hashString;
            byte[] ba = null;

            while (true)
            {
                try
                {
                    ba = File.ReadAllBytes(file.FullName);
                    using (SHA1Managed sha1 = new SHA1Managed())
                    {
                        byte[] hash = sha1.ComputeHash(ba);
                        StringBuilder formatted = new StringBuilder(2 * hash.Length);
                        foreach (byte b in hash)
                        {
                            formatted.AppendFormat("{0:X2}", b);
                        }
                        hashString = formatted.ToString();
                    }
                }
                catch (System.UnauthorizedAccessException)
                {
                    Console.WriteLine("======================================================================================== System.UnauthorizedAccessException");
                    System.Threading.Thread.Sleep(20);
                    continue;
                }
                break;
            }

            string extensionDirName;
            if (file.Extension == "")
                extensionDirName = "no_extension";
            else
                extensionDirName = file.Extension.TrimStart('.').ToLower();

            // if this hash already exists in the dictionary, then only need to update data structures.
            if (m_repoDictionary.ContainsKey(hashString))
            {
                var hashItem = m_repoDictionary[hashString];
                var forThisExtension = hashItem.FirstOrDefault(gi => gi.Extension == extensionDirName);
                // if the file with this hash exists in the dictionary, but does not exist for this extension, then add it
                if (forThisExtension == null)
                {
                    CopyFileIntoRepo(file, hashString, extensionDirName);
                    InternalRepoItem iri = new InternalRepoItem()
                    {
                        Extension = extensionDirName,
                        Monikers = monikers,
                    };
                    var newGuidItemList = hashItem.Concat(new[] { iri }).ToArray();
                    m_repoDictionary[hashString] = newGuidItemList;
                    return true;
                }
                else
                {
                    // the file with this hash and this extension exists in the dictionary.  Need to update monikers.
                    var newMonikerList = forThisExtension.Monikers.Concat(monikers).Distinct().OrderBy(t => t).ToArray();
                    forThisExtension.Monikers = newMonikerList;
                    return true;
                }
            }

            CopyFileIntoRepo(file, hashString, extensionDirName);

            InternalRepoItem iri2 = new InternalRepoItem()
            {
                Extension = extensionDirName,
                Monikers = monikers,
            };
            m_repoDictionary.Add(hashString, new[] { iri2 });

            if (monikers != null)
            {
                // append to the end of the moniker catalog anyway, even though it will be all be written out by CloseAndSaveMonikerFile
                // gives a chance to recover if the process adding files to the repo crashes in the middle.
                var monikerString = monikers.Select(m => m + ":").StrCat().TrimEnd(':');
                FileUtils.ThreadSafeAppendAllLines(m_fiMonikerCatalog, new[] { hashString + "|" + extensionDirName + "|" + monikerString });
            }
            return true;
        }

        private void CopyFileIntoRepo(FileInfo file, string hashString, string extensionDirName)
        {
            var diSubDir = new DirectoryInfo(Path.Combine(m_repoLocation.FullName, extensionDirName));
            FileUtils.ThreadSafeCreateDirectory(diSubDir);

            var hashSubDir = hashString.Substring(0, 2) + "/";
            var fileBaseName = hashString.Substring(2);
            var diHashSubDir = new DirectoryInfo(Path.Combine(m_repoLocation.FullName, extensionDirName, hashSubDir));
            FileUtils.ThreadSafeCreateDirectory(diHashSubDir);

            var fiFileName = new FileInfo(Path.Combine(m_repoLocation.FullName, extensionDirName, hashSubDir, fileBaseName + file.Extension.ToLower()));
            FileUtils.ThreadSafeCopy(file, fiFileName);

            FileInfo fiToMakeReadonly = new FileInfo(fiFileName.FullName);
            fiToMakeReadonly.IsReadOnly = true;
        }

        public FileInfo SaveMonikerFile()
        {
            if (!m_ReadWrite)
                throw new Exception("Repo is opened in ReadOnly mode");

            DateTime n = DateTime.Now;
            var monikerName = string.Format("MonikerCatalog-{0:00}-{1:00}-{2:00}-{3:00}{4:00}{5:00}-{6:000}.txt", n.Year - 2000, n.Month, n.Date, n.Hour, n.Minute, n.Second, n.Millisecond);
            m_fiMonikerCatalog = new FileInfo(Path.Combine(m_repoLocation.FullName, monikerName));
            if (m_fiMonikerCatalog.Exists)
                m_fiMonikerCatalog.Delete();
            using (StreamWriter sw = new StreamWriter(m_fiMonikerCatalog.FullName))
            {
                foreach (var item in m_repoDictionary)
                {
                    foreach (var item2 in item.Value)
	                {
                        string monikers = item2.Monikers.Select(m => m + ":").StrCat().TrimEnd(':');
                        string line = string.Format("{0}|{1}|{2}", item.Key, item2.Extension, monikers) + Environment.NewLine;
                        sw.Write(line);
	                }
                }
            }
            return m_fiMonikerCatalog;
        }

        #region AccessMethods

        // The methods here do not impact the repo in any way.

        public RepoItem GetRepoItem(string guidName)
        {
            try
            {
                RepoItem repoItem = GetRepoItemInternal(guidName);
                var hashSubDir = guidName.Substring(0, 2) + "/";
                var filename = guidName.Substring(2);
                repoItem.FiRepoItem = new FileInfo(Path.Combine(m_repoLocation.FullName, repoItem.Extension.TrimStart('.'), hashSubDir, filename));
                return repoItem;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public IEnumerable<string> GetAllOpenXmlFiles()
        {
            if (m_metricsCatalog == null)
            {
                LoadMetricsCatalog();
            }
            var retValue = m_repoDictionary
                .Select(di => new
                {
                    GuidId = di.Key,
                    ItemList = di.Value,
                })
                .SelectMany(ri =>
                {
                    var openXmlItems = ri.ItemList
                        .Where(z =>
                            IsWordprocessingML(z.Extension) ||
                            IsSpreadsheetML(z.Extension) ||
                            IsPresentationML(z.Extension))
                        .Where(z =>
                            {
                                if (m_metricsCatalog == null)
                                    return true;

                                var guidName = ri.GuidId + z.Extension;

                                if (!m_metricsDictionary.ContainsKey(guidName))
                                    return false;

                                var fileMetrics = m_metricsDictionary[guidName];

                                // todo need to fix this
                                // todo need an option where RunnerCatalog gets all files not looking at metrics, but others
                                // can use metrics

                                if (fileMetrics.Element("Exception") != null)
                                    return false;
                                return true;
                            })
                        .Select(z => ri.GuidId + z.Extension);
                    return openXmlItems;
                })
                .ToList();
            return retValue;
        }

        enum OpenXmlFileType
        {
            WordprocessingML,
            SpreadsheetML,
            PresentationML,
        }

        public IEnumerable<string> GetWordprocessingMLFiles()
        {
            return GetFilesOfType(OpenXmlFileType.WordprocessingML);
        }

        public IEnumerable<string> GetSpreadsheetMLFiles()
        {
            return GetFilesOfType(OpenXmlFileType.SpreadsheetML);
        }

        public IEnumerable<string> GetPresentationMLFiles()
        {
            return GetFilesOfType(OpenXmlFileType.PresentationML);
        }

        private IEnumerable<string> GetFilesOfType(OpenXmlFileType fileType)
        {
            if (m_metricsCatalog == null)
            {
                LoadMetricsCatalog();
            }
            var retValue = m_repoDictionary
                .Select(di => new
                {
                    GuidId = di.Key,
                    ItemList = di.Value,
                })
                .SelectMany(ri =>
                {
                    var openXmlItems = ri.ItemList
                        .Where(z =>
                        {
                            if (fileType == OpenXmlFileType.WordprocessingML)
                                return IsWordprocessingML("." + z.Extension);
                            if (fileType == OpenXmlFileType.SpreadsheetML)
                                return IsSpreadsheetML("." + z.Extension);
                            if (fileType == OpenXmlFileType.PresentationML)
                                return IsPresentationML("." + z.Extension);
                            throw new Exception("Internal error");
                        })
                        .Where(z =>
                        {
                            if (m_metricsCatalog == null)
                                return true;

                            var guidName = ri.GuidId + z.Extension;

                            if (!m_metricsDictionary.ContainsKey(guidName))
                                return false;

                            var fileMetrics = m_metricsDictionary[guidName];

                            // todo need to fix this
                            // todo need an option where RunnerCatalog gets all files not looking at metrics, but others
                            // can use metrics

                            if (fileMetrics.Element("Exception") != null)
                                return false;
                            return true;
                        })
                        .Select(z => ri.GuidId + "." + z.Extension)
                        .ToList();
                    return openXmlItems;
                })
                .ToList();
            return retValue;
        }

        private void LoadMetricsCatalog()
        {
            var fiMetricsCatalog = new FileInfo(Path.Combine(m_repoLocation.FullName, "MetricsCatalog.xml"));
            if (fiMetricsCatalog.Exists)
            {
                m_metricsCatalog = XElement.Load(fiMetricsCatalog.FullName);
                m_metricsDictionary = new Dictionary<string, XElement>();
                foreach (var item in m_metricsCatalog.Element("Documents").Elements("Document"))
                {
                    var guidName = (string)item.Attribute("GuidName");
                    m_metricsDictionary.Add(guidName, item);
                }
            }
        }

        public IEnumerable<string> GetFilesByMoniker(string moniker)
        {
            if (m_monikerDictionary == null)
                InitializeMonikerDictionary();
            if (m_monikerDictionary.ContainsKey(moniker))
                return m_monikerDictionary[moniker].Select(kv => kv.GuidName).ToList();
            else
                return Enumerable.Empty<string>();
        }

        private void InitializeMonikerDictionary()
        {
            m_monikerDictionary = new Dictionary<string, InternalMonikerRepoItem[]>();
            foreach (var item in m_repoDictionary)
            {
                foreach (var repoItem in item.Value)
                {
                    var monikers = repoItem.Monikers;
                    foreach (var moniker in monikers)
                    {
                        if (m_monikerDictionary.ContainsKey(moniker))
                        {
                            InternalMonikerRepoItem imri = new InternalMonikerRepoItem();
                            imri.GuidName = item.Key + repoItem.Extension;
                            imri.Extension = repoItem.Extension;
                            var newArray = m_monikerDictionary[moniker].Concat(new[] { imri }).ToArray();
                            m_monikerDictionary[moniker] = newArray;
                        }
                        else
                        {
                            InternalMonikerRepoItem imri = new InternalMonikerRepoItem();
                            imri.GuidName = item.Key + repoItem.Extension;
                            imri.Extension = repoItem.Extension;
                            m_monikerDictionary.Add(moniker, new[] { imri });
                        }
                    }
                }
            }
        }

        #endregion

        public static string[] WordprocessingExtensions = new[] {
            ".docx",
            ".docm",
            ".dotx",
            ".dotm",
        };

        public static bool IsWordprocessingML(string ext)
        {
            return WordprocessingExtensions.Contains(ext.ToLower());
        }

        public static string[] SpreadsheetExtensions = new[] {
            ".xlsx",
            ".xlsm",
            ".xltx",
            ".xltm",
            ".xlam",
        };

        public static bool IsSpreadsheetML(string ext)
        {
            return SpreadsheetExtensions.Contains(ext.ToLower());
        }

        public static string[] PresentationExtensions = new[] {
            ".pptx",
            ".potx",
            ".ppsx",
            ".pptm",
            ".potm",
            ".ppsm",
            ".ppam",
        };

        public static bool IsPresentationML(string ext)
        {
            return PresentationExtensions.Contains(ext.ToLower());
        }

        #region InternalCatalogs

        // Following dictionary gets all information for a given GUID
        // key is the guid
        // contains multiple InternalRepoItem objects, one for each extension
        private Dictionary<string, InternalRepoItem[]> m_repoDictionary = new Dictionary<string, InternalRepoItem[]>();

        // Following dictionary contains all repo items for a given moniker
        // key is the moniker
        // contains multiple extension / guidName items
        private Dictionary<string, InternalMonikerRepoItem[]> m_monikerDictionary = null;

        #endregion

        #region PrivateMethods

        private static IEnumerable<string> Lines(StreamReader source)
        {
            String line;

            if (source == null)
                throw new ArgumentNullException("source");
            while ((line = source.ReadLine()) != null)
            {
                yield return line;
            }
        }

        private RepoItem GetRepoItemInternal(string guidName)
        {
            var spl = guidName.Split('.');
            var guid = spl[0];
            var extension = spl[1];
            try
            {
                var internalRepoItems = m_repoDictionary[guid];
                var internalRepoItem = internalRepoItems.FirstOrDefault(ri => ri.Extension == extension);
                if (internalRepoItem == null)
                    return null;
                RepoItem repoItem = new RepoItem();
                repoItem.GuidName = guid;
                repoItem.Extension = internalRepoItem.Extension;
                repoItem.Monikers = internalRepoItem.Monikers;
                return repoItem;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        #endregion

    }

    #region PrivateClasses
    class InternalRepoItem
    {
        public string Extension;
        public string[] Monikers; // multiple monikers
    }

    class InternalMonikerRepoItem
    {
        public string Extension;
        public string GuidName;
    }
    #endregion

    public static class LocalExtensions
    {
        public static string StrCat(this IEnumerable<string> source)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in source)
                sb.Append(s);
            return sb.ToString();
        }
    }

}
