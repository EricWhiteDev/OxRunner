using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using OpenXmlPowerTools;
using OxRunner;
using DocumentFormat.OpenXml.Validation;
using System.Globalization;
using Word = Microsoft.Office.Interop.Word;

namespace OxRunner
{
    class RunnerDaemonRevisionProcessor : RunnerDaemon
    {
        // NOTE: when changing the name of the class, must change the following line to get the version properly.
        static System.Version m_RunnerAssemblyVersion = typeof(RunnerDaemonRevisionProcessor).Assembly.GetName().Version;
        static string m_RepoLocation = null;
        static Repo m_Repo = null;

        static void Main(string[] args)
        {
            if (args.Length == 0)
                throw new ArgumentException("ControllerDaemon did not pass any arguments to RunnerDaemon");

            string runnerMasterMachineName = args[0];

            Console.ForegroundColor = ConsoleColor.White;
            ConsolePosition.SetConsolePosition(m_RunnerAssemblyVersion.MinorRevision - 1,
                Environment.MachineName.ToLower() == runnerMasterMachineName.ToLower());

            // MinorRevision will be unique for each daemon, so we append it for the daemon queue name.
            var runnerDaemon = new RunnerDaemonRevisionProcessor(runnerMasterMachineName, m_RunnerAssemblyVersion.MinorRevision);
            if (runnerDaemon.m_RunnerLog.m_FiLog != null)
                runnerDaemon.PrintToConsole(ConsoleColor.White, string.Format("Log: {0}", runnerDaemon.m_RunnerLog.m_FiLog.FullName));
            runnerDaemon.PrintToConsole(string.Format("MasterRunner machine name: {0}", runnerMasterMachineName));
            runnerDaemon.PrintToConsole(string.Format("Daemon Number: {0}", m_RunnerAssemblyVersion.MinorRevision));

            runnerDaemon.SendDaemonReadyMessage();
            runnerDaemon.MessageLoop();
        }

        private class RunnerDaemonThreadData
        {
            public MetricsGetterSettings MetricsGetterSettings;
            public string GuidName;
            public FileInfo FiRepoItem;
            public bool? CollectProcessTimeMetrics;
            public int? TimeoutInMiliseconds;
            public XElement ReturnValue;
            public DateTime PrevTime;
            public DirectoryInfo TestRunOutputDi;
        }

        private void MessageLoop()
        {
            MetricsGetterSettings metricsGetterSettings = new MetricsGetterSettings();
            metricsGetterSettings.IncludeTextInContentControls = false;
            metricsGetterSettings.IncludeXlsxTableCellData = false;
            metricsGetterSettings.RetrieveContentTypeList = true;
            metricsGetterSettings.RetrieveNamespaceList = true;

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
                    bool? collectProcessTimeMetrics = (bool?)doMessage.Xml.Elements("CollectProcessTimeMetrics").Attributes("Val").FirstOrDefault();
                    var testRunOutputDirName = (string)doMessage.Xml.Elements("TestRunOutputDi").Attributes("Val").FirstOrDefault();
                    var testRunOutputDi = new DirectoryInfo(testRunOutputDirName);
                    InitRepoIfNecessary(repoLocation);
                    PrintToConsole(string.Format("Repo: {0}", repoLocation.FullName));

                    // =>=>=>=>=>=>=>=>=>=>=>=> Send Work Complete =>=>=>=>=>=>=>=>=>=>=>=>
                    PrintToConsole("Sending WorkComplete to RunnerMaster");

                    DateTime prevTime = DateTime.Now;

                    var cmsg = new XElement("Message",
                        new XElement("RunnerDaemonMachineName",
                            new XAttribute("Val", Environment.MachineName)),
                        new XElement("RunnerDaemonQueueName",
                            new XAttribute("Val", m_RunnerDaemonLocalQueueName)),
                        new XElement("Documents",
                            doMessage.Xml.Element("Documents").Elements("Document").Select(d =>
                            {
                                var guidName = d.Attribute("GuidName").Value;
                                RepoItem ri = m_Repo.GetRepoItem(guidName);
                                PrintToConsole(guidName);
                                if (Util.IsSpreadsheetML(ri.FiRepoItem.Extension) && ri.FiRepoItem.Length > 2000000)
                                {
                                    var errorXml = new XElement("Document",
                                        new XAttribute("GuidName", guidName),
                                        new XElement("XlsxFileTooBigSkipping"));
                                    if (collectProcessTimeMetrics == true)
                                    {
                                        DateTime currentTime = DateTime.Now;
                                        var ticks = (currentTime - prevTime).Ticks;
                                        errorXml.Add(new XAttribute("Ticks", ticks));
                                        prevTime = currentTime;
                                    }
                                    return errorXml;
                                }

                                int? timeoutInMiliseconds = 60000;    //   <================================================================================================================================= parameterize this

                                RunnerDaemonThreadData rdts = new RunnerDaemonThreadData()
                                {
                                    MetricsGetterSettings = metricsGetterSettings,
                                    CollectProcessTimeMetrics = collectProcessTimeMetrics,
                                    GuidName = guidName,
                                    PrevTime = DateTime.Now,
                                    FiRepoItem = ri.FiRepoItem,
                                    TimeoutInMiliseconds = timeoutInMiliseconds,
                                    ReturnValue = null,
                                    TestRunOutputDi = testRunOutputDi,
                                };
                                System.Threading.ParameterizedThreadStart tcd = new System.Threading.ParameterizedThreadStart(RunnerDaemonCode);
                                System.Threading.Thread thread = new System.Threading.Thread(tcd);
                                System.Timers.Timer t = null;
                                if (timeoutInMiliseconds != null)
                                {
                                    t = new System.Timers.Timer((int)timeoutInMiliseconds);
                                    t.Elapsed += (source, eventArgs) =>
                                    {
                                        t.Enabled = false;
                                        thread.Abort();
                                    };
                                    t.Enabled = true;
                                }
                                thread.Start(rdts);
                                thread.Join();
                                if (t != null)
                                    t.Enabled = false;
                                return rdts.ReturnValue;
                            })));
                    Runner.SendMessage("WorkComplete", cmsg, m_RunnerMasterMachineName, OxRunConstants.RunnerMasterQueueName);
                }
            }
        }

        private static void RunnerDaemonCode(object dataObject)
        {
            RunnerDaemonThreadData data = (RunnerDaemonThreadData)dataObject;
            try
            {
                long workingSetBefore = Environment.WorkingSet;

                var beforeProcessingWml = new WmlDocument(data.FiRepoItem.FullName);
                var afterAcceptingWml = RevisionProcessor.AcceptRevisions(beforeProcessingWml);
                var afterRejectingWml = RevisionProcessor.RejectRevisions(beforeProcessingWml);

                var beforeFi = new FileInfo(Path.Combine(data.TestRunOutputDi.FullName, data.FiRepoItem.Name));
                beforeProcessingWml.SaveAs(beforeFi.FullName);

                var afterAcceptingFi = new FileInfo(Path.Combine(data.TestRunOutputDi.FullName, data.FiRepoItem.Name.Replace(".docx", "-Accepted.docx")));
                afterAcceptingWml.SaveAs(afterAcceptingFi.FullName);

                List<string> expectedErrors;
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(beforeProcessingWml.DocumentByteArray, 0, beforeProcessingWml.DocumentByteArray.Length);
                    using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, false))
                    {
                        OpenXmlValidator validator = new OpenXmlValidator();
                        expectedErrors = validator.Validate(wDoc)
                            .Select(e => e.Description)
                            .Distinct()
                            .ToList();
                    }
                }

                bool acceptedErrors = false;
                bool rejectedErrors = false;

                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(afterAcceptingWml.DocumentByteArray, 0, afterAcceptingWml.DocumentByteArray.Length);
                    using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, false))
                    {
                        OpenXmlValidator validator = new OpenXmlValidator();
                        var errors = validator.Validate(wDoc).Where(e =>
                        {
                            var str = e.Description;
                            foreach (var ee in expectedErrors)
                            {
                                if (str.Contains(ee))
                                    return false;
                            }
                            return true;
                        });
                        if (errors.Count() != 0)
                        {
                            acceptedErrors = true;
                        }
                    }
                }

                var afterRejectingFi = new FileInfo(Path.Combine(data.TestRunOutputDi.FullName, data.FiRepoItem.Name.Replace(".docx", "-Rejected.docx")));
                afterRejectingWml.SaveAs(afterRejectingFi.FullName);

                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(afterRejectingWml.DocumentByteArray, 0, afterRejectingWml.DocumentByteArray.Length);
                    using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, false))
                    {
                        OpenXmlValidator validator = new OpenXmlValidator();
                        var errors = validator.Validate(wDoc).Where(e =>
                        {
                            var str = e.Description;
                            foreach (var ee in expectedErrors)
                            {
                                if (str.Contains(ee))
                                    return false;
                            }
                            return true;
                        });
                        if (errors.Count() != 0)
                        {
                            rejectedErrors = true;
                        }
                    }
                }

                XElement sData = new XElement("SData");

                long workingSetAfter = Environment.WorkingSet;
                sData.Name = "Document";
                sData.Add(new XAttribute("GuidName", data.GuidName));
                sData.Add(new XAttribute("WorkingSetBefore", workingSetBefore));
                sData.Add(new XAttribute("WorkingSetAfter", workingSetAfter));
                if (acceptedErrors)
                    sData.Add(new XAttribute("AcceptedValidationErrors", true));
                if (rejectedErrors)
                    sData.Add(new XAttribute("RejectedValidationErrors", true));
                if (data.CollectProcessTimeMetrics == true)
                {
                    DateTime currentTime = DateTime.Now;
                    var ticks = (currentTime - data.PrevTime).Ticks;
                    sData.Add(new XAttribute("Ticks", ticks));
                    data.PrevTime = currentTime;
                }
                data.ReturnValue = sData;
                return;
            }
            catch (PowerToolsDocumentException e)
            {
                var errorXml = new XElement("Document",
                    new XAttribute("GuidName", data.GuidName),
                    new XElement("PowerToolsDocumentException",
                        MakeValidXml(e.ToString())));
                if (data.CollectProcessTimeMetrics == true)
                {
                    DateTime currentTime = DateTime.Now;
                    var ticks = (currentTime - data.PrevTime).Ticks;
                    errorXml.Add(new XAttribute("Ticks", ticks));
                    data.PrevTime = currentTime;
                }
                data.ReturnValue = errorXml;
                return;
            }
            catch (FileFormatException /* e */)
            {
#if false
                var errorXml = new XElement("Document",
                    new XAttribute("GuidName", data.GuidName),
                    new XElement("FileFormatException",
                        MakeValidXml(e.ToString())));
#endif
                var errorXml = new XElement("Document",
                    new XAttribute("GuidName", data.GuidName),
                    new XElement("FileFormatProblem"));
                if (data.CollectProcessTimeMetrics == true)
                {
                    DateTime currentTime = DateTime.Now;
                    var ticks = (currentTime - data.PrevTime).Ticks;
                    errorXml.Add(new XAttribute("Ticks", ticks));
                    data.PrevTime = currentTime;
                }
                data.ReturnValue = errorXml;
                return;
            }
            catch (System.Threading.ThreadAbortException)
            {
                var errorXml = new XElement("Document",
                    new XAttribute("GuidName", data.GuidName),
                    new XElement("TimeoutException",
                        new XAttribute("Miliseconds", data.TimeoutInMiliseconds)));
                if (data.CollectProcessTimeMetrics == true)
                {
                    DateTime currentTime = DateTime.Now;
                    var ticks = (currentTime - data.PrevTime).Ticks;
                    errorXml.Add(new XAttribute("Ticks", ticks));
                    data.PrevTime = currentTime;
                }
                data.ReturnValue = errorXml;
                return;
            }
            catch (Exception e)
            {
                var errorXml = new XElement("Document",
                    new XAttribute("GuidName", data.GuidName),
                    new XElement("Exception",
                        MakeValidXml(e.ToString())));
                if (data.CollectProcessTimeMetrics == true)
                {
                    DateTime currentTime = DateTime.Now;
                    var ticks = (currentTime - data.PrevTime).Ticks;
                    errorXml.Add(new XAttribute("Ticks", ticks));
                    data.PrevTime = currentTime;
                }
                data.ReturnValue = errorXml;
                return;
            }
        }

        private static string MakeValidXml(string p)
        {
            if (!p.Any(c => c < 0x20))
                return p;
            var newP = p
                .Select(c =>
                {
                    if (c < 0x20)
                        return string.Format("_{0:X}_", (int)c);
                    return c.ToString();
                })
                .StringConcatenate();
            return newP;
        }

        private void InitRepoIfNecessary(DirectoryInfo repoLocation)
        {
            if (repoLocation.FullName != m_RepoLocation)
            {
                m_Repo = new Repo(repoLocation, RepoAccessLevel.FileAccessOnly);
                m_RepoLocation = repoLocation.FullName;
            }
        }

        public RunnerDaemonRevisionProcessor(string runnerMasterMachineName, short minorRevisionNumber)
            : base(runnerMasterMachineName, minorRevisionNumber) { }

    }
}
