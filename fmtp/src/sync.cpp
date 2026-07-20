#define UNICODE
#define _UNICODE

#include "fmtp/sync.h"

#include "fmtp/mtp.h"
#include "fmtp/reporter.h"
#include "fmtp/text.h"

#include <windows.h>

#include <algorithm>
#include <comdef.h>
#include <cwctype>
#include <stdexcept>
#include <unordered_set>
#include <vector>

namespace fs = std::filesystem;

namespace fmtp {
namespace {

std::wstring Lower(std::wstring value) {
    std::transform(value.begin(), value.end(), value.begin(),
                   [](wchar_t ch) { return static_cast<wchar_t>(std::towlower(ch)); });
    return value;
}

std::vector<std::wstring> GetLocalFileNames(const fs::path& localDirectory) {
    std::vector<std::wstring> names;
    for (const auto& entry : fs::directory_iterator(localDirectory)) {
        if (entry.is_regular_file()) {
            names.push_back(entry.path().filename().wstring());
        }
    }
    return names;
}

void MoveToDiff(const fs::path& localDirectory, const fs::path& targetDirectory,
                const std::vector<std::wstring>& files, const Reporter& reporter) {
    if (files.empty()) {
        return;
    }

    reporter.Info(L"Found " + std::to_wstring(files.size()) + L" file(s) to move to diff.");
    std::size_t current = 0;
    for (const auto& fileName : files) {
        const fs::path sourcePath = localDirectory / fileName;
        const fs::path destinationPath = targetDirectory / fileName;
        if (!MoveFileExW(sourcePath.c_str(), destinationPath.c_str(),
                         MOVEFILE_REPLACE_EXISTING | MOVEFILE_COPY_ALLOWED)) {
            throw std::runtime_error("Failed to move " + ToUtf8(fileName) + ": " +
                                     ToUtf8(_com_error(HRESULT_FROM_WIN32(GetLastError())).ErrorMessage()));
        }

        ++current;
        reporter.MoveProgress(current, files.size(), fileName);
    }

    reporter.Info(L"Moved " + std::to_wstring(files.size()) + L" file(s) to " +
                  targetDirectory.wstring());
}

std::size_t CopyFromMtp(MtpDevice& device, const fs::path& localDirectory,
                        const std::vector<MtpFile>& files, const Reporter& reporter) {
    if (files.empty()) {
        return 0;
    }

    reporter.Info(L"Found " + std::to_wstring(files.size()) + L" file(s) to copy from MTP.");

    device.PrepareDownloads();

    std::size_t current = 0;
    std::size_t errors = 0;
    for (const auto& file : files) {
        try {
            device.DownloadFile(file, localDirectory / file.name);
        } catch (const std::exception& ex) {
            ++errors;
            const std::wstring message = L"Failed to download " + file.name + L": " +
                                         FromUtf8(ex.what());
            reporter.Error(message);
        }

        ++current;
        reporter.CopyProgress(current, files.size(), file.name);
    }

    reporter.Info(L"Copied " + std::to_wstring(files.size() - errors) + L" file(s) from MTP.");
    return errors;
}

} // namespace

bool RunSync(const fs::path& localDirectory, const std::wstring& mtpFullPath,
             const Reporter& reporter) {
    if (!fs::is_directory(localDirectory)) {
        reporter.Error(L"Local directory not found: " + localDirectory.wstring());
        return false;
    }

    const MtpPath mtpPath = ParseMtpFullPath(mtpFullPath);
    std::unique_ptr<MtpDevice> device = MtpDevice::OpenByName(mtpPath.deviceName);
    if (device == nullptr) {
        reporter.Error(L"MTP device not found: " + mtpPath.deviceName);
        return false;
    }
    reporter.Info(L"Connect to MTP device: " + device->FriendlyName());

    std::optional<std::vector<MtpFile>> mtpFiles = device->ListFiles(mtpPath.segments);
    if (!mtpFiles.has_value()) {
        std::wstring devicePath;
        for (std::size_t i = 0; i < mtpPath.segments.size(); ++i) {
            if (i != 0) {
                devicePath += L'\\';
            }
            devicePath += mtpPath.segments[i];
        }
        reporter.Error(L"MTP device directory not found: " + devicePath);
        return false;
    }

    const fs::path targetDirectory(L"diff");
    fs::create_directories(targetDirectory);
    const std::vector<std::wstring> localFiles = GetLocalFileNames(localDirectory);

    std::unordered_set<std::wstring> mtpFileNames;
    for (const auto& file : *mtpFiles) {
        mtpFileNames.insert(Lower(file.name));
    }

    std::vector<std::wstring> filesToMove;
    for (const auto& file : localFiles) {
        if (mtpFileNames.find(Lower(file)) == mtpFileNames.end()) {
            filesToMove.push_back(file);
        }
    }
    MoveToDiff(localDirectory, targetDirectory, filesToMove, reporter);

    std::unordered_set<std::wstring> localFileNames;
    for (const auto& file : localFiles) {
        localFileNames.insert(Lower(file));
    }

    std::vector<MtpFile> filesToCopy;
    for (const auto& file : *mtpFiles) {
        if (localFileNames.find(Lower(file.name)) == localFileNames.end()) {
            filesToCopy.push_back(file);
        }
    }

    const std::size_t errors = CopyFromMtp(*device, localDirectory, filesToCopy, reporter);
    if (errors != 0) {
        reporter.Error(L"Completed with " + std::to_wstring(errors) + L" error(s).");
        return false;
    }

    reporter.Success(L"Completed successfully.");
    return true;
}

} // namespace fmtp
