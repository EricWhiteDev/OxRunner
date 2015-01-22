using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OxRun
{
    class RunnerMasterXfmTfs : RunnerMaster
    {
        static DirectoryInfo m_DiRepo;
        static string m_TestFileStorageLocation = @"C:\";
        static string[] m_FilesToProcess;
        static List<List<string>> m_Jobs;
        static int m_NumberOfClientComputers;

        static void Main(string[] args)
        {
            if (args.Length != 0)
            {
                if (!int.TryParse(args[0], out m_NumberOfClientComputers))
                    m_NumberOfClientComputers = 1;
            }
            else
                m_NumberOfClientComputers = 1;
            var runnerMaster = new RunnerMasterXfmTfs();
            runnerMaster.PrintToConsole(ConsoleColor.White, string.Format("Number of client computers: {0}", m_NumberOfClientComputers));
            runnerMaster.PrintToConsole(ConsoleColor.White, "RunnerXFormTestStorageMaster");
            runnerMaster.InitializeWork();
            runnerMaster.ReceivePingSendPong();
            runnerMaster.MessageLoop(m => runnerMaster.ProcessMessage(m));
        }


        public RunnerMasterXfmTfs() : base() { }

        private void InitializeWork()
        {
            //FileInfo fiFileList = new FileInfo(m_TestFileStorageLocation + "/TestFileStorage/TestFileStorageFileList.txt");
            FileInfo fiFileList = new FileInfo(m_TestFileStorageLocation + "/TestFileStorage/SmallFileList.txt");
            m_FilesToProcess = File.ReadAllLines(fiFileList.FullName);
            m_Jobs = DivvyIntoJobs(m_FilesToProcess, m_NumberOfClientComputers * OxRunConstants.RunnerDaemonProcessesPerClient);

            m_DiRepo = FileUtils.GetDateTimeStampedDirectoryInfo("C:/TestFileRepo");
            var repo = new Repo(m_DiRepo);

            PrintToConsole(ConsoleColor.White, string.Format("Creating Repo {0}", m_DiRepo.FullName));
        }

        private void ProcessMessage(DaemonMessage message)
        {
            PrintToConsole(ConsoleColor.White, string.Format("Received {0} from {1} {2}", message.Label, message.RunnerDaemonMachineName, message.RunnerDaemonQueueName));
            //     <=<=<=<=<=<=<=<=<=<=<=<= Receive RunnerDaemonReady sent by RunnerDaemon <=<=<=<=<=<=<=<=<=<=<=<=
            if (message.Label == "RunnerDaemonReady")
            {
                PrintToConsole("Received RunnerDaemonReady");
                if (!m_Jobs.Any())
                {
                    // all done
                    // todo kill the RunnerDaemons
                    Environment.Exit(0);
                }
                SendFilesToDaemon(message.RunnerDaemonMachineName, message.RunnerDaemonQueueName);
                return;
            }
            //     <=<=<=<=<=<=<=<=<=<=<=<= Receive WorkComplete sent by RunnerDaemon <=<=<=<=<=<=<=<=<=<=<=<=
            if (message.Label == "WorkComplete")
            {
                PrintToConsole("Received WorkComplete");
                if (!m_Jobs.Any())
                {
                    // all done
                    // todo kill the RunnerDaemons
                    Environment.Exit(0);
                }
                SendFilesToDaemon(message.RunnerDaemonMachineName, message.RunnerDaemonQueueName);
                return;
            }
        }

        private void SendFilesToDaemon(string runnerDaemonMachineName, string runnerDaemonQueueName)
        {
            int numberOfRunnerDaemons = m_NumberOfClientComputers * OxRunConstants.RunnerDaemonProcessesPerClient;

            List<string> thisJob = m_Jobs.First();
            m_Jobs.Remove(thisJob);

            // =>=>=>=>=>=>=>=>=>=>=>=> Send Do message =>=>=>=>=>=>=>=>=>=>=>=>
            PrintToConsole("Sending Do");
            var cmsg = new XElement("Message",
                new XElement("RunnerMasterMachineName",
                    new XAttribute("Val", Environment.MachineName)),
                new XElement("Repo", new XAttribute("Val", m_DiRepo.FullName)),
                new XElement("Documents",
                    thisJob.Select(item => new XElement("Document",
                        new XAttribute("Name", m_TestFileStorageLocation + item)))));
            //var s = cmsg.ToString().Split(new [] {"\r\n"}, StringSplitOptions.None);
            //foreach (var item in s)
            //    PrintToConsole(item);
            Runner.SendMessage("Do", cmsg, runnerDaemonMachineName, runnerDaemonQueueName);
        }
    }
}
