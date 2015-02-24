using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OxRun;

namespace OxRun
{
    class RunnerMasterCatalog : RunnerMaster
    {
        static DirectoryInfo m_DiRepo;
        static Repo m_Repo;
        static IEnumerable<string> m_FilesToProcess;
        static Dictionary<string, bool> m_RemainingFiles = new Dictionary<string, bool>();
        static List<List<string>> m_Jobs;
        static int m_NumberOfClientComputers;

        static int? m_LimitFileCount = null;


        static void Main(string[] args)
        {
            ConsolePosition.SetConsolePosition();
            if (args.Length != 2)
            {
                throw new ArgumentException("Arguments to RunnerMaster are incorrect.  Should be 1) number of client computers, 2) doc repo location");
            }
            if (!int.TryParse(args[0], out m_NumberOfClientComputers))
                m_NumberOfClientComputers = 1;
            m_DiRepo = new DirectoryInfo(args[1]);
            m_Repo = new Repo(m_DiRepo);
            var runnerMaster = new RunnerMasterCatalog();
            runnerMaster.PrintToConsole(ConsoleColor.White, "RunnerMasterCatalog");
            runnerMaster.PrintToConsole(ConsoleColor.White, string.Format("Number of client computers: {0}", m_NumberOfClientComputers));
            runnerMaster.PrintToConsole(ConsoleColor.White, string.Format("Doc repo location: {0}", m_DiRepo.FullName));
            runnerMaster.InitializeWork();
            runnerMaster.ReceivePingSendPong();
            runnerMaster.SendReportStartToControllerMaster();
            runnerMaster.MessageLoop(m => runnerMaster.ProcessMessage(m));
        }

        public RunnerMasterCatalog() : base() { }

        private void InitializeWork()
        {
            m_FilesToProcess = m_Repo.GetAllOpenXmlFiles();

            if (m_LimitFileCount != null)
                m_FilesToProcess = m_FilesToProcess.Take((int)m_LimitFileCount).ToArray();

            foreach (var item in m_FilesToProcess)
                m_RemainingFiles.Add(item, false);

            m_Jobs = DivvyIntoJobs(m_FilesToProcess, m_NumberOfClientComputers * OxRunConstants.RunnerDaemonProcessesPerClient);
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
                PrintToConsole("Message Size: " + message.MessageSize.ToString());
                PrintToLog(documents.ToString());

                SendReportToControllerMaster(documents);

                foreach (var doc in documents.Elements("Document").Attributes("GuidName").Select(a => (string)a))
                {
                    if (!m_RemainingFiles.Remove(doc))
                    {
                        PrintToConsole("Error, didn't remove: " + doc);
                        Environment.Exit(0);
                    }
                }
                PrintToConsole(string.Format("Remaining files count: {0}", m_RemainingFiles.Count()));
                if (!m_RemainingFiles.Any())
                {
                    PrintToConsole("All done");
                    SendReportCompleteToControllerMaster();
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

        private void SendReportToControllerMaster(XElement report)
        {
            // =>=>=>=>=>=>=>=>=>=>=>=> Send Report message =>=>=>=>=>=>=>=>=>=>=>=>
            PrintToConsole(string.Format("Sending Report"));
            var cmsg = new XElement("Message",
                new XElement("ReportName",
                    new XAttribute("Val", "Catalog")),
                report);

            Runner.SendMessage("Report", cmsg, m_MasterMachineName, OxRunConstants.ControllerMasterStatusQueueName);
        }

        private void SendReportCompleteToControllerMaster()
        {
            // =>=>=>=>=>=>=>=>=>=>=>=> Send Report Complete message =>=>=>=>=>=>=>=>=>=>=>=>
            PrintToConsole(string.Format("Sending Report Complete"));
            var cmsg = new XElement("Message",
                new XElement("ReportName",
                    new XAttribute("Val", "Catalog")));

            Runner.SendMessage("ReportComplete", cmsg, m_MasterMachineName, OxRunConstants.ControllerMasterStatusQueueName);
        }

        private void SendReportStartToControllerMaster()
        {
            // =>=>=>=>=>=>=>=>=>=>=>=> Send Report Start message =>=>=>=>=>=>=>=>=>=>=>=>
            PrintToConsole(string.Format("Sending Report Start"));
            var cmsg = new XElement("Message",
                new XElement("ReportName",
                    new XAttribute("Val", "Catalog")));

            Runner.SendMessage("ReportStart", cmsg, m_MasterMachineName, OxRunConstants.ControllerMasterStatusQueueName);
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
                new XElement("Documents",
                    thisJob.Select(item => new XElement("Document",
                        new XAttribute("GuidName", item)))));

            //var s = cmsg.ToString().Split(new [] {"\r\n"}, StringSplitOptions.None);
            //foreach (var item in s)
            //    PrintToConsole(item);

            Runner.SendMessage("Do", cmsg, runnerDaemonMachineName, runnerDaemonQueueName);

        }
    }
}
