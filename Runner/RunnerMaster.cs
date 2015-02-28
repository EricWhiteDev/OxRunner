using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OxRun
{
    public class RunnerMaster
    {
        MessageQueue m_RunnerMasterQueue;
        MessageQueue m_RunnerMasterIsAliveQueue;
        public RunnerLog m_RunnerLog;
        public string m_MasterMachineName;

        public FileInfo m_FiConfig;
        public XDocument m_XdConfig;
        public string m_Editor = null;
        public bool? m_WriteLog = null;
        public bool? m_CollectProcessTimeMetrics = null;
        public bool m_Verbose = false;

        public RunnerMaster()
        {
            ReadControllerConfig();
            m_RunnerLog = new RunnerLog("../../../RunnerMaster", m_WriteLog == true);
            if (m_WriteLog == true)
                m_RunnerLog.Log(ConsoleColor.White, string.Format("Log: {0}", m_RunnerLog.m_FiLog.FullName));

            // ======================= INIT runner master Queue =======================
            // if runner master exists
            //     clear it
            // else
            //     delete it
            var runnerMasterQueueName = Runner.GetQueueName(Environment.MachineName, OxRunConstants.RunnerMasterQueueName);
            m_RunnerMasterQueue = null;
            if (MessageQueue.Exists(runnerMasterQueueName))
            {
                PrintToConsole(ConsoleColor.White, string.Format("Clearing queue {0}", runnerMasterQueueName));
                m_RunnerMasterQueue = new MessageQueue(runnerMasterQueueName);
                Runner.ClearQueue(m_RunnerMasterQueue);
            }
            else
            {
                PrintToConsole(ConsoleColor.White, string.Format("Creating queue {0}", runnerMasterQueueName));
                m_RunnerMasterQueue = MessageQueue.Create(runnerMasterQueueName, false);
                Runner.ClearQueue(m_RunnerMasterQueue);
            }

            // ======================= INIT runner master IsAlive Queue =======================
            // if runner master IsAlive exists
            //     clear it
            // else
            //     delete it
            var runnerMasterIsAliveQueueName = Runner.GetQueueName(Environment.MachineName, OxRunConstants.RunnerMasterIsAliveQueueName);
            m_RunnerMasterIsAliveQueue = null;
            if (MessageQueue.Exists(runnerMasterIsAliveQueueName))
            {
                PrintToConsole(ConsoleColor.White, string.Format("Clearing queue {0}", runnerMasterIsAliveQueueName));
                m_RunnerMasterIsAliveQueue = new MessageQueue(runnerMasterIsAliveQueueName);
                Runner.ClearQueue(m_RunnerMasterIsAliveQueue);
            }
            else
            {
                PrintToConsole(ConsoleColor.White, string.Format("Creating queue {0}", runnerMasterIsAliveQueueName));
                m_RunnerMasterIsAliveQueue = MessageQueue.Create(runnerMasterIsAliveQueueName, false);
                Runner.ClearQueue(m_RunnerMasterIsAliveQueue);
            }
        }

        private void ReadControllerConfig()
        {
            DirectoryInfo diOxRunner = Runner.GetOxRunnerDirectory();
            m_FiConfig = new FileInfo(Path.Combine(diOxRunner.FullName, "ControllerConfig.xml"));
            m_XdConfig = XDocument.Load(m_FiConfig.FullName);
            m_Editor = (string)m_XdConfig.Root.Elements("Editor").Attributes("Val").FirstOrDefault();
            m_WriteLog = (bool?)m_XdConfig.Root.Elements("WriteLog").Attributes("Val").FirstOrDefault();
            m_CollectProcessTimeMetrics = (bool?)m_XdConfig.Root.Elements("CollectProcessTimeMetrics").Attributes("Val").FirstOrDefault();
        }


        public void MessageLoop(Action<DaemonMessage> processMessage)
        {
            while (true)
            {
                if (m_Verbose)
                    PrintToConsole("Waiting for message");

                // <=<=<=<=<=<=<=<=<=<=<=<= Receive message sent by RunnerDaemon <=<=<=<=<=<=<=<=<=<=<=<=
                OxMessage doMessage = null;
                doMessage = Runner.ReceiveMessage(m_RunnerMasterQueue, 5);
                if (doMessage.Timeout)
                {
                    PrintToConsole("Timed out");
                    continue;
                }

                DaemonMessage daemonMessage = new DaemonMessage();
                daemonMessage.Label = doMessage.Label;
                daemonMessage.Xml = doMessage.Xml;
                daemonMessage.RunnerDaemonMachineName = (string)doMessage.Xml.Elements("RunnerDaemonMachineName").Attributes("Val").FirstOrDefault();
                daemonMessage.RunnerDaemonQueueName = (string)doMessage.Xml.Elements("RunnerDaemonQueueName").Attributes("Val").FirstOrDefault();
                daemonMessage.MessageSize = doMessage.MessageSize;
                processMessage(daemonMessage);
            }
        }

        public void ReceivePingSendPong()
        {
            if (m_Verbose)
                PrintToConsole("Waiting for message");

            // <=<=<=<=<=<=<=<=<=<=<=<= Receive message sent by ControllerMaster <=<=<=<=<=<=<=<=<=<=<=<=
            var doMessage = Runner.ReceiveMessage(m_RunnerMasterIsAliveQueue, 20);
            if (doMessage.Timeout)
            {
                PrintToConsole("Error, did not receive Ping message from ControllerMaster to RunnerMasterStatus queue");
                Console.ReadKey();
                Environment.Exit(0);
            }

            m_MasterMachineName = (string)doMessage.Xml.Elements("MasterMachineName").Attributes("Val").FirstOrDefault();

            //     <=<=<=<=<=<=<=<=<=<=<=<= Receive Ping sent by Controller Master <=<=<=<=<=<=<=<=<=<=<=<=
            if (doMessage.Label == "Ping")
            {
                PrintToConsole("Received Ping");

                // =>=>=>=>=>=>=>=>=>=>=>=> Send Pong message =>=>=>=>=>=>=>=>=>=>=>=>
                PrintToConsole("Sending Pong");
                var cmsg = new XElement("Message",
                    new XElement("MasterMachineName",
                        new XAttribute("Val", Environment.MachineName)));
                Runner.SendMessage("Pong", cmsg, m_MasterMachineName, OxRunConstants.ControllerMasterIsAliveQueueName);
            }
        }

        private static IEnumerable<string> Lines(StreamReader source)
        {
            String line;

            if (source == null)
                throw new ArgumentNullException("source");
            while ((line = source.ReadLine()) != null)
            {
                yield return line;
            }
        }

        public List<List<string>> DivvyIntoJobs(Repo repo, IEnumerable<string> workItems, int totalWorkDaemons)
        {
            var fiProcessTimeMetrics = new FileInfo(Path.Combine(repo.m_RepoLocation.FullName, "ProcessTimeMetrics.txt"));
            if (fiProcessTimeMetrics.Exists)
                return DivvyIntoJobsUsingTimeMetrics(fiProcessTimeMetrics, workItems, totalWorkDaemons);

            List<List<string>> jobs = new List<List<string>>();
            var filteredWorkItems = workItems.Where(i => i != "");

            int remainingItems = filteredWorkItems.Count();
            List<string> currentJob = null;
            int itemsInThisJob = 1;
            foreach (var item in filteredWorkItems)
            {
                if (currentJob == null)
                {
                    currentJob = new List<string>();
                    itemsInThisJob = CalcItemsInCurrentJob(remainingItems, totalWorkDaemons, 300);
                }
                currentJob.Add(item);
                itemsInThisJob--;
                remainingItems--;
                if (itemsInThisJob == 0)
                {
                    jobs.Add(currentJob);
                    currentJob = null;
                }
            }

            return jobs;
        }

        private List<List<string>> DivvyIntoJobsUsingTimeMetrics(FileInfo fiProcessTimeMetrics, IEnumerable<string> workItems, int totalWorkDaemons)
        {
            Dictionary<string, long> processTimeMetrics = new Dictionary<string, long>();

            using (StreamReader sr = new StreamReader(fiProcessTimeMetrics.FullName))
            {
                foreach (var line in Lines(sr))
                {
                    var spl = line.Split('|');
                    processTimeMetrics.Add(spl[0], long.Parse(spl[1]));
                }
            }

            List<List<string>> jobs = new List<List<string>>();
            var filteredWorkItems = workItems.Where(i => i != "");

            // process all documents that take a long time first.
            var workItemsWithMetrics = filteredWorkItems
                .Select(wi =>
                {
                    long ticks = 0;
                    if (processTimeMetrics.ContainsKey(wi))
                        ticks = processTimeMetrics[wi];
                    else
                        ticks = 0;
                    return new
                    {
                        Item = wi,
                        Ticks = ticks,
                    };
                })
                .OrderByDescending(wi => wi.Ticks)
                .ToList();

            double totalSeconds = (workItemsWithMetrics
                .Select(wim => wim.Ticks)
                .Sum()) / (double)TimeSpan.TicksPerSecond;

            // =============================================== change following to increase/decrease items per job
            double divisor = 8.0;

            double remainingSeconds = totalSeconds;
            double maxSecondsPerJob = ((double)remainingSeconds / (double)totalWorkDaemons) / divisor;
            List<string> currentJob = null;
            double secondsInThisJob = 0.0;
            int maxItemsPerJob = 300;
            int itemsInThisJob = 0;

            foreach (var item in workItemsWithMetrics)
            {
                if (currentJob == null)
                    currentJob = new List<string>();

                currentJob.Add(item.Item);
                ++itemsInThisJob;
                double seconds = (double)item.Ticks / (double)TimeSpan.TicksPerSecond;
                secondsInThisJob += seconds;
                remainingSeconds -= seconds;
                if (secondsInThisJob > maxSecondsPerJob || itemsInThisJob == maxItemsPerJob)
                {
                    jobs.Add(currentJob);
                    currentJob = null;
                    secondsInThisJob = 0.0;
                    itemsInThisJob = 0;
                    maxSecondsPerJob = ((double)remainingSeconds / (double)totalWorkDaemons) / divisor;
                }
            }

            return jobs;
        }

        private int CalcItemsInCurrentJob(int remainingItems, int totalWorkDaemons, int maxPerJob)
        {
            int itemsInCurrentJob = (remainingItems / totalWorkDaemons) / 5;
            itemsInCurrentJob = Math.Min(itemsInCurrentJob, maxPerJob);
            itemsInCurrentJob = Math.Max(itemsInCurrentJob, 1);
            return itemsInCurrentJob;
        }

        public void PrintToConsole(string text)
        {
            m_RunnerLog.Log(ConsoleColor.White, text);
        }

        public void PrintToLog(string text)
        {
            m_RunnerLog.LogOnly(text);
        }

        public void PrintToConsole(ConsoleColor color, string text)
        {
            m_RunnerLog.Log(color, text);
        }
    }

}
