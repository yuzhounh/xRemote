#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <string>
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    int count = 0; auto args = CommandLineToArgvW(GetCommandLineW(), &count); if (!args || count < 2) return 2;
    std::wstring content; for (int i = 2; i < count; ++i) content += std::wstring(args[i]) + L"\n";
    HANDLE file = CreateFileW(args[1], GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr);
    DWORD written = 0; bool ok = file != INVALID_HANDLE_VALUE && WriteFile(file, content.data(), (DWORD)(content.size() * sizeof(wchar_t)), &written, nullptr);
    if (file != INVALID_HANDLE_VALUE) CloseHandle(file); LocalFree(args); return ok ? 0 : 1;
}
