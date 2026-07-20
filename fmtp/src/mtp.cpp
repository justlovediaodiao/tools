#define UNICODE
#define _UNICODE

#include "fmtp/mtp.h"

#include "fmtp/text.h"

#include <windows.h>
#include <portabledevice.h>
#include <portabledeviceapi.h>
#include <portabledevicetypes.h>
#include <propvarutil.h>
#include <wrl/client.h>

#include <algorithm>
#include <comdef.h>
#include <cwctype>
#include <fstream>
#include <stdexcept>
#include <utility>

using Microsoft::WRL::ComPtr;

namespace fmtp {
namespace {

class ComInit {
public:
    ComInit() {
        const HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        if (FAILED(hr)) {
            throw std::runtime_error("Failed to initialize COM: " + ToUtf8(_com_error(hr).ErrorMessage()));
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

std::wstring Lower(std::wstring value) {
    std::transform(value.begin(), value.end(), value.begin(),
                   [](wchar_t ch) { return static_cast<wchar_t>(std::towlower(ch)); });
    return value;
}

std::string NarrowError(HRESULT hr) {
    return ToUtf8(_com_error(hr).ErrorMessage());
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
    CheckHr(properties->GetValues(objectId.c_str(), nullptr, &values),
            "Failed to read object properties");

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
    CheckHr(properties->GetValues(objectId.c_str(), nullptr, &values),
            "Failed to read object properties");

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
    CheckHr(properties->GetValues(objectId.c_str(), nullptr, &values),
            "Failed to read object properties");

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

std::vector<std::wstring> EnumerateChildIds(IPortableDeviceContent* content,
                                            const std::wstring& parentId) {
    ComPtr<IEnumPortableDeviceObjectIDs> enumIds;
    CheckHr(content->EnumObjects(0, parentId.c_str(), nullptr, &enumIds),
            "Failed to enumerate MTP directory");

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
        if (Lower(GetDeviceFriendlyName(manager, id)) == wanted) {
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
    CheckHr(clientInfo->SetUnsignedIntegerValue(WPD_CLIENT_MAJOR_VERSION, 1),
            "Failed to set WPD client version");
    CheckHr(clientInfo->SetUnsignedIntegerValue(WPD_CLIENT_MINOR_VERSION, 0),
            "Failed to set WPD client version");
    CheckHr(clientInfo->SetUnsignedIntegerValue(WPD_CLIENT_REVISION, 0),
            "Failed to set WPD client version");
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
            if (Lower(GetObjectName(properties, childId)) == wanted) {
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

        files.push_back({childId, name,
                         GetUnsignedLargeIntegerValue(properties, childId, WPD_OBJECT_SIZE)});
    }
    return files;
}

} // namespace

struct MtpDevice::Impl {
    ComInit com;
    ComPtr<IPortableDevice> device;
    ComPtr<IPortableDeviceContent> content;
    ComPtr<IPortableDeviceProperties> properties;
    ComPtr<IPortableDeviceResources> resources;
    std::wstring friendlyName;

    ~Impl() {
        resources.Reset();
        properties.Reset();
        content.Reset();
        if (device.Get() != nullptr) {
            device->Close();
        }
        device.Reset();
    }
};

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
        } else {
            current.push_back(ch);
        }
    }

    std::wstring part = Trim(current);
    if (!part.empty()) {
        parts.push_back(std::move(part));
    }
    if (parts.size() < 2) {
        throw std::invalid_argument("MTP full path must include device name and device directory");
    }

    MtpPath result;
    result.deviceName = parts.front();
    result.segments.assign(parts.begin() + 1, parts.end());
    return result;
}

MtpDevice::MtpDevice() : impl_(std::make_unique<Impl>()) {}
MtpDevice::~MtpDevice() = default;

std::unique_ptr<MtpDevice> MtpDevice::OpenByName(const std::wstring& deviceName) {
    auto result = std::unique_ptr<MtpDevice>(new MtpDevice());

    ComPtr<IPortableDeviceManager> manager;
    CheckHr(CoCreateInstance(CLSID_PortableDeviceManager, nullptr, CLSCTX_INPROC_SERVER,
                             IID_PPV_ARGS(&manager)),
            "Failed to create portable device manager");

    const std::wstring deviceId = FindDeviceIdByName(manager.Get(), deviceName);
    if (deviceId.empty()) {
        return nullptr;
    }

    result->impl_->friendlyName = GetDeviceFriendlyName(manager.Get(), deviceId);
    result->impl_->device = OpenDevice(deviceId);
    CheckHr(result->impl_->device->Content(&result->impl_->content),
            "Failed to get MTP content interface");
    CheckHr(result->impl_->content->Properties(&result->impl_->properties),
            "Failed to get MTP properties interface");
    return result;
}

const std::wstring& MtpDevice::FriendlyName() const {
    return impl_->friendlyName;
}

std::optional<std::vector<MtpFile>> MtpDevice::ListFiles(
    const std::vector<std::wstring>& directorySegments) const {
    const std::wstring directoryId = ResolveDirectoryObjectId(
        impl_->content.Get(), impl_->properties.Get(), directorySegments);
    if (directoryId.empty()) {
        return std::nullopt;
    }
    return EnumerateMtpFiles(impl_->content.Get(), impl_->properties.Get(), directoryId);
}

void MtpDevice::DownloadFile(const MtpFile& file, const std::filesystem::path& destination) {
    PrepareDownloads();

    ComPtr<IStream> stream;
    DWORD optimalBufferSize = 0;
    CheckHr(impl_->resources->GetStream(file.objectId.c_str(), WPD_RESOURCE_DEFAULT, STGM_READ,
                                        &optimalBufferSize, &stream),
            "Failed to open MTP resource stream");

    const DWORD bufferSize = optimalBufferSize == 0 ? 64 * 1024 : optimalBufferSize;
    std::vector<char> buffer(bufferSize);
    std::ofstream output(destination, std::ios::binary | std::ios::trunc);
    if (!output) {
        throw std::runtime_error("Failed to create local file: " + destination.string());
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
            throw std::runtime_error("Failed to write local file: " + destination.string());
        }
    }
}

void MtpDevice::PrepareDownloads() {
    if (impl_->resources.Get() == nullptr) {
        CheckHr(impl_->content->Transfer(&impl_->resources),
                "Failed to create MTP transfer interface");
    }
}

} // namespace fmtp
