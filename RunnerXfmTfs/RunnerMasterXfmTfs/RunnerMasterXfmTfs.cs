using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OxRunner
{
    class RunnerMasterXfmTfs : RunnerMaster
    {
        static DirectoryInfo m_DiRepo;
        static string m_TestFileStorageRootLocation = null;
        static string m_TestFileStorageFileList = null;
        static IEnumerable<string> m_FilesToProcess;
        static Dictionary<string, bool> m_RemainingFiles = new Dictionary<string,bool>();
        static List<List<string>> m_Jobs;
        static int m_NumberOfClientComputers;

        static int? m_LimitFileCount = null;

        static void Main(string[] args)
        {
            //ConsolePosition.SetConsolePosition();
            if (args.Length != 3)
            {
                throw new ArgumentException("Arguments to RunnerMaster are incorrect.  Should be 1) number of client computers, 2) test file storage root location, 3) test file storage file list");
            }
            if (!int.TryParse(args[0], out m_NumberOfClientComputers))
                m_NumberOfClientComputers = 1;
            m_TestFileStorageRootLocation = args[1];
            m_TestFileStorageFileList = args[2];
            var runnerMaster = new RunnerMasterXfmTfs();
            runnerMaster.PrintToConsole(ConsoleColor.White, "RunnerXFormTestStorageMaster");
            runnerMaster.PrintToConsole(ConsoleColor.White, string.Format("Number of client computers: {0}", m_NumberOfClientComputers));
            runnerMaster.PrintToConsole(ConsoleColor.White, string.Format("Test file storage root location: {0}", m_TestFileStorageRootLocation));
            runnerMaster.PrintToConsole(ConsoleColor.White, string.Format("Test file storage file list: {0}", m_TestFileStorageFileList));
            runnerMaster.InitializeWork();
            runnerMaster.ReceivePingSendPong();
            runnerMaster.MessageLoop(m => runnerMaster.ProcessMessage(m));
        }

        public RunnerMasterXfmTfs() : base() { }

        private void InitializeWork()
        {
            PrintToConsole(ConsoleColor.White, "InitializeWork entry zzz");

            FileInfo fiFileList = new FileInfo(m_TestFileStorageRootLocation + m_TestFileStorageFileList);
            m_FilesToProcess = File.ReadAllLines(fiFileList.FullName);

            PrintToConsole(ConsoleColor.White, "InitializeWork before calling toarray creating small list zzz");

            if (m_LimitFileCount != null)
                m_FilesToProcess = m_FilesToProcess.Take((int)m_LimitFileCount).ToArray();

            PrintToConsole(ConsoleColor.White, "InitializeWork after read all lines zzz");

            foreach (var item in m_FilesToProcess)
                m_RemainingFiles.Add(item, false);

            PrintToConsole(ConsoleColor.White, "InitializeWork after adding to files to process zzz");

            m_Jobs = DivvyIntoJobs(null, m_FilesToProcess, m_NumberOfClientComputers * OxRunConstants.RunnerDaemonProcessesPerClient);

            PrintToConsole(ConsoleColor.White, "InitializeWork after divvy into jobs zzz");

            PrintToConsole(ConsoleColor.White, "InitializeWork before creating new repo zzz");

            m_DiRepo = FileUtils.GetDateTimeStampedDirectoryInfo(@"\\bigi5-8\c\TestFileRepo");
            var repo = new Repo(m_DiRepo);

            PrintToConsole(ConsoleColor.White, "InitializeWork after creating new repo zzz");

            PrintToConsole(ConsoleColor.White, string.Format("Creating Repo {0}", m_DiRepo.FullName));
        }

        private void ProcessMessage(DaemonMessage message)
        {
            PrintToConsole(ConsoleColor.White, string.Format("Received {0} from {1} {2}", message.Label, message.RunnerDaemonMachineName, message.RunnerDaemonQueueName));
            //     <=<=<=<=<=<=<=<=<=<=<=<= Receive RunnerDaemonReady sent by RunnerDaemon <=<=<=<=<=<=<=<=<=<=<=<=
            if (message.Label == "RunnerDaemonReady")
            {
                PrintToConsole("Received RunnerDaemonReady");
                if (m_Jobs.Any())
                {
                    SendFilesToDaemon(message.RunnerDaemonMachineName, message.RunnerDaemonQueueName);
                    return;
                }
            }
            //     <=<=<=<=<=<=<=<=<=<=<=<= Receive WorkComplete sent by RunnerDaemon <=<=<=<=<=<=<=<=<=<=<=<=
            if (message.Label == "WorkComplete")
            {
                var documents = message.Xml.Element("Documents");
                PrintToConsole(string.Format("Received WorkComplete, File count: {0}", documents.Elements("Document").Count()));
                PrintToLog(documents.ToString());

                foreach (var doc in documents.Elements("Document").Attributes("Name").Select(a => (string)a))
                {
                    var toRemove = doc.Substring(m_TestFileStorageRootLocation.Length);
                    //PrintToConsole("toRemove: " + toRemove);
                    //PrintToConsole("m_Remaining.First: " + m_RemainingFiles.First());
                    if (!m_RemainingFiles.Remove(toRemove))
                    {
                        PrintToConsole("Error, didn't remove: " + toRemove);
                        Environment.Exit(0);
                    }
                }
                PrintToConsole(string.Format("Remaining files count: {0}", m_RemainingFiles.Count()));
                if (!m_RemainingFiles.Any())
                {
                    PrintToConsole("All done");
                    // send message to controller daemon to kill runner daemons
                    Environment.Exit(0);
                }

                if (m_Jobs.Any())
                {
                    SendFilesToDaemon(message.RunnerDaemonMachineName, message.RunnerDaemonQueueName);
                    return;
                }
            }
        }

        private void SendFilesToDaemon(string runnerDaemonMachineName, string runnerDaemonQueueName)
        {
            int numberOfRunnerDaemons = m_NumberOfClientComputers * OxRunConstants.RunnerDaemonProcessesPerClient;

            List<string> thisJob = m_Jobs.First();
            m_Jobs.Remove(thisJob);

            // =>=>=>=>=>=>=>=>=>=>=>=> Send Do message =>=>=>=>=>=>=>=>=>=>=>=>
            PrintToConsole(string.Format("Do: remaining: {0}  this job: {1}", m_Jobs.Select(j => j.Count()).Sum(), thisJob.Count().ToString()));
            var cmsg = new XElement("Message",
                new XElement("RunnerMasterMachineName",
                    new XAttribute("Val", Environment.MachineName)),
                new XElement("Repo", new XAttribute("Val", m_DiRepo.FullName)),
                new XElement("TestFileStorageRootLocation", new XAttribute("Val", m_TestFileStorageRootLocation)),
                new XElement("Documents",
                    thisJob.Select(item => new XElement("Document",
                        new XAttribute("Name", m_TestFileStorageRootLocation + item)))));


            //var s = cmsg.ToString().Split(new [] {"\r\n"}, StringSplitOptions.None);
            //foreach (var item in s)
            //    PrintToConsole(item);
            Runner.SendMessage("Do", cmsg, runnerDaemonMachineName, runnerDaemonQueueName);
        }
    }
}
