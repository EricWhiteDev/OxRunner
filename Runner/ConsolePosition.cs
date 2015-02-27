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

        private static int m_TopOffsetMaster = 600;
        private static int m_TopOffsetNonMaster = 100;
        private static int m_LeftOffset = 5;

        private static int m_Width = 120;
        private static int m_Height = 500;

        public static void SetControllerDaemonConsolePosition(int height, int width)
        {
            SetWindowPos(MyConsole, 0, m_LeftOffset, m_TopOffsetNonMaster, 0, 0, SWP_NOSIZE);
            Console.SetWindowSize(width, height);
        }

        public static void SetControllerMasterConsolePosition(int height, int width)
        {
            SetWindowPos(MyConsole, 0, m_LeftOffset, 10, 0, 0, SWP_NOSIZE);
            Console.SetWindowSize(width, height);
        }

        public static void SetConsolePosition(int positionNumber, bool isMasterMachine)
        {
            ConsoleRectangle cr = null;
            if (isMasterMachine)
                cr = new ConsoleRectangle()
                {
                    Top = m_TopOffsetMaster,
                    Left = m_LeftOffset + m_Width * positionNumber,
                    Height = m_Height,
                    Width = m_Width,
                };
            else
                cr = new ConsoleRectangle()
                {
                    Top = m_TopOffsetNonMaster,
                    Left = m_LeftOffset + m_Width * positionNumber,
                    Height = m_Height,
                    Width = m_Width,
                };

            MoveWindow(cr);
        }

        private static void MoveWindow(ConsoleRectangle rect)
        {
            SetWindowPos(MyConsole, 0, rect.Left, rect.Top, 0, 0, SWP_NOSIZE);
            Console.SetWindowSize(ConsoleConstants.Width, ConsoleConstants.Height);
        }
    }
}
