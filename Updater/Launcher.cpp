#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <shlobj.h>
#include <tlhelp32.h>
#include <bcrypt.h>
#include <string>
#include <vector>
#include <stdexcept>

static bool Exists(const std::wstring& path) { return GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES; }
static std::wstring Quote(const std::wstring& text) {
    std::wstring result = L"\""; size_t slashes = 0;
    for (wchar_t c : text) {
        if (c == L'\\') { ++slashes; continue; }
        if (c == L'"') { result.append(slashes * 2 + 1, L'\\'); result += c; }
        else { result.append(slashes, L'\\'); result += c; }
        slashes = 0;
    }
    result.append(slashes * 2, L'\\'); return result + L"\"";
}
static std::string Utf8(const std::wstring& text) {
    int size = WideCharToMultiByte(CP_UTF8, 0, text.data(), (int)text.size(), nullptr, 0, nullptr, nullptr);
    std::string result(size, 0); WideCharToMultiByte(CP_UTF8, 0, text.data(), (int)text.size(), result.data(), size, nullptr, nullptr); return result;
}
static std::string Read(const std::wstring& path, DWORD limit) {
    HANDLE file = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr);
    if (file == INVALID_HANDLE_VALUE) throw std::runtime_error("Open failed");
    LARGE_INTEGER size{};
    if (!GetFileSizeEx(file, &size) || size.QuadPart < 0 || size.QuadPart > limit) { CloseHandle(file); throw std::runtime_error("File size"); }
    std::string bytes((size_t)size.QuadPart, 0); DWORD read = 0;
    bool ok = ReadFile(file, bytes.data(), (DWORD)bytes.size(), &read, nullptr) && read == bytes.size(); CloseHandle(file);
    if (!ok) throw std::runtime_error("Read failed"); return bytes;
}
static std::string Hash(const std::string& bytes) {
    BCRYPT_ALG_HANDLE algorithm{}; BCRYPT_HASH_HANDLE hash{};
    unsigned char digest[32]{};
    if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0) throw std::runtime_error("SHA256");
    bool ok = BCryptCreateHash(algorithm, &hash, nullptr, 0, nullptr, 0, 0) >= 0 &&
        BCryptHashData(hash, (PUCHAR)bytes.data(), (ULONG)bytes.size(), 0) >= 0 && BCryptFinishHash(hash, digest, 32, 0) >= 0;
    if (hash) BCryptDestroyHash(hash); BCryptCloseAlgorithmProvider(algorithm, 0);
    if (!ok) throw std::runtime_error("SHA256");
    const char* hex = "0123456789abcdef"; std::string result;
    for (auto b : digest) { result += hex[b >> 4]; result += hex[b & 15]; } return result;
}
static bool Running(const std::wstring& target) {
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) return true;
    PROCESSENTRY32W entry{}; entry.dwSize = sizeof(entry); bool found = false;
    if (Process32FirstW(snapshot, &entry)) do {
        if (_wcsicmp(entry.szExeFile, L"xRemote.App.exe") != 0 || entry.th32ProcessID == GetCurrentProcessId()) continue;
        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, entry.th32ProcessID);
        if (!process) { found = true; break; }
        wchar_t path[32768]; DWORD count = 32768;
        if (QueryFullProcessImageNameW(process, 0, path, &count)) {
            std::wstring full(path, count); auto slash = full.find_last_of(L"\\/");
            if (slash != std::wstring::npos && _wcsicmp(full.substr(0, slash).c_str(), target.c_str()) == 0) found = true;
        } else found = true;
        CloseHandle(process); if (found) break;
    } while (Process32NextW(snapshot, &entry));
    CloseHandle(snapshot); return found;
}
static bool Spawn(const std::wstring& exe, const std::wstring& parameters, const std::wstring& directory) {
    std::wstring command = Quote(exe) + parameters; STARTUPINFOW startup{}; startup.cb = sizeof(startup); PROCESS_INFORMATION process{};
    bool ok = CreateProcessW(exe.c_str(), command.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, directory.c_str(), &startup, &process) != FALSE;
    if (ok) { CloseHandle(process.hThread); CloseHandle(process.hProcess); } return ok;
}
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
    try {
        wchar_t path[32768]; DWORD length = GetModuleFileNameW(nullptr, path, 32768); if (!length || length == 32768) return 2;
        std::wstring target(path, length); target.resize(target.find_last_of(L"\\/"));
        std::wstring upper = target; LCMapStringEx(LOCALE_NAME_INVARIANT, LCMAP_UPPERCASE, target.data(), (int)target.size(), upper.data(), (int)upper.size(), nullptr, nullptr, 0);
        auto id = Hash(Utf8(upper)).substr(0, 24); wchar_t local[MAX_PATH];
        if (FAILED(SHGetFolderPathW(nullptr, CSIDL_LOCAL_APPDATA, nullptr, SHGFP_TYPE_CURRENT, local))) return 2;
        std::wstring state = std::wstring(local) + L"\\xRemote\\Updates\\" + std::wstring(id.begin(), id.end());
        int argc = 0; LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc); if (!argv) return 2;
        std::wstring parameters; for (int i = 1; i < argc; ++i) parameters += L" " + Quote(argv[i]); LocalFree(argv);
        bool transaction = Exists(state + L"\\transaction.json");
        bool pending = Exists(state + L"\\pending.json") && !Exists(state + L"\\disabled");
        if ((transaction || pending) && !Running(target)) {
            auto name = Read(state + L"\\helper-name.txt", 100);
            if (name.size() != 75 || name.substr(0, 7) != "helper-" || name.substr(71) != ".exe") return 3;
            for (size_t i = 7; i < 71; ++i) if (!((name[i] >= '0' && name[i] <= '9') || (name[i] >= 'a' && name[i] <= 'f'))) return 3;
            auto helper = state + L"\\" + std::wstring(name.begin(), name.end());
            if (Hash(Read(helper, 200 * 1024 * 1024)) != name.substr(7, 64)) return 3;
            FILETIME created{}, exited{}, kernel{}, user{}; if (!GetProcessTimes(GetCurrentProcess(), &created, &exited, &kernel, &user)) return 3;
            ULARGE_INTEGER timestamp{}; timestamp.LowPart = created.dwLowDateTime; timestamp.HighPart = created.dwHighDateTime;
            auto ticks = timestamp.QuadPart + 504911232000000000ULL;
            return Spawn(helper, L" --resume " + Quote(target) + L" " + std::to_wstring(GetCurrentProcessId()) + L" " + std::to_wstring(ticks) + parameters, state) ? 0 : 3;
        }
        return Spawn(target + L"\\xRemote.App.exe", parameters, target) ? 0 : 4;
    } catch (...) { return 1; }
}
