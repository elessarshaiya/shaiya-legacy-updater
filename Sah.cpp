#include <windows.h>
#include <bcrypt.h>

#include <array>
#include <cstdint>
#include <cstring>
#include <vector>

#include "hook.h"

#pragma comment(lib, "bcrypt.lib")

static const DWORD OPEN_ADDR = 0x00631FF6;
static const DWORD READ_ADDR = 0x00633B16;
static const DWORD CLOSE_ADDR = 0x00631DC2;

static const DWORD OPEN_RETN = 0x00631FFC;
static const DWORD READ_RETN = 0x00633B22;
static const DWORD CLOSE_RETN = 0x00631DCE;

static DWORD sah_eh_call = 0x0063CF08;

static constexpr uint16_t kFormatVersion = 1;
static constexpr uint16_t kFlagAes256Gcm = 1;
static constexpr size_t kNonceSize = 12;
static constexpr size_t kTagSize = 16;
static constexpr size_t kKeySize = 32;

static constexpr std::array<uint8_t, kKeySize> kFixedKey = {
    0x45, 0x6C, 0x61, 0x72, 0x69, 0x6F, 0x6E, 0x5F,
    0x53, 0x41, 0x48, 0x5F, 0x4B, 0x45, 0x59, 0x5F,
    0x76, 0x31, 0x41, 0x45, 0x53, 0x5F, 0x32, 0x35,
    0x36, 0x5F, 0x47, 0x43, 0x4D, 0x21, 0x23, 0x39
};

#pragma pack(push, 1)
struct SahEncHeaderV1
{
    char     magic[4];
    uint16_t version;
    uint16_t flags;
    uint64_t plainSize;
    uint8_t  nonce[kNonceSize];
    uint8_t  tag[kTagSize];
    uint8_t  reserved[20];
};
#pragma pack(pop)

static_assert(sizeof(SahEncHeaderV1) == 64, "Unexpected ESAH header size");

static const int kVirtualSahHandle = 0x7F7F0001;

struct VirtualSahState
{
    bool active = false;
    size_t pos = 0;
    std::vector<uint8_t> plain;
};

static VirtualSahState g_sah;

static volatile LONG g_openHandled = 0;
static volatile LONG g_readHandled = 0;
static volatile LONG g_closeHandled = 0;

static volatile LONG g_openRet = 0;
static volatile LONG g_readRet = 0;
static volatile LONG g_closeRet = 0;

static bool NtSuccessCode(NTSTATUS status)
{
    return status >= 0;
}

static bool EndsWithI(const char* s, const char* suffix)
{
    if (!s || !suffix)
        return false;

    const size_t ls = lstrlenA(s);
    const size_t lf = lstrlenA(suffix);
    if (ls < lf)
        return false;

    return lstrcmpiA(s + (ls - lf), suffix) == 0;
}

static bool IsDataSahPath(const char* path)
{
    if (!path || !*path)
        return false;

    return lstrcmpiA(path, "data.sah") == 0 ||
        EndsWithI(path, "\\data.sah") ||
        EndsWithI(path, "/data.sah");
}

static void ResetVirtualSah()
{
    g_sah.active = false;
    g_sah.pos = 0;
    g_sah.plain.clear();
    g_sah.plain.shrink_to_fit();
}

static bool ReadWholeFileA(const char* path, std::vector<uint8_t>& out)
{
    HANDLE h = CreateFileA(path, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE)
        return false;

    LARGE_INTEGER li{};
    if (!GetFileSizeEx(h, &li) || li.QuadPart <= 0 || li.QuadPart > 64ll * 1024ll * 1024ll)
    {
        CloseHandle(h);
        return false;
    }

    out.resize(static_cast<size_t>(li.QuadPart));

    DWORD totalRead = 0;
    while (totalRead < out.size())
    {
        DWORD got = 0;
        const DWORD want = static_cast<DWORD>(out.size() - totalRead);
        if (!ReadFile(h, out.data() + totalRead, want, &got, nullptr))
        {
            CloseHandle(h);
            return false;
        }

        if (got == 0)
            break;

        totalRead += got;
    }

    CloseHandle(h);
    return totalRead == out.size();
}

static bool OpenAesGcm(BCRYPT_ALG_HANDLE& hAlg)
{
    hAlg = nullptr;

    NTSTATUS st = BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_AES_ALGORITHM, nullptr, 0);
    if (!NtSuccessCode(st))
        return false;

    st = BCryptSetProperty(
        hAlg,
        BCRYPT_CHAINING_MODE,
        reinterpret_cast<PUCHAR>(const_cast<wchar_t*>(BCRYPT_CHAIN_MODE_GCM)),
        static_cast<ULONG>(sizeof(BCRYPT_CHAIN_MODE_GCM)),
        0);

    if (!NtSuccessCode(st))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        hAlg = nullptr;
        return false;
    }

    return true;
}

static bool BuildAesKey(BCRYPT_ALG_HANDLE hAlg, BCRYPT_KEY_HANDLE& hKey, std::vector<uint8_t>& keyObject)
{
    hKey = nullptr;

    DWORD objectLength = 0;
    DWORD cbResult = 0;
    NTSTATUS st = BCryptGetProperty(
        hAlg,
        BCRYPT_OBJECT_LENGTH,
        reinterpret_cast<PUCHAR>(&objectLength),
        sizeof(objectLength),
        &cbResult,
        0);

    if (!NtSuccessCode(st))
        return false;

    keyObject.resize(objectLength);

    st = BCryptGenerateSymmetricKey(
        hAlg,
        &hKey,
        keyObject.data(),
        static_cast<ULONG>(keyObject.size()),
        const_cast<PUCHAR>(kFixedKey.data()),
        static_cast<ULONG>(kFixedKey.size()),
        0);

    return NtSuccessCode(st);
}

static bool DecryptEsahBuffer(const std::vector<uint8_t>& enc, std::vector<uint8_t>& outPlain)
{
    if (enc.size() < sizeof(SahEncHeaderV1))
        return false;

    const auto* hdr = reinterpret_cast<const SahEncHeaderV1*>(enc.data());

    if (memcmp(hdr->magic, "ESAH", 4) != 0)
        return false;

    if (hdr->version != kFormatVersion || hdr->flags != kFlagAes256Gcm)
        return false;

    const uint8_t* cipher = enc.data() + sizeof(SahEncHeaderV1);
    const size_t cipherSize = enc.size() - sizeof(SahEncHeaderV1);

    BCRYPT_ALG_HANDLE hAlg = nullptr;
    BCRYPT_KEY_HANDLE hKey = nullptr;
    std::vector<uint8_t> keyObject;

    if (!OpenAesGcm(hAlg))
        return false;

    if (!BuildAesKey(hAlg, hKey, keyObject))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return false;
    }

    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO authInfo;
    BCRYPT_INIT_AUTH_MODE_INFO(authInfo);
    authInfo.pbNonce = const_cast<PUCHAR>(hdr->nonce);
    authInfo.cbNonce = kNonceSize;
    authInfo.pbTag = const_cast<PUCHAR>(hdr->tag);
    authInfo.cbTag = kTagSize;

    ULONG outSize = 0;
    NTSTATUS st = BCryptDecrypt(
        hKey,
        const_cast<PUCHAR>(cipher),
        static_cast<ULONG>(cipherSize),
        &authInfo,
        nullptr,
        0,
        nullptr,
        0,
        &outSize,
        0);

    if (!NtSuccessCode(st))
    {
        BCryptDestroyKey(hKey);
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return false;
    }

    outPlain.resize(outSize);

    st = BCryptDecrypt(
        hKey,
        const_cast<PUCHAR>(cipher),
        static_cast<ULONG>(cipherSize),
        &authInfo,
        nullptr,
        0,
        outPlain.data(),
        outSize,
        &outSize,
        0);

    BCryptDestroyKey(hKey);
    BCryptCloseAlgorithmProvider(hAlg, 0);

    if (!NtSuccessCode(st))
        return false;

    outPlain.resize(outSize);

    if (outPlain.size() != hdr->plainSize)
        return false;

    if (outPlain.size() < 3 || memcmp(outPlain.data(), "SAH", 3) != 0)
        return false;

    return true;
}

// Yalnızca ESAH kabul eder. Plain SAH fallback yok.
static bool LoadEncryptedDataSahStrict(const char* path)
{
    std::vector<uint8_t> bytes;
    if (!ReadWholeFileA(path, bytes))
        return false;

    if (bytes.size() < sizeof(SahEncHeaderV1))
        return false;

    if (memcmp(bytes.data(), "ESAH", 4) != 0)
        return false;

    std::vector<uint8_t> plain;
    if (!DecryptEsahBuffer(bytes, plain))
        return false;

    g_sah.active = true;
    g_sah.pos = 0;
    g_sah.plain = std::move(plain);
    return true;
}

int __cdecl SahOpen_Handler(const char* path, DWORD /*arg2*/, DWORD /*arg3*/)
{
    g_openHandled = 0;
    g_openRet = 0;

    if (!IsDataSahPath(path))
        return 0;

    ResetVirtualSah();

    if (LoadEncryptedDataSahStrict(path))
    {
        g_openHandled = 1;
        g_openRet = kVirtualSahHandle;
        return g_openRet;
    }

    // data.sah plain ise veya decrypt başarısızsa açtırma.
    g_openHandled = 1;
    g_openRet = -1;
    return -1;
}

int __cdecl SahRead_Handler(int handle, void* outBuf, int size)
{
    g_readHandled = 0;
    g_readRet = 0;

    if (!g_sah.active || handle != kVirtualSahHandle)
        return 0;

    g_readHandled = 1;

    if (!outBuf || size <= 0)
    {
        g_readRet = 0;
        return 0;
    }

    size_t remain = (g_sah.pos < g_sah.plain.size()) ? (g_sah.plain.size() - g_sah.pos) : 0;
    size_t toCopy = static_cast<size_t>(size);
    if (toCopy > remain)
        toCopy = remain;

    if (toCopy > 0)
    {
        memcpy(outBuf, g_sah.plain.data() + g_sah.pos, toCopy);
        g_sah.pos += toCopy;
    }

    if (static_cast<size_t>(size) > toCopy)
    {
        memset(reinterpret_cast<uint8_t*>(outBuf) + toCopy, 0, static_cast<size_t>(size) - toCopy);
    }

    g_readRet = static_cast<int>(toCopy);
    return g_readRet;
}

int __cdecl SahClose_Handler(int handle)
{
    g_closeHandled = 0;
    g_closeRet = 0;

    if (!g_sah.active || handle != kVirtualSahHandle)
        return 0;

    ResetVirtualSah();
    g_closeHandled = 1;
    g_closeRet = 0;
    return 0;
}

__declspec(naked) void sah_open_hook()
{
    __asm
    {
        pushad

        mov eax, [esp + 0x24]
        mov ecx, [esp + 0x28]
        mov edx, [esp + 0x2C]

        push edx
        push ecx
        push eax
        call SahOpen_Handler
        add  esp, 0x0C

        mov  dword ptr[g_openRet], eax

        popad

        cmp  dword ptr[g_openHandled], 0
        jne  handled_open

        mov  edi, edi
        push ebp
        mov  ebp, esp
        push ecx
        jmp  OPEN_RETN

        handled_open :
        mov  eax, dword ptr[g_openRet]
            ret
    }
}

__declspec(naked) void sah_read_hook()
{
    __asm
    {
        pushad

        mov eax, [esp + 0x24]
        mov ecx, [esp + 0x28]
        mov edx, [esp + 0x2C]

        push edx
        push ecx
        push eax
        call SahRead_Handler
        add  esp, 0x0C

        mov  dword ptr[g_readRet], eax

        popad

        cmp  dword ptr[g_readHandled], 0
        jne  handled_read

        push 0x10
        push 0x007A80E8
        call sah_eh_call
        jmp  READ_RETN

        handled_read :
        mov  eax, dword ptr[g_readRet]
            ret
    }
}

__declspec(naked) void sah_close_hook()
{
    __asm
    {
        pushad

        mov eax, [esp + 0x24]
        push eax
        call SahClose_Handler
        add  esp, 0x04

        mov  dword ptr[g_closeRet], eax

        popad

        cmp  dword ptr[g_closeHandled], 0
        jne  handled_close

        push 0x10
        push 0x007A7FC8
        call sah_eh_call
        jmp  CLOSE_RETN

        handled_close :
        mov  eax, dword ptr[g_closeRet]
            ret
    }
}

void SAH()
{
    ResetVirtualSah();

    Hook((LPVOID)OPEN_ADDR, sah_open_hook, 6);
    Hook((LPVOID)READ_ADDR, sah_read_hook, 12);
    Hook((LPVOID)CLOSE_ADDR, sah_close_hook, 12);
}
