using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

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

    public class Repo
    {
        public DirectoryInfo m_RepoLocation;
        public FileInfo m_FiMonikerCatalog;
        public Dictionary<string, InternalRepoItem[]> m_repoDictionary = new Dictionary<string, InternalRepoItem[]>();

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
            m_RepoLocation = repoLocation;
            FileUtils.ThreadSafeCreateDirectory(m_RepoLocation);

            m_FiMonikerCatalog = new FileInfo(Path.Combine(m_RepoLocation.FullName, "MonikerCatalog.txt"));
            FileUtils.ThreadSafeCreateEmptyTextFileIfNotExist(m_FiMonikerCatalog);

            if (loadMonikers)
            {
                using (StreamReader sr = new StreamReader(m_FiMonikerCatalog.FullName))
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
                repoItem.FiRepoItem = new FileInfo(Path.Combine(m_RepoLocation.FullName, repoItem.Extension.TrimStart('.'), hashSubDir, filename));
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
            var diSubDir = new DirectoryInfo(Path.Combine(m_RepoLocation.FullName, extensionDirName));
            FileUtils.ThreadSafeCreateDirectory(diSubDir);

            var hashSubDir = hashString.Substring(0, 2) + "/";
            var fileBaseName = hashString.Substring(2);
            var diHashSubDir = new DirectoryInfo(Path.Combine(m_RepoLocation.FullName, extensionDirName, hashSubDir));
            FileUtils.ThreadSafeCreateDirectory(diHashSubDir);

            var fiFileName = new FileInfo(Path.Combine(m_RepoLocation.FullName, extensionDirName, hashSubDir, fileBaseName + file.Extension.ToLower()));
            FileUtils.ThreadSafeCopy(file, fiFileName);

            FileInfo fiToMakeReadonly = new FileInfo(fiFileName.FullName);
            fiToMakeReadonly.IsReadOnly = true;

            if (moniker != null)
                FileUtils.ThreadSafeAppendAllLines(m_FiMonikerCatalog, new[] { hashString + "|." + extensionDirName + "|" + moniker });
        }

        public void RebuildMonikerFile(FileInfo fiNewMonikerFile)
        {
            List<string> monikerContent = new List<string>();
            GetFileList(m_RepoLocation, monikerContent);
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
                        .Select(z => ri.GuidId + z.Extension);
                    return openXmlItems;
                })
                .ToList();
            return retValue;
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
