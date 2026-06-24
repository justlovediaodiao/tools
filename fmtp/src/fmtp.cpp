#define UNICODE
#define _UNICODE

#include <windows.h>
#include <portabledevice.h>
#include <portabledeviceapi.h>
#include <portabledevicetypes.h>
#include <propvarutil.h>
#include <wrl/client.h>

#include <algorithm>
#include <comdef.h>
#include <cstdint>
#include <cwctype>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>
#include <unordered_set>
#include <utility>
#include <vector>

using Microsoft::WRL::ComPtr;
namespace fs = std::filesystem;

std::string Narrow(const std::wstring& value);
std::wstring Widen(const std::string& value);
std::string NarrowError(HRESULT hr);

struct MtpPath {
    std::wstring deviceName;
    std::vector<std::wstring> segments;
};

struct MtpFile {
    std::wstring objectId;
    std::wstring name;
    std::uint64_t size = 0;
};

class ComInit {
public:
    ComInit() {
        const HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        if (FAILED(hr)) {
            throw std::runtime_error("Failed to initialize COM: " + NarrowError(hr));
        }
        initialized_ = true;
    }

    ~ComInit() {
        if (initialized_) {
            CoUninitialize();
        }
    }

    ComInit(const ComInit&) = delete;
    ComInit& operator=(const ComInit&) = delete;

private:
    bool initialized_ = false;
};

class DeviceCloseGuard {
public:
    explicit DeviceCloseGuard(IPortableDevice* device) : device_(device) {}

    ~DeviceCloseGuard() {
        if (device_ != nullptr) {
            device_->Close();
        }
    }

    DeviceCloseGuard(const DeviceCloseGuard&) = delete;
    DeviceCloseGuard& operator=(const DeviceCloseGuard&) = delete;

private:
    IPortableDevice* device_ = nullptr;
};

std::string Narrow(const std::wstring& value) {
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

std::wstring Widen(const std::string& value) {
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

std::wstring Lower(std::wstring value) {
    std::transform(value.begin(), value.end(), value.begin(),
                   [](wchar_t ch) { return static_cast<wchar_t>(std::towlower(ch)); });
    return value;
}

std::string NarrowError(HRESULT hr) {
    _com_error error(hr);
    return Narrow(error.ErrorMessage());
}

void CheckHr(HRESULT hr, const char* action) {
    if (FAILED(hr)) {
        throw std::runtime_error(std::string(action) + ": " + NarrowError(hr));
    }
}

std::wstring GetStringValue(IPortableDeviceProperties* properties,
                            const std::wstring& objectId,
                            const PROPERTYKEY& key) {
    ComPtr<IPortableDeviceValues> values;
    CheckHr(properties->GetValues(objectId.c_str(), nullptr, &values), "Failed to read object properties");

    PWSTR raw = nullptr;
    const HRESULT hr = values->GetStringValue(key, &raw);
    if (FAILED(hr) || raw == nullptr) {
        if (raw != nullptr) {
            CoTaskMemFree(raw);
        }
        return {};
    }

    std::wstring result(raw);
    CoTaskMemFree(raw);
    return result;
}

GUID GetGuidValue(IPortableDeviceProperties* properties,
                  const std::wstring& objectId,
                  const PROPERTYKEY& key) {
    ComPtr<IPortableDeviceValues> values;
    CheckHr(properties->GetValues(objectId.c_str(), nullptr, &values), "Failed to read object properties");

    GUID value = GUID_NULL;
    if (FAILED(values->GetGuidValue(key, &value))) {
        return GUID_NULL;
    }
    return value;
}

std::uint64_t GetUnsignedLargeIntegerValue(IPortableDeviceProperties* properties,
                                           const std::wstring& objectId,
                                           const PROPERTYKEY& key) {
    ComPtr<IPortableDeviceValues> values;
    CheckHr(properties->GetValues(objectId.c_str(), nullptr, &values), "Failed to read object properties");

    ULONGLONG value = 0;
    if (FAILED(values->GetUnsignedLargeIntegerValue(key, &value))) {
        return 0;
    }
    return static_cast<std::uint64_t>(value);
}

std::wstring GetObjectName(IPortableDeviceProperties* properties, const std::wstring& objectId) {
    std::wstring name = GetStringValue(properties, objectId, WPD_OBJECT_ORIGINAL_FILE_NAME);
    if (name.empty()) {
        name = GetStringValue(properties, objectId, WPD_OBJECT_NAME);
    }
    return name;
}

std::vector<std::wstring> EnumerateChildIds(IPortableDeviceContent* content, const std::wstring& parentId) {
    ComPtr<IEnumPortableDeviceObjectIDs> enumIds;
    CheckHr(content->EnumObjects(0, parentId.c_str(), nullptr, &enumIds), "Failed to enumerate MTP directory");

    std::vector<std::wstring> ids;
    while (true) {
        PWSTR rawId = nullptr;
        DWORD fetched = 0;
        const HRESULT hr = enumIds->Next(1, &rawId, &fetched);
        if (FAILED(hr)) {
            CheckHr(hr, "Failed to read MTP object id");
        }
        if (fetched == 0) {
            break;
        }

        ids.emplace_back(rawId);
        CoTaskMemFree(rawId);
    }

    return ids;
}

bool IsFolderLike(const GUID& contentType) {
    return IsEqualGUID(contentType, WPD_CONTENT_TYPE_FOLDER) ||
           IsEqualGUID(contentType, WPD_CONTENT_TYPE_FUNCTIONAL_OBJECT);
}

bool IsRegularFile(const GUID& contentType) {
    return !IsEqualGUID(contentType, WPD_CONTENT_TYPE_FOLDER) &&
           !IsEqualGUID(contentType, WPD_CONTENT_TYPE_FUNCTIONAL_OBJECT);
}

std::wstring Trim(std::wstring value) {
    const auto first = std::find_if_not(value.begin(), value.end(),
                                        [](wchar_t ch) { return std::iswspace(ch) != 0; });
    const auto last = std::find_if_not(value.rbegin(), value.rend(),
                                       [](wchar_t ch) { return std::iswspace(ch) != 0; }).base();
    if (first >= last) {
        return {};
    }
    return std::wstring(first, last);
}

MtpPath ParseMtpFullPath(const std::wstring& fullPath) {
    std::vector<std::wstring> parts;
    std::wstring current;

    for (const wchar_t ch : fullPath) {
        if (ch == L'\\' || ch == L'/') {
            std::wstring part = Trim(current);
            if (!part.empty()) {
                parts.push_back(std::move(part));
            }
            current.clear();
            continue;
        }
        current.push_back(ch);
    }

    if (!current.empty()) {
        std::wstring part = Trim(current);
        if (!part.empty()) {
            parts.push_back(std::move(part));
        }
    }

    if (parts.size() < 2) {
        throw std::invalid_argument("MTP full path must include device name and device directory");
    }

    MtpPath result;
    result.deviceName = parts.front();
    result.segments.assign(parts.begin() + 1, parts.end());
    return result;
}

std::vector<std::wstring> GetDeviceIds(IPortableDeviceManager* manager) {
    DWORD count = 0;
    CheckHr(manager->GetDevices(nullptr, &count), "Failed to count MTP devices");
    if (count == 0) {
        return {};
    }

    std::vector<PWSTR> rawIds(count, nullptr);
    CheckHr(manager->GetDevices(rawIds.data(), &count), "Failed to enumerate MTP devices");

    std::vector<std::wstring> ids;
    ids.reserve(count);
    for (DWORD i = 0; i < count; ++i) {
        ids.emplace_back(rawIds[i]);
        CoTaskMemFree(rawIds[i]);
    }
    return ids;
}

std::wstring GetDeviceFriendlyName(IPortableDeviceManager* manager, const std::wstring& deviceId) {
    DWORD chars = 0;
    HRESULT hr = manager->GetDeviceFriendlyName(deviceId.c_str(), nullptr, &chars);
    if (FAILED(hr) && hr != HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER)) {
        CheckHr(hr, "Failed to read device friendly name length");
    }

    std::wstring name(chars, L'\0');
    CheckHr(manager->GetDeviceFriendlyName(deviceId.c_str(), name.data(), &chars),
            "Failed to read device friendly name");

    if (!name.empty() && name.back() == L'\0') {
        name.pop_back();
    }
    return name;
}

std::wstring FindDeviceIdByName(IPortableDeviceManager* manager, const std::wstring& deviceName) {
    const std::wstring wanted = Lower(deviceName);
    for (const auto& id : GetDeviceIds(manager)) {
        const std::wstring friendlyName = GetDeviceFriendlyName(manager, id);
        if (Lower(friendlyName) == wanted) {
            return id;
        }
    }
    return {};
}

ComPtr<IPortableDevice> OpenDevice(const std::wstring& deviceId) {
    ComPtr<IPortableDevice> device;
    CheckHr(CoCreateInstance(CLSID_PortableDevice, nullptr, CLSCTX_INPROC_SERVER,
                             IID_PPV_ARGS(&device)),
            "Failed to create portable device instance");

    ComPtr<IPortableDeviceValues> clientInfo;
    CheckHr(CoCreateInstance(CLSID_PortableDeviceValues, nullptr, CLSCTX_INPROC_SERVER,
                             IID_PPV_ARGS(&clientInfo)),
            "Failed to create WPD client info");

    CheckHr(clientInfo->SetStringValue(WPD_CLIENT_NAME, L"fmtp"), "Failed to set WPD client name");
    CheckHr(clientInfo->SetUnsignedIntegerValue(WPD_CLIENT_MAJOR_VERSION, 1), "Failed to set WPD client version");
    CheckHr(clientInfo->SetUnsignedIntegerValue(WPD_CLIENT_MINOR_VERSION, 0), "Failed to set WPD client version");
    CheckHr(clientInfo->SetUnsignedIntegerValue(WPD_CLIENT_REVISION, 0), "Failed to set WPD client version");
    CheckHr(clientInfo->SetUnsignedIntegerValue(WPD_CLIENT_SECURITY_QUALITY_OF_SERVICE,
                                                SECURITY_IMPERSONATION),
            "Failed to set WPD security quality");

    CheckHr(device->Open(deviceId.c_str(), clientInfo.Get()), "Failed to connect to MTP device");
    return device;
}

std::wstring ResolveDirectoryObjectId(IPortableDeviceContent* content,
                                      IPortableDeviceProperties* properties,
                                      const std::vector<std::wstring>& segments) {
    std::wstring currentId = WPD_DEVICE_OBJECT_ID;

    for (const auto& segment : segments) {
        const std::wstring wanted = Lower(segment);
        std::wstring nextId;

        for (const auto& childId : EnumerateChildIds(content, currentId)) {
            const GUID contentType = GetGuidValue(properties, childId, WPD_OBJECT_CONTENT_TYPE);
            if (!IsFolderLike(contentType)) {
                continue;
            }

            const std::wstring name = GetObjectName(properties, childId);
            if (Lower(name) == wanted) {
                nextId = childId;
                break;
            }
        }

        if (nextId.empty()) {
            return {};
        }

        currentId = nextId;
    }

    return currentId;
}

std::vector<MtpFile> EnumerateMtpFiles(IPortableDeviceContent* content,
                                       IPortableDeviceProperties* properties,
                                       const std::wstring& directoryId) {
    std::vector<MtpFile> files;

    for (const auto& childId : EnumerateChildIds(content, directoryId)) {
        const GUID contentType = GetGuidValue(properties, childId, WPD_OBJECT_CONTENT_TYPE);
        if (!IsRegularFile(contentType)) {
            continue;
        }

        const std::wstring name = GetObjectName(properties, childId);
        if (name.empty() || name.front() == L'.') {
            continue;
        }

        MtpFile file;
        file.objectId = childId;
        file.name = name;
        file.size = GetUnsignedLargeIntegerValue(properties, childId, WPD_OBJECT_SIZE);
        files.push_back(std::move(file));
    }

    return files;
}

std::vector<std::wstring> GetLocalFileNames(const fs::path& localDir) {
    std::vector<std::wstring> names;
    for (const auto& entry : fs::directory_iterator(localDir)) {
        if (entry.is_regular_file()) {
            names.push_back(entry.path().filename().wstring());
        }
    }
    return names;
}

void DrawProgressBar(std::uint64_t current, std::uint64_t total, const std::wstring& message = L"") {
    if (total == 0) {
        return;
    }

    constexpr int width = 50;
    const double percentage = static_cast<double>(current) / static_cast<double>(total);
    const int filled = static_cast<int>(percentage * width);
    std::wstring text = message.substr(0, 40);
    text.resize(40, L' ');

    std::wcout << L"\r["
               << std::wstring(static_cast<std::size_t>(filled), L'#')
               << std::wstring(static_cast<std::size_t>(width - filled), L'-')
               << L"] " << static_cast<int>(percentage * 100.0 + 0.5) << L"% " << text
               << std::flush;
}

void MoveToDiff(const fs::path& localDir, const fs::path& targetDir, const std::vector<std::wstring>& filesToMove) {
    if (filesToMove.empty()) {
        return;
    }

    std::wcout << L"Found " << filesToMove.size() << L" file(s) to move to diff.\n";

    for (const auto& fileName : filesToMove) {
        const fs::path sourcePath = localDir / fileName;
        const fs::path destPath = targetDir / fileName;

        if (!MoveFileExW(sourcePath.c_str(), destPath.c_str(),
                         MOVEFILE_REPLACE_EXISTING | MOVEFILE_COPY_ALLOWED)) {
            throw std::runtime_error("Failed to move " + Narrow(fileName) + ": " +
                                     NarrowError(HRESULT_FROM_WIN32(GetLastError())));
        }

        std::wcout << fileName << L"\n";
    }

    std::wcout << L"Moved " << filesToMove.size() << L" file(s) to " << targetDir.wstring() << L"\n";
}

void DownloadMtpFile(IPortableDeviceResources* resources, const MtpFile& file, const fs::path& destPath) {
    ComPtr<IStream> stream;
    DWORD optimalBufferSize = 0;
    CheckHr(resources->GetStream(file.objectId.c_str(), WPD_RESOURCE_DEFAULT, STGM_READ,
                                 &optimalBufferSize, &stream),
            "Failed to open MTP resource stream");

    const DWORD bufferSize = optimalBufferSize == 0 ? 64 * 1024 : optimalBufferSize;
    std::vector<char> buffer(bufferSize);
    std::ofstream output(destPath, std::ios::binary | std::ios::trunc);
    if (!output) {
        throw std::runtime_error("Failed to create local file: " + destPath.string());
    }

    while (true) {
        ULONG bytesRead = 0;
        const HRESULT hr = stream->Read(buffer.data(), static_cast<ULONG>(buffer.size()), &bytesRead);
        if (FAILED(hr)) {
            CheckHr(hr, "Failed to read MTP resource stream");
        }
        if (bytesRead == 0) {
            break;
        }

        output.write(buffer.data(), bytesRead);
        if (!output) {
            throw std::runtime_error("Failed to write local file: " + destPath.string());
        }
    }
}

void CopyFromMtp(IPortableDeviceContent* content, const fs::path& localDir, const std::vector<MtpFile>& filesToCopy) {
    if (filesToCopy.empty()) {
        return;
    }

    std::wcout << L"Found " << filesToCopy.size() << L" file(s) to copy from MTP.\n";

    std::uint64_t totalBytes = 0;
    for (const auto& file : filesToCopy) {
        totalBytes += file.size;
    }

    ComPtr<IPortableDeviceResources> resources;
    CheckHr(content->Transfer(&resources), "Failed to create MTP transfer interface");

    std::uint64_t copiedBytes = 0;
    DrawProgressBar(0, totalBytes, L"Starting...");

    for (const auto& file : filesToCopy) {
        const fs::path destPath = localDir / file.name;
        try {
            DrawProgressBar(copiedBytes, totalBytes, file.name);
            DownloadMtpFile(resources.Get(), file, destPath);
            copiedBytes += file.size;
            DrawProgressBar(copiedBytes, totalBytes, file.name);
        } catch (const std::exception& ex) {
            std::wcout << L"\nFailed to download " << file.name << L": " << Widen(ex.what()) << L"\n";
            DrawProgressBar(copiedBytes, totalBytes, L"");
        }
    }

    std::wcout << L"\nCopied " << filesToCopy.size() << L" file(s) from MTP.\n";
}

int wmain(int argc, wchar_t* argv[]) {
    if (argc != 3) {
        std::wcout << L"Usage: <local path> <MTP full path>\n";
        return 0;
    }

    try {
        const fs::path localDir(argv[1]);
        const std::wstring mtpFullPath(argv[2]);
        const fs::path targetDir(L"diff");

        if (!fs::is_directory(localDir)) {
            std::wcout << L"Local directory not found: " << localDir.wstring() << L"\n";
            return 0;
        }

        const MtpPath mtpPath = ParseMtpFullPath(mtpFullPath);

        ComInit com;

        ComPtr<IPortableDeviceManager> manager;
        CheckHr(CoCreateInstance(CLSID_PortableDeviceManager, nullptr, CLSCTX_INPROC_SERVER,
                                 IID_PPV_ARGS(&manager)),
                "Failed to create portable device manager");

        const std::wstring deviceId = FindDeviceIdByName(manager.Get(), mtpPath.deviceName);
        if (deviceId.empty()) {
            std::wcout << L"MTP device not found: " << mtpPath.deviceName << L"\n";
            return 0;
        }

        const std::wstring friendlyName = GetDeviceFriendlyName(manager.Get(), deviceId);
        std::wcout << L"Connect to MTP device: " << friendlyName << L"\n";

        ComPtr<IPortableDevice> device = OpenDevice(deviceId);
        DeviceCloseGuard closeDevice(device.Get());

        ComPtr<IPortableDeviceContent> content;
        CheckHr(device->Content(&content), "Failed to get MTP content interface");

        ComPtr<IPortableDeviceProperties> properties;
        CheckHr(content->Properties(&properties), "Failed to get MTP properties interface");

        const std::wstring directoryId = ResolveDirectoryObjectId(content.Get(), properties.Get(), mtpPath.segments);
        if (directoryId.empty()) {
            std::wstring devicePath;
            for (std::size_t i = 0; i < mtpPath.segments.size(); ++i) {
                if (i != 0) {
                    devicePath += L'\\';
                }
                devicePath += mtpPath.segments[i];
            }
            std::wcout << L"MTP device directory not found: " << devicePath << L"\n";
            return 0;
        }

        fs::create_directories(targetDir);

        const std::vector<std::wstring> localFiles = GetLocalFileNames(localDir);
        const std::vector<MtpFile> mtpFiles = EnumerateMtpFiles(content.Get(), properties.Get(), directoryId);

        std::unordered_set<std::wstring> mtpFileNames;
        for (const auto& file : mtpFiles) {
            mtpFileNames.insert(Lower(file.name));
        }

        std::vector<std::wstring> filesToMove;
        for (const auto& localFile : localFiles) {
            if (mtpFileNames.find(Lower(localFile)) == mtpFileNames.end()) {
                filesToMove.push_back(localFile);
            }
        }

        MoveToDiff(localDir, targetDir, filesToMove);

        std::unordered_set<std::wstring> localFileNames;
        for (const auto& file : localFiles) {
            localFileNames.insert(Lower(file));
        }

        std::vector<MtpFile> filesToCopy;
        for (const auto& file : mtpFiles) {
            if (localFileNames.find(Lower(file.name)) == localFileNames.end()) {
                filesToCopy.push_back(file);
            }
        }

        CopyFromMtp(content.Get(), localDir, filesToCopy);
    } catch (const std::exception& ex) {
        std::wcout << L"Error: " << Widen(ex.what()) << L"\n";
    }

    return 0;
}
