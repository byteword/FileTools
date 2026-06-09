#include <windows.h>
#include <shobjidl.h>
#include <strsafe.h>

#include <algorithm>
#include <cwctype>
#include <iterator>
#include <new>
#include <string>
#include <vector>

namespace
{
constexpr GUID CLSID_FileToolsExplorerCommand =
{ 0x716e7cc4, 0x5941, 0x4362, { 0x8a, 0xca, 0xd3, 0x8c, 0x62, 0x81, 0x7d, 0xe9 } };

HMODULE g_module = nullptr;
long g_objectCount = 0;
long g_lockCount = 0;

enum class CommandKind
{
    Root,
    Rename,
    FolderWrapFiles,
    FolderUnwrapSameName,
    FolderUnwrapUseFolderName,
    FolderUnwrapKeepFileName,
    FolderMoveInnerFilesUp,
    FolderMergeSelectedTargets,
    AutoRelocationCurrentFolder,
    AutoRelocationChooseTarget,
    ArchiveMergeGroupByArchiveName,
    ArchiveMergePreserveInternalPaths,
    OpenApp
};

struct CommandDefinition
{
    CommandKind Kind;
    const wchar_t* Title;
    const wchar_t* Verb;
    const wchar_t* SettingName;
};

constexpr CommandDefinition SubCommands[] =
{
    { CommandKind::Rename, L"파일이름 자동 교정", L"FileNameCorrection", L"ContextMenuFileNameCorrection" },
    { CommandKind::FolderWrapFiles, L"파일 폴더로 모으기", L"FolderWrapFiles", L"ContextMenuFolderWrapFiles" },
    { CommandKind::FolderUnwrapSameName, L"폴더 벗기기", L"FolderUnwrapSameNameSingleFile", L"ContextMenuFolderUnwrapSameNameSingleFile" },
    { CommandKind::FolderUnwrapUseFolderName, L"폴더명으로 벗기기", L"FolderUnwrapUseFolderName", L"ContextMenuFolderUnwrapSingleFile" },
    { CommandKind::FolderUnwrapKeepFileName, L"파일명으로 벗기기", L"FolderUnwrapKeepFileName", L"ContextMenuFolderUnwrapSingleFile" },
    { CommandKind::FolderMoveInnerFilesUp, L"폴더 내부 파일 상위로 이동", L"FolderMoveInnerFilesUp", L"ContextMenuFolderMoveInnerFilesUp" },
    { CommandKind::FolderMergeSelectedTargets, L"폴더합치기", L"FolderMergeSelectedTargets", L"ContextMenuFolderMergeSelectedTargets" },
    { CommandKind::AutoRelocationCurrentFolder, L"현재 폴더에서 자동 재배치", L"AutoRelocationCurrentFolder", L"ContextMenuAutoRelocationCurrentFolder" },
    { CommandKind::AutoRelocationChooseTarget, L"선택한 폴더로 자동 재배치", L"AutoRelocationChooseTarget", L"ContextMenuAutoRelocationChooseTarget" },
    { CommandKind::ArchiveMergeGroupByArchiveName, L"ZIP 병합: 압축파일명 폴더로", L"ArchiveMergeGroupByArchiveName", L"ContextMenuArchiveMergeGroupByArchiveName" },
    { CommandKind::ArchiveMergePreserveInternalPaths, L"ZIP 병합: 내부 경로 유지", L"ArchiveMergePreserveInternalPaths", L"ContextMenuArchiveMergePreserveInternalPaths" },
    { CommandKind::OpenApp, L"FileTools 열기", L"OpenApp", L"ContextMenuOpenApp" }
};

CommandDefinition GetDefinition(CommandKind kind)
{
    if (kind == CommandKind::Root)
    {
        return { CommandKind::Root, L"FileTools", L"", L"" };
    }

    for (const auto& command : SubCommands)
    {
        if (command.Kind == kind)
        {
            return command;
        }
    }

    return { CommandKind::OpenApp, L"FileTools 열기", L"OpenApp", L"ContextMenuOpenApp" };
}

bool IsPathDirectory(const std::wstring& path)
{
    const DWORD attributes = GetFileAttributesW(path.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
}

bool IsPathFile(const std::wstring& path)
{
    const DWORD attributes = GetFileAttributesW(path.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
}

std::wstring GetFileName(const std::wstring& path)
{
    const size_t slash = path.find_last_of(L"\\/");
    return slash == std::wstring::npos ? path : path.substr(slash + 1);
}

std::wstring GetStem(const std::wstring& name)
{
    const size_t dot = name.find_last_of(L'.');
    return dot == std::wstring::npos ? name : name.substr(0, dot);
}

std::wstring JoinPath(const std::wstring& left, const std::wstring& right)
{
    if (left.empty())
    {
        return right;
    }

    if (left.back() == L'\\' || left.back() == L'/')
    {
        return left + right;
    }

    return left + L"\\" + right;
}

bool EqualsIgnoreCase(const std::wstring& left, const std::wstring& right)
{
    return _wcsicmp(left.c_str(), right.c_str()) == 0;
}

enum class SingleFileFolderState
{
    NotSingleFileFolder,
    SameName,
    DifferentName
};

SingleFileFolderState GetSingleFileFolderState(const std::wstring& folderPath)
{
    WIN32_FIND_DATAW data{};
    HANDLE find = FindFirstFileW(JoinPath(folderPath, L"*").c_str(), &data);
    if (find == INVALID_HANDLE_VALUE)
    {
        return SingleFileFolderState::NotSingleFileFolder;
    }

    std::wstring onlyFileName;
    int fileCount = 0;
    int directoryCount = 0;
    do
    {
        const std::wstring name = data.cFileName;
        if (name == L"." || name == L"..")
        {
            continue;
        }

        if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
        {
            directoryCount++;
            continue;
        }

        fileCount++;
        onlyFileName = name;
    }
    while (FindNextFileW(find, &data));

    FindClose(find);
    if (fileCount != 1 || directoryCount != 0)
    {
        return SingleFileFolderState::NotSingleFileFolder;
    }

    const std::wstring folderName = GetFileName(folderPath);
    return EqualsIgnoreCase(folderName, GetStem(onlyFileName))
        ? SingleFileFolderState::SameName
        : SingleFileFolderState::DifferentName;
}

std::vector<std::wstring> GetSelectionPaths(IShellItemArray* selection)
{
    std::vector<std::wstring> paths;
    if (!selection)
    {
        return paths;
    }

    DWORD count = 0;
    if (FAILED(selection->GetCount(&count)))
    {
        return paths;
    }

    for (DWORD index = 0; index < count; index++)
    {
        IShellItem* item = nullptr;
        if (FAILED(selection->GetItemAt(index, &item)) || !item)
        {
            continue;
        }

        PWSTR rawPath = nullptr;
        if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &rawPath)) && rawPath)
        {
            paths.emplace_back(rawPath);
            CoTaskMemFree(rawPath);
        }

        item->Release();
    }

    return paths;
}

bool IsSettingEnabled(const wchar_t* valueName, bool defaultValue)
{
    if (!valueName || valueName[0] == L'\0')
    {
        return defaultValue;
    }

    DWORD value = defaultValue ? 1u : 0u;
    DWORD valueSize = sizeof(value);
    const LSTATUS status = RegGetValueW(
        HKEY_CURRENT_USER,
        L"Software\\FileTools\\ContextMenu",
        valueName,
        RRF_RT_REG_DWORD,
        nullptr,
        &value,
        &valueSize);
    return status == ERROR_SUCCESS ? value != 0 : defaultValue;
}

bool SelectionAllFiles(const std::vector<std::wstring>& paths)
{
    return !paths.empty() && std::all_of(paths.begin(), paths.end(), IsPathFile);
}

bool HasZipExtension(const std::wstring& path)
{
    const size_t dot = path.find_last_of(L'.');
    if (dot == std::wstring::npos)
    {
        return false;
    }

    return EqualsIgnoreCase(path.substr(dot), L".zip");
}

bool SelectionAllZipFiles(const std::vector<std::wstring>& paths)
{
    return paths.size() >= 2 &&
        std::all_of(paths.begin(), paths.end(), [](const std::wstring& path)
        {
            return IsPathFile(path) && HasZipExtension(path);
        });
}

bool SelectionAllDirectories(const std::vector<std::wstring>& paths)
{
    return !paths.empty() && std::all_of(paths.begin(), paths.end(), IsPathDirectory);
}

bool SelectionAnyFileSystemItem(const std::vector<std::wstring>& paths)
{
    return !paths.empty() && std::all_of(paths.begin(), paths.end(), [](const std::wstring& path)
    {
        return IsPathFile(path) || IsPathDirectory(path);
    });
}

bool SelectionSingleFileFolderState(
    const std::vector<std::wstring>& paths,
    SingleFileFolderState expected)
{
    if (!SelectionAllDirectories(paths))
    {
        return false;
    }

    bool sawExpected = false;
    for (const auto& path : paths)
    {
        const SingleFileFolderState state = GetSingleFileFolderState(path);
        if (state == SingleFileFolderState::NotSingleFileFolder)
        {
            return false;
        }

        if (state == expected)
        {
            sawExpected = true;
        }
        else if (state != expected)
        {
            return false;
        }
    }

    return sawExpected;
}

bool IsCommandVisible(CommandKind kind, const std::vector<std::wstring>& paths)
{
    const auto definition = GetDefinition(kind);
    if (!IsSettingEnabled(definition.SettingName, true))
    {
        return false;
    }

    switch (kind)
    {
    case CommandKind::Rename:
        return SelectionAnyFileSystemItem(paths);
    case CommandKind::FolderWrapFiles:
        return SelectionAllFiles(paths);
    case CommandKind::FolderUnwrapSameName:
        return SelectionSingleFileFolderState(paths, SingleFileFolderState::SameName);
    case CommandKind::FolderUnwrapUseFolderName:
    case CommandKind::FolderUnwrapKeepFileName:
        return SelectionSingleFileFolderState(paths, SingleFileFolderState::DifferentName);
    case CommandKind::FolderMoveInnerFilesUp:
        return SelectionAllDirectories(paths);
    case CommandKind::FolderMergeSelectedTargets:
        return paths.size() >= 2 && SelectionAnyFileSystemItem(paths);
    case CommandKind::AutoRelocationCurrentFolder:
    case CommandKind::AutoRelocationChooseTarget:
        return SelectionAnyFileSystemItem(paths);
    case CommandKind::ArchiveMergeGroupByArchiveName:
    case CommandKind::ArchiveMergePreserveInternalPaths:
        return SelectionAllZipFiles(paths);
    case CommandKind::OpenApp:
        return SelectionAnyFileSystemItem(paths);
    default:
        return false;
    }
}

std::wstring GetModuleDirectory()
{
    wchar_t path[MAX_PATH]{};
    DWORD length = GetModuleFileNameW(g_module, path, static_cast<DWORD>(std::size(path)));
    if (length == 0 || length >= std::size(path))
    {
        return {};
    }

    std::wstring modulePath(path, length);
    const size_t slash = modulePath.find_last_of(L"\\/");
    return slash == std::wstring::npos ? std::wstring{} : modulePath.substr(0, slash);
}

std::wstring QuoteArgument(const std::wstring& value)
{
    std::wstring result = L"\"";
    unsigned backslashes = 0;
    for (const wchar_t ch : value)
    {
        if (ch == L'\\')
        {
            backslashes++;
            result.push_back(ch);
            continue;
        }

        if (ch == L'"')
        {
            result.append(backslashes + 1, L'\\');
            result.push_back(ch);
            backslashes = 0;
            continue;
        }

        backslashes = 0;
        result.push_back(ch);
    }

    result.append(backslashes, L'\\');
    result.push_back(L'"');
    return result;
}

HRESULT LaunchFileTools(CommandKind kind, const std::vector<std::wstring>& paths)
{
    const std::wstring exePath = JoinPath(GetModuleDirectory(), L"FileTools.exe");
    if (exePath.empty() || GetFileAttributesW(exePath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
    }

    const auto definition = GetDefinition(kind);
    std::wstring commandLine = QuoteArgument(exePath);
    if (kind == CommandKind::OpenApp)
    {
        commandLine += L" /open";
    }
    else
    {
        commandLine += L" /context ";
        commandLine += definition.Verb;
    }

    for (const auto& path : paths)
    {
        commandLine.push_back(L' ');
        commandLine += QuoteArgument(path);
    }

    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    std::vector<wchar_t> buffer(commandLine.begin(), commandLine.end());
    buffer.push_back(L'\0');

    const BOOL created = CreateProcessW(
        nullptr,
        buffer.data(),
        nullptr,
        nullptr,
        FALSE,
        0,
        nullptr,
        nullptr,
        &startup,
        &process);
    if (!created)
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return S_OK;
}

class ExplorerCommand;

class ExplorerCommandEnum final : public IEnumExplorerCommand
{
public:
    ExplorerCommandEnum();
    ~ExplorerCommandEnum()
    {
        for (auto* command : _commands)
        {
            command->Release();
        }
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** result) override
    {
        if (!result)
        {
            return E_POINTER;
        }

        *result = nullptr;
        if (riid == IID_IUnknown || riid == IID_IEnumExplorerCommand)
        {
            *result = static_cast<IEnumExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&_ref);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG ref = InterlockedDecrement(&_ref);
        if (ref == 0)
        {
            delete this;
        }

        return ref;
    }

    IFACEMETHODIMP Next(ULONG count, IExplorerCommand** commands, ULONG* fetched) override;

    IFACEMETHODIMP Skip(ULONG) override
    {
        return E_NOTIMPL;
    }

    IFACEMETHODIMP Reset() override
    {
        _index = 0;
        return S_OK;
    }

    IFACEMETHODIMP Clone(IEnumExplorerCommand**) override
    {
        return E_NOTIMPL;
    }

private:
    long _ref = 1;
    size_t _index = 0;
    std::vector<IExplorerCommand*> _commands;
};

class ExplorerCommand final : public IExplorerCommand
{
public:
    explicit ExplorerCommand(CommandKind kind) : _kind(kind)
    {
        InterlockedIncrement(&g_objectCount);
    }

    ~ExplorerCommand()
    {
        InterlockedDecrement(&g_objectCount);
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** result) override
    {
        if (!result)
        {
            return E_POINTER;
        }

        *result = nullptr;
        if (riid == IID_IUnknown || riid == IID_IExplorerCommand)
        {
            *result = static_cast<IExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&_ref);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG ref = InterlockedDecrement(&_ref);
        if (ref == 0)
        {
            delete this;
        }

        return ref;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
    {
        if (!title)
        {
            return E_POINTER;
        }

        *title = nullptr;
        const auto definition = GetDefinition(_kind);
        const size_t bytes = (wcslen(definition.Title) + 1) * sizeof(wchar_t);
        *title = static_cast<PWSTR>(CoTaskMemAlloc(bytes));
        if (!*title)
        {
            return E_OUTOFMEMORY;
        }

        HRESULT hr = StringCchCopyW(*title, bytes / sizeof(wchar_t), definition.Title);
        if (FAILED(hr))
        {
            CoTaskMemFree(*title);
            *title = nullptr;
        }

        return hr;
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
    {
        if (!icon)
        {
            return E_POINTER;
        }

        *icon = nullptr;
        const std::wstring iconPath = JoinPath(GetModuleDirectory(), L"FileTools.exe");
        const size_t bytes = (iconPath.length() + 1) * sizeof(wchar_t);
        *icon = static_cast<PWSTR>(CoTaskMemAlloc(bytes));
        if (!*icon)
        {
            return E_OUTOFMEMORY;
        }

        HRESULT hr = StringCchCopyW(*icon, bytes / sizeof(wchar_t), iconPath.c_str());
        if (FAILED(hr))
        {
            CoTaskMemFree(*icon);
            *icon = nullptr;
        }

        return hr;
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* toolTip) override
    {
        if (!toolTip)
        {
            return E_POINTER;
        }

        *toolTip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* guidCommandName) override
    {
        if (!guidCommandName)
        {
            return E_POINTER;
        }

        *guidCommandName = CLSID_FileToolsExplorerCommand;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* selection, BOOL, EXPCMDSTATE* commandState) override
    {
        if (!commandState)
        {
            return E_POINTER;
        }

        const auto paths = GetSelectionPaths(selection);
        if (_kind == CommandKind::Root)
        {
            *commandState = SelectionAnyFileSystemItem(paths) ? ECS_ENABLED : ECS_HIDDEN;
            return S_OK;
        }

        *commandState = IsCommandVisible(_kind, paths) ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* selection, IBindCtx*) override
    {
        if (_kind == CommandKind::Root)
        {
            return S_OK;
        }

        const auto paths = GetSelectionPaths(selection);
        if (!IsCommandVisible(_kind, paths))
        {
            return S_OK;
        }

        return LaunchFileTools(_kind, paths);
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (!flags)
        {
            return E_POINTER;
        }

        *flags = _kind == CommandKind::Root ? ECF_HASSUBCOMMANDS : ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enumCommands) override
    {
        if (!enumCommands)
        {
            return E_POINTER;
        }

        *enumCommands = nullptr;
        if (_kind != CommandKind::Root)
        {
            return E_NOTIMPL;
        }

        *enumCommands = new (std::nothrow) ExplorerCommandEnum();
        return *enumCommands ? S_OK : E_OUTOFMEMORY;
    }

private:
    long _ref = 1;
    CommandKind _kind;
};

ExplorerCommandEnum::ExplorerCommandEnum()
{
    for (const auto& command : SubCommands)
    {
        auto* item = new (std::nothrow) ExplorerCommand(command.Kind);
        if (item)
        {
            _commands.push_back(item);
        }
    }
}

IFACEMETHODIMP ExplorerCommandEnum::Next(ULONG count, IExplorerCommand** commands, ULONG* fetched)
{
    if (!commands)
    {
        return E_POINTER;
    }

    ULONG actual = 0;
    while (actual < count && _index < _commands.size())
    {
        commands[actual] = _commands[_index];
        commands[actual]->AddRef();
        actual++;
        _index++;
    }

    if (fetched)
    {
        *fetched = actual;
    }

    return actual == count ? S_OK : S_FALSE;
}

class ClassFactory final : public IClassFactory
{
public:
    ClassFactory()
    {
        InterlockedIncrement(&g_objectCount);
    }

    ~ClassFactory()
    {
        InterlockedDecrement(&g_objectCount);
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** result) override
    {
        if (!result)
        {
            return E_POINTER;
        }

        *result = nullptr;
        if (riid == IID_IUnknown || riid == IID_IClassFactory)
        {
            *result = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&_ref);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG ref = InterlockedDecrement(&_ref);
        if (ref == 0)
        {
            delete this;
        }

        return ref;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** result) override
    {
        if (outer)
        {
            return CLASS_E_NOAGGREGATION;
        }

        auto* command = new (std::nothrow) ExplorerCommand(CommandKind::Root);
        if (!command)
        {
            return E_OUTOFMEMORY;
        }

        const HRESULT hr = command->QueryInterface(riid, result);
        command->Release();
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock)
        {
            InterlockedIncrement(&g_lockCount);
        }
        else
        {
            InterlockedDecrement(&g_lockCount);
        }

        return S_OK;
    }

private:
    long _ref = 1;
};

HRESULT SetStringValue(HKEY root, const std::wstring& keyPath, const wchar_t* name, const std::wstring& value)
{
    HKEY key = nullptr;
    const LSTATUS createStatus = RegCreateKeyExW(root, keyPath.c_str(), 0, nullptr, 0, KEY_SET_VALUE, nullptr, &key, nullptr);
    if (createStatus != ERROR_SUCCESS)
    {
        return HRESULT_FROM_WIN32(createStatus);
    }

    const DWORD bytes = static_cast<DWORD>((value.length() + 1) * sizeof(wchar_t));
    const LSTATUS setStatus = RegSetValueExW(
        key,
        name,
        0,
        REG_SZ,
        reinterpret_cast<const BYTE*>(value.c_str()),
        bytes);
    RegCloseKey(key);
    return HRESULT_FROM_WIN32(setStatus);
}

HRESULT RegisterComServer()
{
    wchar_t dllPath[MAX_PATH]{};
    const DWORD length = GetModuleFileNameW(g_module, dllPath, static_cast<DWORD>(std::size(dllPath)));
    if (length == 0 || length >= std::size(dllPath))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    const std::wstring clsid = L"Software\\Classes\\CLSID\\{716e7cc4-5941-4362-8aca-d38c62817de9}";
    HRESULT hr = SetStringValue(HKEY_CURRENT_USER, clsid, nullptr, L"FileTools Shell Extension");
    if (FAILED(hr))
    {
        return hr;
    }

    hr = SetStringValue(HKEY_CURRENT_USER, clsid + L"\\InprocServer32", nullptr, dllPath);
    if (FAILED(hr))
    {
        return hr;
    }

    return SetStringValue(HKEY_CURRENT_USER, clsid + L"\\InprocServer32", L"ThreadingModel", L"Apartment");
}

void UnregisterComServer()
{
    RegDeleteTreeW(HKEY_CURRENT_USER, L"Software\\Classes\\CLSID\\{716e7cc4-5941-4362-8aca-d38c62817de9}");
}
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }

    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID classId, REFIID riid, void** result)
{
    if (classId != CLSID_FileToolsExplorerCommand)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    auto* factory = new (std::nothrow) ClassFactory();
    if (!factory)
    {
        return E_OUTOFMEMORY;
    }

    const HRESULT hr = factory->QueryInterface(riid, result);
    factory->Release();
    return hr;
}

STDAPI DllCanUnloadNow()
{
    return g_objectCount == 0 && g_lockCount == 0 ? S_OK : S_FALSE;
}

STDAPI DllRegisterServer()
{
    return RegisterComServer();
}

STDAPI DllUnregisterServer()
{
    UnregisterComServer();
    return S_OK;
}
