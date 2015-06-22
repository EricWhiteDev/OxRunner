using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OxRun
{
    class ManualTest
    {
        static void Main(string[] args)
        {
            string guidName = "000B1840AADC36736630DE44A78E027F58240BD2.docx";

            DirectoryInfo repoLocation = new DirectoryInfo(@"C:\TestFileRepoSmall");
            var repo = new Repo(repoLocation);
            var rpt = SystemIOPackagingTest.DoTest(repo, guidName);
            Console.WriteLine(rpt);
        }

    }
}
