using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxRunner;
using Xunit;

namespace RunnerTests
{
    public class RunnerTests
    {
        [Fact]
        public void OxRt016_GetBytesFromRepo()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();

            r = new Repo(tr);
            var wFiles = r.GetWordprocessingMLFiles();
            var z = wFiles.FirstOrDefault();
            var ri = r.GetRepoItem(z);
            var ba = ri.ByteArray;
            Assert.NotEqual(0, ba.Length);
        }

        [Fact]
        public void OxRt015_TryToStoreInRepoThatIsReadOnly2()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();

            r = new Repo(tr);
            fileList = GetFileList();
            foreach (var file in fileList)
            {
                Assert.Throws<Exception>(() => r.Store(file, new[] { "AnotherMoniker" }));
            }
            Assert.Throws<Exception>(() => r.CloseAndSaveMonikerFile());
        }

        [Fact]
        public void OxRt014_TryToStoreInRepoThatIsReadOnly()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();

            r = new Repo(tr, false);
            fileList = GetFileList();
            foreach (var file in fileList)
            {
                Assert.Throws<Exception>(() => r.Store(file, new[] { "AnotherMoniker" }));
            }
            Assert.Throws<Exception>(() => r.CloseAndSaveMonikerFile());
        }

        [Fact]
        public void OxRt013_TestInvalidMonikerCatWithDupItems()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            var newFileName = new FileInfo(Guid.NewGuid().ToString().Replace("-", "") + ".foo");
            File.Copy(fileList.First().FullName, newFileName.FullName);
            var b2 = r.Store(newFileName, new[] { "blat", "biff" });
            Assert.True(b2);
            var mf = r.CloseAndSaveMonikerFile();
            newFileName.Delete();

            // now duplicate a line in the moniker file
            var l1 = File.ReadAllLines(mf.FullName);
            var l2 = l1.Concat(new[] { l1[0] + ":Whatever" }).ToArray();
            File.WriteAllLines(mf.FullName, l2);

            r = new Repo(tr, true);
            fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "AnotherMoniker" });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt012_TestInvalidMonikerCatWithDupItems()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            var newFileName = new FileInfo(Guid.NewGuid().ToString().Replace("-", "") + ".foo");
            File.Copy(fileList.First().FullName, newFileName.FullName);
            var b2 = r.Store(newFileName, new[] { "blat", "biff" });
            Assert.True(b2);
            var mf = r.CloseAndSaveMonikerFile();
            newFileName.Delete();

            // now duplicate a line in the moniker file
            var l1 = File.ReadAllLines(mf.FullName);
            var l2 = new[] { l1[0] }.Concat(l1).ToArray();
            File.WriteAllLines(mf.FullName, l2);

            r = new Repo(tr, true);
            fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "AnotherMoniker" });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt011_OpenExistingRepoWithDupHash()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            var newFileName = new FileInfo(Guid.NewGuid().ToString().Replace("-", "") + ".foo");
            File.Copy(fileList.First().FullName, newFileName.FullName);
            var b2 = r.Store(newFileName, new[] { "blat", "biff" });
            Assert.True(b2);
            r.CloseAndSaveMonikerFile();
            newFileName.Delete();

            r = new Repo(tr, true);
            fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "AnotherMoniker" });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt010_OpenExistingRepo()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();

            r = new Repo(tr, true);
            fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "AnotherMoniker" });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt009_AddMonikerWithPipe()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "Another|Moniker" });
                Assert.False(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt008_AddMonikerWithColon()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "Another:Moniker" });
                Assert.False(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt007_AddNonexistentFileIntoRepo()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            var newFileName = new FileInfo(Guid.NewGuid().ToString().Replace("-", "") + ".foo");
            var b2 = r.Store(newFileName, new[] { "blat", "biff" });
            Assert.False(b2);
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt006_AddWithSameHashDifferentExtension()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            var newFileName = new FileInfo(Guid.NewGuid().ToString().Replace("-", "") + ".foo");
            File.Copy(fileList.First().FullName, newFileName.FullName);
            var b2 = r.Store(newFileName, new[] { "blat", "biff" });
            Assert.True(b2);
            r.CloseAndSaveMonikerFile();
            newFileName.Delete();
        }

        [Fact]
        public void OxRt005_AddEvenMoreMonikersAfterInRepo()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "AnotherMoniker" });
                Assert.True(b);
            }
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "WhyNot" });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt004_AddDuplicateMonikerAfterInRepo()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "AnotherMoniker" });
                Assert.True(b);
            }
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "AnotherMoniker" });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt003_AddMonikerAfterInRepo()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { "AnotherMoniker" });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt002_TwoMonikersPer()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length), "TestConformance" });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        [Fact]
        public void OxRt001_CreateRepo()
        {
            var tr = GetTempRepoName();
            Repo r = new Repo(tr, true);
            var fileList = GetFileList();
            foreach (var file in fileList)
            {
                var b = r.Store(file, new[] { file.FullName.Substring(@"C:\Users\Eric\Documents\Open-Xml-Sdk\TestFiles\".Length) });
                Assert.True(b);
            }
            r.CloseAndSaveMonikerFile();
        }

        private List<FileInfo> GetFileList()
        {
            DirectoryInfo di = new DirectoryInfo(@"..\..\..\..\Open-Xml-Sdk\TestFiles");
            var fileList = di.GetFiles().ToList();
            return fileList;
        }

        DirectoryInfo GetTempRepoName()
        {
            DateTime n = DateTime.Now;
            var tempRepoName = string.Format(@"C:\TempTestRepo-{0:00}-{1:00}-{2:00}-{3:00}{4:00}{5:00}-{6:0000}", n.Year - 2000, n.Month, n.Day, n.Hour, n.Minute, n.Second, n.Millisecond);
            return new DirectoryInfo(tempRepoName);
        }
    }
}
