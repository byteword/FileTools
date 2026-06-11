namespace FileTools;

/// <summary>
/// 작업 계획을 실제 실행으로 바인딩하고 결과를 집계하는 엔진.
/// </summary>
internal sealed class WorkPlanExecutor
{
    /// <summary>
    /// OS별 경로 비교 방식(Windows는 대소문자 무시).
    /// </summary>
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// 실행 시 기본으로 복제해 쓰는 사용자 설정.
    /// </summary>
    private readonly FileToolsSettings _baseSettings;
    /// <summary>
    /// 아카이브 병합 실행 시 사용자 개입(인코딩/충돌/중복 해석) 채널.
    /// </summary>
    private readonly IArchiveMergeQuestionSink? _archiveMergeQuestionSink;

    public WorkPlanExecutor(FileToolsSettings baseSettings, IArchiveMergeQuestionSink? archiveMergeQuestionSink = null)
    {
        _baseSettings = baseSettings;
        _archiveMergeQuestionSink = archiveMergeQuestionSink;
    }

    /// <summary>
    /// 취소 토큰/진행 로그 없이 기본 실행한다.
    /// </summary>
    public OperationResult Run(IEnumerable<WorkTargetPlan> targets)
    {
        return Run(targets, CancellationToken.None, progress: null);
    }

    /// <summary>
    /// 작업 계획 목록을 순차 실행한다.
    /// </summary>
    /// <param name="targets">실행 대상 계획</param>
    /// <param name="cancellationToken">중단 토큰</param>
    /// <param name="progress">로그 전달 채널</param>
    /// <returns>누적 실행 결과</returns>
    public OperationResult Run(
        IEnumerable<WorkTargetPlan> targets,
        CancellationToken cancellationToken,
        IProgress<string>? progress)
    {
        var aggregate = new OperationResult();
        var executedArchiveMergePlanIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in targets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            progress?.Report(Localizer.Format("LogTargetStartingFormat", Path.GetFileName(target.Path)));
            if (!RunTarget(target, aggregate, executedArchiveMergePlanIds, cancellationToken, progress))
            {
                break;
            }
        }

        return aggregate;
    }

    /// <summary>
    /// 단일 대상의 단계(step)들을 실행하고 결과를 합친다.
    /// </summary>
    /// <remarks>
    /// 단계가 비어 있으면 즉시 건너뛰고, 단계별로 현재 경로를 추적해 체인 입출력을 반영한다.
    /// </remarks>
    private bool RunTarget(
        WorkTargetPlan target,
        OperationResult aggregate,
        HashSet<string> executedArchiveMergePlanIds,
        CancellationToken cancellationToken,
        IProgress<string>? progress)
    {
        var currentPath = target.Path;
        if (target.Steps.Count == 0)
        {
            aggregate.AddSkipped(Path.GetFileName(target.Path) + " has no planned actions");
            return true;
        }

        foreach (var step in target.Steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (step.Kind == WorkPlanStepKind.ArchiveMerge)
            {
                RunArchiveMergeStep(step, aggregate, executedArchiveMergePlanIds, cancellationToken, progress, _archiveMergeQuestionSink);
                continue;
            }

            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            {
                aggregate.AddSkipped(currentPath + " no longer exists");
                break;
            }

            progress?.Report(Localizer.Format("LogStepStartingFormat", Path.GetFileName(currentPath), step.DisplayName));
            var predictedPath = PredictNextPath(step, currentPath);
            var result = RunStep(step, currentPath);
            aggregate.Merge(result);
            ReportStepResult(result, progress);
            if (!result.HasErrors && result.AppliedCount > 0 && !string.IsNullOrWhiteSpace(predictedPath) &&
                (File.Exists(predictedPath) || Directory.Exists(predictedPath)))
            {
                currentPath = predictedPath;
            }
        }

        return true;
    }

    /// <summary>
    /// 아카이브 병합 step을 실행한다.
    /// </summary>
    private static void RunArchiveMergeStep(
        WorkPlanStep step,
        OperationResult aggregate,
        HashSet<string> executedArchiveMergePlanIds,
        CancellationToken cancellationToken,
        IProgress<string>? progress,
        IArchiveMergeQuestionSink? questionSink)
    {
        if (step.ArchiveMergeOptions is null)
        {
            aggregate.AddSkipped(Localizer.Get("ArchiveMergePlanMissingOptions"));
            return;
        }

        if (!executedArchiveMergePlanIds.Add(step.ArchiveMergeOptions.PlanId))
        {
            return;
        }

        progress?.Report(Localizer.Format(
            "LogArchiveMergeStartingFormat",
            step.ArchiveMergeOptions.SourcePaths.Count,
            Path.GetFileName(step.ArchiveMergeOptions.OutputPath)));
        var result = ArchiveMergeOperations.Merge(step.ArchiveMergeOptions, cancellationToken, progress, questionSink);
        aggregate.Merge(result);
        ReportStepResult(result, progress);
    }

    /// <summary>
    /// 실행 결과의 메시지/에러를 진행 로그로 출력한다.
    /// </summary>
    private static void ReportStepResult(OperationResult result, IProgress<string>? progress)
    {
        if (progress is null)
        {
            return;
        }

        foreach (var error in result.Errors)
        {
            progress.Report(Localizer.Format("LogErrorFormat", error));
        }

        foreach (var message in result.Messages)
        {
            progress.Report(message);
        }
    }

    /// <summary>
    /// 단일 step을 타입별로 실제 작업 runner/operation에 매핑해 실행한다.
    /// </summary>
    private OperationResult RunStep(WorkPlanStep step, string path)
    {
        var settings = _baseSettings.Clone();
        var runner = new FileToolRunner(settings);

        switch (step.Kind)
        {
            case WorkPlanStepKind.FileNameCorrection:
                if (!string.IsNullOrWhiteSpace(step.ManualRenameFileName))
                {
                    return RenameOperations.Apply([RenameOperations.CreateManualPreview(
                        path,
                        step.ManualRenameFileName,
                        settings)]);
                }

                return runner.Run(ToolMode.FileNameCorrection, [path]);

            case WorkPlanStepKind.FolderWrap:
                settings.FolderStructureOperation = FolderStructureOperation.WrapFiles;
                return runner.Run(ToolMode.FolderStructure, [path]);
            case WorkPlanStepKind.FolderUnwrap:
                settings.FolderStructureOperation = step.FolderOperation;
                settings.FolderUnwrapNameMismatchMode = step.FolderUnwrapNameMismatchMode;
                return runner.Run(ToolMode.FolderStructure, [path]);
            case WorkPlanStepKind.AutoRelocation:
                settings.AutoRelocationTemplateId = string.IsNullOrWhiteSpace(step.AutoRelocationTemplateId)
                    ? settings.AutoRelocationTemplateId
                    : step.AutoRelocationTemplateId;
                settings.AutoRelocationTargetRootPath = string.IsNullOrWhiteSpace(step.ManualTargetRootPath)
                    ? null
                    : step.ManualTargetRootPath;
                return runner.Run(ToolMode.AutoRelocation, [path]);
            case WorkPlanStepKind.DuplicateDelete:
                return DuplicateDeleteOperations.MoveFileToRecycleBin(path);
            default:
                return new OperationResult();
        }
    }

    /// <summary>
    /// 현재 step이 끝난 뒤 예상되는 다음 경로를 계산한다.
    /// </summary>
    private string? PredictNextPath(WorkPlanStep step, string path)
    {
        return step.Kind switch
        {
            WorkPlanStepKind.FileNameCorrection => PredictRenamePath(step, path),
            WorkPlanStepKind.FolderWrap => PredictWrapPath(path),
            WorkPlanStepKind.FolderUnwrap => PredictUnwrapPath(
                path,
                step.FolderOperation,
                step.FolderUnwrapNameMismatchMode),
            WorkPlanStepKind.AutoRelocation => PredictAutoRelocationPath(step, path),
            WorkPlanStepKind.ArchiveMerge => step.ArchiveMergeOptions?.OutputPath,
            WorkPlanStepKind.DuplicateDelete => null,
            _ => null
        };
    }

    /// <summary>
    /// 이름 변경 step의 예상 결과 경로를 계산한다.
    /// </summary>
    private string? PredictRenamePath(WorkPlanStep step, string path)
    {
        try
        {
            var preview = !string.IsNullOrWhiteSpace(step.ManualRenameFileName)
                ? RenameOperations.CreateManualPreview(path, step.ManualRenameFileName, _baseSettings)
                : new RenamePlanner(CreateFileNameCorrector()).CreatePlan([path]).FirstOrDefault();
            return preview is not null &&
                   !PathComparer.Equals(preview.OriginalPath, preview.SuggestedPath)
                ? preview.SuggestedPath
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Wrap step의 예상 대상 폴더를 계산한다.
    /// </summary>
    private string? PredictWrapPath(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return null;
        }

        var folderName = FolderStructureNameTemplates.ResolveWrapFolderName(path, _baseSettings);
        var targetFolder = Path.Combine(parent, folderName);
        if (!Directory.Exists(targetFolder))
        {
            var folderCollision = NameCollisionResolver.Resolve(
                parent,
                folderName,
                FolderStructureCollisionOptions.Create(_baseSettings, NameCollisionTargetKind.Folder));
            if (!folderCollision.IsReady)
            {
                return null;
            }

            targetFolder = folderCollision.TargetPath;
        }

        var fileCollision = NameCollisionResolver.Resolve(
            targetFolder,
            Path.GetFileName(path),
            FolderStructureCollisionOptions.Create(_baseSettings, NameCollisionTargetKind.File));
        return fileCollision.IsReady ? targetFolder : null;
    }

    /// <summary>
    /// AutoRelocation step의 예상 대상 경로를 계산한다.
    /// 템플릿/컨텍스트 생성 실패 시 null로 실패 신호를 낸다.
    /// </summary>
    /// <remarks>
    /// 1) 템플릿 ID를 기본값/설정값으로 해석한다.
    /// 2) 루트 경로 오버라이드가 있으면 우선 적용한다.
    /// 3) 생성 계획에서 충돌/검토가 필요한 결과는 미리보기에서 배제한다.
    /// </remarks>
    private string? PredictAutoRelocationPath(WorkPlanStep step, string path)
    {
        try
        {
            var templateId = string.IsNullOrWhiteSpace(step.AutoRelocationTemplateId)
                ? _baseSettings.AutoRelocationTemplateId
                : step.AutoRelocationTemplateId;
            var template = AutoRelocationTemplateStore
                .FindTemplateOrDefault(templateId)
                .Document;
            var targetRootOverride = string.IsNullOrWhiteSpace(step.ManualTargetRootPath)
                ? null
                : Path.GetFullPath(step.ManualTargetRootPath);
            var context = CreateRelocationContext(path, targetRootOverride);
            if (context is null)
            {
                return null;
            }

            var plan = new AutoRelocationPlanBuilder()
                .Build(context.RootFolder, template, [context.Context]);
            var item = plan.Items.FirstOrDefault();
            if (item is null || item.RequiresReview)
            {
                return null;
            }

            var targetPath = CreateUniqueTargetPath(item.TargetPath);
            if (PathComparer.Equals(Path.GetFullPath(path), targetPath) ||
                IsSubPathOf(targetPath, path))
            {
                return null;
            }

            return targetPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Unwrap step의 예상 결과 경로를 계산한다.
    /// </summary>
    /// <remarks>
    /// MoveInnerFilesUp은 부모 폴더로 승격하고,
    /// 단일 자식 파일 기반 언랩만 폴더 경로로 전환한다.
    /// </remarks>
    private string? PredictUnwrapPath(
        string path,
        FolderStructureOperation operation,
        FolderUnwrapNameMismatchMode mismatchMode)
    {
        if (!Directory.Exists(path))
        {
            return null;
        }

        var dir = new DirectoryInfo(path);
        if (dir.Parent is null)
        {
            return null;
        }

        if (operation == FolderStructureOperation.MoveInnerFilesUp)
        {
            return dir.Parent.FullName;
        }

        var files = dir.GetFiles("*", SearchOption.TopDirectoryOnly);
        var subDirs = dir.GetDirectories("*", SearchOption.TopDirectoryOnly);
        if (files.Length != 1 || subDirs.Length != 0)
        {
            return null;
        }

        if (operation == FolderStructureOperation.UnwrapSameNameSingleFile &&
            !string.Equals(Path.GetFileNameWithoutExtension(files[0].Name), dir.Name, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var targetFileName = FolderStructureNameTemplates.ResolveUnwrappedFileNameFromFolderPath(
            dir.FullName,
            files[0].Name,
            mismatchMode,
            _baseSettings);
        var fileCollision = NameCollisionResolver.Resolve(
            dir.Parent.FullName,
            targetFileName,
            FolderStructureCollisionOptions.Create(_baseSettings, NameCollisionTargetKind.File));
        return fileCollision.IsReady ? fileCollision.TargetPath : null;
    }

    /// <summary>
    /// AutoRelocation 템플릿 적용에 필요한 컨텍스트를 구성한다.
    /// </summary>
    /// <remarks>
    /// 템플릿 평가/로그 추적에 필요한 메타데이터(크기/시간/속성)를 한 곳에서 묶는다.
    /// </remarks>
    private RelocationContextWithRoot? CreateRelocationContext(string path, string? targetRootOverride)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return null;
        }

        var corrector = CreateFileNameCorrector();
        var preview = corrector.CreatePreview(path);
        var fileNameStem = GetRelocationFileNameStem(path);
        var knownFileKind = AutoRelocationFileTypeClassifier.GetKnownFileKind(path, _baseSettings);
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

    /// <summary>
    /// 대상의 폴더/파일 타입에 따라 템플릿용 스템 이름을 구성한다.
    /// </summary>
    private static string GetRelocationFileNameStem(string path)
    {
        return Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// 폴더 기반/파일 기반 템플릿 계산을 위해 스템을 추출한다.
    /// </summary>
    private static string GetRelocationFileExtension(string path)
    {
        return File.Exists(path) ? Path.GetExtension(path).TrimStart('.') : "";
    }

    /// <summary>
    /// 존재 충돌 시 (2), (3) ... 번호를 붙여서 유효한 경로를 만든다.
    /// </summary>
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

        return targetPath;
    }

    /// <summary>
    /// 후보 경로가 부모 경로 아래인지 판별한다.
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

    /// <summary>
    /// 파일명 정정기 생성 캐시로 플랜별 불필요한 규칙 파싱 비용을 줄인다.
    /// </summary>
    private KoreanFileNameCorrector CreateFileNameCorrector()
    {
        var dictionary = RenameDictionaryStore.Load();
        var parserProfile = RenameParserProfileStore.Load();
        var candidateProfile = RenameCandidateProfileStore.Load(dictionary.CommonPhrases);
        var rules = RenameRuleStore.Load();
        return new KoreanFileNameCorrector(new CorrectionOptions
        {
            ParserProfile = parserProfile,
            RenameDictionary = _baseSettings.RenameUseDictionary ? dictionary.Replacements : [],
            CommonPhrases = _baseSettings.RenameUseDictionary ? dictionary.CommonPhrases.ToArray() : [],
            CandidateProfile = _baseSettings.RenameUseDictionary ? candidateProfile : RenameCandidateProfileStore.CreateDefaultDocument(),
            Rules = rules.Rules
        });
    }
}
