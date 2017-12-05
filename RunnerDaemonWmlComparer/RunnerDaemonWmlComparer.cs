using System;
using System.Collections.Generic;
using System.Drawing;
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

namespace OxRunner
{
    class RunnerDaemonWmlComparer : RunnerDaemon
    {
        // NOTE: when changing the name of the class, must change the following line to get the version properly.
        static System.Version m_RunnerAssemblyVersion = typeof(RunnerDaemonWmlComparer).Assembly.GetName().Version;
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
            var runnerDaemon = new RunnerDaemonWmlComparer(runnerMasterMachineName, m_RunnerAssemblyVersion.MinorRevision);
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

                                int? timeoutInMiliseconds = 500000;    //   <================================================================================================================================= parameterize this

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

        public static string[] ExpectedErrors = new string[] {
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:firstRow' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:lastRow' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:firstColumn' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:lastColumn' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:noHBand' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:noVBand' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:allStyles' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:customStyles' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:latentStyles' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:stylesInUse' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:headingStyles' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:numberingStyles' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:tableStyles' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:directFormattingOnRuns' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:directFormattingOnParagraphs' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:directFormattingOnNumbering' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:directFormattingOnTables' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:clearFormatting' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:top3HeadingStyles' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:visibleStyles' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:alternateStyleNames' attribute is not declared.",
            "The attribute 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:val' has invalid value '0'. The MinInclusive constraint failed. The value must be greater than or equal to 1.",
            "The attribute 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:val' has invalid value '0'. The MinInclusive constraint failed. The value must be greater than or equal to 2.",
            "The 'urn:schemas-microsoft-com:office:office:gfxdata' attribute is not declared.",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:fill' attribute is invalid - The value '0' is not valid according to any of the memberTypes of the union.",
            "The element has invalid child element 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:smartTagPr'.",
        };

        private static void RunnerDaemonCode(object dataObject)
        {
            RunnerDaemonThreadData data = (RunnerDaemonThreadData)dataObject;
            try
            {
                long workingSetBefore = Environment.WorkingSet;

#if false
                // this presumes a document with no tracked revisions -
                // creates a copy, modifies the copy, and the compares them.

                WmlDocument source1Wml = new WmlDocument(data.FiRepoItem.FullName);
                WmlDocument source2Wml = null;

                // clone and make a random change to the text
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(source1Wml.DocumentByteArray, 0, source1Wml.DocumentByteArray.Length);
                    using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, true))
                    {
                        var mxDoc = wDoc.MainDocumentPart.GetXDocument();
                        var numberTElements = mxDoc.Descendants(W.t).Count();
                        if (numberTElements > 2)
                        {
                            var toChange = numberTElements / 2;
                            var elementToChange = mxDoc.Descendants(W.t).Skip(toChange - 1).FirstOrDefault();
                            var v = elementToChange.Value;
                            v = "X" + v;
                            elementToChange.Value = v;
                        }
                        wDoc.MainDocumentPart.PutXDocument();
                    }
                    source2Wml = new WmlDocument("altered.docx", ms.ToArray());
                }
#endif

#if true
                // this is good for a document that contains revisions
                // make two copies -
                // accept revisions in one
                // reject revisions in the other
                // compare / consolidate them.

                WmlDocument repoItemWml = new WmlDocument(data.FiRepoItem.FullName);
                WmlDocument source1Wml = RevisionProcessor.RejectRevisions(repoItemWml);
                WmlDocument source2Wml = RevisionProcessor.AcceptRevisions(repoItemWml);
#endif
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Here, source1Wml and source2Wml are set up appropriately.

                var fromRepoFi = new FileInfo(Path.Combine(data.TestRunOutputDi.FullName, data.FiRepoItem.Name.Replace(".docx", "-Original.docx")));
                var originalWml = WordprocessingMLUtil.BreakLinkToTemplate(repoItemWml);
                originalWml.SaveAs(fromRepoFi.FullName);

                // get list of expected errors from the original
                List<string> originalErrors = null;
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(originalWml.DocumentByteArray, 0, originalWml.DocumentByteArray.Length);
                    using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, false))
                    {
                        OpenXmlValidator validator = new OpenXmlValidator();
                        var errors = validator.Validate(wDoc);
                        originalErrors = errors.Select(e => e.Description).ToList();
                    }
                }

                WmlComparerSettings settings = new WmlComparerSettings();
                WmlDocument comparedWml = WmlComparer.Compare(source1Wml, source2Wml, settings);

                var source1Fi = new FileInfo(Path.Combine(data.TestRunOutputDi.FullName, data.FiRepoItem.Name.Replace(".docx", "-Rejected.docx")));
                WordprocessingMLUtil.BreakLinkToTemplate(source1Wml).SaveAs(source1Fi.FullName);

                var source2Fi = new FileInfo(Path.Combine(data.TestRunOutputDi.FullName, data.FiRepoItem.Name.Replace(".docx", "-Accepted.docx")));
                WordprocessingMLUtil.BreakLinkToTemplate(source2Wml).SaveAs(source2Fi.FullName);

                var afterComparingFi = new FileInfo(Path.Combine(data.TestRunOutputDi.FullName, data.FiRepoItem.Name.Replace(".docx", "-Compared.docx")));
                WordprocessingMLUtil.BreakLinkToTemplate(comparedWml).SaveAs(afterComparingFi.FullName);

                List<WmlRevisedDocumentInfo> revisedDocInfo = new List<WmlRevisedDocumentInfo>()
                {
                    new WmlRevisedDocumentInfo()
                    {
                        RevisedDocument = source2Wml,
                        Color = Color.LightBlue,
                        Revisor = "Revised by Eric White",
                    }
                };
                WmlDocument consolidatedWml = WmlComparer.Consolidate(
                    source1Wml,
                    revisedDocInfo,
                    settings);

                var docxConsolidatedFi = new FileInfo(Path.Combine(data.TestRunOutputDi.FullName, data.FiRepoItem.Name.Replace(".docx", "-Consolidated.docx")));
                WordprocessingMLUtil.BreakLinkToTemplate(consolidatedWml).SaveAs(docxConsolidatedFi.FullName);

                string comparedErrors = null;
                string consolidatedErrors = null;

                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(comparedWml.DocumentByteArray, 0, comparedWml.DocumentByteArray.Length);
                    using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, false))
                    {
                        OpenXmlValidator validator = new OpenXmlValidator();
                        var errors = validator.Validate(wDoc).Where(e =>
                        {
                            var str = e.Description;
                            foreach (var ee in ExpectedErrors)
                            {
                                if (str.Contains(ee))
                                    return false;
                            }
                            foreach (var ee in originalErrors)
                            {
                                if (str.Contains(ee))
                                    return false;
                            }
                            if (e.Description.Contains("Element 'DocumentFormat.OpenXml.Vml.Shape' referenced by 'OLEObject@ShapeID' does not exist in part '/word/document.xml'."))
                                return false;
                            return true;
                        });
                        if (errors.Count() != 0)
                        {
                            comparedErrors = errors.First().Description;
                        }
                    }
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(consolidatedWml.DocumentByteArray, 0, consolidatedWml.DocumentByteArray.Length);
                    using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, false))
                    {
                        OpenXmlValidator validator = new OpenXmlValidator();
                        var errors = validator.Validate(wDoc).Where(e =>
                        {
                            var str = e.Description;
                            foreach (var ee in ExpectedErrors)
                            {
                                if (str.Contains(ee))
                                    return false;
                            }
                            foreach (var ee in originalErrors)
                            {
                                if (str.Contains(ee))
                                    return false;
                            }
                            if (e.Description.Contains("Element 'DocumentFormat.OpenXml.Vml.Shape' referenced by 'OLEObject@ShapeID' does not exist in part '/word/document.xml'."))
                                return false;
                            return true;
                        });
                        if (errors.Count() != 0)
                        {
                            consolidatedErrors = errors.First().Description;
                        }
                    }
                }

                XElement sData = new XElement("SData");

                long workingSetAfter = Environment.WorkingSet;
                sData.Name = "Document";
                sData.Add(new XAttribute("GuidName", data.GuidName));
                sData.Add(new XAttribute("WorkingSetBefore", workingSetBefore));
                sData.Add(new XAttribute("WorkingSetAfter", workingSetAfter));
                if (comparedErrors != null)
                    sData.Add(new XAttribute("ComparedValidationErrors", comparedErrors));
                if (consolidatedErrors != null)
                    sData.Add(new XAttribute("ConsolidatedValidationErrors", consolidatedErrors));
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

        public RunnerDaemonWmlComparer(string runnerMasterMachineName, short minorRevisionNumber)
            : base(runnerMasterMachineName, minorRevisionNumber) { }

    }
}
