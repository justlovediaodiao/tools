#pragma once

#include <cstddef>
#include <string>

namespace fmtp {

class Reporter {
public:
    explicit Reporter(bool programmable);

    void Info(const std::wstring& message) const;
    void Error(const std::wstring& message) const;
    void Success(const std::wstring& message) const;
    void MoveProgress(std::size_t current, std::size_t total, const std::wstring& file) const;
    void CopyProgress(std::size_t current, std::size_t total, const std::wstring& file) const;

private:
    void EndProgressLine() const;
    void WriteStatus(const char* type, const std::wstring& message) const;
    void WriteProgress(const char* type, std::size_t current, std::size_t total,
                       const std::wstring& file) const;

    bool programmable_ = false;
    mutable bool progressLineActive_ = false;
};

} // namespace fmtp
