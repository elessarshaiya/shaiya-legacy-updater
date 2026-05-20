using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Shaiya_Invasion_Updater
{
    public static class GameServerSelectAutomation
    {
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_NOACCESS = 0x01;
        private const uint PAGE_GUARD = 0x100;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_WRITECOPY = 0x08;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PAGE_EXECUTE_WRITECOPY = 0x80;

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", EntryPoint = "WriteProcessMemory", SetLastError = true)]
        private static extern bool NativeWriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        public static bool AutoSelectFirstServer(int processId)
        {
            if (!AppConfig.AutoSelectServer)
            {
                AppLogger.Info("Auto server select disabled by launcher.ini.");
                return false;
            }

            int delay = Math.Max(0, AppConfig.AutoSelectServerDelayMs);
            if (delay > 0)
            {
                AppLogger.Info("Auto server direct-select waiting " + delay + "ms before object scan.");
                Thread.Sleep(delay);
            }

            for (int i = 0; i < AppConfig.AutoSelectServerRetries; i++)
            {
                try
                {
                    ServerListState serverState;
                    if (!TryGetServerListState(processId, AppConfig.AutoSelectServerIndex, out serverState))
                    {
                        AppLogger.Info("Auto server direct-select wait: server list not ready yet. Attempt=" + (i + 1));
                        Thread.Sleep(AppConfig.AutoSelectServerRetryDelayMs);
                        continue;
                    }

                    int obj = FindSelectServerObject(processId);
                    if (obj == 0)
                    {
                        AppLogger.Info("Auto server direct-select wait: CSelectServer object not found yet. Attempt=" + (i + 1));
                        Thread.Sleep(AppConfig.AutoSelectServerRetryDelayMs);
                        continue;
                    }

                    AppLogger.Info("CSelectServer object found at 0x" + obj.ToString("X8") +
                                   ". ServerList=0x" + serverState.ListPointer.ToString("X8") +
                                   ", Count=" + serverState.Count +
                                   ". Direct-selecting index " + AppConfig.AutoSelectServerIndex + ".");

                    SetSelectedIndexFields(processId, obj, AppConfig.AutoSelectServerIndex);
                    InstallOneShotSelectHook(processId);
                    AppLogger.Info("Auto server direct-select hook installed. UI thread will call CSelectServer::OnSelect once.");
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Auto server direct-select attempt " + (i + 1) + " failed: " + ex.Message);
                }

                Thread.Sleep(AppConfig.AutoSelectServerRetryDelayMs);
            }

            AppLogger.Warn("Auto server direct-select failed: ready CSelectServer object/server-list was not found.");
            return false;
        }

        private struct ServerListState
        {
            public int GameStatePointer;
            public int ListPointer;
            public int Count;
        }

        private static bool TryGetServerListState(int processId, int serverIndex, out ServerListState state)
        {
            state = new ServerListState();

            try
            {
                int gameState = ReadInt32(processId, GameOffsets.SelectServerGlobalStatePointer);
                if (gameState == 0) return false;

                int listPtr = ReadInt32(processId, gameState + GameOffsets.SelectServerListPointerOffset);
                int count = ReadInt32(processId, gameState + GameOffsets.SelectServerCountOffset);

                if (listPtr == 0) return false;
                if (count <= serverIndex || count <= 0 || count > 128) return false;

                MemoryManager.ReadMemory(processId, listPtr + (serverIndex * GameOffsets.SelectServerEntrySize), 8);

                state.GameStatePointer = gameState;
                state.ListPointer = listPtr;
                state.Count = count;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int FindSelectServerObject(int processId)
        {
            IntPtr h = MemoryManager.OpenProcess(
                MemoryManager.ProcessAccessFlags.VMRead | MemoryManager.ProcessAccessFlags.QueryInformation,
                false,
                processId);
            if (h == IntPtr.Zero)
                throw new InvalidOperationException("OpenProcess failed for select-server scan. Win32=" + Marshal.GetLastWin32Error());

            try
            {
                byte[] needle = BitConverter.GetBytes(GameOffsets.SelectServerVTable);
                IntPtr address = IntPtr.Zero;
                int mbiSize = Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

                while (VirtualQueryEx(h, address, out MEMORY_BASIC_INFORMATION mbi, (uint)mbiSize) != 0)
                {
                    long baseAddr = mbi.BaseAddress.ToInt64();
                    long size = mbi.RegionSize.ToInt64();
                    long next = baseAddr + size;

                    if (IsReadableWritableCommitted(mbi) && size > 4 && size < 64L * 1024L * 1024L)
                    {
                        int found = SearchRegion(processId, baseAddr, (int)Math.Min(size, int.MaxValue), needle);
                        while (found != 0)
                        {
                            if (ValidateSelectServerObject(processId, found)) return found;

                            int skip = (found - (int)baseAddr) + 4;
                            if (skip >= size) break;
                            found = SearchRegion(processId, baseAddr + skip, (int)Math.Min(size - skip, int.MaxValue), needle);
                        }
                    }

                    if (next <= address.ToInt64()) break;
                    if (next > 0x7FFFFFFF) break;
                    address = new IntPtr(next);
                }
            }
            finally
            {
                MemoryManager.CloseHandle(h);
            }

            return 0;
        }

        private static bool IsReadableWritableCommitted(MEMORY_BASIC_INFORMATION mbi)
        {
            if (mbi.State != MEM_COMMIT) return false;
            if ((mbi.Protect & PAGE_GUARD) != 0 || (mbi.Protect & PAGE_NOACCESS) != 0) return false;

            uint p = mbi.Protect & 0xFF;
            return p == PAGE_READWRITE || p == PAGE_WRITECOPY || p == PAGE_EXECUTE_READWRITE || p == PAGE_EXECUTE_WRITECOPY;
        }

        private static int SearchRegion(int processId, long baseAddress, int size, byte[] needle)
        {
            const int chunkSize = 0x10000;
            byte[] carry = new byte[needle.Length - 1];
            int carryLen = 0;

            for (int offset = 0; offset < size; offset += chunkSize)
            {
                int toRead = Math.Min(chunkSize, size - offset);
                byte[] chunk;
                try
                {
                    chunk = MemoryManager.ReadMemory(processId, unchecked((int)(baseAddress + offset)), toRead);
                }
                catch
                {
                    carryLen = 0;
                    continue;
                }

                byte[] combined = new byte[carryLen + chunk.Length];
                if (carryLen > 0) Buffer.BlockCopy(carry, 0, combined, 0, carryLen);
                Buffer.BlockCopy(chunk, 0, combined, carryLen, chunk.Length);

                int pos = IndexOf(combined, needle);
                if (pos >= 0)
                {
                    long found = baseAddress + offset - carryLen + pos;
                    if (found > 0 && found <= 0x7FFFFFFF) return (int)found;
                }

                carryLen = Math.Min(carry.Length, combined.Length);
                if (carryLen > 0) Buffer.BlockCopy(combined, combined.Length - carryLen, carry, 0, carryLen);
            }

            return 0;
        }

        private static int IndexOf(byte[] data, byte[] needle)
        {
            for (int i = 0; i <= data.Length - needle.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (data[i + j] != needle[j]) { ok = false; break; }
                }
                if (ok) return i;
            }
            return -1;
        }

        private static bool ValidateSelectServerObject(int processId, int objectAddress)
        {
            try
            {
                int vt = ReadInt32(processId, objectAddress);
                if (vt != GameOffsets.SelectServerVTable) return false;

                int a = ReadInt32(processId, objectAddress + GameOffsets.SelectServerSelectedIndex1);
                int b = ReadInt32(processId, objectAddress + GameOffsets.SelectServerSelectedIndex2);

                if (a != b) return false;
                return a >= -1 && a < 128;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadInt32(int processId, int address)
        {
            return BitConverter.ToInt32(MemoryManager.ReadMemory(processId, address, 4), 0);
        }

        private static void SetSelectedIndexFields(int processId, int objectAddress, int serverIndex)
        {
            byte[] idx = BitConverter.GetBytes(serverIndex);
            MemoryManager.WriteMemory(processId, objectAddress + GameOffsets.SelectServerSelectedIndex1, idx);
            MemoryManager.WriteMemory(processId, objectAddress + GameOffsets.SelectServerSelectedIndex2, idx);
            AppLogger.Info("CSelectServer selected index fields written: +0x" +
                           GameOffsets.SelectServerSelectedIndex1.ToString("X") + " / +0x" +
                           GameOffsets.SelectServerSelectedIndex2.ToString("X") + " = " + serverIndex);
        }

        private static void InstallOneShotSelectHook(int processId)
        {
            const int hookAddress = GameOffsets.SelectServerUpdateInputBranch;
            const int hookLength = 8;

            byte[] current = MemoryManager.ReadMemory(processId, hookAddress, hookLength);
            if (LooksLikeJmp(current))
            {
                AppLogger.Info("SelectServer one-shot hook already installed or patched. Skipping re-install.");
                return;
            }

            if (!BytesEqual(current, GameOffsets.SelectServerUpdateInputBranchOriginalBytes))
            {
                throw new InvalidOperationException("SelectServer input branch original bytes mismatch at 0x" + hookAddress.ToString("X8") +
                                                    ". Found=" + ToHex(current));
            }

            IntPtr h = MemoryManager.OpenProcess(
                MemoryManager.ProcessAccessFlags.VMOperation |
                MemoryManager.ProcessAccessFlags.VMWrite |
                MemoryManager.ProcessAccessFlags.VMRead |
                MemoryManager.ProcessAccessFlags.QueryInformation,
                false,
                processId);
            if (h == IntPtr.Zero)
                throw new InvalidOperationException("OpenProcess failed for one-shot hook. Win32=" + Marshal.GetLastWin32Error());

            try
            {
                IntPtr cave = VirtualAllocEx(h, IntPtr.Zero, 128, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE);
                if (cave == IntPtr.Zero) throw new InvalidOperationException("VirtualAllocEx failed for one-shot hook. Win32=" + Marshal.GetLastWin32Error());

                int caveAddr = cave.ToInt32();
                int flagAddr = caveAddr + 96;

                byte[] caveCode = BuildOneShotCave(caveAddr, flagAddr);
                byte[] caveBlob = new byte[100];
                Buffer.BlockCopy(caveCode, 0, caveBlob, 0, caveCode.Length);
                Buffer.BlockCopy(BitConverter.GetBytes(1), 0, caveBlob, 96, 4);

                IntPtr written;
                if (!NativeWriteProcessMemory(h, cave, caveBlob, caveBlob.Length, out written) || written.ToInt32() != caveBlob.Length)
                    throw new InvalidOperationException("WriteProcessMemory failed for one-shot cave. Win32=" + Marshal.GetLastWin32Error());

                byte[] jmp = BuildJmp(hookAddress, caveAddr, hookLength);
                if (!MemoryManager.WriteMemory(processId, hookAddress, jmp))
                    throw new InvalidOperationException("WriteMemory failed for one-shot hook patch.");

                AppLogger.Info("SelectServer one-shot UI-thread hook installed: branch=0x" + hookAddress.ToString("X8") +
                               ", cave=0x" + caveAddr.ToString("X8") + ", flag=0x" + flagAddr.ToString("X8"));
            }
            finally
            {
                MemoryManager.CloseHandle(h);
            }
        }

        private static byte[] BuildOneShotCave(int caveAddr, int flagAddr)
        {
            List<byte> c = new List<byte>();

            // cmp dword ptr [flag], 1
            c.Add(0x81); c.Add(0x3D); c.AddRange(BitConverter.GetBytes(flagAddr)); c.AddRange(BitConverter.GetBytes(1));

            int jneNotForcedAt = caveAddr + c.Count;
            c.Add(0x0F); c.Add(0x85); c.AddRange(new byte[4]);

            // mov dword ptr [flag], 0
            c.Add(0xC7); c.Add(0x05); c.AddRange(BitConverter.GetBytes(flagAddr)); c.AddRange(BitConverter.GetBytes(0));

            int jmpForcedAt = caveAddr + c.Count;
            c.Add(0xE9); c.AddRange(Rel32(jmpForcedAt, GameOffsets.SelectServerUpdateCallOnSelectBlock, 5));

            int notForcedAddr = caveAddr + c.Count;

            // original behavior when flag is already consumed:
            // test eax,eax
            c.Add(0x85); c.Add(0xC0);

            // jne 0x0050CCCE
            int jneOriginalAt = caveAddr + c.Count;
            c.Add(0x0F); c.Add(0x85); c.AddRange(Rel32(jneOriginalAt, GameOffsets.SelectServerUpdateCallOnSelectBlock, 6));

            // jmp 0x0050CC09
            int jmpContinueAt = caveAddr + c.Count;
            c.Add(0xE9); c.AddRange(Rel32(jmpContinueAt, GameOffsets.SelectServerUpdateInputBranchContinue, 5));

            byte[] rel = Rel32(jneNotForcedAt, notForcedAddr, 6);
            for (int i = 0; i < 4; i++) c[(jneNotForcedAt - caveAddr) + 2 + i] = rel[i];

            return c.ToArray();
        }

        private static byte[] BuildJmp(int from, int to, int length)
        {
            byte[] result = new byte[length];
            result[0] = 0xE9;
            Buffer.BlockCopy(Rel32(from, to, 5), 0, result, 1, 4);
            for (int i = 5; i < result.Length; i++) result[i] = 0x90;
            return result;
        }

        private static byte[] Rel32(int instructionAddress, int targetAddress, int instructionLength)
        {
            return BitConverter.GetBytes(targetAddress - (instructionAddress + instructionLength));
        }

        private static bool LooksLikeJmp(byte[] bytes)
        {
            return bytes != null && bytes.Length > 0 && bytes[0] == 0xE9;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null) return "<null>";
            string[] parts = new string[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) parts[i] = bytes[i].ToString("X2");
            return string.Join(" ", parts);
        }
    }
}
