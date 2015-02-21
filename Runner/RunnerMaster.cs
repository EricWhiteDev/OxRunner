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

        public RunnerMaster()
        {
            m_RunnerLog = new RunnerLog("../../../RunnerMaster");
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

        public void MessageLoop(Action<DaemonMessage> processMessage)
        {
            while (true)
            {
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
                processMessage(daemonMessage);
            }
        }

        public void ReceivePingSendPong()
        {
            PrintToConsole("Waiting for message");

            // <=<=<=<=<=<=<=<=<=<=<=<= Receive message sent by ControllerMaster <=<=<=<=<=<=<=<=<=<=<=<=
            var doMessage = Runner.ReceiveMessage(m_RunnerMasterIsAliveQueue, 20);
            if (doMessage.Timeout)
            {
                PrintToConsole("Error, did not receive Ping message from ControllerMaster to RunnerMasterStatus queue");
                Console.ReadKey();
                Environment.Exit(0);
            }

            var masterMachineName = (string)doMessage.Xml.Elements("MasterMachineName").Attributes("Val").FirstOrDefault();

            //     <=<=<=<=<=<=<=<=<=<=<=<= Receive Ping sent by Controller Master <=<=<=<=<=<=<=<=<=<=<=<=
            if (doMessage.Label == "Ping")
            {
                PrintToConsole("Received Ping");

                // =>=>=>=>=>=>=>=>=>=>=>=> Send Pong message =>=>=>=>=>=>=>=>=>=>=>=>
                PrintToConsole("Sending Pong");
                var cmsg = new XElement("Message",
                    new XElement("MasterMachineName",
                        new XAttribute("Val", Environment.MachineName)));
                Runner.SendMessage("Pong", cmsg, masterMachineName, OxRunConstants.ControllerMasterIsAliveQueueName);
            }
        }

        public List<List<string>> DivvyIntoJobs(IEnumerable<string> workItems, int totalWorkDaemons)
        {
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
                    itemsInThisJob = CalcItemsInCurrentJob(remainingItems, totalWorkDaemons, 500);
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
            //foreach (var j in jobs)
            //{
            //    Console.WriteLine(j.Count());
            //}
            //Console.ReadKey();
            return jobs;
        }

        private int CalcItemsInCurrentJob(int remainingItems, int totalWorkDaemons, int maxPerJob)
        {
            int itemsInCurrentJob = (remainingItems / totalWorkDaemons) / 2;
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
