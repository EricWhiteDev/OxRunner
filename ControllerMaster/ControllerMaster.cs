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

namespace OxRun
{
    class ControllerMaster
    {
        static MessageQueue m_ControllerMasterQueue;
        static MessageQueue m_ControllerMasterStatusQueue;
        static MessageQueue m_ControllerMasterIsAliveQueue;
        static FileInfo m_FiConfig;
        static XDocument m_XdConfig;
        static List<string> m_ActiveDaemons;
        static int m_WaitSeconds = 1;
        static List<ConsoleOutputLine> m_ConsoleOutput = new List<ConsoleOutputLine>();
        static FileInfo m_FiLog = null;
        static string m_Editor = null;

        static XElement m_CurrentReport = null;
        static string m_CurrentReportName = null;

        static int m_ConsoleHeight = 40;
        static int m_ConsoleWidth = 80;
        static int m_LogWidth = 40;

        //                                    m_LogWidth                              m_ConsoleWidth
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

        static void Main(string[] args)
        {
            ConsolePosition.SetControllerMasterConsolePosition(m_ConsoleHeight, m_ConsoleWidth);
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
        }

        private static void StartStatusThread()
        {
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
                    }
                    else if (doMessage.Label == "Report")
                    {
                        PrintToConsole(ConsoleColor.White, "Received Report");
                        var currentReportName = (string)doMessage.Xml.Elements("ReportName").Attributes("Val").FirstOrDefault();
                        if (m_CurrentReportName != currentReportName)
                            throw new Exception("What????");  // todo fix exception message
                        m_CurrentReport.Element("Documents").Add(doMessage.Xml.Elements("Documents").Elements());
                    }
                    else if (doMessage.Label == "ReportComplete")
                    {
                        PrintToConsole(ConsoleColor.White, "Received Report Complete");
                        var reportFile = FileUtils.GetDateTimeStampedFileInfo("../../../m_CurrentReportName, ".xml");
                        var sortedReport = new XElement("Report",
                            m_CurrentReport.Attributes(),
                            new XElement("Documents",
                                m_CurrentReport.Elements("Documents").Elements().OrderBy(d => (string)d.Attribute("GuidName"))));
                        sortedReport.Save(reportFile.FullName);
                    }

                    UpdateConsole();
                    Console.SetCursorPosition("Command: ".Length, m_ConsoleHeight - 4);
                }

            });
            thread.Start();
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
                    PrintToConsole(ConsoleColor.Gray, "Log Written");
                    PrintToConsole(ConsoleColor.White, m_FiLog.FullName);
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

            if (command.Name.LocalName == "RunnerMaster")
            {
                var msBuildPath = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe";

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

                var results = ExecutableRunner.RunExecutable(msBuildPath, "", projectPath.FullName);
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
                    args = args + " " + otherArgs;
                var workingDir = (string)command.Attribute("WorkingDirectory");

                if (fiExe.Exists)
                {
                    ProcessStartInfo si = new ProcessStartInfo(fiExe.FullName, args);
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
                    for (int i = 0; i < 20; i++)
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

                            pongMessage = Runner.ReceiveMessage(m_ControllerMasterIsAliveQueue, 1);
                            if (pongMessage.Timeout)
                            {
                                PrintToConsole(ConsoleColor.Red, "Did not receive Pong message from RunnerMaster, try again");
                                continue;
                            }

                            //     <=<=<=<=<=<=<=<=<=<=<=<= Receive Pong sent by RunnerMaster <=<=<=<=<=<=<=<=<=<=<=<=
                            if (pongMessage.Label == "Pong")
                            {
                                PrintToConsole(ConsoleColor.White, "Received Pong from RunnerMaster");
                                receivedPong = true;
                                UpdateConsole();
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
                    Environment.Exit(0);

                }

                UpdateAssemblyInfoVersion(projectPath, count + 1);

                var results = ExecutableRunner.RunExecutable(msBuildPath, "", projectPath.FullName);
                if (results.ExitCode == 0)
                {
                    PrintToConsole(ConsoleColor.Gray, string.Format("Build successful for {0}", newExecutableName.Name));
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
            PrintToConsole(ConsoleColor.Blue, "Setting environment variables");
            PrintToConsole(ConsoleColor.Gray, "DevEnvDir, ExtensionSdkDir, Framework35Version, FrameworkDir, FrameworkDIR32, FrameworkVersion, FrameworkVersion32,");
            PrintToConsole(ConsoleColor.Gray, "INCLUDE, LIB, LIBPATH, mentVariables[, Path, VCINSTALLDIR, VisualStudioVersion, VS110COMNTOOLS, VS120COMNTOOLS,");
            PrintToConsole(ConsoleColor.Gray, "VSINSTALLDIR, WindowsSdkDir, WindowsSdkDir_35, WindowsSdkDir_old");
            PrintToConsole(ConsoleColor.Gray, "");
            UpdateConsole();

            SetEnvironmentVariableIfNecessary("DevEnvDir", @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\");
            SetEnvironmentVariableIfNecessary("ExtensionSdkDir", @"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0\ExtensionSDKs");
            SetEnvironmentVariableIfNecessary("Framework35Version", @"v3.5");
            SetEnvironmentVariableIfNecessary("FrameworkDir", @"C:\Windows\Microsoft.NET\Framework\");
            SetEnvironmentVariableIfNecessary("FrameworkDIR32", @"C:\Windows\Microsoft.NET\Framework\");
            SetEnvironmentVariableIfNecessary("FrameworkVersion", @"v4.0.30319");
            SetEnvironmentVariableIfNecessary("FrameworkVersion32", @"v4.0.30319");
            SetEnvironmentVariableIfNecessary("INCLUDE", @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\INCLUDE;C:\Program Files (x86)\Windows Kits\8.0\include\shared;C:\Program Files (x86)\Windows Kits\8.0\include\um;C:\Program Files (x86)\Windows Kits\8.0\include\winrt;");
            SetEnvironmentVariableIfNecessary("LIB", @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\LIB;C:\Program Files (x86)\Windows Kits\8.0\lib\win8\um\x86;");
            SetEnvironmentVariableIfNecessary("LIBPATH", @"C:\Windows\Microsoft.NET\Framework\v4.0.30319;C:\Windows\Microsoft.NET\Framework\v3.5;C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\LIB;C:\Program Files (x86)\Windows Kits\8.0\References\CommonConfiguration\Neutral;C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0\ExtensionSDKs\Microsoft.VCLibs\11.0\References\CommonConfiguration\neutral;");
            SetEnvironmentVariableIfNecessary("VCINSTALLDIR", @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\");
            SetEnvironmentVariableIfNecessary("VisualStudioVersion", @"11.0");
            SetEnvironmentVariableIfNecessary("VS110COMNTOOLS", @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\Tools\");
            SetEnvironmentVariableIfNecessary("VS120COMNTOOLS", @"C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\Tools\");
            SetEnvironmentVariableIfNecessary("VSINSTALLDIR", @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\");
            SetEnvironmentVariableIfNecessary("WindowsSdkDir", @"C:\Program Files (x86)\Windows Kits\8.0\");
            SetEnvironmentVariableIfNecessary("WindowsSdkDir_35", @"C:\Program Files (x86)\Microsoft SDKs\Windows\v7.0A\Bin\");
            SetEnvironmentVariableIfNecessary("WindowsSdkDir_old", @"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0A\");

            string[] pathsToAdd = new[] {
                    @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\",
                    @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\BIN",
                    @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\Tools",
                    @"C:\Windows\Microsoft.NET\Framework\v4.0.30319",
                    @"C:\Windows\Microsoft.NET\Framework\v3.5",
                    @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\VCPackages",
                    @"C:\ProgramFiles (x86)\Windows Kits\8.0\bin\x86",
                    @"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0A\bin\NETFX 4.0 Tools",
                    @"C:\Program Files (x86)\Microsoft SDKs\Windows\v7.0A\Bin\",
                };

            foreach (var pathToAdd in pathsToAdd)
            {
                var existingPath = Environment.GetEnvironmentVariable("Path");
                if (existingPath.Contains(pathToAdd))
                    continue;
                var path = pathToAdd + ";" + existingPath;
                Environment.SetEnvironmentVariable("Path", path);
            }
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

        private static string SetEnvironmentVariableIfNecessary(string environmentVariableName, string value)
        {
            string vv;
            vv = Environment.GetEnvironmentVariable(environmentVariableName);
            if (vv == null)
                Environment.SetEnvironmentVariable(environmentVariableName, value);
            return vv;
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
            PrintToConsole(ConsoleColor.Gray, "Active Daemons");
            PrintToConsole(ConsoleColor.Gray, "==============");
            foreach (var item in m_ActiveDaemons)
            {
                PrintToConsole(ConsoleColor.White, item);
            }
            PrintToConsole(ConsoleColor.Gray, "");
            UpdateConsole();
        }

        private static void UpdateConsole()
        {
            int row = 1;
            foreach (var item in m_ConsoleOutput.Reverse<ConsoleOutputLine>().Take(m_ConsoleHeight - 5).Reverse())
            {
                Console.SetCursorPosition(0, row++);
                Console.ForegroundColor = item.Color;
                var textToWrite = item.Text;
                if (textToWrite.Length >= m_ConsoleWidth - 1)
                    textToWrite = textToWrite.Substring(0, m_ConsoleWidth - 1);
                Console.Write(textToWrite.PadRight(m_ConsoleWidth));
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
