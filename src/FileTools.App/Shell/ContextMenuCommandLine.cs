namespace FileTools;

internal static class ContextMenuCommandLine
{
    public static bool TryParseCommand(string value, out ContextMenuCommand command)
    {
        if (Enum.TryParse(value, ignoreCase: true, out command) &&
            Enum.IsDefined(command))
        {
            return true;
        }

        if (!Enum.TryParse(value, ignoreCase: true, out ToolMode mode) ||
            !Enum.IsDefined(mode))
        {
            return false;
        }

        command = mode switch
        {
            ToolMode.FileNameCorrection => ContextMenuCommand.FileNameCorrection,
            ToolMode.FolderStructure => ContextMenuCommand.FolderStructure,
            ToolMode.AutoRelocation => ContextMenuCommand.AutoRelocation,
            ToolMode.ArchiveMerge => ContextMenuCommand.ArchiveMergeGroupByArchiveName,
            _ => default
        };
        return true;
    }

    public static string CreateRegistryCommand(string exePath, ContextMenuCommand command)
    {
        return command == ContextMenuCommand.OpenApp
            ? $"\"{exePath}\" /open \"%1\""
            : $"\"{exePath}\" /context {command} \"%1\"";
    }
}
