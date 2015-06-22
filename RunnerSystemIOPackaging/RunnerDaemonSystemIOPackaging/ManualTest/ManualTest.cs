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
            string guidName = "0030A269249BAA7C555F531DD5A68B46F9346EFF.docx";

            DirectoryInfo repoLocation = new DirectoryInfo(@"C:\TestFileRepoSmall");
            var repo = new Repo(repoLocation);
            var rpt = SystemIOPackagingTest.DoTest(repo, guidName);
            Console.WriteLine(rpt);
        }

    }
}
