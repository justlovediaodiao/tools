#define UNICODE
#define _UNICODE

#include "fmtp/text.h"

#include <windows.h>

namespace fmtp {

std::string ToUtf8(const std::wstring& value) {
    if (value.empty()) {
        return {};
    }

    const int count = WideCharToMultiByte(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()),
                                          nullptr, 0, nullptr, nullptr);
    if (count <= 0) {
        return {};
    }

    std::string result(static_cast<std::size_t>(count), '\0');
    WideCharToMultiByte(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()),
                        result.data(), count, nullptr, nullptr);
    return result;
}

std::wstring FromUtf8(const std::string& value) {
    if (value.empty()) {
        return {};
    }

    const int count = MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()),
                                          nullptr, 0);
    if (count <= 0) {
        return std::wstring(value.begin(), value.end());
    }

    std::wstring result(static_cast<std::size_t>(count), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()),
                        result.data(), count);
    return result;
}

} // namespace fmtp
