#define UNICODE
#define _UNICODE

#include "fmtp/reporter.h"
#include "fmtp/sync.h"
#include "fmtp/text.h"

#include <exception>
#include <filesystem>
#include <string>
#include <vector>

int wmain(int argc, wchar_t* argv[]) {
    bool programmable = false;
    std::vector<std::wstring> arguments;
    for (int i = 1; i < argc; ++i) {
        if (std::wstring(argv[i]) == L"-p") {
            programmable = true;
        } else {
            arguments.emplace_back(argv[i]);
        }
    }

    const fmtp::Reporter reporter(programmable);
    if (arguments.size() != 2) {
        reporter.Error(L"Usage: fmtp.exe [-p] <local path> <MTP full path>");
        return programmable ? 1 : 0;
    }

    try {
        const bool success = fmtp::RunSync(std::filesystem::path(arguments[0]), arguments[1], reporter);
        return programmable && !success ? 1 : 0;
    } catch (const std::exception& ex) {
        reporter.Error(L"Error: " + fmtp::FromUtf8(ex.what()));
        return programmable ? 1 : 0;
    }
}
