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

    auto selection = Selection(same), other = Selection(different);
    unsigned analyses = 0;
    auto analyzer = [&](const auto& paths) { ++analyses; return AnalyzeSelection(paths, settings); };
    auto cache = std::make_shared<MenuSelectionCache>(analyzer);
    ExplorerCommand command(CommandKind::FolderUnwrapSameName, cache);
    EXPCMDSTATE state{};
    for (unsigned pass = 0; pass < 3; ++pass)
    {
        Check(command.GetState(selection.Get(), FALSE, &state) == S_OK && state == ECS_ENABLED && analyses == 0,
            "Repeated fast-only calls must remain usable without analysis");
        for (const auto& definition : SubCommands)
        {
            ExplorerCommand sibling(definition.Kind, cache);
            Check(sibling.GetState(selection.Get(), FALSE, &state) == S_OK && state != ECS_DISABLED && analyses == 0,
                "No subcommand may depend on a later slow callback");
        }
    }
    auto fileSelection = Selection(different / L"Other.txt");
    Check(command.GetState(fileSelection.Get(), FALSE, &state) == S_OK && state == ECS_HIDDEN && analyses == 0,
        "Fast fallback hides folder commands for files");
    ExplorerCommand open(CommandKind::OpenApp, cache);
    Check(open.GetState(fileSelection.Get(), FALSE, &state) == S_OK && state == ECS_ENABLED && analyses == 0,
        "Open app does not require directory analysis");
    auto zipPath = fixture.root / L"Archive.zip";
    {
        std::ofstream zip(zipPath, std::ios::binary);
        const char emptyZip[22]{'P', 'K', 5, 6};
        zip.write(emptyZip, sizeof(emptyZip));
    }
    auto zipSelection = Selection(zipPath);
    Check(command.GetState(zipSelection.Get(), FALSE, &state) == S_OK && state == ECS_HIDDEN && analyses == 0,
        "ZIP shell folder must not be treated as a filesystem directory");
    MenuSettings disabled;
    disabled.Enabled.fill(false);
    MenuSelectionCache disabledCache(analyzer, disabled);
    Check(disabledCache.GetState(CommandKind::OpenApp, selection.Get(), FALSE, &state) == S_OK && state == ECS_HIDDEN,
        "Fast fallback respects disabled settings");
    Check(command.GetState(selection.Get(), TRUE, &state) == S_OK && state == ECS_ENABLED && analyses == 1, "Slow path analyzes once");
    for (const auto& definition : SubCommands)
    {
        ExplorerCommand sibling(definition.Kind, cache);
        Check(sibling.GetState(selection.Get(), FALSE, &state) == S_OK && analyses == 1, "Siblings share cache");
    }
    auto clone = Selection(same);
    Check(command.GetState(clone.Get(), FALSE, &state) == S_OK && state == ECS_ENABLED && analyses == 1,
        "New selection identity remains usable without path extraction");
    Check(command.GetState(clone.Get(), TRUE, &state) == S_OK && analyses == 1, "Same paths reuse snapshot");
    Check(command.GetState(other.Get(), TRUE, &state) == S_OK && state == ECS_HIDDEN && analyses == 2, "Changed selection invalidates cache");
    std::ofstream(same / L"Added.txt") << "changed";
    MenuSelectionCache fresh(analyzer);
    Check(fresh.GetState(CommandKind::FolderUnwrapSameName, selection.Get(), TRUE, &state) == S_OK && state == ECS_HIDDEN && analyses == 3,
        "New menu refreshes changed folder contents");

    // A fast callback must never wait on an in-flight slow analysis.
    std::promise<void> entered, release;
    auto released = release.get_future().share();
    MenuSelectionCache busy([&](const auto&) { entered.set_value(); released.wait(); return MenuSnapshot{}; });
    auto ready = entered.get_future();
    std::thread worker([&]
    {
        CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        EXPCMDSTATE unused{};
        busy.GetState(CommandKind::OpenApp, selection.Get(), TRUE, &unused);
        CoUninitialize();
    });
    ready.wait();
    const auto fastResult = busy.GetState(CommandKind::OpenApp, selection.Get(), FALSE, &state);
    release.set_value();
    worker.join();
    Check(fastResult == S_OK && state == ECS_ENABLED, "Fast path remains usable while slow analysis owns mutex");
    std::cout << "1000-file folder: attributes=" << large.Statistics.AttributeReads << ", opens=" << large.Statistics.FolderOpens
        << ", entry reads=" << large.Statistics.EntryReads << "; cache analyses=1 per selection.\n";
}

int wmain(int argc, wchar_t** argv)
{
    CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    try
    {
        TestPolicy();
        TestFilesAndCache();
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
