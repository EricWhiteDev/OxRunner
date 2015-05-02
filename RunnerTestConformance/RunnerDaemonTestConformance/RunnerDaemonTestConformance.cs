using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using DocumentFormat.OpenXml.Tools.DocumentReflector;

namespace OxRun
{
    class RunnerDaemonTestConformance : RunnerDaemon
    {
        // NOTE: when changing the name of the class, must change the following line to get the version properly.
        static System.Version m_RunnerAssemblyVersion = typeof(RunnerDaemonTestConformance).Assembly.GetName().Version;
        static string m_RepoLocation = null;
        static Repo m_Repo = null;
        static DirectoryInfo m_DiReflectedCodeProject = null;

        static void Main(string[] args)
        {
#if false
            FileInfo fi = new FileInfo(@"C:\TestFileRepo\xlsx\00\00059253606EA3001619D73993922C2E39D38F.xlsx");
            MetricsGetterSettings metricsGetterSettings = new MetricsGetterSettings();
            metricsGetterSettings.IncludeTextInContentControls = false;
            metricsGetterSettings.IncludeXlsxTableCellData = false;
            var metrics = MetricsGetter.GetMetrics(fi.FullName, metricsGetterSettings);
            metrics.Name = "Document";
            metrics.Add(new XAttribute("GuidName", fi.Name));
            Console.WriteLine(metrics);
            Environment.Exit(0);
#endif

            if (args.Length == 0)
                throw new ArgumentException("ControllerDaemon did not pass any arguments to RunnerDaemon");

            string runnerMasterMachineName = args[0];

            Console.ForegroundColor = ConsoleColor.White;
            ConsolePosition.SetConsolePosition(m_RunnerAssemblyVersion.MinorRevision - 1,
                Environment.MachineName.ToLower() == runnerMasterMachineName.ToLower());

            // MinorRevision will be unique for each daemon, so we append it for the daemon queue name.
            var runnerDaemon = new RunnerDaemonTestConformance(runnerMasterMachineName, m_RunnerAssemblyVersion.MinorRevision);
            if (runnerDaemon.m_RunnerLog.m_FiLog != null)
                runnerDaemon.PrintToConsole(ConsoleColor.White, string.Format("Log: {0}", runnerDaemon.m_RunnerLog.m_FiLog.FullName));
            runnerDaemon.PrintToConsole(string.Format("MasterRunner machine name: {0}", runnerMasterMachineName));
            runnerDaemon.PrintToConsole(string.Format("Daemon Number: {0}", m_RunnerAssemblyVersion.MinorRevision));

            var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
            var homePath = Environment.GetEnvironmentVariable("HOMEPATH");
            m_DiReflectedCodeProject = OxRun.FileUtils.GetDateTimeStampedDirectoryInfo(homeDrive + homePath + string.Format("/Documents/ReflectedCode-Daemon{0}-", m_RunnerAssemblyVersion.MinorRevision));
            SetUpReflectedCodeProject(m_DiReflectedCodeProject);

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
                                    var fiGeneratedFile = new FileInfo(Path.Combine(m_DiReflectedCodeProject.FullName, "ReflectedCode.cs"));
                                    var xml = TestCodeGeneration(m_Repo, guidName, m_DiReflectedCodeProject, fiGeneratedFile);
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

        public RunnerDaemonTestConformance(string runnerMasterMachineName, short minorRevisionNumber)
            : base(runnerMasterMachineName, minorRevisionNumber) { }

        private static void SetUpReflectedCodeProject(DirectoryInfo diReflectedCodeProject)
        {
            diReflectedCodeProject.Create();
            File.WriteAllText(Path.Combine(diReflectedCodeProject.FullName, "ReflectedCodeProject.sln"),
    @"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2013
VisualStudioVersion = 12.0.21005.1
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""ReflectedCodeProject"", ""ReflectedCodeProject.csproj"", ""{A3992583-95B0-4997-8C2F-6469AAE2A689}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{A3992583-95B0-4997-8C2F-6469AAE2A689}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{A3992583-95B0-4997-8C2F-6469AAE2A689}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{A3992583-95B0-4997-8C2F-6469AAE2A689}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{A3992583-95B0-4997-8C2F-6469AAE2A689}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
");
            File.WriteAllText(Path.Combine(diReflectedCodeProject.FullName, "ReflectedCodeProject.csproj"),
    @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""12.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProjectGuid>{A3992583-95B0-4997-8C2F-6469AAE2A689}</ProjectGuid>
    <OutputType>Exe</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>ReflectedCodeProject</RootNamespace>
    <AssemblyName>ReflectedCodeProject</AssemblyName>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""DocumentFormat.OpenXml, Version=2.5.5631.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35, processorArchitecture=MSIL"" />
    <Reference Include=""System"" />
    <Reference Include=""System.Core"" />
    <Reference Include=""System.Xml.Linq"" />
    <Reference Include=""System.Data.DataSetExtensions"" />
    <Reference Include=""Microsoft.CSharp"" />
    <Reference Include=""System.Data"" />
    <Reference Include=""System.Xml"" />
    <Reference Include=""WindowsBase"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""ReflectedCode.cs"" />
    <Compile Include=""Properties\AssemblyInfo.cs"" />
  </ItemGroup>
  <ItemGroup>
    <None Include=""App.config"" />
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
       Other similar extension points exist, see Microsoft.Common.targets.
  <Target Name=""BeforeBuild"">
  </Target>
  <Target Name=""AfterBuild"">
  </Target>
  -->
</Project>
");

            File.WriteAllText(Path.Combine(diReflectedCodeProject.FullName, "App.config"),
    @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
    <startup> 
        <supportedRuntime version=""v4.0"" sku="".NETFramework,Version=v4.5"" />
    </startup>
</configuration>
");

            var diProperties = new DirectoryInfo(Path.Combine(diReflectedCodeProject.FullName, "Properties"));
            diProperties.Create();

            File.WriteAllText(Path.Combine(diReflectedCodeProject.FullName, "Properties", "AssemblyInfo.cs"),
    @"using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle(""ReflectedCodeProject"")]
[assembly: AssemblyDescription("""")]
[assembly: AssemblyConfiguration("""")]
[assembly: AssemblyCompany("""")]
[assembly: AssemblyProduct(""ReflectedCodeProject"")]
[assembly: AssemblyCopyright(""Copyright ©  2015"")]
[assembly: AssemblyTrademark("""")]
[assembly: AssemblyCulture("""")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid(""1776bdc1-cdfa-4667-a607-04ee9d98b120"")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion(""1.0.*"")]
[assembly: AssemblyVersion(""1.0.0.0"")]
[assembly: AssemblyFileVersion(""1.0.0.0"")]
");


        }

        private static XElement TestCodeGeneration(Repo repo, string guidName, DirectoryInfo diProjectPath, FileInfo fiGeneratedFile)
        {
            try
            {
                var repoItem = repo.GetRepoItemByteArray(guidName);
                if (Repo.IsWordprocessingML(repoItem.Extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(repoItem.ByteArray, 0, repoItem.ByteArray.Length);
                        using (var doc = WordprocessingDocument.Open(ms, false))
                        {
                            return GenerateCompileAndRun(guidName, doc, repoItem.Extension, diProjectPath, fiGeneratedFile);
                        }
                    }
                }
                else if (Repo.IsSpreadsheetML(repoItem.Extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(repoItem.ByteArray, 0, repoItem.ByteArray.Length);
                        using (var doc = SpreadsheetDocument.Open(ms, false))
                        {
                            return GenerateCompileAndRun(guidName, doc, repoItem.Extension, diProjectPath, fiGeneratedFile);
                        }
                    }
                }
                else if (Repo.IsPresentationML(repoItem.Extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(repoItem.ByteArray, 0, repoItem.ByteArray.Length);
                        using (var doc = PresentationDocument.Open(ms, false))
                        {
                            return GenerateCompileAndRun(guidName, doc, repoItem.Extension, diProjectPath, fiGeneratedFile);
                        }
                    }
                }
                else
                {
                    return new XElement("Document",
                        new XAttribute("GuidName", guidName),
                        new XAttribute("Error", true),
                        new XAttribute("ErrorDescription", "IsWordprocessingML, IsPresentationML, and IsSpreadsheetML returned false"),
                        "Invalid document type");
                }
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

        private static XElement GenerateCompileAndRun(string guidName, OpenXmlPackage doc1, string extension, DirectoryInfo diProjectPath, FileInfo fiGeneratedFile)
        {
            try
            {
                ValidationErrors valErrors1 = ValidateAgainstAllVersions(doc1);

                var reflector = new FullCodeReflector();
                var codeDom = reflector.Reflect(doc1);

                if (codeDom != null)
                {
                    var code = new CodeDocument(codeDom);
                    var lines = code.Lines().ToList();

                    var unknownElements = lines.Where(l => l.GetText().Contains("OpenXmlUnknownElement"));
                    XAttribute unknownElementErrorAttribute = null;
                    if (unknownElements.Any())
                        unknownElementErrorAttribute = new XAttribute("OpenXmlUnknownElement", true);

                    var programText = lines.Select(l => l.GetText().TrimStart('\r', '\n')).ToArray();
                    var lineOfGeneratedProgramClass = programText
                        .Select((l, i) =>
                        {
                            return new
                            {
                                Line = l,
                                Index = i,
                            };
                        })
                        .FirstOrDefault(p => p.Line.Contains("public class GeneratedClass"));
                    if (lineOfGeneratedProgramClass == null)
                    {
                        return new XElement("Document",
                            new XAttribute("GuidName", guidName),
                            new XAttribute("Error", true),
                            new XAttribute("ErrorDescription", "Did not find \"public class GeneratedProgram\""),
                            unknownElementErrorAttribute);
                    }

                    var main =
    @"public static void Main(string[] args)
{
    var p = new GeneratedClass();
    p.CreatePackage(""../../out" + extension +
    @""");
}";
                    var newProgramText = programText.Take(lineOfGeneratedProgramClass.Index + 2)
                        .Concat(new[] { main })
                        .Concat(programText.Skip(lineOfGeneratedProgramClass.Index + 2))
                        .ToArray();
                    File.WriteAllLines(fiGeneratedFile.FullName, newProgramText);

                    // compile, check for errors
                    VSTools.SetUpVSEnvironmentVariables();
                    var runResults = VSTools.RunMSBuild(diProjectPath);

                    if (runResults.ExitCode == 0)
                    {
                        // run, check for errors
                        var fiBuiltExePath = new FileInfo(Path.Combine(diProjectPath.FullName, "bin", "debug", "ReflectedCodeProject.exe"));
                        var results = OpenXmlPowerTools.ExecutableRunner.RunExecutable(fiBuiltExePath.FullName, "", fiBuiltExePath.DirectoryName);
                        if (results.ExitCode == 0)
                        {
                            // validate generated file
                            FileInfo fiGeneratedDocument = new FileInfo(Path.Combine(diProjectPath.FullName, "out" + extension));
                            if (Repo.IsWordprocessingML(extension))
                            {
                                using (var doc2 = WordprocessingDocument.Open(fiGeneratedDocument.FullName, true))
                                {
                                    FixUpStartEndAttributes_Hack(doc2);
                                    ValidationErrors valErrors2 = ValidateAgainstAllVersions(doc2);
                                    var rpt = GetValidationReport(guidName, valErrors1, valErrors2);
                                    rpt.Add(unknownElementErrorAttribute);
                                    return rpt;
                                }
                            }
                            else if (Repo.IsSpreadsheetML(extension))
                            {
                                using (var doc2 = SpreadsheetDocument.Open(fiGeneratedDocument.FullName, true))
                                {
                                    ValidationErrors valErrors2 = ValidateAgainstAllVersions(doc2);
                                    var rpt = GetValidationReport(guidName, valErrors1, valErrors2);
                                    rpt.Add(unknownElementErrorAttribute);
                                    return rpt;
                                }
                            }
                            else if (Repo.IsPresentationML(extension))
                            {
                                using (var doc2 = PresentationDocument.Open(fiGeneratedDocument.FullName, true))
                                {
                                    ValidationErrors valErrors2 = ValidateAgainstAllVersions(doc2);
                                    var rpt = GetValidationReport(guidName, valErrors1, valErrors2);
                                    rpt.Add(unknownElementErrorAttribute);
                                    return rpt;
                                }
                            }
                            else
                            {
                                throw new Exception("Internal error"); // todo fix exception
                            }
                        }
                        else
                        {
                            var sa = runResults.Output.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).StringConcatenate();
                            return new XElement("Document",
                                new XAttribute("GuidName", guidName),
                                new XAttribute("Error", true),
                                new XAttribute("ErrorDescription", "Compiled code failed to run"),
                                unknownElementErrorAttribute,
                                PtUtils.MakeValidXml(sa));
                        }
                    }
                    else
                    {
                        var sa = runResults.Output.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).StringConcatenate();
                        if (sa.Contains("error CS1009: Unrecognized escape sequence"))
                        {
                            return new XElement("Document",
                                new XAttribute("GuidName", guidName),
                                new XAttribute("Error", true),
                                new XAttribute("ErrorDescription", "Generated code failed to compile, unrecognized escape sequence"),
                                unknownElementErrorAttribute);
                        }
                        if (sa.Contains("TextAnchoringTypeValues"))
                        {
                            return new XElement("Document",
                                new XAttribute("GuidName", guidName),
                                new XAttribute("Error", true),
                                new XAttribute("ErrorDescription", "Generated code failed to compile, the type 'TextAnchoringTypeValues' could not be found"),
                                unknownElementErrorAttribute);
                        }
                        return new XElement("Document",
                            new XAttribute("GuidName", guidName),
                            new XAttribute("Error", true),
                            new XAttribute("ErrorDescription", "Generated code failed to compile"),
                            unknownElementErrorAttribute,
                            PtUtils.MakeValidXml(sa));
                    }
                }
                else
                {
                    return new XElement("Document",
                        new XAttribute("GuidName", guidName),
                        new XAttribute("Error", true),
                        new XAttribute("ErrorDescription", "Reflector did not instantiate properly"));
                }
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

        private static void FixUpStartEndAttributes_Hack(WordprocessingDocument doc2)
        {
            foreach (var part in doc2.ContentParts())
            {
                XDocument xd = part.GetXDocument();
                var newRoot = (XElement)FixUpTransform(xd.Root);
                xd.Root.ReplaceWith(newRoot);
                part.PutXDocument();
            }
            var styleDefPart = doc2.MainDocumentPart.StyleDefinitionsPart;
            if (styleDefPart != null)
            {
                XDocument xd = styleDefPart.GetXDocument();
                var newRoot = (XElement)FixUpTransform(xd.Root);
                xd.Root.ReplaceWith(newRoot);
                styleDefPart.PutXDocument();
            }
            var numDefPart = doc2.MainDocumentPart.NumberingDefinitionsPart;
            if (numDefPart != null)
            {
                XDocument xd = numDefPart.GetXDocument();
                var newRoot = (XElement)FixUpTransform(xd.Root);
                xd.Root.ReplaceWith(newRoot);
                numDefPart.PutXDocument();
            }
        }

        private static object FixUpTransform(XNode node)
        {
            XElement element = node as XElement;
            if (element != null)
            {
                if (element.Name == W.ind)
                {
                    var newAttributes = element
                        .Attributes()
                        .Select(a =>
                        {
                            if (a.Name == W.start)
                                return new XAttribute(W.left, a.Value);
                            if (a.Name == W.startChars)
                                return new XAttribute(W.leftChars, a.Value);
                            if (a.Name == W.end)
                                return new XAttribute(W.right, a.Value);
                            if (a.Name == W.endChars)
                                return new XAttribute(W.rightChars, a.Value);
                            return a;
                        });
                    return new XElement(element.Name,
                        newAttributes,
                        element.Nodes().Select(n => FixUpTransform(n)));
                }

                return new XElement(element.Name,
                    element.Attributes(),
                    element.Nodes().Select(n => FixUpTransform(n)));
            }
            return node;
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
                errorList1.Zip(errorList2, (e1, e2) => new {
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
