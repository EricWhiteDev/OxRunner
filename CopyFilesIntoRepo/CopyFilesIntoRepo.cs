using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxRunner;

namespace CopyFilesIntoRepo
{
    public class Program
    {
        public static string s_Extension = ".docx";

        public static void Main(string[] args)
        {
            DirectoryInfo dirToCopy = new DirectoryInfo(@"E:\Sync\300-HtmlConverterTestDocuments\QualityBar");

            DirectoryInfo ri = new DirectoryInfo(@"c:\TestFileRepo");
            Repo r = new Repo(ri);

            AddFilesToRepo(r, dirToCopy);
        }

        private static void AddFilesToRepo(Repo r, DirectoryInfo dirToCopy)
        {
            foreach (var file in dirToCopy.GetFiles(s_Extension))
            {
                r.Store(file, new[] { "HtmlConverter", "QualityBar" });
            }
        }
    }
}
