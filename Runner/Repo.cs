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
            Console.WriteLine("zzz Store entry");
            while (true)
            {
                try
                {
                    if (!file.Exists)
                        throw new ArgumentException(string.Format("File {0} can't be opened", file.FullName));
                }
                catch (System.UnauthorizedAccessException)
                {
                    Console.WriteLine("======================================================================================== CAUGHT EXCEPTION FILE EXISTS");
                    System.Threading.Thread.Sleep(20);
                    continue;
                }
                break;
            }

            string hashString;
            byte[] ba = null;

            Console.WriteLine("zzz after test exists");
            // Sometimes the file may just have been written, and the OS is asynchronously finishing the copy.
            // If the copy is not finished, then get UnauthorizedAccessException, so wait a bit, try again.
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
                    Console.WriteLine("======================================================================================== CAUGHT EXCEPTION ");
                    System.Threading.Thread.Sleep(20);
                    continue;
                }
                break;
            }

            Console.WriteLine("zzz after read all bytes");

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

            Console.WriteLine("zzz after threadsafe create dir");

            var fiFileName = new FileInfo(Path.Combine(m_RepoLocation.FullName, extensionDirName, hashSubDir, fileBaseName + file.Extension.ToLower()));
            FileUtils.ThreadSafeCopy(file, fiFileName);

            Console.WriteLine("zzz after threadsafe copy");

            FileInfo fiToMakeReadonly = new FileInfo(fiFileName.FullName);
            fiToMakeReadonly.IsReadOnly = true;

            if (moniker != null)
                FileUtils.ThreadSafeAppendAllLines(m_FiMonikerCatalog, new[] { hashString + "|" + moniker });

            Console.WriteLine("zzz after threadsafe AppendAllLines");

        }
    }
}
