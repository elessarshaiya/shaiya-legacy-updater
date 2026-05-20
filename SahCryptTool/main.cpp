#define NOMINMAX
#include <windows.h>
#include <commdlg.h>
#include <bcrypt.h>

#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
#include <sstream>
#include <string>
#include <vector>

#ifdef max
#undef max
#endif
#ifdef min
#undef min
#endif

#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "Comdlg32.lib")

namespace fs = std::filesystem;

static constexpr uint16_t kFormatVersion = 1;
static constexpr uint16_t kFlagAes256Gcm = 1;
static constexpr size_t kNonceSize = 12;
static constexpr size_t kTagSize = 16;
static constexpr size_t kKeySize = 32;

// Public sample key. Replace it with the same production key used by launcher.ini before release.
static constexpr std::array<uint8_t, kKeySize> kFixedKey = {
    0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37,
    0x38, 0x39, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46,
    0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37,
    0x38, 0x39, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46
};

#pragma pack(push, 1)
struct SahEncHeaderV1
{
    char magic[4];
    uint16_t version;
    uint16_t flags;
    uint64_t plainSize;
    uint8_t nonce[kNonceSize];
    uint8_t tag[kTagSize];
    uint8_t reserved[20];
};
#pragma pack(pop)

static_assert(sizeof(SahEncHeaderV1) == 64, "Unexpected header size");

enum class Mode
{
    Encrypt,
    Decrypt
};

bool NtSuccessCode(NTSTATUS status)
{
    return status >= 0;
}

std::string NtStatusToString(NTSTATUS status)
{
    std::ostringstream oss;
    oss << "NTSTATUS=0x" << std::hex << std::uppercase
        << static_cast<unsigned long>(status);
    return oss.str();
}

void PauseAtEnd()
{
    std::cout << "\nPress Enter to close...";
    std::cin.clear();
    std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
    std::cin.get();
}

std::string PathToDisplayString(const fs::path& path)
{
    return path.u8string();
}

bool ReadFileBytes(const fs::path& path, std::vector<uint8_t>& out, std::string& error)
{
    std::ifstream file(path, std::ios::binary);
    if (!file)
    {
        error = "Cannot open input file: " + PathToDisplayString(path);
        return false;
    }

    file.seekg(0, std::ios::end);
    const std::streamoff size = file.tellg();
    if (size < 0)
    {
        error = "Cannot determine input size: " + PathToDisplayString(path);
        return false;
    }

    file.seekg(0, std::ios::beg);
    out.resize(static_cast<size_t>(size));
    if (!out.empty())
    {
        file.read(reinterpret_cast<char*>(out.data()), static_cast<std::streamsize>(out.size()));
        if (!file)
        {
            error = "Failed to read input file: " + PathToDisplayString(path);
            return false;
        }
    }

    return true;
}

bool WriteFileBytes(const fs::path& path, const std::vector<uint8_t>& data, std::string& error)
{
    std::ofstream file(path, std::ios::binary | std::ios::trunc);
    if (!file)
    {
        error = "Cannot open output file: " + PathToDisplayString(path);
        return false;
    }

    if (!data.empty())
    {
        file.write(reinterpret_cast<const char*>(data.data()), static_cast<std::streamsize>(data.size()));
        if (!file)
        {
            error = "Failed to write output file: " + PathToDisplayString(path);
            return false;
        }
    }

    return true;
}

bool GenRandomBytes(uint8_t* out, size_t size, std::string& error)
{
    const NTSTATUS status = BCryptGenRandom(nullptr, out, static_cast<ULONG>(size), BCRYPT_USE_SYSTEM_PREFERRED_RNG);
    if (!NtSuccessCode(status))
    {
        error = "BCryptGenRandom failed: " + NtStatusToString(status);
        return false;
    }
    return true;
}

bool OpenAesGcm(BCRYPT_ALG_HANDLE& hAlg, std::string& error)
{
    const NTSTATUS openStatus = BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_AES_ALGORITHM, nullptr, 0);
    if (!NtSuccessCode(openStatus))
    {
        error = "BCryptOpenAlgorithmProvider(AES) failed: " + NtStatusToString(openStatus);
        return false;
    }

    const NTSTATUS modeStatus = BCryptSetProperty(
        hAlg,
        BCRYPT_CHAINING_MODE,
        reinterpret_cast<PUCHAR>(const_cast<wchar_t*>(BCRYPT_CHAIN_MODE_GCM)),
        static_cast<ULONG>(sizeof(BCRYPT_CHAIN_MODE_GCM)),
        0);

    if (!NtSuccessCode(modeStatus))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        hAlg = nullptr;
        error = "BCryptSetProperty(GCM) failed: " + NtStatusToString(modeStatus);
        return false;
    }

    return true;
}

bool BuildAesKey(BCRYPT_ALG_HANDLE hAlg, BCRYPT_KEY_HANDLE& hKey, std::vector<uint8_t>& keyObject, std::string& error)
{
    DWORD objectLength = 0;
    DWORD bytesWritten = 0;
    const NTSTATUS propStatus = BCryptGetProperty(
        hAlg,
        BCRYPT_OBJECT_LENGTH,
        reinterpret_cast<PUCHAR>(&objectLength),
        sizeof(objectLength),
        &bytesWritten,
        0);

    if (!NtSuccessCode(propStatus))
    {
        error = "BCryptGetProperty(OBJECT_LENGTH) failed: " + NtStatusToString(propStatus);
        return false;
    }

    keyObject.resize(objectLength);
    const NTSTATUS keyStatus = BCryptGenerateSymmetricKey(
        hAlg,
        &hKey,
        keyObject.data(),
        static_cast<ULONG>(keyObject.size()),
        const_cast<PUCHAR>(kFixedKey.data()),
        static_cast<ULONG>(kFixedKey.size()),
        0);

    if (!NtSuccessCode(keyStatus))
    {
        error = "BCryptGenerateSymmetricKey failed: " + NtStatusToString(keyStatus);
        return false;
    }

    return true;
}

bool Aes256GcmEncrypt(const std::vector<uint8_t>& plain,
                      const uint8_t nonce[kNonceSize],
                      std::vector<uint8_t>& cipher,
                      uint8_t tag[kTagSize],
                      std::string& error)
{
    BCRYPT_ALG_HANDLE hAlg = nullptr;
    BCRYPT_KEY_HANDLE hKey = nullptr;
    std::vector<uint8_t> keyObject;

    if (!OpenAesGcm(hAlg, error))
    {
        return false;
    }

    if (!BuildAesKey(hAlg, hKey, keyObject, error))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return false;
    }

    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO authInfo;
    BCRYPT_INIT_AUTH_MODE_INFO(authInfo);
    authInfo.pbNonce = const_cast<PUCHAR>(nonce);
    authInfo.cbNonce = kNonceSize;
    authInfo.pbTag = tag;
    authInfo.cbTag = kTagSize;

    ULONG cipherSize = 0;
    NTSTATUS status = BCryptEncrypt(
        hKey,
        const_cast<PUCHAR>(plain.data()),
        static_cast<ULONG>(plain.size()),
        &authInfo,
        nullptr,
        0,
        nullptr,
        0,
        &cipherSize,
        0);

    if (!NtSuccessCode(status))
    {
        BCryptDestroyKey(hKey);
        BCryptCloseAlgorithmProvider(hAlg, 0);
        error = "BCryptEncrypt(size) failed: " + NtStatusToString(status);
        return false;
    }

    cipher.resize(cipherSize);
    status = BCryptEncrypt(
        hKey,
        const_cast<PUCHAR>(plain.data()),
        static_cast<ULONG>(plain.size()),
        &authInfo,
        nullptr,
        0,
        cipher.data(),
        cipherSize,
        &cipherSize,
        0);

    BCryptDestroyKey(hKey);
    BCryptCloseAlgorithmProvider(hAlg, 0);

    if (!NtSuccessCode(status))
    {
        error = "BCryptEncrypt(data) failed: " + NtStatusToString(status);
        return false;
    }

    cipher.resize(cipherSize);
    return true;
}

bool Aes256GcmDecrypt(const std::vector<uint8_t>& cipher,
                      const uint8_t nonce[kNonceSize],
                      const uint8_t tag[kTagSize],
                      std::vector<uint8_t>& plain,
                      std::string& error)
{
    BCRYPT_ALG_HANDLE hAlg = nullptr;
    BCRYPT_KEY_HANDLE hKey = nullptr;
    std::vector<uint8_t> keyObject;

    if (!OpenAesGcm(hAlg, error))
    {
        return false;
    }

    if (!BuildAesKey(hAlg, hKey, keyObject, error))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return false;
    }

    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO authInfo;
    BCRYPT_INIT_AUTH_MODE_INFO(authInfo);
    authInfo.pbNonce = const_cast<PUCHAR>(nonce);
    authInfo.cbNonce = kNonceSize;
    authInfo.pbTag = const_cast<PUCHAR>(tag);
    authInfo.cbTag = kTagSize;

    ULONG plainSize = 0;
    NTSTATUS status = BCryptDecrypt(
        hKey,
        const_cast<PUCHAR>(cipher.data()),
        static_cast<ULONG>(cipher.size()),
        &authInfo,
        nullptr,
        0,
        nullptr,
        0,
        &plainSize,
        0);

    if (!NtSuccessCode(status))
    {
        BCryptDestroyKey(hKey);
        BCryptCloseAlgorithmProvider(hAlg, 0);
        error = "BCryptDecrypt(size) failed: " + NtStatusToString(status);
        return false;
    }

    plain.resize(plainSize);
    status = BCryptDecrypt(
        hKey,
        const_cast<PUCHAR>(cipher.data()),
        static_cast<ULONG>(cipher.size()),
        &authInfo,
        nullptr,
        0,
        plain.data(),
        plainSize,
        &plainSize,
        0);

    BCryptDestroyKey(hKey);
    BCryptCloseAlgorithmProvider(hAlg, 0);

    if (!NtSuccessCode(status))
    {
        error = "BCryptDecrypt(data) failed: " + NtStatusToString(status);
        return false;
    }

    plain.resize(plainSize);
    return true;
}

bool IsPlainSah(const std::vector<uint8_t>& data)
{
    return data.size() >= 3 && data[0] == 'S' && data[1] == 'A' && data[2] == 'H';
}

bool IsEncryptedSah(const std::vector<uint8_t>& data)
{
    return data.size() >= sizeof(SahEncHeaderV1)
        && data[0] == 'E' && data[1] == 'S' && data[2] == 'A' && data[3] == 'H';
}

bool EncryptBuffer(const std::vector<uint8_t>& plain,
                   std::vector<uint8_t>& encrypted,
                   std::string& error)
{
    SahEncHeaderV1 header{};
    std::memcpy(header.magic, "ESAH", 4);
    header.version = kFormatVersion;
    header.flags = kFlagAes256Gcm;
    header.plainSize = static_cast<uint64_t>(plain.size());

    if (!GenRandomBytes(header.nonce, kNonceSize, error))
    {
        return false;
    }

    std::vector<uint8_t> cipher;
    if (!Aes256GcmEncrypt(plain, header.nonce, cipher, header.tag, error))
    {
        return false;
    }

    encrypted.resize(sizeof(SahEncHeaderV1) + cipher.size());
    std::memcpy(encrypted.data(), &header, sizeof(SahEncHeaderV1));
    if (!cipher.empty())
    {
        std::memcpy(encrypted.data() + sizeof(SahEncHeaderV1), cipher.data(), cipher.size());
    }

    return true;
}

bool DecryptBuffer(const std::vector<uint8_t>& encrypted,
                   std::vector<uint8_t>& plain,
                   std::string& error)
{
    if (encrypted.size() < sizeof(SahEncHeaderV1))
    {
        error = "Input file is too small to contain ESAH header.";
        return false;
    }

    SahEncHeaderV1 header{};
    std::memcpy(&header, encrypted.data(), sizeof(SahEncHeaderV1));

    if (std::memcmp(header.magic, "ESAH", 4) != 0)
    {
        error = "Invalid magic. This is not an ESAH file.";
        return false;
    }

    if (header.version != kFormatVersion)
    {
        error = "Unsupported version: " + std::to_string(header.version);
        return false;
    }

    if (header.flags != kFlagAes256Gcm)
    {
        error = "Unsupported flags/algo: " + std::to_string(header.flags);
        return false;
    }

    const std::vector<uint8_t> cipher(
        encrypted.begin() + static_cast<std::ptrdiff_t>(sizeof(SahEncHeaderV1)),
        encrypted.end());

    if (!Aes256GcmDecrypt(cipher, header.nonce, header.tag, plain, error))
    {
        error = "Decrypt failed. File may be corrupted or key/header mismatch. " + error;
        return false;
    }

    if (plain.size() != header.plainSize)
    {
        error = "Plain size mismatch after decrypt.";
        return false;
    }

    return true;
}

fs::path BuildBackupPath(const fs::path& originalPath, Mode mode)
{
    const fs::path parent = originalPath.parent_path();
    const std::wstring stem = originalPath.stem().wstring();
    const std::wstring ext = originalPath.extension().wstring();
    const std::wstring suffix = (mode == Mode::Encrypt) ? L".dec" : L".enc";
    return parent / fs::path(stem + suffix + ext);
}

bool ReplaceFileInPlace(const fs::path& originalPath,
                        const std::vector<uint8_t>& newData,
                        const fs::path& backupPath,
                        std::string& error)
{
    const fs::path tempPath = originalPath.parent_path() /
        fs::path(originalPath.filename().wstring() + L".tmp_sahcrypt");

    std::error_code ec;
    if (fs::exists(backupPath, ec) && !ec)
    {
        fs::remove(backupPath, ec);
        if (ec)
        {
            error = "Cannot remove existing backup file: " + PathToDisplayString(backupPath);
            return false;
        }
    }

    if (!WriteFileBytes(tempPath, newData, error))
    {
        return false;
    }

    try
    {
        fs::rename(originalPath, backupPath);
        fs::rename(tempPath, originalPath);
    }
    catch (const std::exception& ex)
    {
        std::error_code ignored;
        fs::remove(tempPath, ignored);

        if (!fs::exists(originalPath, ignored) && fs::exists(backupPath, ignored))
        {
            std::error_code restoreEc;
            fs::rename(backupPath, originalPath, restoreEc);
        }

        error = "Failed to replace file in place: ";
        error += ex.what();
        return false;
    }

    return true;
}

bool RunInPlaceConversion(const fs::path& selectedPath, Mode mode, std::string& error)
{
    std::vector<uint8_t> inputBytes;
    if (!ReadFileBytes(selectedPath, inputBytes, error))
    {
        return false;
    }

    std::vector<uint8_t> outputBytes;
    if (mode == Mode::Encrypt)
    {
        if (!IsPlainSah(inputBytes))
        {
            error = "Selected file does not look like plain Data.sah (SAH header missing).";
            return false;
        }

        if (!EncryptBuffer(inputBytes, outputBytes, error))
        {
            return false;
        }
    }
    else
    {
        if (!IsEncryptedSah(inputBytes))
        {
            error = "Selected file does not look like encrypted ESAH data.";
            return false;
        }

        if (!DecryptBuffer(inputBytes, outputBytes, error))
        {
            return false;
        }

        if (!IsPlainSah(outputBytes))
        {
            error = "Decrypt succeeded but output is not a valid SAH file.";
            return false;
        }
    }

    const fs::path backupPath = BuildBackupPath(selectedPath, mode);
    if (!ReplaceFileInPlace(selectedPath, outputBytes, backupPath, error))
    {
        return false;
    }

    std::cout << "\n[ok] Conversion complete.\n";
    std::cout << "     Active file : " << PathToDisplayString(selectedPath) << "\n";
    std::cout << "     Backup file : " << PathToDisplayString(backupPath) << "\n";
    std::cout << "     Mode        : " << (mode == Mode::Encrypt ? "encrypt" : "decrypt") << "\n";
    return true;
}

bool SelectFileDialog(fs::path& selectedPath, std::string& error)
{
    wchar_t fileBuffer[MAX_PATH] = {};

    OPENFILENAMEW ofn{};
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = GetConsoleWindow();
    ofn.lpstrFile = fileBuffer;
    ofn.nMaxFile = MAX_PATH;
    ofn.lpstrFilter = L"SAH Files (*.sah)\0*.sah\0All Files (*.*)\0*.*\0\0";
    ofn.lpstrTitle = L"Select Data.sah";
    ofn.Flags = OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST | OFN_HIDEREADONLY;

    if (!GetOpenFileNameW(&ofn))
    {
        const DWORD dlgError = CommDlgExtendedError();
        if (dlgError == 0)
        {
            error = "File selection cancelled.";
        }
        else
        {
            std::ostringstream oss;
            oss << "GetOpenFileNameW failed. Error=0x" << std::hex << std::uppercase << dlgError;
            error = oss.str();
        }
        return false;
    }

    selectedPath = fs::path(fileBuffer);
    return true;
}

bool AskMode(Mode& mode)
{
    while (true)
    {
        std::cout << "\nChoose operation:\n";
        std::cout << "  1) Encrypt selected Data.sah\n";
        std::cout << "  2) Decrypt selected Data.sah\n";
        std::cout << "  Q) Exit\n";
        std::cout << "> ";

        std::string input;
        if (!std::getline(std::cin, input))
        {
            return false;
        }

        if (input == "1")
        {
            mode = Mode::Encrypt;
            return true;
        }
        if (input == "2")
        {
            mode = Mode::Decrypt;
            return true;
        }
        if (input == "q" || input == "Q")
        {
            return false;
        }

        std::cout << "Invalid choice. Try again.\n";
    }
}

void PrintGuide()
{
    std::cout << "========================================\n";
    std::cout << "      SAHCRYPT - Data.sah Tool\n";
    std::cout << "========================================\n\n";
    std::cout << "How it works:\n";
    std::cout << "  - You select Data.sah with a file picker.\n";
    std::cout << "  - You choose encrypt or decrypt.\n";
    std::cout << "  - The file is converted in place.\n";
    std::cout << "  - The old version is kept as a backup in the same folder.\n\n";
    std::cout << "Backup naming:\n";
    std::cout << "  Encrypt -> old file becomes Data.dec.sah\n";
    std::cout << "  Decrypt -> old file becomes Data.enc.sah\n\n";
    std::cout << "Current active file name always stays Data.sah\n";
}

int wmain(int argc, wchar_t* argv[])
{
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleTitleW(L"SahCrypt - Data.sah Encrypt/Decrypt Tool");

    PrintGuide();

    try
    {
        if (argc == 3)
        {
            const std::wstring modeArg = argv[1];
            const fs::path pathArg = fs::path(argv[2]);
            Mode mode{};

            if (modeArg == L"encrypt")
            {
                mode = Mode::Encrypt;
            }
            else if (modeArg == L"decrypt")
            {
                mode = Mode::Decrypt;
            }
            else
            {
                std::cerr << "[err] Invalid CLI mode. Use: encrypt or decrypt\n";
                PauseAtEnd();
                return 1;
            }

            std::string error;
            if (!RunInPlaceConversion(pathArg, mode, error))
            {
                std::cerr << "[err] " << error << "\n";
                PauseAtEnd();
                return 2;
            }

            PauseAtEnd();
            return 0;
        }

        Mode mode{};
        if (!AskMode(mode))
        {
            return 0;
        }

        fs::path selectedPath;
        std::string error;
        if (!SelectFileDialog(selectedPath, error))
        {
            std::cerr << "[err] " << error << "\n";
            PauseAtEnd();
            return 1;
        }

        std::cout << "\nSelected file: " << PathToDisplayString(selectedPath) << "\n";
        if (!RunInPlaceConversion(selectedPath, mode, error))
        {
            std::cerr << "[err] " << error << "\n";
            PauseAtEnd();
            return 2;
        }

        PauseAtEnd();
        return 0;
    }
    catch (const std::exception& ex)
    {
        std::cerr << "[err] Unhandled exception: " << ex.what() << "\n";
        PauseAtEnd();
        return 3;
    }
}
