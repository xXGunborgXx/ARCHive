using System.Text;
using System.Collections.ObjectModel;

namespace ARCHive.Core;

public static class PathUtilities
{
    public static long? TryGetAvailableFreeSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            return string.IsNullOrWhiteSpace(root)
                ? null
                : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public static string? TryGetDriveFormat(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            return string.IsNullOrWhiteSpace(root)
                ? null
                : new DriveInfo(root).DriveFormat;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public static DriveType? TryGetDriveType(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            return string.IsNullOrWhiteSpace(root)
                ? null
                : new DriveInfo(root).DriveType;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public static string GetTopLevelName(string sourcePath) =>
        Directory.Exists(sourcePath)
            ? new DirectoryInfo(sourcePath).Name
            : Path.GetFileName(sourcePath);

    public static string FormatArgumentsForLog(Collection<string> arguments)
    {
        var builder = new StringBuilder();
        foreach (var argument in arguments)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append('"').Append(argument.Replace("\"", "\\\"")).Append('"');
        }

        return builder.ToString();
    }

    public static string FormatBytesForLog(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
