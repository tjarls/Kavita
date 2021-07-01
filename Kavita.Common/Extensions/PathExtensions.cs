using System.IO;
using System.Text.RegularExpressions;
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
    }
}