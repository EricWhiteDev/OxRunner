using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OxRun
{
    public class VSTools
    {
        public static ExecutableRunner.RunResults RunMSBuild(DirectoryInfo projectPath)
        {
            var msBuildPath = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe";

            var results = ExecutableRunner.RunExecutable(msBuildPath, "", projectPath.FullName);
            return results;
        }

        public static void SetUpVSEnvironmentVariables()
        {
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

        private static string SetEnvironmentVariableIfNecessary(string environmentVariableName, string value)
        {
            string vv;
            vv = Environment.GetEnvironmentVariable(environmentVariableName);
            if (vv == null)
                Environment.SetEnvironmentVariable(environmentVariableName, value);
            return vv;
        }

    }
}
