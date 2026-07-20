#pragma once

#include <cstdint>
#include <filesystem>
#include <memory>
#include <optional>
#include <string>
#include <vector>

namespace fmtp {

struct MtpPath {
    std::wstring deviceName;
    std::vector<std::wstring> segments;
};

struct MtpFile {
    std::wstring objectId;
    std::wstring name;
    std::uint64_t size = 0;
};

MtpPath ParseMtpFullPath(const std::wstring& fullPath);

class MtpDevice {
public:
    static std::unique_ptr<MtpDevice> OpenByName(const std::wstring& deviceName);

    ~MtpDevice();
    MtpDevice(const MtpDevice&) = delete;
    MtpDevice& operator=(const MtpDevice&) = delete;

    const std::wstring& FriendlyName() const;
    std::optional<std::vector<MtpFile>> ListFiles(
        const std::vector<std::wstring>& directorySegments) const;
    void PrepareDownloads();
    void DownloadFile(const MtpFile& file, const std::filesystem::path& destination);

private:
    struct Impl;

    MtpDevice();
    std::unique_ptr<Impl> impl_;
};

} // namespace fmtp
