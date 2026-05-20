using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Shaiya_Invasion_Updater
{
    public static class MemoryManager
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        public static byte[] ReadMemory(int processId, int offset, int length)
        {
            IntPtr h = OpenProcess(ProcessAccessFlags.VMRead | ProcessAccessFlags.QueryInformation, false, processId);
            if (h == IntPtr.Zero) throw new InvalidOperationException("OpenProcess failed for read. Win32=" + Marshal.GetLastWin32Error());
            try
            {
                byte[] buffer = new byte[length];
                IntPtr read;
                if (!ReadProcessMemory(h, (IntPtr)offset, buffer, length, out read))
                    throw new InvalidOperationException("ReadProcessMemory failed. Win32=" + Marshal.GetLastWin32Error());
                return buffer;
            }
            finally { CloseHandle(h); }
        }

        public static bool WriteMemory(int processId, int offset, byte[] buffer)
        {
            IntPtr h = OpenProcess(ProcessAccessFlags.VMOperation | ProcessAccessFlags.VMWrite | ProcessAccessFlags.QueryInformation, false, processId);
            if (h == IntPtr.Zero) throw new InvalidOperationException("OpenProcess failed for write. Win32=" + Marshal.GetLastWin32Error());
            try
            {
                IntPtr written;
                if (!WriteProcessMemory(h, (IntPtr)offset, buffer, buffer.Length, out written))
                    throw new InvalidOperationException("WriteProcessMemory failed. Win32=" + Marshal.GetLastWin32Error());
                return written.ToInt32() == buffer.Length;
            }
            finally { CloseHandle(h); }
        }

        public static void WriteFixedAscii(int processId, int offset, string value, int length)
        {
            byte[] buffer = new byte[length];
            if (!string.IsNullOrEmpty(value))
            {
                byte[] data = Encoding.ASCII.GetBytes(value);
                Buffer.BlockCopy(data, 0, buffer, 0, Math.Min(data.Length, length - 1));
            }
            WriteMemory(processId, offset, buffer);
        }

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            Terminate = 0x0001,
            CreateThread = 0x0002,
            VMOperation = 0x0008,
            VMRead = 0x0010,
            VMWrite = 0x0020,
            QueryInformation = 0x0400,
            Synchronize = 0x00100000,
            All = 0x001F0FFF
        }
    }
}
