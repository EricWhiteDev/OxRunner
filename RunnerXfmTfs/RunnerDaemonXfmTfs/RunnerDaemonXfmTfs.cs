using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OxRunner
{
    class RunnerDaemonXfmTfs : RunnerDaemon
    {
        // NOTE: when changing the name of the class, must change the following line to get the version properly.
        static System.Version m_RunnerAssemblyVersion = typeof(RunnerDaemonXfmTfs).Assembly.GetName().Version;
        static FileInfo m_FiNamesDict = new FileInfo(@"\\Bigi5-8\e\TestFileStorage-Mash-Names\NamesDict.txt");
        static Dictionary<string, string> m_DictNames = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;

            if (args.Length == 0)
                throw new ArgumentException("ControllerDaemon did not pass any arguments to RunnerDaemon");

            string runnerMasterMachineName = args[0];

            // MinorRevision will be unique for each daemon, so we append it for the daemon queue name.
            var runnerDaemon = new RunnerDaemonXfmTfs(runnerMasterMachineName, m_RunnerAssemblyVersion.MinorRevision);
            runnerDaemon.PrintToConsole(ConsoleColor.White, string.Format("Log: {0}", runnerDaemon.m_RunnerLog.m_FiLog.FullName));
            runnerDaemon.PrintToConsole(string.Format("MasterRunner machine name: {0}", runnerMasterMachineName));
            runnerDaemon.PrintToConsole(string.Format("Daemon Number: {0}", m_RunnerAssemblyVersion.MinorRevision));

            var mashNames = File.ReadAllLines(m_FiNamesDict.FullName);
            foreach (var mn in mashNames)
            {
                var spl = mn.Split('|');
                m_DictNames.Add(spl[1], spl[0]);
            }

            runnerDaemon.SendDaemonReadyMessage();
            runnerDaemon.MessageLoop();
        }

        static string Hydrate(Match m)
        {
            // Get the matched string. 
            string x = m.ToString();
            string r = m_DictNames[x];
            return r;
        }

        private void MessageLoop()
        {
            var diTemp = new DirectoryInfo(Environment.GetEnvironmentVariable("HOMEDRIVE") + Environment.GetEnvironmentVariable("HOMEPATH") + @"\Documents\XfmTfsTemp-Delete\");
            if (!diTemp.Exists)
                diTemp.Create();
            while (true)
            {
                PrintToConsole("Waiting for message");

                // <=<=<=<=<=<=<=<=<=<=<=<= Receive message sent by RunnerMaster <=<=<=<=<=<=<=<=<=<=<=<=
                var doMessage = ReceiveMessage();

                //     <=<=<=<=<=<=<=<=<=<=<=<= Receive Do sent by RunnerMaster <=<=<=<=<=<=<=<=<=<=<=<=
                if (doMessage.Label == "Do")
                {
                    PrintToConsole("Received Do");
                    var repoLocation = new DirectoryInfo((string)doMessage.Xml.Elements("Repo").Attributes("Val").FirstOrDefault());
                    var testFileStorageRootLocation = (string)doMessage.Xml.Elements("TestFileStorageRootLocation").Attributes("Val").FirstOrDefault();
                    var repo = new Repo(repoLocation);
                    PrintToConsole(string.Format("Adding to Repo: {0}", repoLocation.FullName));
                    var bailList = new List<string>();
                    foreach (var file in doMessage.Xml.Elements("Documents").Elements("Document").Attributes("Name").Select(a => (string)a))
                    {
#if false
                        if (file.StartsWith(@"\\") || file.StartsWith("//"))
                            fileWithWidePrefix = @"//?/UNC/" + file.Substring(1);
                        else
                            fileWithWidePrefix = "//?/" + file;
#endif
                        var moniker = file.Substring(testFileStorageRootLocation.Length);

                        Regex r = new Regex("~-[0-9]+-~");
                        MatchEvaluator eval = new MatchEvaluator(Hydrate);
                        var hydratedMoniker = r.Replace(moniker, eval);

                        PrintToConsole("Storing: " + (hydratedMoniker.Length > 40 ? hydratedMoniker.Substring(hydratedMoniker.Length - 40) : hydratedMoniker));

                        var lastDecimal = file.LastIndexOf('.');
                        string extension = null;
                        if (lastDecimal == -1)
                            extension = "";
                        else
                        {
                            var possibleExtension = file.Substring(lastDecimal);
                            if (possibleExtension.Length <= 5)
                                extension = possibleExtension;
                            else
                                extension = "";
                        }


                        var fiTemp = new FileInfo(Path.Combine(diTemp.FullName, Guid.NewGuid().ToString() + extension));
#if false
                        var lpTemp = "//?/" + fiTemp.FullName;
#endif
                        var lpTemp = fiTemp.FullName;

                        PrintToConsole("Temp: " + lpTemp);

                        int cnt = 0;
                        bool bail = false;
                        while (true)
                        {
                            if (++cnt > 10)
                            {
                                Console.WriteLine("Bailing on this file");
                                bail = true;
                                break;
                            }
                            try
                            {
                                File.Copy(file, lpTemp, false);
                                break;
                            }
                            catch (System.ComponentModel.Win32Exception)
                            {
                                Console.WriteLine("Caught System.ComponentModel.Win32Exception");
                                System.Threading.Thread.Sleep(300);
                            }
                        }

                        if (bail)
                        {
                            bailList.Add(file);
                            continue;
                        }

                        while (true)
                        {
                            try
                            {
                                fiTemp = new FileInfo(fiTemp.FullName);
                                break;
                            }
                            catch (IOException)
                            {
                                System.Threading.Thread.Sleep(20);
                                continue;
                            }
                        }

                        FileAttributes attributes;
                        while (true)
                        {
                            try
                            {
                                attributes = File.GetAttributes(fiTemp.FullName);
                                break;
                            }
                            catch (IOException)
                            {
                                System.Threading.Thread.Sleep(50);
                                continue;
                            }
                        }


                        attributes = RemoveAttribute(attributes, FileAttributes.ReadOnly);
                        File.SetAttributes(fiTemp.FullName, attributes);

                        PrintToConsole("Calling repo.Store: " + fiTemp.FullName); 
                        repo.Store(fiTemp, hydratedMoniker);

                        while (true)
                        {
                            try
                            {
                                fiTemp.Delete();
                            }
                            catch (System.UnauthorizedAccessException)
                            {
                                Console.WriteLine("======================================================================================== CAUGHT EXCEPTION DELETE FILE zzz");
                                var atts = File.GetAttributes(fiTemp.FullName);

                                bool Archive = (atts & FileAttributes.Archive) == FileAttributes.Archive;
                                Console.WriteLine("Archive: {0}", Archive);

                                bool Compressed = (atts & FileAttributes.Compressed) == FileAttributes.Compressed;
                                Console.WriteLine("Compressed: {0}", Compressed);

                                bool Directory = (atts & FileAttributes.Directory) == FileAttributes.Directory;
                                Console.WriteLine("Directory: {0}", Directory);

                                bool Encrypted = (atts & FileAttributes.Encrypted) == FileAttributes.Encrypted;
                                Console.WriteLine("Encrypted: {0}", Encrypted);

                                bool Hidden = (atts & FileAttributes.Hidden) == FileAttributes.Hidden;
                                Console.WriteLine("Hidden: {0}", Hidden);

                                bool ReadOnly = (atts & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
                                Console.WriteLine("ReadOnly: {0}", ReadOnly);
                                
                                System.Threading.Thread.Sleep(20);
                                continue;
                            }
                            break;
                        }
                    }
                    // =>=>=>=>=>=>=>=>=>=>=>=> Send Work Complete =>=>=>=>=>=>=>=>=>=>=>=>
                    PrintToConsole("Sending WorkComplete to RunnerMaster");

                    var cmsg = new XElement("Message",
                        new XElement("RunnerDaemonMachineName",
                            new XAttribute("Val", Environment.MachineName)),
                        new XElement("RunnerDaemonQueueName",
                            new XAttribute("Val", m_RunnerDaemonLocalQueueName)),
                        new XElement("Documents",
                            doMessage.Xml.Element("Documents").Elements("Document").Select(d => {
                                bool bailed = bailList.Contains(d.Attribute("Name").Value);
                                if (bailed)
                                    return new XElement("Document",
                                        d.Attributes(),
                                        new XAttribute("CopyFailed", true));
                                return new XElement("Document", d.Attributes());
                            })));
                    Runner.SendMessage("WorkComplete", cmsg, m_RunnerMasterMachineName, OxRunConstants.RunnerMasterQueueName);
                }
            }
        }

        private static FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove)
        {
            return attributes & ~attributesToRemove;
        }

        public RunnerDaemonXfmTfs(string runnerMasterMachineName, short minorRevisionNumber)
            : base(runnerMasterMachineName, minorRevisionNumber) { }
    }
}
