using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OxRunner
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
            SetEnvironmentVariableIfNecessary("DevEnvDir", @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\");
            //SetEnvironmentVariableIfNecessary("ExtensionSdkDir", @"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0\ExtensionSDKs");
            SetEnvironmentVariableIfNecessary("Framework40Version", @"v4.0");
            SetEnvironmentVariableIfNecessary("FrameworkDir", @"C:\Windows\Microsoft.NET\Framework\");
            SetEnvironmentVariableIfNecessary("FrameworkDIR32", @"C:\Windows\Microsoft.NET\Framework\");
            SetEnvironmentVariableIfNecessary("FrameworkVersion", @"v4.0.30319");
            SetEnvironmentVariableIfNecessary("FrameworkVersion32", @"v4.0.30319");
            SetEnvironmentVariableIfNecessary("INCLUDE", @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\VC\INCLUDE;C:\Program Files (x86)\Windows Kits\NETFXSDK\4.6.1\include\um;C:\Program Files (x86)\Windows Kits\8.1\include\\shared;C:\Program Files (x86)\Windows Kits\8.1\include\\um;C:\Program Files (x86)\Windows Kits\8.1\include\\winrt;");
            SetEnvironmentVariableIfNecessary("LIB", @"C:\Program Files (x86)\Windows Kits\NETFXSDK\4.6.1\lib\um\x86;C:\Program Files (x86)\Windows Kits\8.1\lib\winv6.3\um\x86;");
            SetEnvironmentVariableIfNecessary("LIBPATH", @"C:\Windows\Microsoft.NET\Framework\v4.0.30319;C:\Program Files (x86)\Windows Kits\8.1\References\CommonConfiguration\Neutral;\Microsoft.VCLibs\14.0\References\CommonConfiguration\neutral;");
            SetEnvironmentVariableIfNecessary("VCINSTALLDIR", @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\");
            SetEnvironmentVariableIfNecessary("VisualStudioVersion", @"14.0");
            //SetEnvironmentVariableIfNecessary("VS110COMNTOOLS", @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\Tools\");
            SetEnvironmentVariableIfNecessary("VS120COMNTOOLS", @"C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\Tools\");
            SetEnvironmentVariableIfNecessary("VS140COMNTOOLS", @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\Tools\");
            SetEnvironmentVariableIfNecessary("VSINSTALLDIR", @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\VC\");
            SetEnvironmentVariableIfNecessary("WindowsSdkDir", @"C:\Program Files (x86)\Windows Kits\8.1\");
            //SetEnvironmentVariableIfNecessary("WindowsSdkDir_35", @"C:\Program Files (x86)\Microsoft SDKs\Windows\v7.0A\Bin\");
            //SetEnvironmentVariableIfNecessary("WindowsSdkDir_old", @"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0A\");

#if false
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
#endif

            string[] pathsToAdd = new[] {
                @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow;",
                @"C:\Program Files (x86)\MSBuild\14.0\bin; C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\;",
                @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\VC\BIN;",
                @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\Tools;",
                @"C:\Windows\Microsoft.NET\Framework\v4.0.30319;",
                @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\VC\VCPackages;",
                @"C:\Program Files (x86)\HTML Help Workshop;",
                @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Team Tools\Performance Tools;",
                @"C:\Program Files (x86)\Windows Kits\8.1\bin\x86;",
                @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\;",
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
