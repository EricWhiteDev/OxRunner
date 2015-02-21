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

        public Repo(DirectoryInfo repoLocation)
        {
            m_RepoLocation = repoLocation;
            FileUtils.ThreadSafeCreateDirectory(m_RepoLocation);

            m_FiMonikerCatalog = new FileInfo(Path.Combine(m_RepoLocation.FullName, "MonikerCatalog.txt"));
            FileUtils.ThreadSafeCreateEmptyTextFileIfNotExist(m_FiMonikerCatalog);

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
                        var newInternalRepoItems = m_repoDictionary[key].Concat(new[] { repoItem }).ToArray();
                        m_repoDictionary[key] = newInternalRepoItems;
                    }
                    else
                        m_repoDictionary.Add(key, new [] {repoItem});
                }
            }
        }

        public RepoItem GetRepoItem(string guid, string extension)
        {
            try
            {
                var sExtension = extension.TrimStart('.');
                var internalRepoItems = m_repoDictionary[guid];
                var internalRepoItem = internalRepoItems.FirstOrDefault(ri => ri.Extension == sExtension);
                if (internalRepoItem == null)
                    return null;
                RepoItem repoItem = new RepoItem();
                repoItem.GuidName = guid;
                repoItem.Extension = "." + internalRepoItem.Extension;
                repoItem.Monikers = internalRepoItem.Monikers;
                return repoItem;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public RepoItem GetRepoItemFileInfo(string guid, string extension)
        {
            try
            {
                RepoItem repoItem = GetRepoItem(guid, extension);
                var hashSubDir = guid.Substring(0, 2) + "/";
                var filename = guid.Substring(2) + repoItem.Extension;
                repoItem.FiRepoItem = new FileInfo(Path.Combine(m_RepoLocation.FullName, repoItem.Extension.TrimStart('.'), hashSubDir, filename));
                return repoItem;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public RepoItem GetRepoItemByteArray(string guid, string extension)
        {
            RepoItem repoItem = GetRepoItemFileInfo(guid, extension);
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
                extensionDirName = file.Extension.TrimStart('.').ToLower() + "/";
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
                FileUtils.ThreadSafeAppendAllLines(m_FiMonikerCatalog, new[] { hashString + "|" + extensionDirName + "|" + moniker });
        }
    }
}
