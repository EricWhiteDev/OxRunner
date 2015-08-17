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
    public class Program
    {
        public static string s_Pattern = "*.docx";

        public static void Main(string[] args)
        {
            DirectoryInfo dirToCopy = new DirectoryInfo(@"E:\Sync\TestFiles");

            DirectoryInfo ri = new DirectoryInfo(@"C:\TestFileRepo");
            Repo r = new Repo(ri, true);

            AddFilesToRepo(r, dirToCopy);

            r.SaveMonikerFile();
        }

        private static void AddFilesToRepo(Repo r, DirectoryInfo dirToCopy)
        {
            foreach (var file in dirToCopy.GetFiles(s_Pattern))
            {
                Console.WriteLine("Adding {0}", file.FullName);
                r.Store(file, new string[] { });
            }
            foreach (var dir in dirToCopy.GetDirectories())
            {
                AddFilesToRepo(r, dir);
            }
        }
    }
}
