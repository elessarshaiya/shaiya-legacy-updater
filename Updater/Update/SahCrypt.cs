using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Shaiya_Invasion_Updater
{
    public enum SahCryptState
    {
        Unknown = 0,
        PlainSah = 1,
        EncryptedEsah = 2,
        LegacyRolSah = 3
    }

    /// <summary>
    /// Compatible implementation of the supplied SahCryptTool.
    /// Format: 64 byte ESAH header + AES-256-GCM ciphertext.
    /// Magic: ESAH, Version: 1, Flags: 1, Nonce: 12 bytes, Tag: 16 bytes.
    /// The AES key is not embedded in the updater; it is loaded from server-side launcher.ini.
    /// </summary>
    public static class SahCrypt
    {
        private const ushort FormatVersion = 1;
        private const ushort FlagAes256Gcm = 1;
        private const int HeaderSize = 64;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private static readonly byte[] PlainMagic = Encoding.ASCII.GetBytes("SAH");
        private static readonly byte[] EncryptedMagic = Encoding.ASCII.GetBytes("ESAH");

        // The key is intentionally not embedded here.
        // It is resolved at runtime from server-side launcher.ini.


        public static SahCryptState GetState(string path)
        {
            try
            {
                if (!File.Exists(path)) return SahCryptState.Unknown;
                byte[] data = ReadPrefix(path, HeaderSize);
                if (StartsWith(data, PlainMagic)) return SahCryptState.PlainSah;
                if (StartsWith(data, EncryptedMagic)) return SahCryptState.EncryptedEsah;

                // Compatibility: older updater SAH files are whole-file ROL/XOR obfuscated.
                // The supplied SahCryptTool does not create this format, but old patches/data may still have it.
                byte[] full = File.ReadAllBytes(path);
                byte[] legacyPlain = LegacyDecryptBuffer(full);
                if (StartsWith(legacyPlain, PlainMagic)) return SahCryptState.LegacyRolSah;

                return SahCryptState.Unknown;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("SAH crypt state failed for " + path + ": " + ex.Message);
                return SahCryptState.Unknown;
            }
        }

        public static bool UnlockIfNeeded(string path, string ignoredKey)
        {
            return DecryptInPlaceIfNeeded(path);
        }

        public static bool ProtectIfNeeded(string path, string ignoredKey)
        {
            return EncryptInPlaceIfNeeded(path);
        }

        public static bool DecryptInPlaceIfNeeded(string path)
        {
            if (!File.Exists(path)) return false;
            SahCryptState state = GetState(path);
            if (state == SahCryptState.PlainSah) return false;

            byte[] input = File.ReadAllBytes(path);
            byte[] plain;
            string backupSuffix;

            if (state == SahCryptState.EncryptedEsah)
            {
                plain = DecryptBuffer(input);
                backupSuffix = ".enc";
            }
            else if (state == SahCryptState.LegacyRolSah)
            {
                plain = LegacyDecryptBuffer(input);
                backupSuffix = ".legacy";
            }
            else
            {
                throw new InvalidDataException("Unsupported SAH header in " + Path.GetFileName(path) + ". Expected SAH, ESAH or legacy updater SAH.");
            }

            if (!StartsWith(plain, PlainMagic))
                throw new InvalidDataException("SAH decrypt/normalize succeeded but output is not a valid SAH file.");

            ReplaceInPlace(path, plain, BuildBackupPath(path, backupSuffix));
            AppLogger.Info("SAH normalized to plain SAH: " + path);
            return true;
        }

        public static bool EncryptInPlaceIfNeeded(string path)
        {
            if (!File.Exists(path)) return false;
            SahCryptState state = GetState(path);
            if (state == SahCryptState.EncryptedEsah) return false;

            byte[] plain;
            if (state == SahCryptState.PlainSah)
            {
                plain = File.ReadAllBytes(path);
            }
            else if (state == SahCryptState.LegacyRolSah)
            {
                plain = LegacyDecryptBuffer(File.ReadAllBytes(path));
            }
            else
            {
                throw new InvalidDataException("Unsupported SAH header in " + Path.GetFileName(path) + ". Expected SAH, ESAH or legacy updater SAH.");
            }

            byte[] encrypted = EncryptBuffer(plain);
            ReplaceInPlace(path, encrypted, BuildBackupPath(path, ".dec"));
            AppLogger.Info("SAH encrypted to ESAH: " + path);
            return true;
        }

        public static byte[] EncryptBuffer(byte[] plain)
        {
            if (plain == null) throw new ArgumentNullException("plain");
            if (!StartsWith(plain, PlainMagic))
                throw new InvalidDataException("Input does not look like plain Data.sah. SAH header is missing.");

            byte[] nonce = new byte[NonceSize];
            BCryptRandom(nonce);

            byte[] tag;
            byte[] cipher = AesGcmCrypt(true, plain, nonce, null, out tag);

            byte[] output = new byte[HeaderSize + cipher.Length];
            Buffer.BlockCopy(EncryptedMagic, 0, output, 0, 4);
            WriteUInt16(output, 4, FormatVersion);
            WriteUInt16(output, 6, FlagAes256Gcm);
            WriteUInt64(output, 8, (ulong)plain.Length);
            Buffer.BlockCopy(nonce, 0, output, 16, NonceSize);
            Buffer.BlockCopy(tag, 0, output, 28, TagSize);
            Buffer.BlockCopy(cipher, 0, output, HeaderSize, cipher.Length);
            return output;
        }

        public static byte[] DecryptBuffer(byte[] encrypted)
        {
            if (encrypted == null) throw new ArgumentNullException("encrypted");
            if (encrypted.Length < HeaderSize) throw new InvalidDataException("Input file is too small to contain ESAH header.");
            if (!StartsWith(encrypted, EncryptedMagic)) throw new InvalidDataException("Invalid magic. This is not an ESAH file.");

            ushort version = ReadUInt16(encrypted, 4);
            ushort flags = ReadUInt16(encrypted, 6);
            ulong plainSize = ReadUInt64(encrypted, 8);
            if (version != FormatVersion) throw new InvalidDataException("Unsupported ESAH version: " + version);
            if (flags != FlagAes256Gcm) throw new InvalidDataException("Unsupported ESAH flags/algo: " + flags);

            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] cipher = new byte[encrypted.Length - HeaderSize];
            Buffer.BlockCopy(encrypted, 16, nonce, 0, NonceSize);
            Buffer.BlockCopy(encrypted, 28, tag, 0, TagSize);
            Buffer.BlockCopy(encrypted, HeaderSize, cipher, 0, cipher.Length);

            byte[] ignoredTag;
            byte[] plain = AesGcmCrypt(false, cipher, nonce, tag, out ignoredTag);
            if ((ulong)plain.Length != plainSize) throw new InvalidDataException("Plain size mismatch after decrypt.");
            return plain;
        }

        private static string BuildBackupPath(string originalPath, string suffix)
        {
            string dir = Path.GetDirectoryName(originalPath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(originalPath);
            string ext = Path.GetExtension(originalPath);
            return Path.Combine(dir, name + suffix + ext);
        }

        private static void ReplaceInPlace(string originalPath, byte[] newData, string backupPath)
        {
            string tempPath = originalPath + ".tmp_sahcrypt";
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                File.WriteAllBytes(tempPath, newData);
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Move(originalPath, backupPath);
                File.Move(tempPath, originalPath);
                try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch { }
            }
            catch
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                try
                {
                    if (!File.Exists(originalPath) && File.Exists(backupPath))
                        File.Move(backupPath, originalPath);
                }
                catch { }
                throw;
            }
        }

        private static byte[] LegacyDecryptBuffer(byte[] buffer)
        {
            byte[] output = (byte[])buffer.Clone();
            for (int index = 0; index < output.Length; ++index)
                output[index] = Ror((byte)(Ror((byte)(output[index] ^ (byte)index), 1) ^ 0x7F), 3);
            return output;
        }

        private static byte Ror(byte value, int count)
        {
            return (byte)((value >> count) | (value << (8 - count)));
        }

        private static byte[] ReadPrefix(string path, int count)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                int len = (int)Math.Min(count, fs.Length);
                byte[] buffer = new byte[len];
                int read = fs.Read(buffer, 0, len);
                if (read == len) return buffer;
                byte[] smaller = new byte[read];
                Buffer.BlockCopy(buffer, 0, smaller, 0, read);
                return smaller;
            }
        }

        private static bool StartsWith(byte[] data, byte[] magic)
        {
            if (data == null || data.Length < magic.Length) return false;
            for (int i = 0; i < magic.Length; i++)
                if (data[i] != magic[i]) return false;
            return true;
        }

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        private static void WriteUInt64(byte[] buffer, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++) buffer[offset + i] = (byte)(value >> (8 * i));
        }

        private static ulong ReadUInt64(byte[] buffer, int offset)
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++) value |= ((ulong)buffer[offset + i]) << (8 * i);
            return value;
        }

        private static byte[] ResolveKeyFromServerIni()
        {
            byte[] key = null;

            if (!string.IsNullOrWhiteSpace(AppConfig.SahCryptKeyHex))
                key = ParseHexKey(AppConfig.SahCryptKeyHex.Trim());
            else if (!string.IsNullOrWhiteSpace(AppConfig.SahCryptKeyBase64))
                key = Convert.FromBase64String(AppConfig.SahCryptKeyBase64.Trim());
            else if (!string.IsNullOrEmpty(AppConfig.SahCryptKeyText))
                key = Encoding.UTF8.GetBytes(AppConfig.SahCryptKeyText);
            else if (!string.IsNullOrEmpty(AppConfig.SahCryptKey))
            {
                string raw = AppConfig.SahCryptKey.Trim();
                key = LooksLikeHexKey(raw) ? ParseHexKey(raw) : Encoding.UTF8.GetBytes(AppConfig.SahCryptKey);
            }

            if (key == null || key.Length != 32)
                throw new InvalidOperationException("SahCrypt AES-256 key is missing or invalid. Put KeyHex, KeyBase64 or exact 32-byte KeyText under [SahCrypt] in server launcher.ini.");

            return key;
        }

        private static bool LooksLikeHexKey(string value)
        {
            string clean = value.Replace(" ", string.Empty).Replace("-", string.Empty);
            if (clean.Length != 64) return false;
            for (int i = 0; i < clean.Length; i++)
            {
                char c = clean[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
        }

        private static byte[] ParseHexKey(string value)
        {
            string clean = value.Replace(" ", string.Empty).Replace("-", string.Empty);
            if (clean.Length != 64) throw new InvalidOperationException("SahCrypt KeyHex must contain exactly 64 hex characters / 32 bytes.");
            byte[] key = new byte[32];
            for (int i = 0; i < key.Length; i++)
                key[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
            return key;
        }

        private static void BCryptRandom(byte[] buffer)
        {
            int status = Native.BCryptGenRandom(IntPtr.Zero, buffer, buffer.Length, Native.BCRYPT_USE_SYSTEM_PREFERRED_RNG);
            ThrowIfFailed(status, "BCryptGenRandom");
        }

        private static byte[] AesGcmCrypt(bool encrypt, byte[] input, byte[] nonce, byte[] tagIn, out byte[] tagOut)
        {
            IntPtr hAlg = IntPtr.Zero;
            IntPtr hKey = IntPtr.Zero;
            byte[] keyObject = null;
            tagOut = tagIn != null ? (byte[])tagIn.Clone() : new byte[TagSize];

            try
            {
                ThrowIfFailed(Native.BCryptOpenAlgorithmProvider(out hAlg, Native.BCRYPT_AES_ALGORITHM, null, 0), "BCryptOpenAlgorithmProvider(AES)");

                byte[] mode = Encoding.Unicode.GetBytes(Native.BCRYPT_CHAIN_MODE_GCM + "\0");
                ThrowIfFailed(Native.BCryptSetProperty(hAlg, Native.BCRYPT_CHAINING_MODE, mode, mode.Length, 0), "BCryptSetProperty(GCM)");

                int objectLength;
                int bytesWritten;
                ThrowIfFailed(Native.BCryptGetProperty(hAlg, Native.BCRYPT_OBJECT_LENGTH, out objectLength, 4, out bytesWritten, 0), "BCryptGetProperty(OBJECT_LENGTH)");

                keyObject = new byte[objectLength];
                byte[] key = ResolveKeyFromServerIni();
                ThrowIfFailed(Native.BCryptGenerateSymmetricKey(hAlg, out hKey, keyObject, keyObject.Length, key, key.Length, 0), "BCryptGenerateSymmetricKey");

                return encrypt
                    ? BCryptEncrypt(hKey, input, nonce, tagOut)
                    : BCryptDecrypt(hKey, input, nonce, tagOut);
            }
            finally
            {
                if (hKey != IntPtr.Zero) Native.BCryptDestroyKey(hKey);
                if (hAlg != IntPtr.Zero) Native.BCryptCloseAlgorithmProvider(hAlg, 0);
            }
        }

        private static byte[] BCryptEncrypt(IntPtr hKey, byte[] plain, byte[] nonce, byte[] tag)
        {
            using (PinnedBuffer pInput = new PinnedBuffer(plain))
            using (PinnedBuffer pNonce = new PinnedBuffer(nonce))
            using (PinnedBuffer pTag = new PinnedBuffer(tag))
            {
                Native.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO auth = Native.CreateAuthInfo(pNonce.Pointer, nonce.Length, pTag.Pointer, tag.Length);
                int cipherSize;
                ThrowIfFailed(Native.BCryptEncrypt(hKey, pInput.Pointer, plain.Length, ref auth, IntPtr.Zero, 0, IntPtr.Zero, 0, out cipherSize, 0), "BCryptEncrypt(size)");

                byte[] cipher = new byte[cipherSize];
                using (PinnedBuffer pOutput = new PinnedBuffer(cipher))
                {
                    auth = Native.CreateAuthInfo(pNonce.Pointer, nonce.Length, pTag.Pointer, tag.Length);
                    int written;
                    ThrowIfFailed(Native.BCryptEncrypt(hKey, pInput.Pointer, plain.Length, ref auth, IntPtr.Zero, 0, pOutput.Pointer, cipher.Length, out written, 0), "BCryptEncrypt(data)");
                    if (written != cipher.Length) Array.Resize(ref cipher, written);
                }
                return cipher;
            }
        }

        private static byte[] BCryptDecrypt(IntPtr hKey, byte[] cipher, byte[] nonce, byte[] tag)
        {
            using (PinnedBuffer pInput = new PinnedBuffer(cipher))
            using (PinnedBuffer pNonce = new PinnedBuffer(nonce))
            using (PinnedBuffer pTag = new PinnedBuffer(tag))
            {
                Native.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO auth = Native.CreateAuthInfo(pNonce.Pointer, nonce.Length, pTag.Pointer, tag.Length);
                int plainSize;
                ThrowIfFailed(Native.BCryptDecrypt(hKey, pInput.Pointer, cipher.Length, ref auth, IntPtr.Zero, 0, IntPtr.Zero, 0, out plainSize, 0), "BCryptDecrypt(size)");

                byte[] plain = new byte[plainSize];
                using (PinnedBuffer pOutput = new PinnedBuffer(plain))
                {
                    auth = Native.CreateAuthInfo(pNonce.Pointer, nonce.Length, pTag.Pointer, tag.Length);
                    int written;
                    ThrowIfFailed(Native.BCryptDecrypt(hKey, pInput.Pointer, cipher.Length, ref auth, IntPtr.Zero, 0, pOutput.Pointer, plain.Length, out written, 0), "BCryptDecrypt(data)");
                    if (written != plain.Length) Array.Resize(ref plain, written);
                }
                return plain;
            }
        }

        private static void ThrowIfFailed(int status, string operation)
        {
            if (status < 0)
                throw new InvalidOperationException(operation + " failed. NTSTATUS=0x" + status.ToString("X8"));
        }

        private sealed class PinnedBuffer : IDisposable
        {
            private GCHandle _handle;
            private readonly bool _allocated;
            public IntPtr Pointer { get { return _allocated ? _handle.AddrOfPinnedObject() : IntPtr.Zero; } }

            public PinnedBuffer(byte[] buffer)
            {
                if (buffer != null && buffer.Length > 0)
                {
                    _handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    _allocated = true;
                }
            }

            public void Dispose()
            {
                if (_allocated) _handle.Free();
            }
        }

        private static class Native
        {
            public const int BCRYPT_USE_SYSTEM_PREFERRED_RNG = 0x00000002;
            public const string BCRYPT_AES_ALGORITHM = "AES";
            public const string BCRYPT_CHAINING_MODE = "ChainingMode";
            public const string BCRYPT_CHAIN_MODE_GCM = "ChainingModeGCM";
            public const string BCRYPT_OBJECT_LENGTH = "ObjectLength";

            [StructLayout(LayoutKind.Sequential)]
            public struct BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO
            {
                public int cbSize;
                public int dwInfoVersion;
                public IntPtr pbNonce;
                public int cbNonce;
                public IntPtr pbAuthData;
                public int cbAuthData;
                public IntPtr pbTag;
                public int cbTag;
                public IntPtr pbMacContext;
                public int cbMacContext;
                public int cbAAD;
                public long cbData;
                public int dwFlags;
            }

            public static BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO CreateAuthInfo(IntPtr nonce, int nonceSize, IntPtr tag, int tagSize)
            {
                return new BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO
                {
                    cbSize = Marshal.SizeOf(typeof(BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO)),
                    dwInfoVersion = 1,
                    pbNonce = nonce,
                    cbNonce = nonceSize,
                    pbAuthData = IntPtr.Zero,
                    cbAuthData = 0,
                    pbTag = tag,
                    cbTag = tagSize,
                    pbMacContext = IntPtr.Zero,
                    cbMacContext = 0,
                    cbAAD = 0,
                    cbData = 0,
                    dwFlags = 0
                };
            }

            [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
            public static extern int BCryptOpenAlgorithmProvider(out IntPtr phAlgorithm, string pszAlgId, string pszImplementation, int dwFlags);

            [DllImport("bcrypt.dll")]
            public static extern int BCryptCloseAlgorithmProvider(IntPtr hAlgorithm, int dwFlags);

            [DllImport("bcrypt.dll")]
            public static extern int BCryptSetProperty(IntPtr hObject, [MarshalAs(UnmanagedType.LPWStr)] string pszProperty, byte[] pbInput, int cbInput, int dwFlags);

            [DllImport("bcrypt.dll")]
            public static extern int BCryptGetProperty(IntPtr hObject, [MarshalAs(UnmanagedType.LPWStr)] string pszProperty, out int pbOutput, int cbOutput, out int pcbResult, int dwFlags);

            [DllImport("bcrypt.dll")]
            public static extern int BCryptGenerateSymmetricKey(IntPtr hAlgorithm, out IntPtr phKey, byte[] pbKeyObject, int cbKeyObject, byte[] pbSecret, int cbSecret, int dwFlags);

            [DllImport("bcrypt.dll")]
            public static extern int BCryptDestroyKey(IntPtr hKey);

            [DllImport("bcrypt.dll")]
            public static extern int BCryptGenRandom(IntPtr hAlgorithm, byte[] pbBuffer, int cbBuffer, int dwFlags);

            [DllImport("bcrypt.dll")]
            public static extern int BCryptEncrypt(IntPtr hKey, IntPtr pbInput, int cbInput, ref BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO pPaddingInfo, IntPtr pbIV, int cbIV, IntPtr pbOutput, int cbOutput, out int pcbResult, int dwFlags);

            [DllImport("bcrypt.dll")]
            public static extern int BCryptDecrypt(IntPtr hKey, IntPtr pbInput, int cbInput, ref BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO pPaddingInfo, IntPtr pbIV, int cbIV, IntPtr pbOutput, int cbOutput, out int pcbResult, int dwFlags);
        }
    }
}
