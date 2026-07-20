#pragma once

#include <filesystem>
#include <string>

namespace fmtp {

class Reporter;

bool RunSync(const std::filesystem::path& localDirectory,
             const std::wstring& mtpFullPath,
             const Reporter& reporter);

} // namespace fmtp
