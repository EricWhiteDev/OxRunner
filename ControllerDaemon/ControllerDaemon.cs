using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OxRunner
{
    class ControllerDaemon
    {
        private static ProcessStartInfo RunRunnerDaemon(string exe, string args)
        {
            // ======================================================================
            // ======================================================================
            // =========================== Running Daemon ===========================
            // ======================================================================
            // ======================================================================
            // To start the RunnerDaemon in debug, set breakpoint here, then run RunnerDaemon in VS with specified args.
            ProcessStartInfo si = new ProcessStartInfo(exe, args);
            return si;
        }

        static MessageQueue m_ControllerDaemonQueue;
        static XDocument m_XdConfig;
        static XElement m_ThisComputerConfig;
        static bool? m_WriteLog;

        static void Main(string[] args)
        {
            var fiConfig = new FileInfo("../../../ControllerConfig.xml");
            m_XdConfig = XDocument.Load(fiConfig.FullName);
            m_WriteLog = (bool?)m_XdConfig.Root.Elements("WriteLog").Attributes("Val").FirstOrDefault();

            ConsolePosition.SetControllerDaemonConsolePosition(ConsoleConstants.Height, ConsoleConstants.Width);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("ControllerDaemon: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(fiConfig.FullName);

            m_ThisComputerConfig = m_XdConfig
                .Root
                .Elements("Computers")
                .Elements("Computer")
                .FirstOrDefault(c => ((string)c.Attribute("Name")).ToLower() == Environment.MachineName.ToLower());
            if (m_ThisComputerConfig == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error, this computer ({0}) is not in ControllerConfig.xml", Environment.MachineName);
                Environment.Exit(0);
            }

            Init();
            MessageLoop();
        }

        private static void MessageLoop()
        {
            while (true)
            {
                Console.WriteLine("Waiting for message");

                // <=<=<=<=<=<=<=<=<=<=<=<= Receive message sent by ControllerMaster <=<=<=<=<=<=<=<=<=<=<=<=
                var doMessage = Runner.ReceiveMessage(m_ControllerDaemonQueue, null);

                var masterMachineName = (string)doMessage.Xml.Elements("MasterMachineName").Attributes("Val").FirstOrDefault();

                //     <=<=<=<=<=<=<=<=<=<=<=<= Receive Ping sent by Master <=<=<=<=<=<=<=<=<=<=<=<=
                if (doMessage.Label == "Ping")
                {
                    Console.WriteLine("Received Ping");

                    // =>=>=>=>=>=>=>=>=>=>=>=> Send Pong message =>=>=>=>=>=>=>=>=>=>=>=>
                    Console.WriteLine("Sending Pong");
                    var cmsg = new XElement("Message",
                        new XElement("DaemonMachineName",
                            new XAttribute("Val", Environment.MachineName)));
                    Runner.SendMessage("Pong", cmsg, masterMachineName, OxRunConstants.ControllerMasterQueueName);
                }

                //     <=<=<=<=<=<=<=<=<=<=<=<= Receive Do sent by Master <=<=<=<=<=<=<=<=<=<=<=<=
                else if (doMessage.Label == "Do")
                {
                    if (doMessage.Xml.Name.LocalName == "Run")
                    {
                        var exe = (string)doMessage.Xml.Attribute("Exe");
                        // there is one arg to each daemon - the masterMachineName
                        var args = masterMachineName;
                        Console.WriteLine("Received Do {0} {1}", doMessage.Xml.Name.LocalName, exe);

                        if (exe.Contains('*'))
                        {
                            for (int i = 1; i <= OxRunConstants.RunnerDaemonProcessesPerClient; i++)
                            {
                                var thisExeName = new FileInfo(exe.Replace("*", string.Format("{0:00}", i)));
                                RunOneExe(masterMachineName, thisExeName.FullName, args);
                            }
                        }
                        else
                        {
                            RunOneExe(masterMachineName, exe, args);
                        }
                    }

                    else if (doMessage.Xml.Name.LocalName == "ListProcesses")
                    {
                        var searchFor = (string)doMessage.Xml.Attribute("SearchFor");
                        Console.WriteLine("Received Do {0} {1}", doMessage.Xml.Name.LocalName, searchFor);
                        List<Process> processes = new List<Process>();
                        if (searchFor.EndsWith("*"))
                        {
                            var s = searchFor.Substring(0, searchFor.Length - 1);
                            for (int count = 1; count <= OxRunConstants.RunnerDaemonProcessesPerClient; count++)
                            {
                                var s2 = s + string.Format("{0:00}", count);
                                var p = Process.GetProcessesByName(s2);
                                foreach (var item in p)
                                    processes.Add(item);
                            }
                        }
                        else
                        {
                            processes = Process.GetProcessesByName(searchFor).ToList();
                        }

                        // =>=>=>=>=>=>=>=>=>=>=>=> Send Process List message =>=>=>=>=>=>=>=>=>=>=>=>
                        Console.WriteLine("Sending Process List message");
                        var cmsg = new XElement("Message",
                            new XElement("DaemonMachineName",
                                new XAttribute("Val", Environment.MachineName)),
                            new XElement("Processes",
                                processes.Select(p => new XElement("Process",
                                    new XAttribute("ProcessId", p.Id)))));
                        Runner.SendMessage("ProcessList", cmsg, masterMachineName, OxRunConstants.ControllerMasterStatusQueueName);
                    }

                    else if (doMessage.Xml.Name.LocalName == "KillProcesses")
                    {
                        var searchFor = (string)doMessage.Xml.Attribute("SearchFor");
                        Console.WriteLine("Received Do {0} {1}", doMessage.Xml.Name.LocalName, searchFor);
                        if (searchFor.EndsWith("*"))
                        {
                            var s = searchFor.Substring(0, searchFor.Length - 1);
                            for (int count = 1; count <= OxRunConstants.RunnerDaemonProcessesPerClient; count++)
                            {
                                var s2 = s + string.Format("{0:00}", count);
                                Process[] processes = Process.GetProcessesByName(s2);
                                foreach (var item in processes)
                                    item.Kill();
                            }
                        }
                        else
                        {
                            Process[] processes = Process.GetProcessesByName(searchFor);
                            foreach (var item in processes)
                            {
                                item.Kill();
                            }
                        }
                    }
                    else if (doMessage.Xml.Name.LocalName == "DeleteLogs")
                    {
                        Console.WriteLine("Received Do {0}", doMessage.Xml.Name.LocalName);
                        DirectoryInfo oxRunnerDir = new DirectoryInfo(@"../../../");
                        Console.WriteLine("Deleting logs from {0}", oxRunnerDir.FullName);
                        DeleteLogsRecursive(oxRunnerDir);
                    }
                }
            }
        }

        private static void DeleteLogsRecursive(DirectoryInfo dir)
        {
            foreach (var logFile in dir.GetFiles("*.log"))
                logFile.Delete();
            foreach (var subDir in dir.GetDirectories())
                DeleteLogsRecursive(subDir);
        }

        private static void RunOneExe(string masterMachineName, string exe, string args)
        {
            ProcessStartInfo si = RunRunnerDaemon(exe, args);
            string windowStyle = (string)m_ThisComputerConfig.Attribute("WindowStyle");
            if (windowStyle == null)
                windowStyle = "Normal";
            if (windowStyle == "Hidden")
                si.WindowStyle = ProcessWindowStyle.Hidden;
            else if (windowStyle == "Maximized")
                si.WindowStyle = ProcessWindowStyle.Maximized;
            else if (windowStyle == "Minimized")
                si.WindowStyle = ProcessWindowStyle.Minimized;
            else
                si.WindowStyle = ProcessWindowStyle.Normal;

            Process process = null;
            while (true)
            {
                // ====================================== Process.Start ======================================
                process = Process.Start(si);
                if (process != null)
                {
                    process.EnableRaisingEvents = true;
                    process.Exited += (sender, e) =>
                    {
                        // =>=>=>=>=>=>=>=>=>=>=>=> Send Process Exited message =>=>=>=>=>=>=>=>=>=>=>=>
                        Console.WriteLine("Sending Process Exited message");
                        var cmsg = new XElement("Message",
                            new XElement("DaemonMachineName",
                                new XAttribute("Val", Environment.MachineName)),
                            new XElement("ProcessExited",
                                new XAttribute("ProcessId", process.Id)));
                        Runner.SendMessage("ProcessExited", cmsg, masterMachineName, OxRunConstants.ControllerMasterStatusQueueName);
                    };
                    break;
                }
                System.Threading.Thread.Sleep(100);
            }

            if (process == null)
            {
                // =>=>=>=>=>=>=>=>=>=>=>=> Send Process Null error message =>=>=>=>=>=>=>=>=>=>=>=>
                Console.WriteLine("Sending Process Null error message");
                var cmsg = new XElement("Message",
                    new XElement("DaemonMachineName",
                        new XAttribute("Val", Environment.MachineName)),
                    new XElement("Error",
                        new XAttribute("Val", "Process is null!!!???")));
                Runner.SendMessage("Error", cmsg, masterMachineName, OxRunConstants.ControllerMasterStatusQueueName);
            }
            else
            {
                // =>=>=>=>=>=>=>=>=>=>=>=> Send Process Started message =>=>=>=>=>=>=>=>=>=>=>=>
                Console.WriteLine("Sending Process Started message");
                var cmsg = new XElement("Message",
                    new XElement("DaemonMachineName",
                        new XAttribute("Val", Environment.MachineName)),
                    new XElement("ProcessStarted",
                        new XAttribute("ProcessId", process.Id)));
                Runner.SendMessage("ProcessStarted", cmsg, masterMachineName, OxRunConstants.ControllerMasterStatusQueueName);
            }
        }

        static void Init()
        {
            // ======================= INIT controller daemon Queue =======================
            // if controller daemon exists
            //     clear it
            // else
            //     create it
            var controllerDaemonQueueName = Runner.GetQueueName(Environment.MachineName, OxRunConstants.ControllerDaemonQueueName);
            m_ControllerDaemonQueue = null;
            if (MessageQueue.Exists(controllerDaemonQueueName))
            {
                m_ControllerDaemonQueue = new MessageQueue(controllerDaemonQueueName);
                Runner.ClearQueue(m_ControllerDaemonQueue);
            }
            else
            {
                m_ControllerDaemonQueue = MessageQueue.Create(controllerDaemonQueueName, false);
                Runner.ClearQueue(m_ControllerDaemonQueue);
            }
        }
    }
}
