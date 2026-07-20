#pragma once

#include <string>

namespace fmtp {

std::string ToUtf8(const std::wstring& value);
std::wstring FromUtf8(const std::string& value);

} // namespace fmtp
