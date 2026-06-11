namespace FileTools;

/// <summary>
/// 모드별 실행을 담당하고, 필요한 경우 보정/폴더 작업/자동 재배치로 라우팅한다.
/// </summary>
internal sealed class FileToolRunner
{
    /// <summary>
    /// OS별 경로 비교 규칙(Windows는 대소문자 무시).
    /// </summary>
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// 실행 시점의 설정 스냅샷.
    /// </summary>
    private readonly FileToolsSettings _settings;

    public FileToolRunner(FileToolsSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// 선택 모드에 따라 경로들을 정규화한 뒤 해당 작업을 실행한다.
    /// </summary>
    /// <param name="mode">실행할 운영 모드</param>
    /// <param name="paths">입력 경로</param>
    /// <returns>적용 결과 집계</returns>
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

    /// <summary>
    /// 이름 교정 모드 실행을 위한 후보 생성/적용을 한 번에 수행한다.
    /// </summary>
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

    /// <summary>
    /// 폴더 구조 작업(랩/언랩/상향 이동)을 경로별로 분기 실행한다.
    /// </summary>
    /// <param name="paths">정규화된 대상 경로</param>
    /// <returns>폴더 구조 작업 결과</returns>
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

    /// <summary>
    /// 폴더 구조 모드는 현재 설정된 동작(랩/언랩/상향 이동)에 따라 분기 실행한다.
    /// </summary>
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

    /// <summary>
    /// 파일을 랩 모드 대상 폴더로 이동한다.
    /// </summary>
    /// <param name="filePath">랩 대상 파일 경로</param>
    /// <param name="result">결과 집계</param>
    private void WrapFile(string filePath, OperationResult result)
    {
        result.AddCandidate();
        var parent = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            result.AddSkipped(filePath + " 부모 폴더 없음");
            return;
        }

        var folderName = FolderStructureNameTemplates.ResolveWrapFolderName(filePath, _settings);
        var targetFolder = Path.Combine(parent, folderName);
        if (!Directory.Exists(targetFolder))
        {
            var folderCollision = NameCollisionResolver.Resolve(
                parent,
                folderName,
                FolderStructureCollisionOptions.Create(_settings, NameCollisionTargetKind.Folder));
            if (!folderCollision.IsReady)
            {
                result.AddSkipped(Path.GetFileName(filePath) + " wrapping 대상 폴더명과 같은 파일 존재");
                return;
            }

            targetFolder = folderCollision.TargetPath;
        }

        var fileCollision = NameCollisionResolver.Resolve(
            targetFolder,
            Path.GetFileName(filePath),
            FolderStructureCollisionOptions.Create(_settings, NameCollisionTargetKind.File));
        if (!fileCollision.IsReady)
        {
            result.AddSkipped(Path.GetFileName(filePath) + " 대상 파일 이미 존재");
            return;
        }

        Directory.CreateDirectory(targetFolder);
        var targetPath = fileCollision.TargetPath;
        File.Move(filePath, targetPath);
        result.AddApplied(Path.GetFileName(filePath) + " -> " + Path.GetFileName(targetFolder) + "\\");
        FileToolsEnvironment.Log("WRAP", filePath + " -> " + targetPath);
    }

    /// <summary>
    /// 단일 파일이 들어 있는 폴더를 언랩으로 처리 가능한지 판정하고 실행한다.
    /// </summary>
    /// <param name="folderPath">처리 대상 폴더</param>
    /// <param name="sameNameOnly">폴더명과 파일명 일치 조건</param>
    /// <param name="result">결과 집계</param>
    /// <returns>성공 여부</returns>
    /// <summary>
    /// 단일 파일 언랩을 시도하고, 조건 불일치 시 사유를 결과에 기록한다.
    /// </summary>
    private bool UnwrapSingleFileFolder(string folderPath, bool sameNameOnly, OperationResult result)
    {
        result.AddCandidate();
        if (!CanUnwrapSingleFileFolder(folderPath, sameNameOnly, out var dir, out var file, out var reason))
        {
            result.AddSkipped(reason);
            return false;
        }

        var targetFileName = FolderStructureNameTemplates.ResolveUnwrappedFileNameFromFolderPath(
            dir.FullName,
            file.Name,
            _settings.FolderUnwrapNameMismatchMode,
            _settings);
        var fileCollision = NameCollisionResolver.Resolve(
            dir.Parent!.FullName,
            targetFileName,
            FolderStructureCollisionOptions.Create(_settings, NameCollisionTargetKind.File));
        if (!fileCollision.IsReady)
        {
            result.AddSkipped(targetFileName + " 대상 경로 이미 존재");
            return false;
        }

        var targetPath = fileCollision.TargetPath;
        File.Move(file.FullName, targetPath);
        if (!Directory.EnumerateFileSystemEntries(dir.FullName).Any())
        {
            Directory.Delete(dir.FullName, recursive: false);
        }

        result.AddApplied(dir.Name + "\\" + file.Name + " -> " + targetFileName);
        FileToolsEnvironment.Log("UNWRAP", file.FullName + " -> " + targetPath);
        return true;
    }

    /// <summary>
    /// 단일 파일 폴더 조건(파일 1개/하위 폴더 0개 등) 판정을 수행한다.
    /// </summary>
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

    /// <summary>
    /// 하위 직접 파일을 상위 폴더로 승격시키는 작업이다.
    /// </summary>
    /// <param name="folderPath">처리 대상 폴더</param>
    /// <param name="result">결과 집계</param>
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

    /// <summary>
    /// 자동 재배치 실행 엔트리.
    /// 템플릿을 불러오고 대상들을 그룹핑해 템플릿 빌드 후 이동을 적용한다.
    /// </summary>
    /// <param name="paths">처리 경로</param>
    /// <returns>실행 결과</returns>
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

    /// <summary>
    /// 자동 재배치 계획에 필요한 텍스트/분류 속성 맥락을 구성한다.
    /// </summary>
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

    /// <summary>
    /// 파일/폴더 이름에서 템플릿용 파일명 스템(확장자 제외)을 구한다.
    /// </summary>
    private static string GetRelocationFileNameStem(string path)
    {
        return Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// 템플릿에 들어갈 파일 확장자(점 제외)를 추출한다.
    /// </summary>
    private static string GetRelocationFileExtension(string path)
    {
        return File.Exists(path) ? Path.GetExtension(path).TrimStart('.') : "";
    }

    /// <summary>
    /// 단일 템플릿 항목의 이동 의도를 실제 파일/폴더 이동으로 반영한다.
    /// </summary>
    /// <param name="item">자동 재배치 항목</param>
    /// <param name="result">결과 집계</param>
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

    /// <summary>
    /// 충돌을 피하기 위해 경로 뒤에 번호를 붙여 유효한 대상 경로를 만든다.
    /// </summary>
    /// <param name="targetPath">원본 대상 경로</param>
    /// <returns>유일한 경로</returns>
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

    /// <summary>
    /// candidate가 부모 경로 내부인지 판단해 하위로 이동되는 경우를 방지한다.
    /// </summary>
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
        var candidateProfile = RenameCandidateProfileStore.Load(dictionary.CommonPhrases);
        var rules = RenameRuleStore.Load();
        return new KoreanFileNameCorrector(new CorrectionOptions
        {
            ParserProfile = parserProfile,
            RenameDictionary = _settings.RenameUseDictionary ? dictionary.Replacements : [],
            CommonPhrases = _settings.RenameUseDictionary ? dictionary.CommonPhrases.ToArray() : [],
            CandidateProfile = _settings.RenameUseDictionary ? candidateProfile : RenameCandidateProfileStore.CreateDefaultDocument(),
            Rules = rules.Rules
        });
    }
}
