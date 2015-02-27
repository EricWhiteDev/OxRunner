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
    public class DaemonMessage
    {
        public string Label;
        public XElement Xml;
        public string RunnerDaemonMachineName;
        public string RunnerDaemonQueueName;
        public int MessageSize;
    }

    public class RunnerDaemon
    {
        public string m_RunnerDaemonLocalQueueName;
        public string m_RunnerMasterMachineName;
        MessageQueue m_RunnerDaemonQueue;
        MessageQueue m_RunnerMasterQueue;
        public RunnerLog m_RunnerLog;

        public FileInfo m_FiConfig;
        public XDocument m_XdConfig;
        public string m_Editor = null;
        public bool? m_WriteLog = null;

        public RunnerDaemon(string runnerMasterMachineName, short minorRevisionNumber)
        {
            ReadControllerConfig();
            m_RunnerLog = new RunnerLog(string.Format("../../../RunnerDaemon-{0:00}", minorRevisionNumber), m_WriteLog == true);
            if (m_WriteLog == true)
                m_RunnerLog.Log(ConsoleColor.White, string.Format("Log: {0}", m_RunnerLog.m_FiLog.FullName));
            m_RunnerMasterMachineName = runnerMasterMachineName;
            InitializeRunnerDaemonQueues(runnerMasterMachineName, minorRevisionNumber);
        }

        private void ReadControllerConfig()
        {
            m_FiConfig = new FileInfo("../../../ControllerConfig.xml");
            m_XdConfig = XDocument.Load(m_FiConfig.FullName);
            m_Editor = (string)m_XdConfig.Root.Elements("Editor").Attributes("Val").FirstOrDefault();
            m_WriteLog = (bool?)m_XdConfig.Root.Elements("WriteLog").Attributes("Val").FirstOrDefault();
        }

        public void SendDaemonReadyMessage()
        {
            // =>=>=>=>=>=>=>=>=>=>=>=> Send RunnerDaemonReady =>=>=>=>=>=>=>=>=>=>=>=>
            PrintToConsole("Sending RunnerDaemonReady to RunnerMaster");
            PrintToConsole("RunnerMaster machine name: " + m_RunnerMasterMachineName);
            PrintToConsole("RunnerMaster queue name: " + OxRunConstants.RunnerMasterQueueName);

            var cmsg = new XElement("Message",
                new XElement("RunnerDaemonMachineName",
                    new XAttribute("Val", Environment.MachineName)),
                new XElement("RunnerDaemonQueueName",
                    new XAttribute("Val", m_RunnerDaemonLocalQueueName)));
            Runner.SendMessage("RunnerDaemonReady", cmsg, m_RunnerMasterMachineName, OxRunConstants.RunnerMasterQueueName);
        }

        public void InitializeRunnerDaemonQueues(string runnerMasterMachineName, short minorRevisionNumber)
        {
            // ======================= INIT controller daemon Queue =======================
            // if controller daemon exists
            //     clear it
            // else
            //     create it
            m_RunnerDaemonLocalQueueName = OxRunConstants.RunnerDaemonQueueName + string.Format("{0:00}", minorRevisionNumber);
            var runnerDaemonQueueName = Runner.GetQueueName(Environment.MachineName, m_RunnerDaemonLocalQueueName);
            m_RunnerDaemonQueue = null;
            if (MessageQueue.Exists(runnerDaemonQueueName))
            {
                m_RunnerDaemonQueue = new MessageQueue(runnerDaemonQueueName);
                Runner.ClearQueue(m_RunnerDaemonQueue);
            }
            else
            {
                m_RunnerDaemonQueue = MessageQueue.Create(runnerDaemonQueueName, false);
                Runner.ClearQueue(m_RunnerDaemonQueue);
            }
            PrintToConsole("Runner daemon queue created");

            // ======================= INIT runner master queue =======================
            // create it
            var runnerMasterQueueName = Runner.GetQueueName(runnerMasterMachineName, OxRunConstants.RunnerMasterQueueName);
            m_RunnerMasterQueue = new MessageQueue(runnerMasterQueueName);
            PrintToConsole("Runner master queue created");
        }

        public OxMessage ReceiveMessage()
        {
            var message = Runner.ReceiveMessage(m_RunnerDaemonQueue, null);
            return message;
        }

        public void PrintToConsole(string text)
        {
            m_RunnerLog.Log(ConsoleColor.White, text);
        }

        public void PrintToConsole(ConsoleColor color, string text)
        {
            m_RunnerLog.Log(color, text);
        }
    }

}
