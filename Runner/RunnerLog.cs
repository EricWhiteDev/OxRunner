using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OxRun
{
    public class RunnerLog
    {
        public FileInfo m_FiLog;
        public bool m_WriteLog = true;

        public RunnerLog(string prefix)
        {
            m_FiLog = FileUtils.GetDateTimeStampedFileInfo(prefix, ".log");
            Console.WriteLine("Creating log " + m_FiLog.FullName);
        }

        public void Log(ConsoleColor color, string text)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            if (m_WriteLog)
                File.AppendAllLines(m_FiLog.FullName, new[] { text });
        }
    }
}
