// todo need a more general way to select
// - specific set of files
// - not get size 0 files
// have a funny issue where if there is no extenion, then the extension is set to "." in the RepoItem returned by methods.  Probably in the m_repoDictionary incorrectly.

// todo need an option where RunnerCatalog gets all files not looking at metrics, but others
// can use metrics

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace OxRun
{
    public class RepoItem
    {
        public string GuidName;
        public string Extension;
        public string Monikers;
        public FileInfo FiRepoItem;
        public byte[] ByteArray;
    }

    public class InternalRepoItem
    {
        public string Extension;
        public string Monikers; // multiple monikers separated by :
    }

    public class InternalMonikerRepoItem
    {
        public string Extension;
        public string GuidName;
    }

    public class Repo
    {
        public DirectoryInfo m_repoLocation;
        public FileInfo m_fiMonikerCatalog;
        public Dictionary<string, InternalRepoItem[]> m_repoDictionary = new Dictionary<string, InternalRepoItem[]>();
        public Dictionary<string, InternalMonikerRepoItem[]> m_monikerDictionary = null;
        public XElement m_metricsCatalog = null;
        public Dictionary<string, XElement> m_metricsDictionary = null;

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

        public Repo(DirectoryInfo repoLocation, bool loadMonikers)
        {
            LoadRepo(repoLocation, loadMonikers);
        }

        public Repo(DirectoryInfo repoLocation)
        {
            LoadRepo(repoLocation, true);
        }

        private void LoadRepo(DirectoryInfo repoLocation, bool loadMonikers)
        {
            m_repoLocation = repoLocation;
            FileUtils.ThreadSafeCreateDirectory(m_repoLocation);

            m_fiMonikerCatalog = new FileInfo(Path.Combine(m_repoLocation.FullName, "MonikerCatalog.txt"));
            FileUtils.ThreadSafeCreateEmptyTextFileIfNotExist(m_fiMonikerCatalog);

            if (loadMonikers)
            {
                using (StreamReader sr = new StreamReader(m_fiMonikerCatalog.FullName))
                {
                    foreach (var line in Lines(sr))
                    {
                        var spl = line.Split('|');
                        var repoItem = new InternalRepoItem();
                        var key = spl[0];
                        repoItem.Extension = spl[1];
                        repoItem.Monikers = spl[2];
                        if (m_repoDictionary.ContainsKey(key))
                        {
                            var repoDictionaryEntry = m_repoDictionary[key];
                            var existingItemWithSameExtension = repoDictionaryEntry.FirstOrDefault(q => q.Extension == repoItem.Extension);
                            if (existingItemWithSameExtension != null)
                            {
                                existingItemWithSameExtension.Monikers = existingItemWithSameExtension.Monikers + ":" + repoItem.Monikers;
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
        }

        public RepoItem GetRepoItem(string guidName)
        {
            var spl = guidName.Split('.');
            var guid = spl[0];
            var extension = "." + spl[1];
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

        public RepoItem GetRepoItemFileInfo(string guidName)
        {
            try
            {
                RepoItem repoItem = GetRepoItem(guidName);
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

        public RepoItem GetRepoItemByteArray(string guidName)
        {
            RepoItem repoItem = GetRepoItemFileInfo(guidName);
            repoItem.ByteArray = File.ReadAllBytes(repoItem.FiRepoItem.FullName);
            return repoItem;
        }

        public void Store(FileInfo file, string moniker)
        {
            // Sometimes the file may just have been written, and the OS is asynchronously finishing the copy.
            // If the copy is not finished, then get UnauthorizedAccessException, so wait a bit, try again.
            while (true)
            {
                try
                {
                if (!file.Exists)
                    throw new ArgumentException(string.Format("File {0} does not exist", file.FullName));
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

            if (moniker != null)
                FileUtils.ThreadSafeAppendAllLines(m_fiMonikerCatalog, new[] { hashString + "|." + extensionDirName + "|" + moniker });
        }

        public void RebuildMonikerFile(FileInfo fiNewMonikerFile)
        {
            List<string> monikerContent = new List<string>();
            GetFileList(m_repoLocation, monikerContent);
            File.WriteAllLines(fiNewMonikerFile.FullName, monikerContent.ToArray());
        }

        private void GetFileList(DirectoryInfo di, List<string> fileList)
        {
            // iterate through extension directories
            foreach (var extensionDir in di.GetDirectories())
            {
                // iterate through 2 char sha1 dirs
                foreach (var sha1Dir in extensionDir.GetDirectories())
                {
                    // iterate through files in each extension dir
                    foreach (var sha1File in sha1Dir.GetFiles())
                    {
                        var nameWithoutExtension = sha1File.Name.Substring(0, sha1File.Name.Length - sha1File.Extension.Length);
                        var sha1 = sha1Dir.Name + nameWithoutExtension;
                        if (m_repoDictionary.ContainsKey(sha1))
                        {
                            var listRepoItems = m_repoDictionary[sha1];
                            var repoItem = listRepoItems.FirstOrDefault(ri => ri.Extension.ToLower() == sha1File.Extension.ToLower());
                            if (repoItem == null)
                                throw new Exception("What????");
                            fileList.Add(string.Format("{0}|{1}|{2}", sha1, sha1File.Extension.ToLower(), repoItem.Monikers));
                        }
                    }
                }
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

        public IEnumerable<string> GetWordprocessingMLFiles()
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
                            IsWordprocessingML(z.Extension))
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

        public IEnumerable<string> GetSpreadsheetMLFiles()
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
                            IsSpreadsheetML(z.Extension))
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

        public IEnumerable<string> GetPresentationMLFiles()
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
                    var monikers = repoItem.Monikers.Split(':');
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
    }
}
