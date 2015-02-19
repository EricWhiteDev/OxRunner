using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;


namespace OxRun
{
    public class ConsoleRectangle
    {
        public int Top;
        public int Left;
        public int Width;
        public int Height;
    }

    // these are in characters
    public class ConsoleConstants
    {
        public static int Width = 55;
        public static int Height = 24;
    }
    
    public class ConsolePosition
    {
        const int SWP_NOSIZE = 0x0001;

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        private static IntPtr MyConsole = GetConsoleWindow();

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        private static int m_TopOffset = 5;
        private static int m_LeftOffset = 5;

        private static int m_Width = 320; // should match up to ConsoleConstants, which is in characters
        private static int m_Height = 500;

        static ConsoleRectangle[] m_ConsoleInfo = new[] {
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 1,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 2,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 3,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset + m_Height,
                Left = m_LeftOffset,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset + m_Height,
                Left = m_LeftOffset + m_Width * 1,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset + m_Height,
                Left = m_LeftOffset + m_Width * 2,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset + m_Height,
                Left = m_LeftOffset + m_Width * 3,
                Height = m_Height,
                Width = m_Width,
            },
        };

        public static void SetControllerMasterConsolePosition(int height, int width)
        {
            SetWindowPos(MyConsole, 0, m_LeftOffset, m_TopOffset, 0, 0, SWP_NOSIZE);
            Console.SetWindowSize(width, height);
        }

        private static string m_LastConsolePositionFileName = "LastConsolePosition.log";

        public static void SetConsolePosition()
        {
            int lastConsolePosition;

            DirectoryInfo dir = GetOxRunnerDirectory();
            if (dir == null)
                return;
            var fiCountFile = new FileInfo(Path.Combine(dir.FullName, m_LastConsolePositionFileName));
            if (!fiCountFile.Exists)
            {
                lastConsolePosition = 1;
                File.WriteAllText(fiCountFile.FullName, lastConsolePosition.ToString());
                MoveWindow(m_ConsoleInfo[lastConsolePosition - 1]);
            }
            string t;
            while (true)
            {
                bool updatedValue = false;
                while (true)
                {
                    try
                    {
                        t = File.ReadAllText(fiCountFile.FullName);
                        break;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(50);
                    }
                }
                int parsedValue;
                if (int.TryParse(t, out parsedValue))
                    lastConsolePosition = parsedValue;
                else
                    lastConsolePosition = 1;
                int nextConsolePosition = lastConsolePosition + 1;
                if (nextConsolePosition == 8)
                    nextConsolePosition = 1;
                try
                {
                    File.WriteAllText(fiCountFile.FullName, nextConsolePosition.ToString());
                    updatedValue = true;
                }
                catch (IOException)
                {
                    updatedValue = false; // try again, race condition with another process
                }
                if (updatedValue)
                    break;
            }
            MoveWindow(m_ConsoleInfo[lastConsolePosition - 1]);
        }

        private static void MoveWindow(ConsoleRectangle rect)
        {
            SetWindowPos(MyConsole, 0, rect.Left, rect.Top, 0, 0, SWP_NOSIZE);
            Console.SetWindowSize(ConsoleConstants.Width, ConsoleConstants.Height);
        }

        public static void ResetConsolePositioning()
        {
            DirectoryInfo dir = GetOxRunnerDirectory();
            if (dir == null)
                return;
            var fiCountFile = new FileInfo(Path.Combine(dir.FullName, m_LastConsolePositionFileName));
            if (fiCountFile.Exists)
                fiCountFile.Delete();
        }

        private static DirectoryInfo GetOxRunnerDirectory()
        {
            var dir = new DirectoryInfo(Environment.CurrentDirectory);
            while (true)
            {
                if (dir.FullName.EndsWith("OxRunner"))
                    break;
                if (dir.Parent == null)
                    return null;
                dir = dir.Parent;
            }
            return dir;
        }

    }
}
