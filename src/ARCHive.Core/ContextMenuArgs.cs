namespace ARCHive.Core;

public enum ContextMenuAction
{
    Copy,
    CreateArchive,
    ExtractArchive
}

public sealed record ContextMenuResult
{
    public bool IsEmpty { get; init; } = true;
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }
    public ContextMenuAction Action { get; init; }
    public IReadOnlyList<string> SourcePaths { get; init; } = [];

    public static ContextMenuResult Empty() => new() { IsEmpty = true };

    public static ContextMenuResult Error(string message) => new()
    {
        HasError = true,
        ErrorMessage = message
    };

    public static ContextMenuResult Success(
        ContextMenuAction action,
        IReadOnlyList<string> sourcePaths) => new()
    {
        IsEmpty = false,
        Action = action,
        SourcePaths = sourcePaths
    };
}

public static class ContextMenuArgs
{
    private const int MaxCommandLineLength = 32000;

    public static ContextMenuResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return ContextMenuResult.Empty();
        }

        var fullCommandLine = Environment.CommandLine;
        if (fullCommandLine.Length > MaxCommandLineLength)
        {
            return ContextMenuResult.Error(
                "The combined file paths exceed the Windows command-line " +
                "limit. Reduce the number of selected files or open ARCHive " +
                "directly and add files from within the application.");
        }

        if (args.Length < 2)
        {
            return ContextMenuResult.Empty();
        }

        var actionFlag = args[0];
        var sourcePaths = args.Skip(1).ToArray();

        return actionFlag.ToLowerInvariant() switch
        {
            "--copy" => ValidateAndReturn(
                ContextMenuAction.Copy, sourcePaths),
            "--archive" => ValidateAndReturn(
                ContextMenuAction.CreateArchive, sourcePaths),
            "--extract" => ValidateAndReturn(
                ContextMenuAction.ExtractArchive, sourcePaths),
            _ => ContextMenuResult.Empty()
        };
    }

    private static ContextMenuResult ValidateAndReturn(
        ContextMenuAction action,
        string[] sourcePaths)
    {
        if (sourcePaths.Length == 0)
        {
            return ContextMenuResult.Error(
                $"The {action} action was requested but no source files " +
                "were provided.");
        }

        if (action == ContextMenuAction.ExtractArchive &&
            sourcePaths.Length > 1)
        {
            return ContextMenuResult.Error(
                "Extract Archive accepts only one file at a time. " +
                "Select a single .7z or .zip file.");
        }

        return ContextMenuResult.Success(action, sourcePaths);
    }
}
