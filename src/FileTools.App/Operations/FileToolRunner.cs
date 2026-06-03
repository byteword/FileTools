namespace FileTools;

internal sealed class FileToolRunner
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly FileToolsSettings _settings;

    public FileToolRunner(FileToolsSettings settings)
    {
        _settings = settings;
    }

    public OperationResult Run(ToolMode mode, IEnumerable<string> paths)
    {
        var normalizedPaths = paths
            .Select(static path => path.Trim().Trim('"'))
            .Where(static path => path.Length > 0 && (File.Exists(path) || Directory.Exists(path)))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();

        return mode switch
        {
            ToolMode.FileNameCorrection => RunFileNameCorrection(normalizedPaths),
            ToolMode.FolderStructure => RunFolderStructure(normalizedPaths),
            ToolMode.AutoRelocation => RunAutoRelocation(normalizedPaths),
            _ => new OperationResult()
        };
    }

    private OperationResult RunFileNameCorrection(IReadOnlyList<string> paths)
    {
        try
        {
            return RenameOperations.Apply(RenameOperations.CreatePlan(paths, _settings));
        }
        catch (Exception ex)
        {
            var result = new OperationResult();
            result.AddError("파일명 교정 계획 생성 실패: " + ex.Message);
            return result;
        }
    }

    private OperationResult RunFolderStructure(IReadOnlyList<string> paths)
    {
        var result = new OperationResult();

        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    if (_settings.FolderStructureOperation is FolderStructureOperation.Auto or FolderStructureOperation.WrapFiles)
                    {
                        WrapFile(path, result);
                    }
                    else
                    {
                        result.AddCandidate();
                        result.AddSkipped(Path.GetFileName(path) + " 파일은 선택한 폴더 작업 대상이 아님");
                    }
                    continue;
                }

                if (Directory.Exists(path))
                {
                    RunFolderOperation(path, result);
                }
            }
            catch (Exception ex)
            {
                result.AddError(path + " | " + ex.Message);
            }
        }

        return result;
    }

    private void RunFolderOperation(string folderPath, OperationResult result)
    {
        switch (_settings.FolderStructureOperation)
        {
            case FolderStructureOperation.Auto:
                if (CanUnwrapSingleFileFolder(folderPath, sameNameOnly: true, out _, out _, out _))
                {
                    UnwrapSingleFileFolder(folderPath, sameNameOnly: true, result);
                    return;
                }

                if (CanUnwrapSingleFileFolder(folderPath, sameNameOnly: false, out _, out _, out _))
                {
                    UnwrapSingleFileFolder(folderPath, sameNameOnly: false, result);
                    return;
                }

                MoveInnerFilesUp(folderPath, result);
                return;
            case FolderStructureOperation.UnwrapSameNameSingleFile:
                UnwrapSingleFileFolder(folderPath, sameNameOnly: true, result);
                return;
            case FolderStructureOperation.UnwrapSingleFileFolder:
                UnwrapSingleFileFolder(folderPath, sameNameOnly: false, result);
                return;
            case FolderStructureOperation.MoveInnerFilesUp:
                MoveInnerFilesUp(folderPath, result);
                return;
            case FolderStructureOperation.WrapFiles:
                result.AddCandidate();
                result.AddSkipped(Path.GetFileName(folderPath) + " 폴더는 파일 wrapping 대상이 아님");
                return;
        }
    }

    private static void WrapFile(string filePath, OperationResult result)
    {
        result.AddCandidate();
        var parent = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            result.AddSkipped(filePath + " 부모 폴더 없음");
            return;
        }

        var folderName = WindowsFileNameSafety.MakeSafeFileName(Path.GetFileNameWithoutExtension(filePath));
        var targetFolder = Path.Combine(parent, folderName);
        if (File.Exists(targetFolder))
        {
            result.AddSkipped(Path.GetFileName(filePath) + " wrapping 대상 폴더명과 같은 파일 존재");
            return;
        }

        var targetPath = Path.Combine(targetFolder, Path.GetFileName(filePath));
        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            result.AddSkipped(Path.GetFileName(filePath) + " 대상 파일 이미 존재");
            return;
        }

        Directory.CreateDirectory(targetFolder);
        File.Move(filePath, targetPath);
        result.AddApplied(Path.GetFileName(filePath) + " -> " + Path.GetFileName(targetFolder) + "\\");
        FileToolsEnvironment.Log("WRAP", filePath + " -> " + targetPath);
    }

    private bool UnwrapSingleFileFolder(string folderPath, bool sameNameOnly, OperationResult result)
    {
        result.AddCandidate();
        if (!CanUnwrapSingleFileFolder(folderPath, sameNameOnly, out var dir, out var file, out var reason))
        {
            result.AddSkipped(reason);
            return false;
        }

        var targetFileName = ResolveUnwrappedFileName(dir.Name, file.Name);
        var targetPath = Path.Combine(dir.Parent!.FullName, targetFileName);
        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            result.AddSkipped(targetFileName + " 대상 경로 이미 존재");
            return false;
        }

        File.Move(file.FullName, targetPath);
        if (!Directory.EnumerateFileSystemEntries(dir.FullName).Any())
        {
            Directory.Delete(dir.FullName, recursive: false);
        }

        result.AddApplied(dir.Name + "\\" + file.Name + " -> " + targetFileName);
        FileToolsEnvironment.Log("UNWRAP", file.FullName + " -> " + targetPath);
        return true;
    }

    private string ResolveUnwrappedFileName(string folderName, string fileName)
    {
        var fileStem = Path.GetFileNameWithoutExtension(fileName);
        if (string.Equals(folderName, fileStem, StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        return _settings.FolderUnwrapNameMismatchMode switch
        {
            FolderUnwrapNameMismatchMode.UseFolderName =>
                WindowsFileNameSafety.MakeSafeFileName(folderName + extension),
            FolderUnwrapNameMismatchMode.PrefixFolderName =>
                WindowsFileNameSafety.MakeSafeFileName(folderName + "-" + fileStem + extension),
            _ => fileName
        };
    }

    private static bool CanUnwrapSingleFileFolder(
        string folderPath,
        bool sameNameOnly,
        out DirectoryInfo dir,
        out FileInfo file,
        out string reason)
    {
        dir = new DirectoryInfo(folderPath);
        file = null!;
        if (!dir.Exists || dir.Parent is null)
        {
            reason = folderPath + " 부모 폴더 없음";
            return false;
        }

        var files = dir.GetFiles("*", SearchOption.TopDirectoryOnly);
        var subDirs = dir.GetDirectories("*", SearchOption.TopDirectoryOnly);
        if (files.Length != 1 || subDirs.Length != 0)
        {
            reason = dir.Name + " 단일 파일 폴더 조건 불일치";
            return false;
        }

        file = files[0];
        if (sameNameOnly &&
            !string.Equals(Path.GetFileNameWithoutExtension(file.Name), dir.Name, StringComparison.OrdinalIgnoreCase))
        {
            reason = dir.Name + " 폴더명/파일명 불일치";
            return false;
        }

        reason = "";
        return true;
    }

    private static void MoveInnerFilesUp(string folderPath, OperationResult result)
    {
        result.AddCandidate();
        var dir = new DirectoryInfo(folderPath);
        if (!dir.Exists || dir.Parent is null)
        {
            result.AddSkipped(folderPath + " 부모 폴더 없음");
            return;
        }

        var files = dir.GetFiles("*", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            result.AddSkipped(dir.Name + " 이동할 직접 하위 파일 없음");
            return;
        }

        var moved = 0;
        foreach (var file in files)
        {
            var targetPath = Path.Combine(dir.Parent.FullName, file.Name);
            if (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                result.AddSkipped(file.Name + " 대상 경로 이미 존재");
                continue;
            }

            File.Move(file.FullName, targetPath);
            moved++;
            FileToolsEnvironment.Log("MOVE-UP", file.FullName + " -> " + targetPath);
        }

        if (Directory.Exists(dir.FullName) && !Directory.EnumerateFileSystemEntries(dir.FullName).Any())
        {
            Directory.Delete(dir.FullName, recursive: false);
        }

        if (moved == 0)
        {
            result.AddSkipped(dir.Name + " 이동 완료 항목 없음");
            return;
        }

        result.AddApplied($"{dir.Name} 직접 하위 파일 {moved}개 상위 이동");
    }

    private OperationResult RunAutoRelocation(IReadOnlyList<string> paths)
    {
        var result = new OperationResult();
        AutoRelocationTemplateDocument template;
        try
        {
            template = AutoRelocationTemplateStore
                .FindTemplateOrDefault(_settings.AutoRelocationTemplateId)
                .Document;
        }
        catch (Exception ex)
        {
            result.AddError("자동 재배치 템플릿 로드 실패: " + ex.Message);
            return result;
        }

        var corrector = CreateFileNameCorrector();
        var targetRootOverride = string.IsNullOrWhiteSpace(_settings.AutoRelocationTargetRootPath)
            ? null
            : Path.GetFullPath(_settings.AutoRelocationTargetRootPath);

        var grouped = paths
            .Select(path => CreateRelocationContext(path, corrector, result, targetRootOverride, _settings))
            .Where(static item => item is not null)
            .Cast<RelocationContextWithRoot>()
            .GroupBy(static item => item.RootFolder, PathComparer);

        var builder = new AutoRelocationPlanBuilder();
        foreach (var group in grouped)
        {
            AutoRelocationPlanBuildResult plan;
            try
            {
                plan = builder.Build(group.Key, template, group.Select(static item => item.Context));
            }
            catch (Exception ex)
            {
                result.AddError(group.Key + " | 자동 재배치 계획 생성 실패: " + ex.Message);
                continue;
            }

            for (var i = 0; i < plan.ExcludedCount; i++)
            {
                result.AddSkipped("템플릿 prefilter 제외");
            }

            foreach (var item in plan.Items)
            {
                ApplyRelocationItem(item, result);
            }
        }

        return result;
    }

    private static RelocationContextWithRoot? CreateRelocationContext(
        string path,
        KoreanFileNameCorrector corrector,
        OperationResult result,
        string? targetRootOverride,
        FileToolsSettings settings)
    {
        result.AddCandidate();
        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            result.AddSkipped(path + " 부모 폴더 없음");
            return null;
        }

        try
        {
            var preview = corrector.CreatePreview(path);
            var fileNameStem = GetRelocationFileNameStem(path);
            var knownFileKind = AutoRelocationFileTypeClassifier.GetKnownFileKind(path, settings);
            var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["fileName"] = Path.GetFileName(path),
                ["fileNameStem"] = fileNameStem,
                ["fileExtension"] = GetRelocationFileExtension(path),
                ["knownFileKind"] = knownFileKind,
                ["fileKind"] = knownFileKind,
                ["fileType"] = knownFileKind,
                ["title"] = preview.Parts.Title,
                ["originalTitle"] = fileNameStem,
                ["episodeRange"] = preview.Parts.EpisodeRange
            };

            var info = File.Exists(path)
                ? new FileInfo(path) as FileSystemInfo
                : new DirectoryInfo(path);
            var sizeBytes = File.Exists(path) ? new FileInfo(path).Length : 0;

            return new RelocationContextWithRoot(
                targetRootOverride ?? parent,
                new AutoRelocationItemContext(
                    path,
                    properties,
                    sizeBytes,
                    ImageCount: null,
                    info.LastWriteTime,
                    info.CreationTime));
        }
        catch (Exception ex)
        {
            result.AddError(path + " | 자동 재배치 대상 분석 실패: " + ex.Message);
            return null;
        }
    }

    private static string GetRelocationFileNameStem(string path)
    {
        return Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
    }

    private static string GetRelocationFileExtension(string path)
    {
        return File.Exists(path) ? Path.GetExtension(path).TrimStart('.') : "";
    }

    private static void ApplyRelocationItem(AutoRelocationPlanItem item, OperationResult result)
    {
        try
        {
            if (item.RequiresReview)
            {
                result.AddSkipped(Path.GetFileName(item.SourcePath) + " 템플릿 검토 필요");
                return;
            }

            if (PathComparer.Equals(item.SourcePath, item.TargetPath))
            {
                result.AddSkipped(Path.GetFileName(item.SourcePath) + " 대상 경로 동일");
                return;
            }

            var targetPath = CreateUniqueTargetPath(item.TargetPath);
            if (IsSubPathOf(targetPath, item.SourcePath))
            {
                result.AddSkipped(Path.GetFileName(item.SourcePath) + " 원본 하위로 이동 불가");
                return;
            }

            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                result.AddSkipped(Path.GetFileName(item.SourcePath) + " 대상 부모 폴더 없음");
                return;
            }

            Directory.CreateDirectory(targetDirectory);
            if (Directory.Exists(item.SourcePath))
            {
                Directory.Move(item.SourcePath, targetPath);
            }
            else if (File.Exists(item.SourcePath))
            {
                File.Move(item.SourcePath, targetPath);
            }
            else
            {
                result.AddSkipped(Path.GetFileName(item.SourcePath) + " 원본 없음");
                return;
            }

            result.AddApplied(Path.GetFileName(item.SourcePath) + " -> " + targetDirectory);
            FileToolsEnvironment.Log("RELOCATE", item.SourcePath + " -> " + targetPath);
        }
        catch (Exception ex)
        {
            result.AddError(item.SourcePath + " | " + ex.Message);
        }
    }

    private static string CreateUniqueTargetPath(string targetPath)
    {
        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            return targetPath;
        }

        var directory = Path.GetDirectoryName(targetPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("중복 대상 경로를 해결할 수 없습니다: " + targetPath);
    }

    private static bool IsSubPathOf(string candidatePath, string parentPath)
    {
        var parentFull = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidateFull = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return candidateFull.StartsWith(parentFull, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
    }

    private sealed record RelocationContextWithRoot(
        string RootFolder,
        AutoRelocationItemContext Context);

    private KoreanFileNameCorrector CreateFileNameCorrector()
    {
        var dictionary = RenameDictionaryStore.Load();
        var parserProfile = RenameParserProfileStore.Load();
        var rules = RenameRuleStore.Load();
        return new KoreanFileNameCorrector(new CorrectionOptions
        {
            ParserProfile = parserProfile,
            RenameDictionary = _settings.RenameUseDictionary ? dictionary.Replacements : [],
            CommonPhrases = _settings.RenameUseDictionary ? dictionary.CommonPhrases.ToArray() : [],
            Rules = rules.Rules
        });
    }
}
