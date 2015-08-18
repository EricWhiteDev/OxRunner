// todo need a more general way to select
// - specific set of files
// - not get size 0 files

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
        public string HashFileName;
        public string BaseName
        {
            get {
                return HashFileName.Split('.')[0];
            }
        }
        public string Extension
        {
            get
            {
                return HashFileName.Split('.')[1];
            }
        }
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

            var fiMonikerCatalog = m_repoLocation
                .GetFiles("MonikerCatalog*")
                .OrderBy(t => t.CreationTime)
                .LastOrDefault();

            if (fiMonikerCatalog != null)
            {
                using (StreamReader sr = new StreamReader(fiMonikerCatalog.FullName))
                {
                    foreach (var line in Lines(sr))
                    {
                        var spl = line.Split('|');
                        string[] monikers;
                        var key = spl[0];
                        if (spl[1].Contains(':'))
                            monikers = spl[1].Split(':');
                        else
                        {
                            if (spl[1] == "")
                                monikers = new string[] { };  // create an empty array
                            else
                                monikers = new string[] { spl[1] };  // create array with one element
                        }
                        if (m_repoDictionary.ContainsKey(key))
                        {
                            var existingMonikers = m_repoDictionary[key];
                            var newMonikers = existingMonikers.Concat(monikers).Where(m => m != "").OrderBy(m => m).Distinct().ToArray();
                            m_repoDictionary[key] = monikers;
                        }
                        else
                            m_repoDictionary.Add(key, monikers);
                    }
                }
            }
        }

        public bool Store(FileInfo file, string[] monikers)
        {
            if (m_ReadWrite == false)
                throw new Exception("Repo is opened for readonly access");

            if (file.Extension == "")
            {
                Console.WriteLine("File {0} has no extension", file.Name);
                return false;
            }

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

            string extensionDirName = file.Extension.TrimStart('.').ToLower();

            string hashFileName = hashString + file.Extension.ToLower();

            // if this hashFileName already exists in the dictionary, then only need to update data structures.
            if (m_repoDictionary.ContainsKey(hashFileName))
            {
                var existingMonikers = m_repoDictionary[hashFileName];
                var newMonikerList = existingMonikers.Concat(monikers).Distinct().OrderBy(t => t).ToArray();
                m_repoDictionary[hashFileName] = newMonikerList;
                return true;
            }

            CopyFileIntoRepo(file, hashString, extensionDirName);
            m_repoDictionary.Add(hashFileName, monikers);
            return true;
        }

        private void CopyFileIntoRepo(FileInfo file, string hashString, string extensionDirName)
        {
            var diSubdir = new DirectoryInfo(Path.Combine(m_repoLocation.FullName, extensionDirName));
            FileUtils.ThreadSafeCreateDirectory(diSubdir);

            var hashSubdir = hashString.Substring(0, 2) + "/";
            var fileBaseName = hashString.Substring(2);
            var diHashSubDir = new DirectoryInfo(Path.Combine(m_repoLocation.FullName, extensionDirName, hashSubdir));
            FileUtils.ThreadSafeCreateDirectory(diHashSubDir);

            var fiFileName = new FileInfo(Path.Combine(m_repoLocation.FullName, extensionDirName, hashSubdir, fileBaseName + file.Extension.ToLower()));
            FileUtils.ThreadSafeCopy(file, fiFileName);

            FileInfo fiToMakeReadonly = new FileInfo(fiFileName.FullName);
            fiToMakeReadonly.IsReadOnly = true;
        }

        public FileInfo SaveMonikerFile()
        {
            if (!m_ReadWrite)
                throw new Exception("Repo is opened in ReadOnly mode");

            DateTime n = DateTime.Now;
            var monikerName = string.Format("MonikerCatalog-{0:00}-{1:00}-{2:00}-{3:00}{4:00}{5:00}-{6:000}.txt", n.Year - 2000, n.Month, n.Day, n.Hour, n.Minute, n.Second, n.Millisecond);
            var fiMonikerCatalog = new FileInfo(Path.Combine(m_repoLocation.FullName, monikerName));
            using (StreamWriter sw = new StreamWriter(fiMonikerCatalog.FullName))
            {
                foreach (var item in m_repoDictionary)
                {
                    string monikers = item.Value.Select(m => m + ":").StrCat().TrimEnd(':');
                    string line = string.Format("{0}|{1}", item.Key, monikers) + Environment.NewLine;
                    sw.Write(line);
                }
            }
            return fiMonikerCatalog;
        }

        #region AccessMethods

        // The methods here do not impact the repo in any way.

        // hashFileName is the hash plus the extension, i.e. A45E0E6EF4CAB5D21F310BE41D4217679D2F83BE.docx
        public RepoItem GetRepoItem(string hashFileName)
        {
            if (!hashFileName.Contains('.'))
                return null;
            var spl = hashFileName.Split('.');
            try
            {
                RepoItem repoItem = GetRepoItemInternal(hashFileName);
                var hashSubDir = hashFileName.Substring(0, 2) + "/";
                var filename = hashFileName.Substring(2);
                repoItem.FiRepoItem = new FileInfo(Path.Combine(m_repoLocation.FullName, spl[1].ToLower(), hashSubDir, filename));
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
                .Select(di =>
                {
                    var spl = di.Key.Split('.');
                    return new
                    {
                        HashFileName = di.Key,
                        HashBaseName = spl[0],
                        Extension = spl[1],
                        Monikers = di.Value,
                    };
                })
                .Where(ri =>
                {
                    return IsWordprocessingML(ri.Extension) ||
                        IsPresentationML(ri.Extension) ||
                        IsSpreadsheetML(ri.Extension);
                })
                .Where(z =>
                    {
                        if (m_metricsCatalog == null)
                            return true;

                        var hashFileName = z.HashFileName;

                        if (!m_metricsDictionary.ContainsKey(hashFileName))
                            return false;

                        var fileMetrics = m_metricsDictionary[hashFileName];

                        // todo need to fix this
                        // todo need an option where RunnerCatalog gets all files not looking at metrics, but others
                        // can use metrics

                        if (fileMetrics.Element("Exception") != null)
                            return false;
                        return true;
                    })
                .Select(z => z.HashFileName)
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
                .Select(di =>
                {
                    var spl = di.Key.Split('.');
                    return new
                    {
                        HashFileName = di.Key,
                        HashBaseName = spl[0],
                        Extension = spl[1],
                        Monikers = di.Value,
                    };
                })
                .Where(ri =>
                {
                    if (fileType == OpenXmlFileType.WordprocessingML)
                        return IsWordprocessingML(ri.Extension);
                    if (fileType == OpenXmlFileType.SpreadsheetML)
                        return IsSpreadsheetML(ri.Extension);
                    if (fileType == OpenXmlFileType.PresentationML)
                        return IsPresentationML(ri.Extension);
                    throw new Exception("Internal error");
                })
                .Where(z =>
                {
                    if (m_metricsCatalog == null)
                        return true;

                    var hashFileName = z.HashFileName;

                    if (!m_metricsDictionary.ContainsKey(hashFileName))
                        return false;

                    var fileMetrics = m_metricsDictionary[hashFileName];

                    // todo need to fix this
                    // todo need an option where RunnerCatalog gets all files not looking at metrics, but others
                    // can use metrics

                    if (fileMetrics.Element("Exception") != null)
                        return false;
                    return true;
                })
                .Select(z => z.HashFileName)
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
                    var hashFileName = (string)item.Attribute("HashName");
                    m_metricsDictionary.Add(hashFileName, item);
                }
            }
        }

        public IEnumerable<string> GetFilesByMoniker(string moniker)
        {
            if (m_monikerDictionary == null)
                InitializeMonikerDictionary();
            if (m_monikerDictionary.ContainsKey(moniker))
                return m_monikerDictionary[moniker].ToList();
            else
                return Enumerable.Empty<string>();
        }

        private void InitializeMonikerDictionary()
        {
            m_monikerDictionary = new Dictionary<string, string[]>();
            foreach (var item in m_repoDictionary)
            {
                var monikers = item.Value;
                foreach (var moniker in monikers)
                {
                    if (m_monikerDictionary.ContainsKey(moniker))
                    {
                        var newArray = m_monikerDictionary[moniker].Concat(new[] { item.Key }).ToArray();
                        m_monikerDictionary[moniker] = newArray;
                    }
                    else
                    {
                        m_monikerDictionary.Add(moniker, new[] { item.Key });
                    }
                }
            }
        }

        #endregion

        public static string[] WordprocessingExtensions = new[] {
            "docx",
            "docm",
            "dotx",
            "dotm",
        };

        public static bool IsWordprocessingML(string ext)
        {
            return WordprocessingExtensions.Contains(ext.ToLower());
        }

        public static string[] SpreadsheetExtensions = new[] {
            "xlsx",
            "xlsm",
            "xltx",
            "xltm",
            "xlam",
        };

        public static bool IsSpreadsheetML(string ext)
        {
            return SpreadsheetExtensions.Contains(ext.ToLower());
        }

        public static string[] PresentationExtensions = new[] {
            "pptx",
            "potx",
            "ppsx",
            "pptm",
            "potm",
            "ppsm",
            "ppam",
        };

        public static bool IsPresentationML(string ext)
        {
            return PresentationExtensions.Contains(ext.ToLower());
        }

        #region InternalCatalogs

        // Following dictionary gets all information for a given hash
        // key is the hash
        // contains multiple InternalRepoItem objects, one for each extension
        private Dictionary<string, string[]> m_repoDictionary = new Dictionary<string, string[]>();

        // Following dictionary contains all repo items for a given moniker
        // key is the moniker
        // contains multiple hashFileNames
        private Dictionary<string, string[]> m_monikerDictionary = null;

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

        private RepoItem GetRepoItemInternal(string hashFileName)
        {
            var spl = hashFileName.Split('.');
            var hash = spl[0];
            var extension = spl[1];
            try
            {
                var monikers = m_repoDictionary[hashFileName];
                RepoItem repoItem = new RepoItem();
                repoItem.HashFileName = hashFileName;
                repoItem.Monikers = monikers;
                return repoItem;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        #endregion

    }

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
