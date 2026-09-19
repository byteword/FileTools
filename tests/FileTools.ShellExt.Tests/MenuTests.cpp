// Include the shipped implementation: these tests exercise its COM cache and I/O, not a copy.
#include "../../src/FileTools.ShellExt/FileToolsShellExt.cpp"
#include <shlobj.h>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <future>
#include <thread>

namespace fs = std::filesystem;
using namespace FileToolsMenu;
static unsigned checks = 0;
void Check(bool condition, const char* message)
{
    ++checks;
    if (!condition) throw std::runtime_error(message);
}

struct Fixture
{
    fs::path root;
    Fixture()
    {
        GUID id{};
        CoCreateGuid(&id);
        wchar_t text[40]{};
        StringFromGUID2(id, text, 40);
        root = fs::temp_directory_path() / (std::wstring(L"FileTools.MenuTests-") + text);
        fs::create_directory(root);
    }
    ~Fixture()
    {
        // Only delete this test's unique, absolute child of the temporary directory.
        if (root.is_absolute() && root.parent_path() == fs::temp_directory_path() &&
            root.filename().wstring().rfind(L"FileTools.MenuTests-", 0) == 0)
        {
            std::error_code ignored;
            fs::remove_all(root, ignored);
        }
    }
    fs::path Folder(const wchar_t* name)
    {
        auto path = root / name;
        fs::create_directory(path);
        return path;
    }
};

Microsoft::WRL::ComPtr<IShellItemArray> Selection(const fs::path& path)
{
    Microsoft::WRL::ComPtr<IShellItem> item;
    Check(SUCCEEDED(SHCreateItemFromParsingName(path.c_str(), nullptr, IID_PPV_ARGS(&item))), "Create shell item");
    Microsoft::WRL::ComPtr<IShellItemArray> result;
    Check(SUCCEEDED(SHCreateShellItemArrayFromShellItem(item.Get(), IID_PPV_ARGS(&result))), "Create selection");
    return result;
}

unsigned UnwrapMask(const MenuSnapshot& snapshot)
{
    unsigned mask = 0;
    for (const auto& command : SubCommands)
        if (snapshot.Visible[static_cast<size_t>(command.Kind)]) mask |= GetUnwrapBit(command.Kind);
    return mask;
}

void TestPolicy()
{
    const unsigned expected[]{ 0, 1, 14, 15, 16, 17, 30, 31 };
    for (unsigned kinds = 0; kinds < 8; ++kinds)
    {
        Check(VisibleUnwrapCommands(kinds, 31, true) == expected[kinds], "Reviewed condition table");
        Check(VisibleUnwrapCommands(kinds | Empty, 31, true) == expected[kinds], "Empty folders do not add work");
        for (unsigned enabled = 0; enabled < 32; ++enabled)
        {
            const auto visible = VisibleUnwrapCommands(kinds, enabled, true);
            Check(!(visible & ~enabled), "Disabled commands must stay hidden");
            Check(VisibleUnwrapCommands(kinds | FileToolsMenu::Unknown, enabled, true) == enabled, "Unknown preserves enabled commands");
        }
    }
    Check(VisibleUnwrapCommands(Same, KeepName | AllContents, true) == KeepName, "Disabled representative fallback");
    Check(VisibleUnwrapCommands(Same, AllContents, true) == AllContents, "Last fallback");
    Check(VisibleUnwrapCommands(Same, 31, false) == (SameName | AllContents), "Unknown or different collision policy");
    Check(VisibleUnwrapCommands(Different, 31, false) == 30, "AutoNumber needs all-contents");

    unsigned calls = 0;
    auto classify = [&](std::initializer_list<EntryKind> entries)
    {
        calls = 0;
        return ClassifyFolderEntries([&](bool& same)
        {
            Check(calls < entries.size(), "Unexpected extra directory read");
            same = true;
            return *(entries.begin() + calls++);
        });
    };
    Check(classify({EntryKind::File, EntryKind::Error}) == FileToolsMenu::Unknown, "Partial enumeration is not single-file");
    Check(classify({EntryKind::File, EntryKind::File}) == Multiple && calls == 2, "Stop at second file");
    Check(classify({EntryKind::Directory}) == Multiple && calls == 1, "Stop at first directory");
    Check(classify({EntryKind::File, EntryKind::End}) == Same, "Single file needs normal end");
    Check(classify({EntryKind::End}) == Empty, "Empty directory");
    calls = 0;
    Check(ClassifyFolderEntries([&](bool&) { ++calls; return EntryKind::Ignored; }) == FileToolsMenu::Unknown && calls == 8,
        "Bounded enumeration even for unexpected entries");
}

void TestFilesAndCache()
{
    Fixture fixture;
    auto same = fixture.Folder(L"Same"), different = fixture.Folder(L"Different"),
        many = fixture.Folder(L"Many"), empty = fixture.Folder(L"Empty"), noExt = fixture.Folder(L"NoExt");
    std::ofstream(same / L"sAmE.txt") << "same";
    std::ofstream(different / L"Other.txt") << "different";
    std::ofstream(noExt / L"NoExt") << "no extension";
    for (int i = 0; i < 1000; ++i) std::ofstream(many / (std::to_wstring(i) + L".txt")) << i;
    MenuSettings settings;
    settings.SingleFileSkipsCollisions = true;
    const std::array<fs::path, 4> folders{same, different, many, empty};
    const unsigned expected[]{0, 1, 14, 15, 16, 17, 30, 31};
    for (unsigned kinds = 0; kinds < 16; ++kinds)
    {
        std::vector<std::wstring> paths;
        for (size_t i = 0; i < folders.size(); ++i)
            if (kinds & (1u << i)) paths.push_back(folders[i].wstring());
        Check(UnwrapMask(AnalyzeSelection(paths, settings)) == expected[kinds & 7], "Real directory selection matches table");
    }
    auto nested = fixture.Folder(L"NestedOnly");
    fs::create_directory(nested / L"Child");
    Check(UnwrapMask(AnalyzeSelection({nested.wstring()}, settings)) == AllContents, "Child directory is not a single file");
    auto large = AnalyzeSelection({ many.wstring() }, settings);
    Check(UnwrapMask(large) == AllContents, "Large folder classification");
    Check(large.Statistics.AttributeReads == 1 && large.Statistics.FolderOpens == 1 && large.Statistics.EntryReads <= 4,
        "Large folder stops after two files, including dot entries");
    Check(UnwrapMask(AnalyzeSelection({same.wstring()}, settings)) == SameName, "Case insensitive filename");
    Check(UnwrapMask(AnalyzeSelection({noExt.wstring()}, settings)) == SameName, "Extensionless file");
    Check(UnwrapMask(AnalyzeSelection({empty.wstring()}, settings)) == 0, "Empty selection contents");
    auto mixed = AnalyzeSelection({same.wstring(), (different / L"Other.txt").wstring()}, settings);
    Check(UnwrapMask(mixed) == 0 && mixed.Statistics.FolderOpens == 0, "Mixed file selection avoids directory reads");
    auto remote = AnalyzeSelection({L"\\\\unavailable.invalid\\share\\folder"}, settings);
    Check(UnwrapMask(remote) == 31 && remote.Statistics.AttributeReads == 0 && remote.Statistics.FolderOpens == 0,
        "Remote folders preserve commands without disk reads");
    Check(AnalyzeSelection({L"\\\\unavailable.invalid\\share\\1.zip", L"\\\\unavailable.invalid\\share\\2.zip"}, settings)
        .Visible[static_cast<size_t>(CommandKind::ArchiveMergePreserveInternalPaths)], "Remote ZIP action remains available");
    settings.Enabled.fill(false);
    Check(AnalyzeSelection({same.wstring()}, settings).Statistics.FolderOpens == 0, "No enabled unwrap, no folder reads");
    settings.Enabled.fill(true);
    std::vector<std::wstring> huge(1000, same.wstring());
    auto bounded = AnalyzeSelection(huge, settings);
    Check(bounded.Statistics.AttributeReads <= 256 && bounded.Statistics.FolderOpens <= 128 && UnwrapMask(bounded) == 31,
        "Large selection is bounded and conservative");

    auto fastMask = [](MenuSelectionCache& cache, IShellItemArray* selection, BOOL slow = FALSE)
    {
        unsigned mask = 0;
        for (const auto& definition : SubCommands)
        {
            EXPCMDSTATE state{};
            Check(cache.GetState(definition.Kind, selection, slow, &state) == S_OK && state != ECS_DISABLED,
                "All menu states must complete without a required slow callback");
            if (state == ECS_ENABLED) mask |= GetUnwrapBit(definition.Kind);
        }
        return mask;
    };
    auto analysisCount = std::make_shared<std::atomic<unsigned>>(0);
    auto& analyses = *analysisCount;
    const auto callerThread = GetCurrentThreadId();
    auto callerFlag = std::make_shared<std::atomic<bool>>(false);
    auto analyzer = [analysisCount, callerFlag, callerThread, settings](const auto& paths)
    {
        ++*analysisCount;
        if (GetCurrentThreadId() == callerThread) *callerFlag = true;
        return AnalyzeSelection(paths, settings);
    };
    const unsigned folderMasks[]{SameName, KeepName | UseFolderName | PrefixName, AllContents, 0};
    for (size_t index = 0; index < folders.size(); ++index)
    {
        auto selection = Selection(folders[index]);
        MenuSelectionCache cache(analyzer, settings);
        const auto before = analyses.load();
        Check(fastMask(cache, selection.Get()) == folderMasks[index], "First fast-only menu must match folder contents");
        Check(fastMask(cache, selection.Get()) == folderMasks[index], "Repeated fast calls keep the exact mask");
        Check(analyses == before + 1, "Sibling commands share one analysis");
        auto clone = Selection(folders[index]);
        Check(fastMask(cache, clone.Get()) == folderMasks[index] && analyses == before + 1,
            "New selection identity with the same path reuses exact visibility");
        Check(fastMask(cache, clone.Get(), TRUE) == folderMasks[index] && analyses == before + 1,
            "Optional slow call does not alter correct visibility");
        MenuSelectionCache reopened(analyzer, settings);
        Check(fastMask(reopened, clone.Get()) == folderMasks[index] && analyses == before + 2,
            "New menu computes correct visibility without a slow callback");
    }
    Check(!*callerFlag, "Folder analysis must not run on the Shell caller thread");
    auto selection = Selection(same), other = Selection(different);
    MenuSelectionCache changing(analyzer, settings);
    Check(fastMask(changing, selection.Get()) == SameName, "Initial selected folder");
    Check(fastMask(changing, other.Get()) == 14, "Changed path invalidates the snapshot on fast calls");
    std::ofstream(same / L"Added.txt") << "changed";
    MenuSelectionCache fresh(analyzer, settings);
    Check(fastMask(fresh, selection.Get()) == AllContents, "Reopening refreshes changed folder contents");

    auto fileSelection = Selection(different / L"Other.txt");
    Check(fastMask(changing, fileSelection.Get()) == 0, "File selection hides every unwrap command");
    auto zipPath = fixture.root / L"Archive.zip";
    { std::ofstream zip(zipPath, std::ios::binary); const char emptyZip[22]{'P','K',5,6}; zip.write(emptyZip, sizeof(emptyZip)); }
    auto zipSelection = Selection(zipPath);
    Check(fastMask(changing, zipSelection.Get()) == 0, "ZIP shell folders are not filesystem directories");
    MenuSettings disabled;
    disabled.Enabled.fill(false);
    MenuSelectionCache disabledCache(analyzer, disabled);
    const auto beforeDisabled = analyses.load();
    EXPCMDSTATE state{};
    Check(disabledCache.GetState(CommandKind::OpenApp, selection.Get(), FALSE, &state) == S_OK && state == ECS_HIDDEN &&
        analyses == beforeDisabled, "Disabled commands do not schedule analysis");

    // Block the worker on purpose: only the first callback may spend the 50ms wait budget.
    auto entered = std::make_shared<std::promise<void>>();
    std::promise<void> release;
    auto released = release.get_future().share();
    MenuSelectionCache busy([entered, released](const auto&)
    { entered->set_value(); released.wait(); return MenuSnapshot{}; }, settings);
    auto ready = entered->get_future();
    auto started = AnalysisClock::now();
    const auto first = busy.GetState(CommandKind::OpenApp, selection.Get(), FALSE, &state);
    const auto firstElapsed = AnalysisClock::now() - started;
    Check(first == S_OK && state == ECS_ENABLED, "Slow storage retains a usable fallback");
    Check(firstElapsed < std::chrono::milliseconds(250), "First callback wait is bounded");
    Check(ready.wait_for(std::chrono::seconds(2)) == std::future_status::ready, "Background analysis started");
    started = AnalysisClock::now();
    Check(fastMask(busy, selection.Get()) == 31, "Unfinished analysis uses conservative states");
    Check(AnalysisClock::now() - started < std::chrono::milliseconds(150), "Sibling callbacks must not each wait 50ms");
    release.set_value();
    const auto until = AnalysisClock::now() + std::chrono::seconds(2);
    do { busy.GetState(CommandKind::OpenApp, selection.Get(), FALSE, &state); std::this_thread::yield(); }
    while (state != ECS_HIDDEN && AnalysisClock::now() < until);
    Check(state == ECS_HIDDEN, "Completed background result is consumed on subsequent fast calls");
    std::cout << "Blocked analysis first callback wait: "
        << std::chrono::duration_cast<std::chrono::milliseconds>(firstElapsed).count() << "ms.\n";
    std::cout << "1000-file folder: attributes=" << large.Statistics.AttributeReads << ", opens=" << large.Statistics.FolderOpens
        << ", entry reads=" << large.Statistics.EntryReads << "; cache analyses=1 per selection.\n";
}

void TestWorkerLifetime()
{
    auto waitForIdle = []
    {
        const auto until = AnalysisClock::now() + std::chrono::seconds(3);
        while (g_menuAnalysisWorkers != 0 && AnalysisClock::now() < until) std::this_thread::yield();
        Check(g_menuAnalysisWorkers == 0, "Workers finish without retaining a menu object");
    };
    waitForIdle();
    Fixture fixture;
    const auto same = fixture.Folder(L"Same");
    std::ofstream(same / L"Same.txt") << "same";
    auto selection = Selection(same);
    MenuSettings settings;
    settings.SingleFileSkipsCollisions = true;
    std::promise<void> release;
    auto released = release.get_future().share();
    std::atomic<unsigned> unexpected{0};
    {
        MenuSelectionCache first([released](const auto&) { released.wait(); return MenuSnapshot{}; }, settings);
        MenuSelectionCache second([released](const auto&) { released.wait(); return MenuSnapshot{}; }, settings);
        EXPCMDSTATE state{};
        first.GetState(CommandKind::OpenApp, selection.Get(), FALSE, &state);
        second.GetState(CommandKind::OpenApp, selection.Get(), FALSE, &state);
        Check(g_menuAnalysisWorkers == MaxMenuAnalysisWorkers, "Only two analyses may be outstanding");
        Check(DllCanUnloadNow() == S_FALSE, "Outstanding workers retain DLL lifetime");
        MenuSelectionCache third([&](const auto&) { ++unexpected; return MenuSnapshot{}; }, settings);
        const auto start = AnalysisClock::now();
        Check(third.GetState(CommandKind::OpenApp, selection.Get(), FALSE, &state) == S_OK && state == ECS_ENABLED,
            "Worker saturation preserves usable commands");
        Check(unexpected == 0 && AnalysisClock::now() - start < std::chrono::milliseconds(100),
            "Worker saturation neither queues more work nor waits");
    }
    // Both owning menus have been destroyed; workers must only access their own input.
    release.set_value();
    waitForIdle();
    MenuSelectionCache failed([](const auto&) -> MenuSnapshot { throw std::runtime_error("I/O failed"); }, settings);
    EXPCMDSTATE state{};
    Check(failed.GetState(CommandKind::OpenApp, selection.Get(), FALSE, &state) == S_OK && state == ECS_ENABLED,
        "Analyzer exception produces conservative fallback, not disabled commands");
    waitForIdle();

    // An older selection can finish after a new one; it must not overwrite the new result.
    const auto other = fixture.Folder(L"Different");
    std::ofstream(other / L"Other.txt") << "different";
    auto otherSelection = Selection(other);
    std::promise<void> oldRelease;
    auto oldReleased = oldRelease.get_future().share();
    MenuSelectionCache switched([oldReleased, path = same.wstring(), settings](const auto& paths)
    {
        if (paths[0] == path) oldReleased.wait();
        return AnalyzeSelection(paths, settings);
    }, settings);
    switched.GetState(CommandKind::FolderUnwrapSameName, selection.Get(), FALSE, &state);
    Check(switched.GetState(CommandKind::FolderUnwrapSameName, otherSelection.Get(), FALSE, &state) == S_OK && state == ECS_HIDDEN,
        "New selection is not blocked by an older outstanding analysis");
    oldRelease.set_value();
    waitForIdle();
    Check(switched.GetState(CommandKind::FolderUnwrapSameName, otherSelection.Get(), FALSE, &state) == S_OK && state == ECS_HIDDEN,
        "Late old result cannot overwrite current selection");
}

int wmain(int argc, wchar_t** argv)
{
    CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    try
    {
        TestPolicy();
        TestFilesAndCache();
        TestWorkerLifetime();
        if (argc == 2)
        {
            std::ofstream matrix{fs::path(argv[1])};
            if (!matrix) throw std::runtime_error("Cannot write policy matrix");
            matrix << "kinds,enabled,skip,visible\n";
            for (unsigned kinds = 0; kinds < 16; ++kinds)
                for (unsigned enabled = 0; enabled < 32; ++enabled)
                    for (unsigned skip = 0; skip < 2; ++skip)
                        matrix << kinds << ',' << enabled << ',' << skip << ',' << VisibleUnwrapCommands(kinds, enabled, skip != 0) << '\n';
        }
        std::cout << "Passed " << checks << " native checks.\n";
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        CoUninitialize();
        return 1;
    }
    CoUninitialize();
    return 0;
}
