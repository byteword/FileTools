using System.Reflection;
using System.Runtime.ExceptionServices;

namespace FileTools.Tests;

internal sealed class ArchiveMergeInvoker
{
    private static readonly Assembly FileToolsAssembly = LoadFileToolsAssembly();

    private readonly MethodInfo _mergeMethod;
    private readonly Type _optionsType;

    public ArchiveMergeInvoker()
    {
        _optionsType = FileToolsAssembly.GetType("FileTools.ArchiveMergeOptions", throwOnError: true)!;
        var operationsType = FileToolsAssembly.GetType("FileTools.ArchiveMergeOperations", throwOnError: true)!;
        _mergeMethod = operationsType.GetMethod("Merge", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(operationsType.FullName, "Merge");
    }

    public ArchiveMergeResult Merge(
        IReadOnlyList<string> sourcePaths,
        string outputPath,
        string layout = "PreserveInternalPaths",
        string collisionPolicy = "AutoNumber",
        string duplicatePolicy = "KeepBoth",
        string failurePolicy = "AbortAll",
        string compressionLevel = "StoreOnly")
    {
        var options = Activator.CreateInstance(_optionsType)
            ?? throw new InvalidOperationException("Could not create ArchiveMergeOptions.");
        SetProperty(options, "SourcePaths", sourcePaths.ToList());
        SetProperty(options, "OutputPath", outputPath);
        SetEnumProperty(options, "Layout", layout);
        SetEnumProperty(options, "CollisionPolicy", collisionPolicy);
        SetEnumProperty(options, "DuplicatePolicy", duplicatePolicy);
        SetEnumProperty(options, "FailurePolicy", failurePolicy);
        SetEnumProperty(options, "CompressionLevel", compressionLevel);
        SetProperty(options, "DeleteOriginals", false);

        var result = InvokeWithUnwrappedException(
            _mergeMethod,
            instance: null,
            [options, CancellationToken.None, null, null]);
        return ArchiveMergeResult.From(result!);
    }

    private void SetEnumProperty(object target, string propertyName, string value)
    {
        var property = GetProperty(propertyName);
        property.SetValue(target, Enum.Parse(property.PropertyType, value));
    }

    private void SetProperty(object target, string propertyName, object value)
    {
        GetProperty(propertyName).SetValue(target, value);
    }

    private PropertyInfo GetProperty(string propertyName)
    {
        return _optionsType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMemberException(_optionsType.FullName, propertyName);
    }

    private static object? InvokeWithUnwrappedException(MethodInfo method, object? instance, object?[] arguments)
    {
        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static Assembly LoadFileToolsAssembly()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
                   .FirstOrDefault(static assembly => assembly.GetName().Name == "FileTools")
               ?? Assembly.Load("FileTools");
    }
}

internal sealed record ArchiveMergeResult(
    int CandidateCount,
    int AppliedCount,
    int SkippedCount,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Errors)
{
    public static ArchiveMergeResult From(object result)
    {
        var type = result.GetType();
        return new ArchiveMergeResult(
            GetInt(type, result, "CandidateCount"),
            GetInt(type, result, "AppliedCount"),
            GetInt(type, result, "SkippedCount"),
            GetStrings(type, result, "Messages"),
            GetStrings(type, result, "Errors"));
    }

    private static int GetInt(Type type, object result, string propertyName)
    {
        return (int)(type.GetProperty(propertyName)?.GetValue(result)
            ?? throw new MissingMemberException(type.FullName, propertyName));
    }

    private static string[] GetStrings(Type type, object result, string propertyName)
    {
        var value = type.GetProperty(propertyName)?.GetValue(result)
            ?? throw new MissingMemberException(type.FullName, propertyName);
        return ((IEnumerable<string>)value).ToArray();
    }
}
