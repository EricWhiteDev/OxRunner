using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace OxRun
{
    public class Repo
    {
        public DirectoryInfo m_RepoLocation;
        public FileInfo m_FiMonikerCatalog;

        public Repo(DirectoryInfo repoLocation)
        {
            m_RepoLocation = repoLocation;
            FileUtils.ThreadSafeCreateDirectory(m_RepoLocation);

            m_FiMonikerCatalog = new FileInfo(Path.Combine(m_RepoLocation.FullName, "MonikerCatalog.txt"));
            FileUtils.ThreadSafeCreateEmptyTextFileIfNotExist(m_FiMonikerCatalog);
        }

        public void Store(FileInfo file, string moniker)
        {
            if (!file.Exists)
                throw new ArgumentException(string.Format("File {0} can't be opened", file.FullName));

            string hashString;

            var ba = File.ReadAllBytes(file.FullName);
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
                FileUtils.ThreadSafeAppendAllLines(m_FiMonikerCatalog, new[] { hashString + "|" + moniker });
        }
    }
}
