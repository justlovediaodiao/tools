#include "fmtp/reporter.h"

#include "fmtp/text.h"

#include <iostream>

namespace fmtp {
namespace {

std::string EscapeJson(const std::string& value) {
    static constexpr char hex[] = "0123456789abcdef";
    std::string result;
    result.reserve(value.size());

    for (const unsigned char ch : value) {
        switch (ch) {
        case '"': result += "\\\""; break;
        case '\\': result += "\\\\"; break;
        case '\b': result += "\\b"; break;
        case '\f': result += "\\f"; break;
        case '\n': result += "\\n"; break;
        case '\r': result += "\\r"; break;
        case '\t': result += "\\t"; break;
        default:
            if (ch < 0x20) {
                result += "\\u00";
                result.push_back(hex[ch >> 4]);
                result.push_back(hex[ch & 0x0f]);
            } else {
                result.push_back(static_cast<char>(ch));
            }
            break;
        }
    }

    return result;
}

} // namespace

Reporter::Reporter(bool programmable) : programmable_(programmable) {}

void Reporter::Info(const std::wstring& message) const {
    if (programmable_) {
        WriteStatus("info", message);
    } else {
        EndProgressLine();
        std::wcout << message << L'\n';
    }
}

void Reporter::Error(const std::wstring& message) const {
    if (programmable_) {
        WriteStatus("error", message);
    } else {
        EndProgressLine();
        std::wcout << message << L'\n';
    }
}

void Reporter::Success(const std::wstring& message) const {
    if (programmable_) {
        WriteStatus("success", message);
    }
}

void Reporter::MoveProgress(std::size_t current, std::size_t total,
                            const std::wstring& file) const {
    if (programmable_) {
        WriteProgress("move", current, total, file);
    } else {
        std::wcout << file << L'\n';
    }
}

void Reporter::CopyProgress(std::size_t current, std::size_t total,
                            const std::wstring& file) const {
    if (programmable_) {
        WriteProgress("copy", current, total, file);
        return;
    }
    if (total == 0) {
        return;
    }

    constexpr int width = 50;
    const double percentage = static_cast<double>(current) / static_cast<double>(total);
    const int filled = static_cast<int>(percentage * width);
    std::wstring text = file.substr(0, 40);
    text.resize(40, L' ');

    std::wcout << L"\r["
               << std::wstring(static_cast<std::size_t>(filled), L'#')
               << std::wstring(static_cast<std::size_t>(width - filled), L'-')
               << L"] " << static_cast<int>(percentage * 100.0 + 0.5) << L"% " << text
               << std::flush;
    progressLineActive_ = true;
}

void Reporter::WriteProgress(const char* type, std::size_t current, std::size_t total,
                             const std::wstring& file) const {
    std::cout << "{\"type\":\"" << type
              << "\",\"current\":" << current
              << ",\"total\":" << total
              << ",\"file\":\"" << EscapeJson(ToUtf8(file)) << "\"}\n"
              << std::flush;
}

void Reporter::EndProgressLine() const {
    if (!programmable_ && progressLineActive_) {
        std::wcout << L'\n';
        progressLineActive_ = false;
    }
}

void Reporter::WriteStatus(const char* type, const std::wstring& message) const {
    std::cout << "{\"type\":\"" << type
              << "\",\"message\":\"" << EscapeJson(ToUtf8(message)) << "\"}\n"
              << std::flush;
}

} // namespace fmtp
