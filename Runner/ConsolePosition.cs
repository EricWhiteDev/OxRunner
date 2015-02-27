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

        private static int m_TopOffset = 500;
        private static int m_LeftOffset = 5;

        private static int m_Width = 120;
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
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 4,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 5,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 6,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 7,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 8,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 9,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 10,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 11,
                Height = m_Height,
                Width = m_Width,
            },
            new ConsoleRectangle() {
                Top = m_TopOffset,
                Left = m_LeftOffset + m_Width * 12,
                Height = m_Height,
                Width = m_Width,
            },
        };

        public static void SetControllerDaemonConsolePosition(int height, int width)
        {
            SetWindowPos(MyConsole, 0, m_LeftOffset, m_TopOffset, 0, 0, SWP_NOSIZE);
            Console.SetWindowSize(width, height);
        }

        public static void SetControllerMasterConsolePosition(int height, int width)
        {
            SetWindowPos(MyConsole, 0, m_LeftOffset, 10, 0, 0, SWP_NOSIZE);
            Console.SetWindowSize(width, height);
        }

        public static void SetConsolePosition(int positionNumber)
        {
            MoveWindow(m_ConsoleInfo[positionNumber]);
        }

        private static void MoveWindow(ConsoleRectangle rect)
        {
            SetWindowPos(MyConsole, 0, rect.Left, rect.Top, 0, 0, SWP_NOSIZE);
            Console.SetWindowSize(ConsoleConstants.Width, ConsoleConstants.Height);
        }
    }
}
