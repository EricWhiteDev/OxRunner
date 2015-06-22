using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using OxRun;
using OpenXmlPowerTools;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace OxRun
{
    class RunnerDaemonSystemIOPackaging : RunnerDaemon
    {
        // NOTE: when changing the name of the class, must change the following line to get the version properly.
        static System.Version m_RunnerAssemblyVersion = typeof(RunnerDaemonSystemIOPackaging).Assembly.GetName().Version;
        static string m_RepoLocation = null;
        static Repo m_Repo = null;
        static DirectoryInfo m_DiReflectedCodeProject = null;

        static void Main(string[] args)
        {
            if (args.Length == 0)
                throw new ArgumentException("ControllerDaemon did not pass any arguments to RunnerDaemon");

            string runnerMasterMachineName = args[0];

            Console.ForegroundColor = ConsoleColor.White;
            ConsolePosition.SetConsolePosition(m_RunnerAssemblyVersion.MinorRevision - 1,
                Environment.MachineName.ToLower() == runnerMasterMachineName.ToLower());

            // MinorRevision will be unique for each daemon, so we append it for the daemon queue name.
            var runnerDaemon = new RunnerDaemonSystemIOPackaging(runnerMasterMachineName, m_RunnerAssemblyVersion.MinorRevision);
            if (runnerDaemon.m_RunnerLog.m_FiLog != null)
                runnerDaemon.PrintToConsole(ConsoleColor.White, string.Format("Log: {0}", runnerDaemon.m_RunnerLog.m_FiLog.FullName));
            runnerDaemon.PrintToConsole(string.Format("MasterRunner machine name: {0}", runnerMasterMachineName));
            runnerDaemon.PrintToConsole(string.Format("Daemon Number: {0}", m_RunnerAssemblyVersion.MinorRevision));

            var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
            var homePath = Environment.GetEnvironmentVariable("HOMEPATH");

            runnerDaemon.SendDaemonReadyMessage();
            runnerDaemon.MessageLoop();
        }

        private void MessageLoop()
        {
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
                                RepoItem ri = m_Repo.GetRepoItemFileInfo(guidName);
                                PrintToConsole(guidName);
                                try
                                {
                                    var xml = RegenerateUsingSystemIoPackaging(m_Repo, guidName, m_DiReflectedCodeProject);
                                    return xml;
                                }
                                catch (PowerToolsDocumentException e)
                                {
                                    var errorXml = new XElement("Document",
                                        new XAttribute("GuidName", guidName),
                                        new XElement("PowerToolsDocumentException",
                                            PtUtils.MakeValidXml(e.ToString())));
                                    if (collectProcessTimeMetrics == true)
                                    {
                                        DateTime currentTime = DateTime.Now;
                                        var ticks = (currentTime - prevTime).Ticks;
                                        errorXml.Add(new XAttribute("Ticks", ticks));
                                        prevTime = currentTime;
                                    }
                                    return errorXml;
                                }
                                catch (FileFormatException e)
                                {
                                    var errorXml = new XElement("Document",
                                        new XAttribute("GuidName", guidName),
                                        new XElement("FileFormatException",
                                            PtUtils.MakeValidXml(e.ToString())));
                                    if (collectProcessTimeMetrics == true)
                                    {
                                        DateTime currentTime = DateTime.Now;
                                        var ticks = (currentTime - prevTime).Ticks;
                                        errorXml.Add(new XAttribute("Ticks", ticks));
                                        prevTime = currentTime;
                                    }
                                    return errorXml;
                                }
                                catch (Exception e)
                                {
                                    var errorXml = new XElement("Document",
                                        new XAttribute("GuidName", guidName),
                                        new XElement("Exception",
                                            PtUtils.MakeValidXml(e.ToString())));
                                    if (collectProcessTimeMetrics == true)
                                    {
                                        DateTime currentTime = DateTime.Now;
                                        var ticks = (currentTime - prevTime).Ticks;
                                        errorXml.Add(new XAttribute("Ticks", ticks));
                                        prevTime = currentTime;
                                    }
                                    return errorXml;
                                }
                            })));
                    Runner.SendMessage("WorkComplete", cmsg, m_RunnerMasterMachineName, OxRunConstants.RunnerMasterQueueName);
                }
            }
        }

        private void InitRepoIfNecessary(DirectoryInfo repoLocation)
        {
            if (repoLocation.FullName != m_RepoLocation)
            {
                m_Repo = new Repo(repoLocation);
                m_RepoLocation = repoLocation.FullName;
            }
        }

        public RunnerDaemonSystemIOPackaging(string runnerMasterMachineName, short minorRevisionNumber)
            : base(runnerMasterMachineName, minorRevisionNumber) { }

        private static XElement RegenerateUsingSystemIoPackaging(Repo repo, string guidName, DirectoryInfo diProjectPath)
        {
            try
            {
                var repoItem = repo.GetRepoItemByteArray(guidName);
                return GenerateNewOpenXmlFile(guidName, repoItem.ByteArray, repoItem.Extension, diProjectPath);
            }
            catch (Exception e)
            {
                return new XElement("Document",
                    new XAttribute("GuidName", guidName),
                    new XAttribute("Error", true),
                    new XAttribute("ErrorDescription", "Exception thrown (1)"),
                    PtUtils.MakeValidXml(e.ToString()));
            }
        }

        private static XElement GenerateNewOpenXmlFile(string guidName, byte[] byteArray, string extension, DirectoryInfo diProjectPath)
        {
            try
            {
                ValidationErrors valErrors1 = null;
                ValidationErrors valErrors2 = null;

                if (Util.IsWordprocessingML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(byteArray, 0, byteArray.Length);
                        using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, true))
                        {
                            valErrors1 = ValidateAgainstAllVersions(wDoc);
                        }
                    }
                }
                else if (Util.IsSpreadsheetML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(byteArray, 0, byteArray.Length);
                        using (SpreadsheetDocument sDoc = SpreadsheetDocument.Open(ms, true))
                        {
                            valErrors1 = ValidateAgainstAllVersions(sDoc);
                        }
                    }
                }
                else if (Util.IsPresentationML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(byteArray, 0, byteArray.Length);
                        using (PresentationDocument pDoc = PresentationDocument.Open(ms, true))
                        {
                            valErrors1 = ValidateAgainstAllVersions(pDoc);
                        }
                    }
                }
                else
                {
                    return new XElement("Document",
                        new XAttribute("GuidName", guidName),
                        new XAttribute("Error", true),
                        new XAttribute("ErrorDescription", "Not one of the three Open XML document types."));
                }

                byte[] newByteArray = ClonePackage(byteArray);

                if (Util.IsWordprocessingML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(newByteArray, 0, newByteArray.Length);
                        using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, true))
                        {
                            valErrors2 = ValidateAgainstAllVersions(wDoc);
                        }
                    }
                }
                else if (Util.IsSpreadsheetML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(newByteArray, 0, newByteArray.Length);
                        using (SpreadsheetDocument sDoc = SpreadsheetDocument.Open(ms, true))
                        {
                            valErrors2 = ValidateAgainstAllVersions(sDoc);
                        }
                    }
                }
                else if (Util.IsPresentationML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(newByteArray, 0, newByteArray.Length);
                        using (PresentationDocument pDoc = PresentationDocument.Open(ms, true))
                        {
                            valErrors2 = ValidateAgainstAllVersions(pDoc);
                        }
                    }
                }
                return GetValidationReport(guidName, valErrors1, valErrors2);
            }
            catch (Exception e)
            {
                return new XElement("Document",
                    new XAttribute("GuidName", guidName),
                    new XAttribute("Error", true),
                    new XAttribute("ErrorDescription", "Exception thrown when opening document"),
                    PtUtils.MakeValidXml(e.ToString()));
            }
        }

        static byte[] ClonePackage(byte[] fromByteArray)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(fromByteArray, 0, fromByteArray.Length);
                using (Package pkg = Package.Open(ms, FileMode.Open, FileAccess.Read))
                using (MemoryStream newMs = new MemoryStream())
                {
                    using (Package newPkg = Package.Open(newMs, FileMode.Create, FileAccess.ReadWrite))
                    {
                        foreach (var part in pkg.GetParts())
                        {
                            if (part.ContentType != "application/vnd.openxmlformats-package.relationships+xml")
                            {
                                var newPart = newPkg.CreatePart(part.Uri, part.ContentType, CompressionOption.Normal);
                                using (var oldStream = part.GetStream())
                                using (var newStream = newPart.GetStream())
                                    CopyStream(oldStream, newStream);
                                foreach (var rel in part.GetRelationships())
                                    newPart.CreateRelationship(rel.TargetUri, rel.TargetMode, rel.RelationshipType, rel.Id);
                            }
                        }
                        foreach (var rel in pkg.GetRelationships())
                            newPkg.CreateRelationship(rel.TargetUri, rel.TargetMode, rel.RelationshipType, rel.Id);
                    }
                    return newMs.ToArray();
                }
            }
        }

        private static void CopyStream(Stream source, Stream target)
        {
            const int BufSize = 0x4096;
            byte[] buf = new byte[BufSize];
            int bytesRead = 0;
            while ((bytesRead = source.Read(buf, 0, BufSize)) > 0)
                target.Write(buf, 0, bytesRead);
        }

        private static XElement GetValidationReport(string guidName, ValidationErrors valErrors1, ValidationErrors valErrors2)
        {
            XElement v2007 = GetDeltaInOneErrorList(valErrors1.Office2007Errors, valErrors2.Office2007Errors, "Office2007Errors");
            XElement v2010 = GetDeltaInOneErrorList(valErrors1.Office2010Errors, valErrors2.Office2010Errors, "Office2010Errors");
            XElement v2013 = GetDeltaInOneErrorList(valErrors1.Office2013Errors, valErrors2.Office2013Errors, "Office2013Errors");
            bool haveErrors = v2007 != null || v2010 != null || v2013 != null;
            XAttribute errorAtt = null;
            XAttribute errorDescription = null;
            if (haveErrors)
            {
                errorAtt = new XAttribute("Error", true);
                errorDescription = new XAttribute("ErrorDescription", "Difference in validation errors before vs. after (only 1st 3 errors listed)");
            }
            var docElement = new XElement("Document",
                new XAttribute("GuidName", guidName),
                errorAtt,
                errorDescription,
                v2007,
                v2010,
                v2013);
            return docElement;
        }

        private static XElement GetDeltaInOneErrorList(List<ValidationErrorInfo> errorList1, List<ValidationErrorInfo> errorList2, XName errorElementName)
        {
            if (errorList1.Count() != errorList2.Count() ||
                errorList1.Zip(errorList2, (e1, e2) => new
                {
                    Error1 = e1,
                    Error2 = e2,
                })
                .Any(p =>
                {
                    if (p.Error1.ToString() == p.Error2.ToString())
                        return false;
                    return true;
                }))
            {
                XElement deltaErrors = new XElement(errorElementName,
                    new XElement("Before",
                        SerializeErrors(errorList1)),
                    new XElement("After",
                        SerializeErrors(errorList2)));
                return deltaErrors;
            }
            return null;
        }

        private static string SerializeErrors(IEnumerable<ValidationErrorInfo> errorList)
        {
            return errorList.Take(3).Select(err =>
            {
                StringBuilder sb = new StringBuilder();
                if (err.Description.Length > 300)
                    sb.Append(PtUtils.MakeValidXml(err.Description.Substring(0, 300) + " ... elided ...") + Environment.NewLine);
                else
                    sb.Append(PtUtils.MakeValidXml(err.Description) + Environment.NewLine);
                sb.Append("  in part " + PtUtils.MakeValidXml(err.Part.Uri.ToString()) + Environment.NewLine);
                sb.Append("  at " + PtUtils.MakeValidXml(err.Path.XPath) + Environment.NewLine);
                return sb.ToString();
            })
            .StringConcatenate();
        }

        private static XElement ValidateAgainstAllFormatsGenerateErrorXml(string guidName, OpenXmlPackage oxDoc)
        {
            List<XElement> errorElements = new List<XElement>();
            bool pass = ValidateAgainstSpecificVersionGenerateErrorXml(oxDoc, errorElements, FileFormatVersions.Office2007, H.SdkValidationError2007) &&
                ValidateAgainstSpecificVersionGenerateErrorXml(oxDoc, errorElements, FileFormatVersions.Office2010, H.SdkValidationError2010) &&
                ValidateAgainstSpecificVersionGenerateErrorXml(oxDoc, errorElements, FileFormatVersions.Office2013, H.SdkValidationError2013);
            if (pass)
            {
                return new XElement("Document",
                    new XAttribute("GuidName", guidName));
            }
            else
            {
                return new XElement("Document",
                    new XAttribute("GuidName", guidName),
                    new XAttribute("Error", true),
                    new XAttribute("ErrorDescription", "Generated document failed to validate"),
                    errorElements);
            }
        }

        private static bool ValidateAgainstSpecificVersionGenerateErrorXml(OpenXmlPackage oxDoc, List<XElement> errorElements, DocumentFormat.OpenXml.FileFormatVersions versionToValidateAgainst, XName versionSpecificMetricName)
        {
            OpenXmlValidator validator = new OpenXmlValidator(versionToValidateAgainst);
            var errors = validator.Validate(oxDoc);
            bool valid = errors.Count() == 0;
            if (!valid)
            {
                errorElements.Add(new XElement(versionSpecificMetricName, new XAttribute(H.Val, true),
                    errors.Take(3).Select(err =>
                    {
                        StringBuilder sb = new StringBuilder();
                        if (err.Description.Length > 300)
                            sb.Append(PtUtils.MakeValidXml(err.Description.Substring(0, 300) + " ... elided ...") + Environment.NewLine);
                        else
                            sb.Append(PtUtils.MakeValidXml(err.Description) + Environment.NewLine);
                        sb.Append("  in part " + PtUtils.MakeValidXml(err.Part.Uri.ToString()) + Environment.NewLine);
                        sb.Append("  at " + PtUtils.MakeValidXml(err.Path.XPath) + Environment.NewLine);
                        return sb.ToString();
                    })));
            }
            return valid;
        }

        private class ValidationErrors
        {
            public List<ValidationErrorInfo> Office2007Errors = new List<ValidationErrorInfo>();
            public List<ValidationErrorInfo> Office2010Errors = new List<ValidationErrorInfo>();
            public List<ValidationErrorInfo> Office2013Errors = new List<ValidationErrorInfo>();
        }

        private static ValidationErrors ValidateAgainstAllVersions(OpenXmlPackage oxDoc)
        {
            ValidationErrors validationErrors = new ValidationErrors();
            OpenXmlValidator validator = new OpenXmlValidator(FileFormatVersions.Office2007);
            validationErrors.Office2007Errors = validator.Validate(oxDoc).Where(err => UnexpectedError(err)).ToList();
            validator = new OpenXmlValidator(FileFormatVersions.Office2010);
            validationErrors.Office2010Errors = validator.Validate(oxDoc).Where(err => UnexpectedError(err)).ToList();
            validator = new OpenXmlValidator(FileFormatVersions.Office2013);
            validationErrors.Office2013Errors = validator.Validate(oxDoc).Where(err => UnexpectedError(err)).ToList();
            return validationErrors;
        }

        private static string[] ExpectedErrors = new string[] {
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:firstRow' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:lastRow' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:firstCol' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:lastCol' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:firstColumn' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:lastColumn' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:noHBand' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:noVBand' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:band1Vert' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:band2Vert' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:band1Horz' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:band2Horz' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:neCell' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:nwCell' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:seCell' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:swCell' attribute is not declared",
        };

        private static bool UnexpectedError(ValidationErrorInfo err)
        {
            var errStr = err.Description;
            if (ExpectedErrors.Any(e => errStr.Contains(e)))
                return false;
            return true;
        }
    }
}
