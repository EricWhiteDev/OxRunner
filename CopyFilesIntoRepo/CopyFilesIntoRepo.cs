using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxRunner;

// Important Monikers:
// HtmlConverter, QualityBar
// ListItemRetriever
// RevisionAccepter
// FormattingAssembler
// 
// 
// 
// 
// 
// 
// 
// 
// 

namespace CopyFilesIntoRepo
{
    public class PatternMonikersItem
    {
        public string Pattern;
        public string[] Monikers;
    };

    public class Program
    {
        public static DirectoryInfo TheRepoToOperateOn = new DirectoryInfo(@"E:\TestFileRepo");
        public static List<DirectoryInfo> s_DirectoriesToSearch = new List<DirectoryInfo>()
        {
            //new DirectoryInfo(@"E:\Sync\TestFiles"),
            //new DirectoryInfo(@"E:\DownloadedDocuments - Copy -qm7"),
            //new DirectoryInfo(@"E:\DownloadedDocuments-qm7"),
            //new DirectoryInfo(@"E:\OXml-Test-Files"),
            //new DirectoryInfo(@"E:\DownloadedDocuments"),

            new DirectoryInfo(@"C:\Users\Eric\Bts"),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
            //new DirectoryInfo(@),
        };

        public static string[] s_Patterns = new [] {
            "*.docx",
            "*.docm",
            "*.dotx",
            "*.dotm",
            "*.xlsx",
            "*.xlsm",
            "*.xltx",
            "*.xltm",
            "*.xlam",
            "*.pptx",
            "*.potx",
            "*.ppsx",
            "*.pptm",
            "*.potm",
            "*.ppsm",
            "*.ppam",
        };

        public static List<PatternMonikersItem> s_PatternMonikers = new List<PatternMonikersItem>
        {
            new PatternMonikersItem() { Pattern = "100-RevisionAccepterTestDocuments",                                  Monikers = new [] { "RevisionAccepter" },},
            new PatternMonikersItem() { Pattern = "200-FormattingAssemblerTestDocuments",                               Monikers = new [] { "FormattingAssembler" },},
            new PatternMonikersItem() { Pattern = "QualityBar",                                                         Monikers = new [] { "HtmlConverter", "QualityBar" },},
            new PatternMonikersItem() { Pattern = "ListItemRetriever",                                                  Monikers = new [] { "ListItemRetriever" },},
            new PatternMonikersItem() { Pattern = "Restart",                                                            Monikers = new [] { "HtmlConverter", "ListItemRetriever", "Restart" },},
        };

        public static int s_RepoLength;
        public static int s_Stored;
        public static int s_StoredWithAdditionalMonikers;
        public static int s_FileHasNoExtension;
        public static int s_InvalidMoniker;
        public static int s_FileDoesNotExist;
        public static int s_AlreadyExistsInRepoWithSameMonikers;
        public static Dictionary<string, int> s_FileTypeCount = new Dictionary<string, int>();

        public static void Main(string[] args)
        {
            Repo r = new Repo(TheRepoToOperateOn, RepoAccessLevel.ReadWrite);
            s_RepoLength = r.GetRepoLength();
            foreach (var dirToCopy in s_DirectoriesToSearch)
            {
                AddFilesToRepo(r, dirToCopy);
            }
            r.SaveMonikerFile();
            Console.WriteLine("Press Enter");
            Console.ReadKey();
        }

        private static void AddFilesToRepo(Repo r, DirectoryInfo dirToCopy)
        {
            foreach (var pattern in s_Patterns)
            {
                foreach (var file in dirToCopy.GetFiles(pattern))
                {
                    Console.SetCursorPosition(0, 2);
                    Console.WriteLine(string.Format("Adding {0}", file.FullName).PadRight(80));
                    PatternMonikersItem pmi = s_PatternMonikers.FirstOrDefault(pm => file.FullName.Contains(pm.Pattern));
                    Repo.StoreStatus rss;
                    if (pmi == null)
                        rss = r.Store(file, new string[] { });
                    else
                        rss = r.Store(file, pmi.Monikers);
                    AddToMetrics(file, rss);
                    UpdateConsole();
                }
            }
            foreach (var dir in dirToCopy.GetDirectories())
            {
                AddFilesToRepo(r, dir);
            }
        }

        private static DateTime s_LastUpdateTime = DateTime.Now;
        private static bool s_First = true;

        private static void UpdateConsole()
        {
            int tab = 40;

            if (s_First)
            {
                s_First = false;
                Console.Clear();
            }
            else
                Console.SetCursorPosition(0, 0);

            DateTime n = DateTime.Now;
            if (s_LastUpdateTime == null || (n - s_LastUpdateTime).Seconds > 1.0)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine("Copy Into OxRunner Repo");
                Console.WriteLine();
                WriteStatusLine("Initial Repo File Count", tab, s_RepoLength);
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                WriteStatusLine("Stored", tab, s_Stored);
                WriteStatusLine("StoredWithAdditionalMonikers", tab, s_StoredWithAdditionalMonikers);
                WriteStatusLine("AlreadyExistsInRepoWithSameMonikers", tab, s_AlreadyExistsInRepoWithSameMonikers);
                Console.WriteLine();
                WriteStatusLine("FileHasNoExtension", tab, s_FileHasNoExtension);
                WriteStatusLine("InvalidMoniker", tab, s_InvalidMoniker);
                WriteStatusLine("FileDoesNotExist", tab, s_FileDoesNotExist);
                Console.WriteLine();
                foreach (var item in s_FileTypeCount)
                    WriteStatusLine(item.Key, 8, item.Value);
                s_LastUpdateTime = n;
           }
        }

        private static void WriteStatusLine(string s, int width, int value)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(s.PadRight(width));
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(string.Format("{0:0000000}", value));
        }

        private static void AddToMetrics(FileInfo file, Repo.StoreStatus rss)
        {
            if (rss == Repo.StoreStatus.Stored)
                ++s_Stored;
            else if (rss == Repo.StoreStatus.StoredWithAdditionalMonikers)
                ++s_StoredWithAdditionalMonikers;
            else if (rss == Repo.StoreStatus.FileHasNoExtension)
                ++s_FileHasNoExtension;
            else if (rss == Repo.StoreStatus.InvalidMoniker)
                ++s_InvalidMoniker;
            else if (rss == Repo.StoreStatus.FileDoesNotExist)
                ++s_FileDoesNotExist;
            else if (rss == Repo.StoreStatus.AlreadyExistsInRepoWithSameMonikers)
                ++s_AlreadyExistsInRepoWithSameMonikers;
            var extension = file.Extension.TrimStart('.').ToLower();
            if (s_FileTypeCount.ContainsKey(extension))
            {
                int newVal = s_FileTypeCount[extension] + 1;
                s_FileTypeCount[extension] = newVal;
            }
            else
                s_FileTypeCount.Add(extension, 1);
        }

    }
}
