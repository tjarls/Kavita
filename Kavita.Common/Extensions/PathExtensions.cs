using System;
using System.IO;
using System.Text.RegularExpressions;
using Kavita.Common.Disk;
using Kavita.Common.EnvironmentInfo;

namespace Kavita.Common.Extensions
{
    public static class PathExtensions
    {
        private static readonly Regex PARENT_PATH_END_SLASH_REGEX = new Regex(@"(?<!:)\\$", RegexOptions.Compiled);
        
        public static string GetActualCasing(this string path)
        {
            if (OsInfo.IsNotWindows || path.StartsWith("\\"))
            {
                return path;
            }

            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
            {
                return GetProperCapitalization(new DirectoryInfo(path));
            }

            var fileInfo = new FileInfo(path);
            var dirInfo = fileInfo.Directory;

            var fileName = fileInfo.Name;

            if (dirInfo != null && fileInfo.Exists)
            {
                fileName = dirInfo.GetFiles(fileInfo.Name)[0].Name;
            }

            return Path.Combine(GetProperCapitalization(dirInfo), fileName);
        }
        
        private static string GetProperCapitalization(DirectoryInfo dirInfo)
        {
            var parentDirInfo = dirInfo.Parent;
            if (parentDirInfo == null)
            {
                //Drive letter
                return dirInfo.Name.ToUpper();
            }

            var folderName = dirInfo.Name;

            if (dirInfo.Exists)
            {
                folderName = parentDirInfo.GetDirectories(dirInfo.Name)[0].Name;
            }

            return Path.Combine(GetProperCapitalization(parentDirInfo), folderName);
        }
        
        public static string GetParentPath(this string childPath)
        {
            var cleanPath = OsInfo.IsWindows
                ? PARENT_PATH_END_SLASH_REGEX.Replace(childPath, "")
                : childPath.TrimEnd(Path.DirectorySeparatorChar);

            if (string.IsNullOrWhiteSpace(cleanPath))
            {
                return null;
            }

            return Directory.GetParent(cleanPath)?.FullName;
        }
        
        public static string ProcessNameToExe(this string processName, PlatformType runtime)
        {
            if (OsInfo.IsWindows || runtime != PlatformType.NetCore)
            {
                processName += ".exe";
            }

            return processName;
        }
        
        public static string ProcessNameToExe(this string processName)
        {
            return processName.ProcessNameToExe(PlatformInfo.Platform);
        }
        
        public static bool PathEquals(this string firstPath, string secondPath, StringComparison? comparison = null)
        {
            if (!comparison.HasValue)
            {
                comparison = DiskService.PathStringComparison;
            }

            if (firstPath.Equals(secondPath, comparison.Value))
            {
                return true;
            }

            return string.Equals(firstPath.CleanFilePath(), secondPath.CleanFilePath(), comparison.Value);
        }
        
        public static string CleanFilePath(this string path)
        {
            //Ensure.That(path, () => path).IsNotNullOrWhiteSpace();
            //Ensure.That(path, () => path).IsValidPath();

            var info = new FileInfo(path.Trim());
            return info.FullName.CleanFilePathBasic();
        }
        
        public static string CleanFilePathBasic(this string path)
        {
            //UNC
            if (OsInfo.IsWindows && path.StartsWith(@"\\"))
            {
                return path.TrimEnd('/', '\\', ' ');
            }

            if (OsInfo.IsNotWindows && path.TrimEnd('/').Length == 0)
            {
                return "/";
            }

            return path.TrimEnd('/').Trim('\\', ' ');
        }
    }
}