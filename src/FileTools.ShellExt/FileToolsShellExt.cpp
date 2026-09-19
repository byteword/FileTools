#include <windows.h>
#include <shobjidl.h>
#include <strsafe.h>

#include <algorithm>
#include <cwctype>
#include <iterator>
#include <new>
#include <string>
#include <vector>
#include <array>
#include <chrono>
#include <functional>
#include <memory>
#include <mutex>
#include <wrl/client.h>
#include "UnwrapMenuPolicy.h"

namespace
{
// FileTools 쉘 확장에서 등록할 COM 클래스의 고정 식별자.
constexpr GUID CLSID_FileToolsExplorerCommand =
{ 0x716e7cc4, 0x5941, 0x4362, { 0x8a, 0xca, 0xd3, 0x8c, 0x62, 0x81, 0x7d, 0xe9 } };

// DLL 핸들과 COM 객체/LOCK 수 카운터.
// DllCanUnloadNow에서 모두 0인지 판정해 언로드 가능 여부를 결정한다.
HMODULE g_module = nullptr;
long g_objectCount = 0;
long g_lockCount = 0;

// 셸 메뉴에서 노출되는 동작 목록.
// Root는 최상위 메뉴, 나머지는 실제 실행 동작을 담당한다.
enum class CommandKind
{
    Root,
    Rename,
    FolderWrapFiles,
    FolderUnwrapSameName,
    FolderUnwrapUseFolderName,
    FolderUnwrapKeepFileName,
    FolderUnwrapPrefixFolderName,
    FolderMoveInnerFilesUp,
    FolderMergeSelectedTargets,
    AutoRelocationCurrentFolder,
    AutoRelocationChooseTarget,
    ArchiveMergeGroupByArchiveName,
    ArchiveMergePreserveInternalPaths,
    FileCompare,
    OpenApp
};

struct CommandDefinition
{
    // 동작 식별자
    CommandKind Kind;
    // 메뉴에 표시할 문자열
    const wchar_t* Title;
    // FileTools 실행 시 사용할 verb
    const wchar_t* Verb;
    // 설정 저장 키
    const wchar_t* SettingName;
};

// 서브 메뉴 노출 순서와 설정 키를 묶어 둔 테이블.
constexpr CommandDefinition SubCommands[] =
{
    { CommandKind::Rename, L"파일이름 자동 교정", L"FileNameCorrection", L"ContextMenuFileNameCorrection" },
    { CommandKind::FolderWrapFiles, L"폴더 씌우기", L"FolderWrapFiles", L"ContextMenuFolderWrapFiles" },
    { CommandKind::FolderUnwrapSameName, L"같은 이름 단일 파일 폴더 벗기기", L"FolderUnwrapSameNameSingleFile", L"ContextMenuFolderUnwrapSameNameSingleFile" },
    { CommandKind::FolderUnwrapUseFolderName, L"폴더명으로 벗기기", L"FolderUnwrapUseFolderName", L"ContextMenuFolderUnwrapSingleFile" },
    { CommandKind::FolderUnwrapKeepFileName, L"파일명으로 벗기기", L"FolderUnwrapKeepFileName", L"ContextMenuFolderUnwrapSingleFile" },
    { CommandKind::FolderUnwrapPrefixFolderName, L"폴더명-파일명으로 벗기기", L"FolderUnwrapPrefixFolderName", L"ContextMenuFolderUnwrapSingleFile" },
    { CommandKind::FolderMoveInnerFilesUp, L"폴더 벗기기 — 전체 내용", L"FolderMoveInnerFilesUp", L"ContextMenuFolderMoveInnerFilesUp" },
    { CommandKind::FolderMergeSelectedTargets, L"폴더 병합", L"FolderMergeSelectedTargets", L"ContextMenuFolderMergeSelectedTargets" },
    { CommandKind::AutoRelocationCurrentFolder, L"현재 폴더에서 자동 재배치", L"AutoRelocationCurrentFolder", L"ContextMenuAutoRelocationCurrentFolder" },
    { CommandKind::AutoRelocationChooseTarget, L"선택한 폴더로 자동 재배치", L"AutoRelocationChooseTarget", L"ContextMenuAutoRelocationChooseTarget" },
    { CommandKind::ArchiveMergeGroupByArchiveName, L"ZIP 병합: 압축파일명 폴더로", L"ArchiveMergeGroupByArchiveName", L"ContextMenuArchiveMergeGroupByArchiveName" },
    { CommandKind::ArchiveMergePreserveInternalPaths, L"ZIP 병합: 내부 경로 유지", L"ArchiveMergePreserveInternalPaths", L"ContextMenuArchiveMergePreserveInternalPaths" },
    { CommandKind::FileCompare, L"파일 비교", L"FileCompare", L"ContextMenuFileCompare" },
    { CommandKind::OpenApp, L"FileTools 열기", L"OpenApp", L"ContextMenuOpenApp" }
};

CommandDefinition GetDefinition(CommandKind kind)
{
    // CommandKind를 실제 메뉴 메타데이터로 변환한다.
    // 유효하지 않은 kind는 OpenApp으로 폴백해 안정적으로 동작한다.
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
    return CompareStringOrdinal(left.c_str(), -1, right.c_str(), -1, TRUE) == CSTR_EQUAL;
}

std::vector<std::wstring> GetSelectionPaths(IShellItemArray* selection)
{
    // 셸 선택 목록에서 파일 시스템 경로만 추출해 순서를 유지한 벡터로 반환한다.
    // API 호출이 실패해도 예외를 던지지 않고 빈 목록으로 종료한다.
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
            return {};
        }

        PWSTR rawPath = nullptr;
        if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &rawPath)) && rawPath)
        {
            paths.emplace_back(rawPath);
            CoTaskMemFree(rawPath);
        }

        item->Release();
        if (paths.size() != index + 1)
        {
            return {};
        }
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

// 메뉴 수명에만 보관한다. 빠른 GetState 호출에서는 디스크/레지스트리를 읽지 않는다.
constexpr size_t CommandCount = static_cast<size_t>(CommandKind::OpenApp) + 1;
struct MenuSettings
{
    std::array<bool, CommandCount> Enabled{};
    bool SingleFileSkipsCollisions = false; // 값이 없으면 전체 내용과 합치지 않는다.

    MenuSettings() { Enabled.fill(true); }
};

MenuSettings ReadMenuSettings()
{
    MenuSettings settings;
    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, L"Software\\FileTools\\ContextMenu", 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS)
    {
        return settings;
    }

    for (const auto& command : SubCommands)
    {
        DWORD value = 1, size = sizeof(value);
        if (RegGetValueW(key, nullptr, command.SettingName, RRF_RT_REG_DWORD, nullptr, &value, &size) == ERROR_SUCCESS)
        {
            settings.Enabled[static_cast<size_t>(command.Kind)] = value != 0;
        }
    }
    DWORD policy = MAXDWORD, size = sizeof(policy);
    if (RegGetValueW(key, nullptr, L"FolderUnwrapCollisionPolicy", RRF_RT_REG_DWORD,
        nullptr, &policy, &size) == ERROR_SUCCESS)
    {
        settings.SingleFileSkipsCollisions = policy == 0;
    }
    RegCloseKey(key);
    return settings;
}

unsigned GetUnwrapBit(CommandKind kind)
{
    using namespace FileToolsMenu;
    switch (kind)
    {
    case CommandKind::FolderUnwrapSameName: return SameName;
    case CommandKind::FolderUnwrapKeepFileName: return KeepName;
    case CommandKind::FolderUnwrapUseFolderName: return UseFolderName;
    case CommandKind::FolderUnwrapPrefixFolderName: return PrefixName;
    case CommandKind::FolderMoveInnerFilesUp: return AllContents;
    default: return 0;
    }
}

struct AnalysisStatistics
{
    size_t AttributeReads = 0;
    size_t FolderOpens = 0;
    size_t EntryReads = 0;
};
struct MenuSnapshot
{
    std::array<bool, CommandCount> Visible{};
    AnalysisStatistics Statistics;
};
using AnalysisClock = std::chrono::steady_clock;

/// <summary>단일 파일 여부만 검사한다. 대형 폴더도 두 번째 파일/첫 폴더에서 끝낸다.</summary>
unsigned ReadFolderKind(const std::wstring& path, AnalysisStatistics& statistics, AnalysisClock::time_point deadline)
{
    using namespace FileToolsMenu;
    WIN32_FIND_DATAW data{};
    ++statistics.FolderOpens;
    HANDLE handle = FindFirstFileExW(JoinPath(path, L"*").c_str(), FindExInfoBasic, &data,
        FindExSearchNameMatch, nullptr, 0);
    if (handle == INVALID_HANDLE_VALUE)
    {
        return GetLastError() == ERROR_FILE_NOT_FOUND ? Empty : FileToolsMenu::Unknown;
    }
    struct FindCloser { HANDLE Value; ~FindCloser() { FindClose(Value); } } closer{ handle };
    bool first = true;
    return ClassifyFolderEntries([&](bool& sameName)
    {
        if (AnalysisClock::now() >= deadline) return EntryKind::Error;
        ++statistics.EntryReads;
        if (!first && !FindNextFileW(handle, &data))
        {
            return GetLastError() == ERROR_NO_MORE_FILES ? EntryKind::End : EntryKind::Error;
        }
        first = false;
        const std::wstring name = data.cFileName;
        if (name == L"." || name == L"..") return EntryKind::Ignored;
        if (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) return EntryKind::Directory;
        sameName = EqualsIgnoreCase(GetFileName(path), GetStem(name));
        return EntryKind::File;
    });
}

/// <summary>UNC/네트워크 드라이브는 메뉴에서 폴더 열거를 시도하지 않는다.</summary>
bool IsRemotePath(const std::wstring& path, std::array<int, 26>& driveTypes)
{
    if (path.rfind(L"\\\\", 0) == 0) return true;
    if (path.size() < 3 || path[1] != L':') return true;
    const wchar_t letter = static_cast<wchar_t>(towupper(path[0]));
    if (letter < L'A' || letter > L'Z') return true;
    const size_t index = letter - L'A';
    if (driveTypes[index] == -1)
    {
        const wchar_t root[]{ letter, L':', L'\\', L'\0' };
        driveTypes[index] = static_cast<int>(GetDriveTypeW(root));
    }
    return driveTypes[index] == DRIVE_REMOTE || driveTypes[index] == DRIVE_UNKNOWN || driveTypes[index] == DRIVE_NO_ROOT_DIR;
}

/// <summary>형식 속성은 경로마다 한 번, 내용은 폴더마다 한 번만 읽어 모든 메뉴를 계산한다.</summary>
MenuSnapshot AnalyzeSelection(const std::vector<std::wstring>& paths, const MenuSettings& settings)
{
    using namespace FileToolsMenu;
    MenuSnapshot snapshot;
    if (paths.empty()) return snapshot;
    const auto deadline = AnalysisClock::now() + std::chrono::milliseconds(50);
    constexpr size_t MaxAttributeReads = 256, MaxFolderReads = 128;
    std::array<int, 26> driveTypes;
    driveTypes.fill(-1);
    bool allFileSystem = true, allDirectories = true, allZipFiles = true;
    unsigned kinds = 0;
    unsigned enabledUnwrap = 0;
    for (const auto& command : SubCommands)
        if (settings.Enabled[static_cast<size_t>(command.Kind)]) enabledUnwrap |= GetUnwrapBit(command.Kind);
    std::vector<std::wstring> folders;
    for (const auto& path : paths)
    {
        if (!HasZipExtension(path)) allZipFiles = false;
        if (snapshot.Statistics.AttributeReads >= MaxAttributeReads || AnalysisClock::now() >= deadline || IsRemotePath(path, driveTypes))
        {
            kinds |= FileToolsMenu::Unknown;
            continue;
        }
        ++snapshot.Statistics.AttributeReads;
        const DWORD attributes = GetFileAttributesW(path.c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES)
        {
            // 소실/권한 오류를 단일 파일 폴더로 확정하지 않는다.
            kinds |= FileToolsMenu::Unknown;
            allZipFiles = false;
            if (GetLastError() == ERROR_FILE_NOT_FOUND || GetLastError() == ERROR_PATH_NOT_FOUND)
                allFileSystem = allDirectories = false;
        }
        else if (attributes & FILE_ATTRIBUTE_DIRECTORY)
        {
            allZipFiles = false;
            if (attributes & FILE_ATTRIBUTE_REPARSE_POINT) kinds |= FileToolsMenu::Unknown;
            else folders.push_back(path);
        }
        else
        {
            allDirectories = false;
        }
    }

    // 파일 혼합/명령 비활성/판정 불가 선택에는 추가 열거가 표시 결과를 바꾸지 않는다.
    if (enabledUnwrap && allDirectories && allFileSystem && !(kinds & FileToolsMenu::Unknown))
    {
        for (const auto& folder : folders)
        {
            if (snapshot.Statistics.FolderOpens >= MaxFolderReads || AnalysisClock::now() >= deadline)
            {
                kinds |= FileToolsMenu::Unknown;
                break;
            }
            kinds |= ReadFolderKind(folder, snapshot.Statistics, deadline);
            if ((kinds & FileToolsMenu::Unknown) || (kinds & (Same | Different | Multiple)) == (Same | Different | Multiple))
                break;
        }
    }

    const auto unwrap = allDirectories && allFileSystem
        ? VisibleUnwrapCommands(kinds, enabledUnwrap, settings.SingleFileSkipsCollisions) : 0;
    for (const auto& command : SubCommands)
    {
        const auto index = static_cast<size_t>(command.Kind);
        if (!settings.Enabled[index]) continue;
        if (const auto bit = GetUnwrapBit(command.Kind)) snapshot.Visible[index] = (unwrap & bit) != 0;
        else switch (command.Kind)
        {
        case CommandKind::FolderMergeSelectedTargets: snapshot.Visible[index] = allDirectories && allFileSystem && paths.size() >= 2; break;
        case CommandKind::ArchiveMergeGroupByArchiveName:
        case CommandKind::ArchiveMergePreserveInternalPaths: snapshot.Visible[index] = allZipFiles && allFileSystem && paths.size() >= 2; break;
        case CommandKind::FileCompare: snapshot.Visible[index] = allFileSystem && paths.size() >= 2; break;
        default: snapshot.Visible[index] = allFileSystem; break;
        }
    }
    return snapshot;
}

/// <summary>형제 명령이 같은 선택 스냅샷을 공유한다. 빠른 호출은 검사나 잠금 대기를 하지 않는다.</summary>
class MenuSelectionCache
{
public:
    using Analyzer = std::function<MenuSnapshot(const std::vector<std::wstring>&)>;
    explicit MenuSelectionCache(Analyzer analyzer = [](const auto& paths) { return AnalyzeSelection(paths, ReadMenuSettings()); })
        : _analyzer(std::move(analyzer)) {}

    HRESULT GetState(CommandKind kind, IShellItemArray* selection, BOOL okToBeSlow, EXPCMDSTATE* state)
    {
        *state = ECS_DISABLED;
        if (!selection) { *state = ECS_HIDDEN; return S_OK; }
        Microsoft::WRL::ComPtr<IUnknown> identity;
        const auto identityResult = selection->QueryInterface(IID_PPV_ARGS(&identity));
        if (FAILED(identityResult)) return identityResult;
        std::unique_lock<std::mutex> lock(_mutex, std::defer_lock);
        if (!okToBeSlow)
        {
            if (!lock.try_lock() || !_ready || identity.Get() != _identity.Get()) return E_PENDING;
        }
        else
        {
            lock.lock();
            if (!_ready || identity.Get() != _identity.Get())
            {
                auto paths = GetSelectionPaths(selection);
                if (!_ready || paths != _paths)
                {
                    _snapshot = _analyzer(paths);
                    _paths = std::move(paths);
                    _ready = true;
                }
                _identity = std::move(identity);
            }
        }
        *state = _snapshot.Visible[static_cast<size_t>(kind)] ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }
private:
    std::mutex _mutex;
    Microsoft::WRL::ComPtr<IUnknown> _identity;
    std::vector<std::wstring> _paths;
    MenuSnapshot _snapshot;
    bool _ready = false;
    Analyzer _analyzer;
};

/// <summary>클릭 시에는 중복 대표 여부가 아닌 현재 설정/선택 종류만 확인한다. 내용은 앱이 재검증한다.</summary>
bool CanInvokeCommand(CommandKind kind, const std::vector<std::wstring>& paths)
{
    if (!IsSettingEnabled(GetDefinition(kind).SettingName, true)) return false;
    if (GetUnwrapBit(kind)) return SelectionAllDirectories(paths);
    switch (kind)
    {
    case CommandKind::FolderMergeSelectedTargets: return paths.size() >= 2 && SelectionAllDirectories(paths);
    case CommandKind::ArchiveMergeGroupByArchiveName:
    case CommandKind::ArchiveMergePreserveInternalPaths: return SelectionAllZipFiles(paths);
    case CommandKind::FileCompare: return paths.size() >= 2 && SelectionAnyFileSystemItem(paths);
    default: return SelectionAnyFileSystemItem(paths);
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
    // CreateProcess로 전달할 인자 문자열을 안전하게 만들기 위해
    // 역슬래시/따옴표를 Win32 규칙에 맞게 이스케이프한다.
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
    // 실제 동작 실행 진입점.
    // exe 존재 여부 확인 -> 커맨드 라인 구성 -> 자식 프로세스 시작.
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
    // 하위 명령을 순회해서 쉘에 반환하는 열거자.
    ExplorerCommandEnum();
    ~ExplorerCommandEnum()
    {
        // 열거자 종료 시 소유한 명령 객체의 COM 참조를 정리한다.
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
    // COM 참조 카운트.
    long _ref = 1;
    // 다음으로 반환할 항목 인덱스.
    size_t _index = 0;
    // 캐시해 둔 하위 명령 목록.
    std::vector<IExplorerCommand*> _commands;
};

class ExplorerCommand final : public IExplorerCommand
{
public:
    // 각 메뉴 항목을 나타내는 COM 객체. kind로 동작을 분기한다.
    explicit ExplorerCommand(CommandKind kind, std::shared_ptr<MenuSelectionCache> cache = nullptr)
        : _kind(kind), _cache(std::move(cache))
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

    IFACEMETHODIMP GetState(IShellItemArray* selection, BOOL okToBeSlow, EXPCMDSTATE* commandState) override
    {
        if (!commandState) return E_POINTER;
        *commandState = ECS_HIDDEN;
        if (_kind == CommandKind::Root)
        {
            DWORD count = 0;
            if (selection && SUCCEEDED(selection->GetCount(&count)) && count > 0) *commandState = ECS_ENABLED;
            return S_OK;
        }
        try
        {
            return _cache ? _cache->GetState(_kind, selection, okToBeSlow, commandState) : E_UNEXPECTED;
        }
        catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
        catch (...) { return E_FAIL; }
    }

    IFACEMETHODIMP Invoke(IShellItemArray* selection, IBindCtx*) override
    {
        // Root는 실제 실행 동작이 없고, 하위 항목만 LaunchFileTools를 호출한다.
        if (_kind == CommandKind::Root)
        {
            return S_OK;
        }

        const auto paths = GetSelectionPaths(selection);
        if (!CanInvokeCommand(_kind, paths))
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
        // 하위 메뉴가 필요한 root 메뉴에서만 subcommand enumerator를 반환한다.
        if (!enumCommands)
        {
            return E_POINTER;
        }

        *enumCommands = nullptr;
        if (_kind != CommandKind::Root)
        {
            return E_NOTIMPL;
        }

        try
        {
            *enumCommands = new (std::nothrow) ExplorerCommandEnum();
            return *enumCommands ? S_OK : E_OUTOFMEMORY;
        }
        catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
        catch (...) { return E_FAIL; }
    }

private:
    // COM 참조 카운트.
    long _ref = 1;
    // 이 객체가 담당하는 커맨드 타입.
    CommandKind _kind;
    std::shared_ptr<MenuSelectionCache> _cache;
};

ExplorerCommandEnum::ExplorerCommandEnum()
{
    // 새 열거자는 새 캐시를 소유하므로 이전 메뉴의 파일 상태가 남지 않는다.
    const auto cache = std::make_shared<MenuSelectionCache>();
    _commands.reserve(std::size(SubCommands));
    for (const auto& command : SubCommands)
    {
        auto* item = new (std::nothrow) ExplorerCommand(command.Kind, cache);
        if (item)
        {
            _commands.push_back(item);
        }
    }
}

IFACEMETHODIMP ExplorerCommandEnum::Next(ULONG count, IExplorerCommand** commands, ULONG* fetched)
{
    // IEnumExplorerCommand 규약에 따라 요청 수만큼 포인터를 채우고 실제 반환 수를 반환한다.
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
    // COM 클래스 팩토리.
    // class object 요청 시 Root 명령 객체를 생성해 반환한다.
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
        // 쉘은 aggregation을 사용하지 않으므로 outer를 허용하지 않는다.
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
        // 전역 잠금 카운트를 통해 클래스 로더가 언로드되지 않도록 보조한다.
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
    // 팩토리 COM 참조 카운트.
    long _ref = 1;
};

HRESULT SetStringValue(HKEY root, const std::wstring& keyPath, const wchar_t* name, const std::wstring& value)
{
    // HKCU 하위 키에 문자열 값(REg_SZ)을 설정하는 공통 유틸.
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
    // 등록 시 CLSID와 InprocServer32 경로, threading model을 설정한다.
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
    // 등록 해제 시 동일 CLSID 트리를 제거해 잔여키를 청소한다.
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
    // CLSID 불일치면 클래스 미지원을 반환하고,
    // 일치 시 ClassFactory를 통해 인터페이스를 제공한다.
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
    // 전역 객체/잠금 카운트가 모두 0이면 언로드 허용.
    return g_objectCount == 0 && g_lockCount == 0 ? S_OK : S_FALSE;
}

STDAPI DllRegisterServer()
{
    // regsvr32 /s /i? 경로에서 호출되는 COM 등록 엔트리.
    return RegisterComServer();
}

STDAPI DllUnregisterServer()
{
    // regsvr32 /u에서 호출되는 COM 해제 엔트리.
    UnregisterComServer();
    return S_OK;
}
