using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace OxRunner
{
    class ControllerMaster
    {
        private static ProcessStartInfo RunRunnerMaster(FileInfo fiExe, string args)
        {
            // ================================================================
            // ================================================================
            // ======================= Run RunnerMaster =======================
            // ================================================================
            // ================================================================
            // To start the RunnerMaster in debug, set breakpoint here, then run RunnerMaster in VS with specified args.
            ProcessStartInfo si = new ProcessStartInfo(fiExe.FullName, args);
            return si;
        }

        static MessageQueue m_ControllerMasterQueue;
        static MessageQueue m_ControllerMasterStatusQueue;
        static MessageQueue m_ControllerMasterIsAliveQueue;
        static List<string> m_ActiveDaemons;
        static int m_WaitSeconds = 3;
        static List<ConsoleOutputLine> m_ConsoleOutput = new List<ConsoleOutputLine>();
        static FileInfo m_FiLog = null;

        static FileInfo m_FiConfig;
        static XDocument m_XdConfig;
        static string m_Editor = null;
        static bool? m_WriteLog = true;
        static bool? m_CollectProcessTimeMetrics = null;

        static XElement m_CurrentReport = null;
        static string m_CurrentReportName = null;
        static bool m_RunnerMasterComplete = false;

        static int m_ConsoleHeight = 40;
        static int m_ConsoleWidth = 100;
        static int m_LogWidth = 50;
        static int m_LabelWidth = 20;
        static int m_Col1Width = 12;
        static int m_Col2Width = 20;
        static int m_Col3Width = 12;

        static bool m_UpdatingConsole = false;

        static void Main(string[] args)
        {
            ConsolePosition.SetControllerMasterConsolePosition(m_ConsoleHeight, m_ConsoleWidth + 4);
            ReadControllerConfig();
            SetUpConsoleWindow();
            InitializeLog();
            InitializeQueues();
            SetUpEnvironmentVariablesForCompilation();
            GetActiveDaemons();
            StartStatusThread();
            GetAndRunCommand();
        }

        private static void ReadControllerConfig()
        {
            m_FiConfig = new FileInfo("../../../ControllerConfig.xml");
            m_XdConfig = XDocument.Load(m_FiConfig.FullName);
            m_Editor = (string)m_XdConfig.Root.Elements("Editor").Attributes("Val").FirstOrDefault();
            m_WriteLog = (bool?)m_XdConfig.Root.Elements("WriteLog").Attributes("Val").FirstOrDefault();
            m_CollectProcessTimeMetrics = (bool?)m_XdConfig.Root.Elements("CollectProcessTimeMetrics").Attributes("Val").FirstOrDefault();
        }

        private static void InitializeLog()
        {
            DateTime now = DateTime.Now;
            m_FiLog = new FileInfo(string.Format("../../../ControllerMaster-{0:00}-{1:00}-{2:00}-{3:00}{4:00}{5:00}.log",
                now.Year - 2000,
                now.Month,
                now.Day,
                now.Hour,
                now.Minute,
                now.Second));
            if (m_WriteLog == true)
                PrintToConsole(ConsoleColor.White, string.Format("Log: {0}", m_FiLog.FullName));
            UpdateConsole();
        }

        private static void SetUpConsoleWindow()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.SetCursorPosition(0, 0);
            Console.Write("ControllerMaster: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(m_FiConfig.FullName);

            Point p = new Point();
            p.X = 0;
            p.Y = 1;
            SplitRectangle cr = new SplitRectangle(m_ConsoleWidth, m_ConsoleHeight - 7, m_LogWidth, p, ConsoleColor.DarkGray);
            cr.Draw();
        }

        private class Point
        {
            public int X;
            public int Y;
        }

        private class SplitRectangle
        {
            private int hWidth;
            private int hHeight;
            private int hSplit;
            private Point hLocation;
            private ConsoleColor hBorderColor;

            public SplitRectangle(int width, int height, int split, Point location, ConsoleColor borderColor)
            {
                hWidth = width;
                hHeight = height;
                hSplit = split;
                hLocation = location;
                hBorderColor = borderColor;
            }

            public Point Location
            {
                get { return hLocation; }
                set { hLocation = value; }
            }

            public int Width
            {
                get { return hWidth; }
                set { hWidth = value; }
            }

            public int Hieght
            {
                get { return hHeight; }
                set { hHeight = value; }
            }

            public ConsoleColor BorderColor
            {
                get { return hBorderColor; }
                set { hBorderColor = value; }
            }

            public void Draw()
            {
                string s = "╔";
                string space = "";
                string temp = "";
                for (int i = 0; i < Width; i++)
                {
                    if (i == hSplit)
                        space += "║";
                    else
                        space += " ";
                    if (i == hSplit)
                        s += "╦";
                    else
                        s += "═";
                }

                for (int j = 0; j < Location.X; j++)
                    temp += " ";

                s += "╗" + "\n";

                for (int i = 0; i < Hieght; i++)
                    s += temp + "║" + space + "║" + "\n";

                s += temp + "╚";
                for (int i = 0; i < Width; i++)
                {
                    if (i == hSplit)
                        s += "╩";
                    else
                        s += "═";
                }

                s += "╝" + "\n";

                Console.ForegroundColor = BorderColor;
                Console.CursorTop = hLocation.Y;
                Console.CursorLeft = hLocation.X;
                Console.Write(s);
                Console.ResetColor();

            }
        }

        //                                    m_LogWidth         m_LabelWidth         m_ConsoleWidth
        // +----------------------------------------+---------------------------------------------+
        // |                                        |XfmTfs                                       |
        // |                                        |                                             |
        // |                                        |Start Time      : 12:42PM                    |
        // |                                        |Elapsed Time    : 0:36:23                    |
        // |                                        |Time Left       : 0:21:23                    |
        // |                                        |                                             |
        // |                                        |Total Items     : 32333                      |
        // |                                        |Completed Items : 12000                      |
        // |                                        |Items/Min       : 11323                      |
        // |                                        |                                             |
        // |                                        |         m_Col1Width        m_Col2Width   m_Col3Width
        // |                                        |             V                   V           V
        // |                                        |Computer     Completed Items     Items/Min   |
        // |                                        |pc17         1023                102.4       |
        // |                                        |mini-1       967                 88.6        |
        // |                                        |mini-2       988                 90.1        |
        // |                                        |                                             |
        // +----------------------------------------+---------------------------------------------+
        // |                                                                                      |
        // |                                                                                      |
        // |                                                                                      |
        // |                                                                                      |
        // +----------------------------------------+---------------------------------------------+
        //

        private class RunnerMetrics
        {
            public string RunnerName;
            public DateTime StartTime;
            public int TotalItems;
            public Dictionary<string, int> CompletedItems = new Dictionary<string, int>();
            public bool ClearMetricsWindow;
        }

        private static void StartStatusThread()
        {
            RunnerMetrics runnerMetrics = null;
            System.Threading.Thread thread = new System.Threading.Thread(() =>
            {
                while (true)
                {
                    // <=<=<=<=<=<=<=<=<=<=<=<= Receive message sent by Controller Daemon <=<=<=<=<=<=<=<=<=<=<=<=
                    var doMessage = Runner.ReceiveMessage(m_ControllerMasterStatusQueue, null);

                    var daemonMachineName = (string)doMessage.Xml.Elements("DaemonMachineName").Attributes("Val").FirstOrDefault();

                    //     <=<=<=<=<=<=<=<=<=<=<=<= Receive ProcessStarted sent by Controller Daemon <=<=<=<=<=<=<=<=<=<=<=<=
                    if (doMessage.Label == "ProcessStarted")
                    {
                        var s = string.Format("Received Process Started from {0}, ProcessId: {1}", daemonMachineName,
                            (string)doMessage.Xml.Elements("ProcessStarted").Attributes("ProcessId").FirstOrDefault());
                        PrintToConsole(ConsoleColor.White, s);
                    }
                    else if (doMessage.Label == "ProcessExited")
                    {
                        var s = string.Format("Received Process Exited from {0}, ProcessId: {1}", daemonMachineName,
                            (string)doMessage.Xml.Elements("ProcessExited").Attributes("ProcessId").FirstOrDefault());
                        PrintToConsole(ConsoleColor.White, s);
                    }
                    else if (doMessage.Label == "ProcessNull")
                    {
                        var s = string.Format("Received Process Null error from {0}", daemonMachineName);
                        PrintToConsole(ConsoleColor.White, s);
                    }
                    else if (doMessage.Label == "ProcessList")
                    {
                        foreach (var item in doMessage.Xml.Elements("Processes").Elements("Process"))
                        {
                            PrintToConsole(ConsoleColor.White, string.Format("ProcessList:  Daemon: {0}  Process Id: {1}", daemonMachineName, (string)item.Attribute("ProcessId")));
                        }
                    }
                    else if (doMessage.Label == "ReportStart")
                    {
                        PrintToConsole(ConsoleColor.White, "Received Report Start");
                        m_CurrentReportName = (string)doMessage.Xml.Elements("ReportName").Attributes("Val").FirstOrDefault();
                        if (m_CurrentReportName == null)
                            throw new Exception("What????");  // todo fix exception message
                        m_CurrentReport = new XElement("Report",
                            new XAttribute("Name", m_CurrentReportName),
                            new XElement("Documents"));
                        runnerMetrics = new RunnerMetrics();
                        runnerMetrics.RunnerName = m_CurrentReportName;
                        runnerMetrics.StartTime = DateTime.Now;
                        runnerMetrics.TotalItems = (int)doMessage.Xml.Elements("TotalItemsToProcess").Attributes("Val").FirstOrDefault();
                        runnerMetrics.ClearMetricsWindow = true;
                        UpdateMetrics(runnerMetrics, false);
                    }
                    else if (doMessage.Label == "Report")
                    {
                        var currentReportName = (string)doMessage.Xml.Elements("ReportName").Attributes("Val").FirstOrDefault();
                        var runnerDaemonMachineName = (string)doMessage.Xml.Elements("RunnerDaemonMachineName").Attributes("Val").FirstOrDefault();
                        var itemsProcessed = (int)doMessage.Xml.Elements("ItemsProcessed").Attributes("Val").FirstOrDefault();
                        if (runnerMetrics.CompletedItems.ContainsKey(runnerDaemonMachineName))
                            runnerMetrics.CompletedItems[runnerDaemonMachineName] = runnerMetrics.CompletedItems[runnerDaemonMachineName] + itemsProcessed;
                        else
                            runnerMetrics.CompletedItems[runnerDaemonMachineName] = itemsProcessed;
                        UpdateMetrics(runnerMetrics, false);
                        //PrintToConsole(ConsoleColor.White, string.Format("Received Report, {0} processed {1} items", runnerDaemonMachineName, itemsProcessed));
                        if (m_CurrentReportName != currentReportName)
                            throw new Exception("What????");  // todo fix exception message
                        m_CurrentReport.Element("Documents").Add(doMessage.Xml.Elements("Documents").Elements());
                    }
                    else if (doMessage.Label == "ReportComplete")
                    {
                        PrintToConsole(ConsoleColor.White, "Received Report Complete");
                        if (m_UpdatingConsole)
                            System.Threading.Thread.Sleep(200);
                        UpdateMetrics(runnerMetrics, true);
                        var reportFile = FileUtils.GetDateTimeStampedFileInfo("../../../" + m_CurrentReportName, ".log");
                        var elapsedTime = DateTime.Now - runnerMetrics.StartTime;
                        int itemsPerMinute = (int)(runnerMetrics.TotalItems / elapsedTime.TotalMinutes);

                        var anomalies = m_CurrentReport.Elements("Documents").Elements().Where(e =>
                        {
                            if (e.Attributes().Any(at => at.Name != "GuidName" && at.Name != "WorkingSetBefore" && at.Name != "WorkingSetAfter"))
                                return true;
                            if (e.HasElements)
                                return true;
                            return false;
                        });
                        var anomalyCount = anomalies.Count();
                        var sortedReport = new XElement("Report",
                            m_CurrentReport.Attributes(),
                            new XElement("Metrics",
                                new XElement("StartTime", new XAttribute("Val", runnerMetrics.StartTime.ToString("T"))),
                                new XElement("ElapsedTime", new XAttribute("Val", elapsedTime.ToString("c").Split('.')[0])),
                                new XElement("ItemsPerMinute", itemsPerMinute),
                                new XElement("AnomalyCount", anomalyCount),
                                new XElement("ErrorDocuments",
                                    anomalies)),
                            new XElement("Documents",
                                m_CurrentReport.Elements("Documents").Elements().OrderBy(d => (string)d.Attribute("GuidName"))));
                        sortedReport.Save(reportFile.FullName);
                        m_RunnerMasterComplete = true;
                        if (m_CollectProcessTimeMetrics == true)
                        {
                            var processTimeFileLines = m_CurrentReport.Elements("Documents").Elements("Document").Select(e =>
                                {
                                    string ptms = e.Attribute("GuidName").Value + "|" + e.Attribute("Ticks").Value;
                                    return ptms;
                                })
                                .ToArray();
                            var processTimeMetricsFile = FileUtils.GetDateTimeStampedFileInfo("../../../ProcessTimeMetrics", ".txt");
                            File.WriteAllLines(processTimeMetricsFile.FullName, processTimeFileLines);
                        }
                    }
                    UpdateConsole();
                }

            });
            thread.Start();
        }

        private static DateTime m_LastMetricsUpdate = DateTime.Now;

        private static void UpdateMetrics(RunnerMetrics runnerMetrics, bool lastUpdate)
        {
            if (!lastUpdate)
            {
                // only update every third second
                var now = DateTime.Now;
                if ((now - m_LastMetricsUpdate).TotalSeconds < .33)
                    return;
                m_LastMetricsUpdate = now;
            }

            if (!m_UpdatingConsole || lastUpdate)
            {
                int oldLeft = Console.CursorLeft;
                int oldTop = Console.CursorTop;

                int metricsWidth = m_ConsoleWidth - m_LogWidth - 1;
                int y = 2;
                int x = m_LogWidth + 2;

                if (runnerMetrics.ClearMetricsWindow)
                {
                    int mw = m_ConsoleWidth - m_LogWidth - 1;
                    string s2 = "".PadRight(mw, ' ');
                    int x2 = m_LogWidth + 2;
                    for (int y2 = 2; y2 <= m_ConsoleHeight - 6; ++y2)
                    {
                        Console.SetCursorPosition(x2, y2);
                        Console.Write(s2);
                    }
                    runnerMetrics.ClearMetricsWindow = false;
                }

                var elapsedTime = DateTime.Now - runnerMetrics.StartTime;
                int completedItems = runnerMetrics.CompletedItems
                    .Select(ci => ci.Value)
                    .Sum();
                int itemsPerMinute;
                if (completedItems == 0 || elapsedTime.TotalMinutes == 0)
                    itemsPerMinute = 0;
                else
                    itemsPerMinute = (int)(completedItems / elapsedTime.TotalMinutes);
                TimeSpan timeRemaining;
                if (itemsPerMinute != 0)
                {
                    int itemsRemaining = runnerMetrics.TotalItems - completedItems;
                    double minutesRemaining = (double)itemsRemaining / (double)itemsPerMinute;
                    int hours = (int)minutesRemaining / 60;
                    minutesRemaining = minutesRemaining - (hours * 60);
                    int minutes = (int)minutesRemaining;
                    int seconds = (int)((minutesRemaining - Math.Floor(minutesRemaining)) * 60);

                    timeRemaining = new TimeSpan(hours, minutes, seconds);
                }
                else
                {
                    timeRemaining = new TimeSpan(1, 0, 0);
                }

                string s = runnerMetrics.RunnerName.PadRight(metricsWidth);
                Console.SetCursorPosition(x, y);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(s);
                y += 2;

                UpdateMetric("Start Time", runnerMetrics.StartTime.ToString("T"), y);
                y += 1;

                UpdateMetric("Elapsed Time", elapsedTime.ToString("c").Split('.')[0], y);
                y += 1;

                UpdateMetric("Time Remaining", timeRemaining.ToString("c"), y);
                y += 2;

                UpdateMetric("Total Items", runnerMetrics.TotalItems.ToString(), y);
                y += 1;

                UpdateMetric("Completed Items", completedItems.ToString(), y);
                y += 1;

                UpdateMetric("Items / Min", itemsPerMinute.ToString(), y);
                y += 2;

                WriteTableLine("Computer", "Completed Items", "Items/Min", y, ConsoleColor.Gray);
                y += 1;

                foreach (var item in runnerMetrics.CompletedItems.OrderByDescending(z => z.Value))
                {
                    int i = (int)(item.Value / elapsedTime.TotalMinutes);
                    WriteTableLine(item.Key, item.Value.ToString(), i.ToString(), y, ConsoleColor.White);
                    y += 1;
                }

                Console.SetCursorPosition(oldLeft, oldTop);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static void WriteTableLine(string p1, string p2, string p3, int y, ConsoleColor consoleColor)
        {
            int x = m_LogWidth + 2;
            Console.SetCursorPosition(x, y);
            UpdateColumn(p1, m_Col1Width, consoleColor);
            UpdateColumn(p2, m_Col2Width, consoleColor);
            UpdateColumn(p3, m_Col3Width, consoleColor);
        }

        private static void UpdateColumn(string p, int width, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            var s = p.PadRight(width);
            Console.Write(s);
        }

        private static void UpdateMetric(string p1, string p2, int y)
        {
            int x = m_LogWidth + 2;

            int metricsWidth = m_ConsoleWidth - m_LogWidth - 1;
            int valueWidth = metricsWidth - m_LabelWidth - 2;

            string s = p1.PadRight(m_LabelWidth) + ": ";
            Console.SetCursorPosition(x, y);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(s);

            s = p2.PadRight(valueWidth);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(s);
        }

        private static void GetAndRunCommand()
        {
            while (true)
            {
                Console.SetCursorPosition(0, m_ConsoleHeight - 4);
                Console.Write("".PadRight(m_ConsoleWidth));

                Console.SetCursorPosition(0, m_ConsoleHeight - 4);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Command: ");
                var pCmd = GetCommand();
                if (pCmd.CommandType == CommandType.Exit)
                {
                    if (m_WriteLog == true)
                    {
                        PrintToConsole(ConsoleColor.Gray, "Log Written");
                        PrintToConsole(ConsoleColor.White, m_FiLog.FullName);
                    }
                    UpdateConsole();
                    WriteLog();
                    Environment.Exit(0);
                }
                DirectoryInfo di = new DirectoryInfo("../../../");
                bool found = false;
                foreach (var item in di.GetFiles("*.xml"))
                {
                    if (item.Name.ToLower().StartsWith(pCmd.Text.ToLower()))
                    {
                        RunCommand(item);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error, {0} does not exist.", pCmd.Text);
                }
            }
        }

        private static void WriteLog()
        {
            UpdateConsole();
            if (m_WriteLog == true)
            {
                var content = m_ConsoleOutput.Select(co => co.Text).ToArray();
                File.WriteAllLines(m_FiLog.FullName, content);
                if (m_Editor != null)
                {
                    ProcessStartInfo si = new ProcessStartInfo(m_Editor, m_FiLog.FullName);
                    si.WindowStyle = ProcessWindowStyle.Normal;

                    Process process = null;
                    while (true)
                    {
                        process = Process.Start(si);
                        if (process != null)
                            break;
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
        }

        private static void RunCommand(FileInfo fiCommand)
        {
            try
            {
                var xeCommand = XElement.Load(fiCommand.FullName);
                foreach (var command in xeCommand.Elements())
                {
                    if (RunOneCommand(command) == false)
                        break;
                }
            }
            catch (XmlException xe)
            {
                PrintToConsole(ConsoleColor.Red, "XmlException");
                var s = xe.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None);
                foreach (var item in s)
                {
                    PrintToConsole(ConsoleColor.Red, item);
                }
                PrintToConsole(ConsoleColor.Gray, "Log Written");
                PrintToConsole(ConsoleColor.White, m_FiLog.FullName);
                UpdateConsole();
                WriteLog();
                Environment.Exit(0);
            }
        }

        private static bool RunOneCommand(XElement command)
        {
            if (command.Name.LocalName == "BuildMultipleExes")
            {
                var projectPath = new DirectoryInfo((string)command.Attribute("ProjectPath"));
                var exeName = (string)command.Attribute("ExeName");
                if (!BuildMultipleExes(projectPath, exeName))
                    return false;
                return true;
            }

            if (command.Name.LocalName == "If")
            {
                var ifMachineName = (string)command.Attribute("MachineName");
                if (ifMachineName.ToLower() == Environment.MachineName.ToLower())
                {
                    foreach (var subCommand in command.Elements())
                    {
                        var retValue = RunOneCommand(subCommand);
                        if (retValue == false)
                            return false;
                    }
                    return true;
                }
                return true;
            }

            if (command.Name.LocalName == "WaitForRunnerMaster")
            {
                while (true)
                {
                    if (m_RunnerMasterComplete)
                    {
                        m_RunnerMasterComplete = false;
                        return true;
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            }

            if (command.Name.LocalName == "RunnerMaster")
            {
                var projectPath = new DirectoryInfo((string)command.Attribute("ProjectPath"));
                var diExeLocation = new DirectoryInfo(Path.Combine(projectPath.FullName, "bin/debug/"));
                var exeName = (string)command.Attribute("ExeName");
                var fiExe = new FileInfo(Path.Combine(projectPath.FullName, "bin/debug/", exeName));

                try
                {
                    if (fiExe.Exists)
                        fiExe.Delete();
                }
                catch (Exception)
                {
                    PrintToConsole(ConsoleColor.Red, string.Format("Can't delete RunnerMaster exe: ", fiExe.Name));
                    PrintToConsole(ConsoleColor.Red, "Is it still running?");
                    UpdateConsole();
                    return false;
                }

                // ======================= Compile RunnerMaster =======================
                var results = VSTools.RunMSBuild(projectPath);
                if (results.ExitCode == 0)
                {
                    PrintToConsole(ConsoleColor.Gray, string.Format("Build successful for {0}", fiExe.Name));
                    UpdateConsole();
                }
                else
                {
                    PrintToConsole(ConsoleColor.Red, string.Format("Build for {0} failed", fiExe.Name));
                    var sa = results.Output.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in sa)
                        PrintToConsole(ConsoleColor.Red, item);
                    UpdateConsole();
                    return false;
                }

                // first argument from controller master to RunnerMaster is the number of client computers.
                // other arguments are appended to the first arg
                var args = m_ActiveDaemons.Count().ToString();
                var otherArgs = (string)command.Attribute("Args");
                if (otherArgs != null)
                    args = args + " " + otherArgs + " ";

                var skip = (string)command.Attribute("Skip");
                if (skip == null || skip.ToLower() == "null")
                    args += "null ";
                else
                    args = args + skip + " ";

                var take = (string)command.Attribute("Take");
                if (take == null || take.ToLower() == "null")
                    args += "null ";
                else
                    args = args + take + " ";

                var specificFile = (string)command.Attribute("SpecificFile");
                if (specificFile == null || specificFile.ToLower() == "null")
                    args += "null ";
                else
                    args = args + specificFile + " ";

                var workingDir = (string)command.Attribute("WorkingDirectory");

                if (fiExe.Exists)
                {
                    ProcessStartInfo si = RunRunnerMaster(fiExe, args);
                    si.WindowStyle = ProcessWindowStyle.Normal;
                    while (true)
                    {
                        Process proc = Process.Start(si);
                        if (proc == null)
                            System.Threading.Thread.Sleep(100);
                        else
                            break;
                    }

                    bool receivedPong = false;
                    for (int i = 0; i < 40; i++)
                    {
                        // Send Ping and receive Pong to make sure that RunnerMaster is alive.

                        // =>=>=>=>=>=>=>=>=>=>=>=> Send Ping =>=>=>=>=>=>=>=>=>=>=>=>
                        PrintToConsole(ConsoleColor.White, "Sending Ping to RunnerMaster");
                        UpdateConsole();

                        var cmsg = new XElement("Message",
                            new XElement("MasterMachineName",
                                new XAttribute("Val", Environment.MachineName)));

                        bool sentMessage = false;
                        for (int j = 0; j < 10; j++)
                        {
                            try
                            {
                                Runner.SendMessage("Ping", cmsg, Environment.MachineName, OxRunConstants.RunnerMasterIsAliveQueueName);
                                sentMessage = true;
                                break;
                            }
                            catch (MessageQueueException)
                            {
                                // it may not have been created yet
                                // wait one second, then try again
                                System.Threading.Thread.Sleep(1000);
                            }
                        }

                        if (sentMessage)
                        {
                            // <=<=<=<=<=<=<=<=<=<=<=<= Receive message sent by RunnerMaster <=<=<=<=<=<=<=<=<=<=<=<=
                            OxMessage pongMessage;

                            pongMessage = Runner.ReceiveMessage(m_ControllerMasterIsAliveQueue, 5);
                            if (pongMessage.Timeout)
                            {
                                PrintToConsole(ConsoleColor.Red, "Did not receive Pong message from RunnerMaster, try again");
                                continue;
                            }

                            //     <=<=<=<=<=<=<=<=<=<=<=<= Receive Pong sent by RunnerMaster <=<=<=<=<=<=<=<=<=<=<=<=
                            if (pongMessage.Label == "Pong")
                            {
                                PrintToConsole(ConsoleColor.White, "Received Pong from RunnerMaster");
                                UpdateConsole();

                                receivedPong = true;
                                return true;
                            }
                            else
                            {
                                throw new Exception("ControllerMaster: Internal error, received other than Pong message"); // todo
                            }
                        }
                    }
                    if (!receivedPong)
                    {
                        throw new Exception("ControllerMaster: Did not receive Pong from RunnerMaster"); // todo
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid executable path.", "exePath"); // todo
                }
                return true;
            }

            command.Add(new XElement("MasterMachineName",
                new XAttribute("Val", Environment.MachineName)));

            if (command.Name.LocalName == "ListProcesses")
            {
                PrintToConsole(ConsoleColor.Gray, "");
                PrintToConsole(ConsoleColor.Gray, "List Processes");
                PrintToConsole(ConsoleColor.Gray, "==============");
            }

            foreach (var daemonMachineName in m_ActiveDaemons)
            {
                // =>=>=>=>=>=>=>=>=>=>=>=> Send Do =>=>=>=>=>=>=>=>=>=>=>=>
                PrintToConsole(ConsoleColor.White, string.Format("Sending Do {0} to {1}", command.Name.LocalName, daemonMachineName));
                Runner.SendMessage("Do", command, daemonMachineName, OxRunConstants.ControllerDaemonQueueName);
            }

            UpdateConsole();
            return true;
        }

        private static bool BuildMultipleExes(DirectoryInfo projectPath, string exeName)
        {
            var msBuildPath = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe";

            var diExeLocation = new DirectoryInfo(Path.Combine(projectPath.FullName, "bin/debug/"));

            for (int count = 0; count < OxRunConstants.RunnerDaemonProcessesPerClient; count++)
            {
                var fiExe = new FileInfo(Path.Combine(projectPath.FullName, "bin/debug/", exeName));
                var fiExeBase = fiExe.Name.Substring(0, fiExe.Name.Length - fiExe.Extension.Length);
                var newExecutableName = new FileInfo(Path.Combine(projectPath.FullName, "bin/debug/", fiExeBase + string.Format("{0:00}", count + 1) + ".exe"));
                try
                {
                    if (newExecutableName.Exists)
                        newExecutableName.Delete();
                }
                catch (UnauthorizedAccessException)
                {
                    PrintToConsole(ConsoleColor.Red, "UnauthorizedAccessException attempting to delete RunnerDaemon executables before building them.  Are they still running?");
                    UpdateConsole();
                    WriteLog();
                    Console.ReadKey();
                    Environment.Exit(0);

                }

                UpdateAssemblyInfoVersion(projectPath, count + 1);

                var results = ExecutableRunner.RunExecutable(msBuildPath, "", projectPath.FullName + " /t:clean");
                if (results.ExitCode == 0)
                {
                    PrintToConsole(ConsoleColor.Gray, string.Format("Clean successful for {0}", newExecutableName.Name));
                    UpdateConsole();
                }
                else
                {
                    PrintToConsole(ConsoleColor.Red, string.Format("Clean for {0} failed", newExecutableName.Name));
                    var sa = results.Output.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in sa)
                    {
                        PrintToConsole(ConsoleColor.Red, item);
                    }
                    UpdateConsole();
                    return false;
                }

                results = ExecutableRunner.RunExecutable(msBuildPath, "", projectPath.FullName);
                if (results.ExitCode == 0)
                {
                    PrintToConsole(ConsoleColor.Gray, string.Format("Build successful for {0}", newExecutableName.Name));
                    if (newExecutableName.Exists)
                    {
                        for (int i1 = 1; i1 <= 10; ++i1)
                        {
                            try
                            {
                                newExecutableName.Delete();
                                break;
                            }
                            catch (UnauthorizedAccessException)
                            {
                                System.Threading.Thread.Sleep(2000);
                                continue;
                            }
                        }
                    }
                    File.Move(fiExe.FullName, newExecutableName.FullName);
                    UpdateConsole();
                }
                else
                {
                    PrintToConsole(ConsoleColor.Red, string.Format("Build for {0} failed", newExecutableName.Name));
                    var sa = results.Output.ToString().Split(new [] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in sa)
                    {
                        PrintToConsole(ConsoleColor.Red, item);
                    }
                    UpdateConsole();

                    UpdateAssemblyInfoVersion(projectPath, 0);

                    return false;
                }
            }
            UpdateAssemblyInfoVersion(projectPath, 0);
            return true;
        }

        private static void SetUpEnvironmentVariablesForCompilation()
        {
            PrintToConsole(ConsoleColor.Yellow, "Setting environment variables");
            PrintToConsole(ConsoleColor.Gray, "DevEnvDir, ExtensionSdkDir, Framework35Version, FrameworkDir, FrameworkDIR32, FrameworkVersion, FrameworkVersion32,");
            PrintToConsole(ConsoleColor.Gray, "INCLUDE, LIB, LIBPATH, mentVariables[, Path, VCINSTALLDIR, VisualStudioVersion, VS110COMNTOOLS, VS120COMNTOOLS,");
            PrintToConsole(ConsoleColor.Gray, "VSINSTALLDIR, WindowsSdkDir, WindowsSdkDir_35, WindowsSdkDir_old");
            PrintToConsole(ConsoleColor.Gray, "");
            UpdateConsole();
            VSTools.SetUpVSEnvironmentVariables();
        }

        private static void UpdateAssemblyInfoVersion(DirectoryInfo projectPath, int minorVersion)
        {
            var fiAssemblyInfo = new FileInfo(Path.Combine(projectPath.FullName, "Properties/AssemblyInfo.cs"));
            var assemblyInfoText = File.ReadAllText(fiAssemblyInfo.FullName);
            var newVersion = string.Format("1.0.0.{0}", minorVersion);
            Regex regex = new Regex(@"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+");
            var newAssemblyInfoText = regex.Replace(assemblyInfoText, newVersion);
            File.WriteAllText(fiAssemblyInfo.FullName, newAssemblyInfoText);
        }

        private static Command GetCommand()
        {
            Console.SetCursorPosition("Command: ".Length, m_ConsoleHeight - 4);

            string cmd = Console.ReadLine();
            Command command = new Command();
            command.Text = cmd;
            if (cmd == "-")
                command.CommandType = CommandType.Exit;
            else
                command.CommandType = CommandType.String;
            return command;
        }

        private static void GetActiveDaemons()
        {
            PrintToConsole(ConsoleColor.Gray, "");
            PrintToConsole(ConsoleColor.Gray, "Getting Active Daemons");
            PrintToConsole(ConsoleColor.Gray, "======================");

            foreach (var computer in m_XdConfig.Root.Elements("Computers").Elements("Computer"))
            {
                var daemonMachineName = (string)computer.Attribute("Name");

                // =>=>=>=>=>=>=>=>=>=>=>=> Send Ping =>=>=>=>=>=>=>=>=>=>=>=>
                PrintToConsole(ConsoleColor.White, string.Format("Sending Ping to {0}", daemonMachineName));

                var cmsg = new XElement("Message",
                    new XElement("MasterMachineName",
                        new XAttribute("Val", Environment.MachineName)));
                Runner.SendMessage("Ping", cmsg, daemonMachineName, OxRunConstants.ControllerDaemonQueueName);
            }

            UpdateConsole();

            m_ActiveDaemons = new List<string>();
            while (true)
            {
                PrintToConsole(ConsoleColor.White, "Waiting for Pong messages");
                UpdateConsole();

                // <=<=<=<=<=<=<=<=<=<=<=<= Receive message sent by Daemons <=<=<=<=<=<=<=<=<=<=<=<=
                OxMessage pongMessage;

                pongMessage = Runner.ReceiveMessage(m_ControllerMasterQueue, m_WaitSeconds);
                if (pongMessage.Timeout)
                    break;

                var masterMachineName = (string)pongMessage.Xml.Elements("MasterMachineName").Attributes("Val").FirstOrDefault();

                //     <=<=<=<=<=<=<=<=<=<=<=<= Receive Pong sent by Daemon <=<=<=<=<=<=<=<=<=<=<=<=
                if (pongMessage.Label == "Pong")
                {
                    var daemonMachineName = (string)pongMessage.Xml.Elements("DaemonMachineName").Attributes("Val").FirstOrDefault();
                    var s = string.Format("Received Pong from {0}", daemonMachineName);
                    PrintToConsole(ConsoleColor.White, s);
                    UpdateConsole();
                    m_ActiveDaemons.Add(daemonMachineName);
                }
            }
            PrintToConsole(ConsoleColor.Gray, "");
            string activeDaemons = string.Format("Active Daemons ({0})", m_ActiveDaemons.Count());
            PrintToConsole(ConsoleColor.Gray, activeDaemons);
            PrintToConsole(ConsoleColor.Gray, "".PadRight(activeDaemons.Length, '='));
            foreach (var item in m_ActiveDaemons)
            {
                PrintToConsole(ConsoleColor.White, item);
            }
            PrintToConsole(ConsoleColor.Gray, "");
            UpdateConsole();
        }

        private static DateTime m_LastConsoleUpdate = DateTime.Now;

        private static void UpdateConsole()
        {
            // only update every 1/5 second
            var now = DateTime.Now;
            if ((now - m_LastConsoleUpdate).TotalSeconds < .2)
                return;
            m_LastConsoleUpdate = now;

            if (!m_UpdatingConsole)
            {
                m_UpdatingConsole = true;
                int oldLeft = Console.CursorLeft;
                int oldTop = Console.CursorTop;

                int row = 2;
                foreach (var item in m_ConsoleOutput.Reverse<ConsoleOutputLine>().Take(m_ConsoleHeight - 7).Reverse())
                {
                    Console.SetCursorPosition(2, row++);
                    Console.ForegroundColor = item.Color;
                    var textToWrite = item.Text;
                    if (textToWrite.Length >= m_LogWidth - 2)
                        textToWrite = textToWrite.Substring(0, m_LogWidth - 2);
                    Console.Write(textToWrite.PadRight(m_LogWidth - 2));
                }

                Console.SetCursorPosition(oldLeft, oldTop);
                m_UpdatingConsole = false;
            }
        }

        private static void PrintToConsole(ConsoleColor color, string text)
        {
            ConsoleOutputLine col = new ConsoleOutputLine();
            col.Color = color;
            col.Text = text;
            m_ConsoleOutput.Add(col);
        }

        static void InitializeQueues()
        {
            // ======================= INIT controller master Queue =======================
            // if controller master exists
            //     clear it
            // else
            //     create it
            var controllerMasterQueueName = Runner.GetQueueName(Environment.MachineName, OxRunConstants.ControllerMasterQueueName);
            m_ControllerMasterQueue = null;
            if (MessageQueue.Exists(controllerMasterQueueName))
            {
                PrintToConsole(ConsoleColor.White, string.Format("Clearing {0}", controllerMasterQueueName));
                m_ControllerMasterQueue = new MessageQueue(controllerMasterQueueName);
                Runner.ClearQueue(m_ControllerMasterQueue);
            }
            else
            {
                PrintToConsole(ConsoleColor.White, string.Format("Creating {0}", controllerMasterQueueName));
                m_ControllerMasterQueue = MessageQueue.Create(controllerMasterQueueName, false);
                Runner.ClearQueue(m_ControllerMasterQueue);
            }

            // ======================= INIT controller masterstatus Queue =======================
            // if controller masterstatus exists
            //     clear it
            // else
            //     create it
            var controllerMasterStatusQueueName = Runner.GetQueueName(Environment.MachineName, OxRunConstants.ControllerMasterStatusQueueName);
            m_ControllerMasterStatusQueue = null;
            if (MessageQueue.Exists(controllerMasterStatusQueueName))
            {
                PrintToConsole(ConsoleColor.White, string.Format("Clearing {0}", controllerMasterStatusQueueName));
                m_ControllerMasterStatusQueue = new MessageQueue(controllerMasterStatusQueueName);
                Runner.ClearQueue(m_ControllerMasterStatusQueue);
            }
            else
            {
                PrintToConsole(ConsoleColor.White, string.Format("Creating {0}", controllerMasterStatusQueueName));
                m_ControllerMasterStatusQueue = MessageQueue.Create(controllerMasterStatusQueueName, false);
                Runner.ClearQueue(m_ControllerMasterStatusQueue);
            }

            // ======================= INIT controller master IsAlive Queue =======================
            var controllerMasterIsAliveQueueName = Runner.GetQueueName(Environment.MachineName, OxRunConstants.ControllerMasterIsAliveQueueName);
            m_ControllerMasterIsAliveQueue = null;
            if (MessageQueue.Exists(controllerMasterIsAliveQueueName))
            {
                PrintToConsole(ConsoleColor.White, string.Format("Clearing {0}", controllerMasterIsAliveQueueName));
                m_ControllerMasterIsAliveQueue = new MessageQueue(controllerMasterIsAliveQueueName);
                Runner.ClearQueue(m_ControllerMasterIsAliveQueue);
            }
            else
            {
                PrintToConsole(ConsoleColor.White, string.Format("Creating {0}", controllerMasterIsAliveQueueName));
                m_ControllerMasterIsAliveQueue = MessageQueue.Create(controllerMasterIsAliveQueueName, false);
                Runner.ClearQueue(m_ControllerMasterIsAliveQueue);
            }
        }
    }

    enum CommandType
    {
        Exit,
        String,
    }

    class Command
    {
        public CommandType CommandType;
        public string Text;
    }

    class ConsoleOutputLine
    {
        public ConsoleColor Color;
        public string Text;
    }
}
