using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OxRunner
{
    public class FileUtils
    {
        public static DirectoryInfo GetDateTimeStampedDirectoryInfo(string prefix)
        {
            DateTime now = DateTime.Now;
            var dirName = prefix + string.Format("-{0:00}-{1:00}-{2:00}-{3:00}{4:00}{5:00}{6:000}",
                now.Year - 2000,
                now.Month,
                now.Day,
                now.Hour,
                now.Minute,
                now.Second,
                now.Millisecond);
            return new DirectoryInfo(dirName);
        }

        public static FileInfo GetDateTimeStampedFileInfo(string prefix, string suffix)
        {
            DateTime now = DateTime.Now;
            var fileName = prefix + string.Format("-{0:00}-{1:00}-{2:00}-{3:00}{4:00}{5:00}{6:000}",
                now.Year - 2000,
                now.Month,
                now.Day,
                now.Hour,
                now.Minute,
                now.Second,
                now.Millisecond) + suffix;
            return new FileInfo(fileName);
        }

        public static void ThreadSafeCreateDirectory(DirectoryInfo dir)
        {
            while (true)
            {
                if (dir.Exists)
                    break;
                try
                {
                    dir.Create();
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(50);
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        public static void ThreadSafeCopy(FileInfo sourceFile, FileInfo destFile)
        {
            while (true)
            {
                if (destFile.Exists)
                    break;
                try
                {
                    File.Copy(sourceFile.FullName, destFile.FullName);
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(50);
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        public static void ThreadSafeCreateEmptyTextFileIfNotExist(FileInfo file)
        {
            while (true)
            {
                if (file.Exists)
                    break;
                try
                {
                    File.WriteAllText(file.FullName, "");
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(50);
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }


        internal static void ThreadSafeAppendAllLines(FileInfo file, string[] strings)
        {
            while (true)
            {
                try
                {
                    File.AppendAllLines(file.FullName, strings);
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(50);
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        public static List<string> GetFilesRecursive(DirectoryInfo dir, string searchPattern)
        {
            List<string> fileList = new List<string>();
            GetFilesRecursiveInternal(dir, searchPattern, fileList);
            return fileList;
        }

        private static void GetFilesRecursiveInternal(DirectoryInfo dir, string searchPattern, List<string> fileList)
        {
            foreach (var file in dir.GetFiles(searchPattern))
                fileList.Add(file.FullName);
            foreach (var subdir in dir.GetDirectories())
                GetFilesRecursiveInternal(subdir, searchPattern, fileList);
        }

        public static List<string> GetFilesRecursive(DirectoryInfo dir)
        {
            List<string> fileList = new List<string>();
            GetFilesRecursiveInternal(dir, fileList);
            return fileList;
        }

        private static void GetFilesRecursiveInternal(DirectoryInfo dir, List<string> fileList)
        {
            foreach (var file in dir.GetFiles())
                fileList.Add(file.FullName);
            foreach (var subdir in dir.GetDirectories())
                GetFilesRecursiveInternal(subdir, fileList);
        }
    }
}
