using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace System.IO.LongPath
{    
    public static class Directory
    {        
        /// <summary>
        /// Create a directory with the given name.
        /// </summary>
        /// <param name="directory">Path of directory to be created.  Must begin with \\?\ for local paths or \\?\UNC\ for network paths.</param>
        public static void Create(string directory)
        {
            string pathLower = directory.ToLower();
            string uncStarter = slash + slash + "?" + slash + "unc";
            bool unc = pathLower.StartsWith(uncStarter);

            List<string> dirsSplit = new List<string>();

            dirsSplit.AddRange(directory.Split(new char[] { cslash }, StringSplitOptions.RemoveEmptyEntries));

            string sSubPath = slash + slash + "?" + slash;

            int i;
            if (unc)
            {
                sSubPath += "UNC" + slash + dirsSplit[2] + slash;
                i = 3;
            }
            else
            {
                sSubPath += dirsSplit[1] + slash;
                i = 2;
            }
            
            for (; i < dirsSplit.Count; i++)
            {
                sSubPath += (dirsSplit[i]+slash);
                if (!Exists(sSubPath))
                {
                    bool result = CreateDirectory(sSubPath, IntPtr.Zero);
                    int lastWin32Error = Marshal.GetLastWin32Error();
                    if (!result)
                    {
                        throw new System.ComponentModel.Win32Exception(lastWin32Error);
                    }
                }
            }
        }

        /// <summary>
        /// Deletes the specified directory and all sub-directories and files.
        /// </summary>
        /// <param name="directory">Directory to be deleted.  Must begin with \\?\ for local paths or \\?\UNC\ for network paths.</param>
        public static void Delete(string directory)
        {
            WIN32_FIND_DATA findData;

            IntPtr findHandle = FindFirstFile(directory + @"\*", out findData);

            if (findHandle != INVALID_HANDLE_VALUE)
            {
                bool found;

                do
                {
                    string currentFileName = findData.cFileName;

                    // if this is a directory, find its contents
                    if (((int)findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                    {
                        if (currentFileName != "." && currentFileName != "..")
                        {
                            string adir = Path.Combine(directory, currentFileName);
                            Delete(adir);
                            bool result = RemoveDirectory(adir);
                            int lastWin32Error = Marshal.GetLastWin32Error();
                            if (!result)
                            {
                                throw new System.ComponentModel.Win32Exception(lastWin32Error);
                            }
                        }
                    }
                    else // it's a file; add it to the results
                    {
                        File.Delete(Path.Combine(directory, currentFileName));
                    }

                    // find next
                    found = FindNextFile(findHandle, out findData);
                }
                while (found);
            }

            // close the find handle
            FindClose(findHandle);
        }

        /// <summary>
        /// Check if the specified directory exists.
        /// </summary>
        /// <param name="directory">Path of directory to check.  Must begin with \\?\ for local paths or \\?\UNC\ for network paths.</param>
        /// <returns></returns>
        public static bool Exists(string directory)
        {
            FileAttributes fa = GetFileAttributes(directory);
            if ((int)fa == -1)
            {
                return false;
            }
            return fa.HasFlag(FileAttributes.FILE_ATTRIBUTE_DIRECTORY);
        }

        /// <summary>
        /// Gets file system entries for the directory provided by the directory argument.  This is not a recursive function only file system entries for the provided directory are returned.
        /// </summary>
        /// <param name="directory">Directory to return results for.</param>
        /// <returns>An array of file system entries from the provided directory.</returns>
        public static string[] GetFileSystemEntries(string directory)
        {
            List<string> results = new List<string>();
            WIN32_FIND_DATA findData;
            IntPtr findHandle = FindFirstFile(directory + @"\*", out findData);

            if (findHandle != INVALID_HANDLE_VALUE)
            {
                bool found;

                do
                {
                    string currentFileName = findData.cFileName;

                    // if this is a directory, find its contents
                    if (((int)findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                    {
                        if (currentFileName != "." && currentFileName != "..")
                        {
                            results.Add(Path.Combine(directory, currentFileName));
                        }
                    }
                    else
                    {
                        results.Add(Path.Combine(directory, currentFileName));
                    }

                    // find next
                    found = FindNextFile(findHandle, out findData);
                }
                while (found);
            }

            // close the find handle
            FindClose(findHandle);

            return results.ToArray();
        }


        #region constants
        internal const int MAX_PATH = 260;
        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        internal const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        #endregion

        #region externs
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FindClose(IntPtr hFindFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern FileAttributes GetFileAttributes(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveDirectory(string lpPathName);
        #endregion

        #region enums
        [Flags]
        internal enum FileAttributes
        {
            FILE_ATTRIBUTE_READONLY = 1,
            FILE_ATTRIBUTE_HIDDEN = 2,
            FILE_ATTRIBUTE_SYSTEM = 4,
            FILE_ATTRIBUTE_DIRECTORY = 16,
            FILE_ATTRIBUTE_ARCHIVE = 32,
            FILE_ATTRIBUTE_DEVICE = 64,
            FILE_ATTRIBUTE_NORMAL = 128,
            FILE_ATTRIBUTE_TEMPORARY = 256,
            FILE_ATTRIBUTE_SPARSE_FILE = 512,
            FILE_ATTRIBUTE_REPARSE_POINT = 1024,
            FILE_ATTRIBUTE_COMPRESSED = 2048,
            FILE_ATTRIBUTE_OFFLINE = 4096,
            FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 8192,
            FILE_ATTRIBUTE_ENCRYPTED = 16384,
            FILE_ATTRIBUTE_VIRTUAL = 65536
        }
        #endregion

        #region helpers
        private static readonly string slash = System.IO.Path.DirectorySeparatorChar.ToString();
        private static readonly char cslash = System.IO.Path.DirectorySeparatorChar;
        #endregion

        #region structs
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WIN32_FIND_DATA
        {
            internal System.IO.FileAttributes dwFileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            internal int nFileSizeHigh;
            internal int nFileSizeLow;
            internal int dwReserved0;
            internal int dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            internal string cFileName;
            // not using this
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            internal string cAlternate;
        }
        #endregion
    }
    
    public class File
    {
        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="file">Path of file to be deleted.  Must begin with \\?\ for local paths or \\?\UNC\ for network paths.</param>
        public static void Delete(string file)
        {
            DeleteFile(file);
        }

        /// <summary>
        /// Opens a file stream to the file specified in the path argument.
        /// </summary>
        /// <param name="path">Path to file to open.  Must begin with \\?\ for local paths or \\?\UNC\ for network paths.</param>
        /// <param name="mode">Specifies how the file is opened or if existing data in the file is retained.</param>
        /// <param name="access">Specifies type of access to the file, read, write or both.</param>
        /// <param name="share">Specifies share access capabilities of subsequent open calls to this file.</param>
        /// <returns>A FileStream to the file specified in the path argument.</returns>
        public static System.IO.FileStream Open(string path, System.IO.FileMode mode, System.IO.FileAccess access, System.IO.FileShare share)
        {
            System.IO.FileStream result = null;

            SafeFileHandle fileHandle = CreateFile(path, getAccessFromAccess(access), getShareFromShare(share), IntPtr.Zero, getDispositionFromMode(mode), 0, IntPtr.Zero);
            int lastWin32Error = Marshal.GetLastWin32Error();
            if (fileHandle.IsInvalid)
            {
                throw new System.ComponentModel.Win32Exception(lastWin32Error);
            }
            result = new System.IO.FileStream(fileHandle, access);
            if (mode == System.IO.FileMode.Append)
            {
                result.Seek(0, System.IO.SeekOrigin.End);
            }
            return result;
        }


        public static void Copy(string sourceFile, string destinationFile, bool failIfDestinationExists)
        {
            bool result = CopyFile(sourceFile, destinationFile, failIfDestinationExists);
            int lastWin32Error = Marshal.GetLastWin32Error();
            if (!result)
            {
                throw new System.ComponentModel.Win32Exception(lastWin32Error);
            }
        }
        public static void Move(string sourceFile, string destinationFile, bool failIfDestinationExists)
        {
            Copy(sourceFile, destinationFile, failIfDestinationExists);
            Delete(sourceFile);
        }

        private static EFileShare getShareFromShare(System.IO.FileShare share)
        {
            switch (share)
            {
                case System.IO.FileShare.Delete:
                    return EFileShare.Delete;
                case System.IO.FileShare.Inheritable:
                    throw new NotSupportedException("Inheritible is not supported.");
                case System.IO.FileShare.None:
                    return EFileShare.None;
                case System.IO.FileShare.Read:
                    return EFileShare.Read;
                case System.IO.FileShare.ReadWrite:
                    return EFileShare.Read | EFileShare.Write;
                case System.IO.FileShare.Write:
                    return EFileShare.Write;
            }
            throw new NotSupportedException();
        }
        private static EFileAccess getAccessFromAccess(System.IO.FileAccess access)
        {
            switch (access)
            {
                case System.IO.FileAccess.Read:
                    return EFileAccess.GenericRead;
                case System.IO.FileAccess.Write:
                    return EFileAccess.GenericWrite;
                case System.IO.FileAccess.ReadWrite:
                    return EFileAccess.GenericRead | EFileAccess.GenericWrite;
            }
            throw new NotSupportedException();
        }
        private static ECreationDisposition getDispositionFromMode(System.IO.FileMode mode)
        {
            switch (mode)
            {
                case System.IO.FileMode.Create:
                    return  ECreationDisposition.CreateAlways;
                case System.IO.FileMode.CreateNew:
                    return ECreationDisposition.New;
                case System.IO.FileMode.Open:
                    return ECreationDisposition.OpenExisting;
                case System.IO.FileMode.OpenOrCreate:
                    return ECreationDisposition.OpenAlways;
                case System.IO.FileMode.Truncate:
                    return ECreationDisposition.TruncateExisting;
                case System.IO.FileMode.Append:
                    return ECreationDisposition.OpenAlways;
            }
            throw new NotSupportedException();
        }

        #region constants
        internal static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        internal static int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        #endregion

        #region externs
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteFile(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFileHandle CreateFile(
            string lpFileName,
            EFileAccess dwDesiredAccess,
            EFileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            ECreationDisposition dwCreationDisposition,
            EFileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CopyFile(string lpExistingFileName, string lpNewFileName, bool bFailIfExists);
        #endregion

        #region enums
        [Flags]
        public enum EFileAccess : uint
        {
            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,
        }
        [Flags]
        public enum EFileShare : uint
        {
            None = 0x00000000,
            Read = 0x00000001,
            Write = 0x00000002,
            Delete = 0x00000004,
        }
        public enum ECreationDisposition : uint
        {
            New = 1,
            CreateAlways = 2,
            OpenExisting = 3,
            OpenAlways = 4,
            TruncateExisting = 5,
        }
        [Flags]
        public enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }
        #endregion

        #region structs
        [StructLayout(LayoutKind.Sequential)]
        internal struct FILETIME
        {
            internal uint dwLowDateTime;
            internal uint dwHighDateTime;
        };
        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }
        #endregion
    }


}
