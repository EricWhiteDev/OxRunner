using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OxRun
{
    public class ExecutableRunner
    {
        public class RunResults
        {
            public int ExitCode;
            public Exception RunException;
            public StringBuilder Output;
            public StringBuilder Error;
        }

        public static RunResults RunExecutable(string executablePath, string arguments, string workingDirectory)
        {
            RunResults runResults = new RunResults
            {
                Output = new StringBuilder(),
                Error = new StringBuilder(),
                RunException = null
            };
            try
            {
                if (File.Exists(executablePath))
                {
                    using (Process proc = new Process())
                    {
                        proc.StartInfo.FileName = executablePath;
                        proc.StartInfo.Arguments = arguments;
                        proc.StartInfo.WorkingDirectory = workingDirectory;
                        proc.StartInfo.UseShellExecute = false;
                        proc.StartInfo.RedirectStandardOutput = true;
                        proc.StartInfo.RedirectStandardError = true;
                        proc.OutputDataReceived +=
                            (o, e) => runResults.Output.Append(e.Data).Append(Environment.NewLine);
                        proc.ErrorDataReceived +=
                            (o, e) => runResults.Error.Append(e.Data).Append(Environment.NewLine);
                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        proc.WaitForExit();
                        runResults.ExitCode = proc.ExitCode;
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid executable path.", "exePath");
                }
            }
            catch (Exception e)
            {
                runResults.RunException = e;
            }
            return runResults;
        }
    }
}
