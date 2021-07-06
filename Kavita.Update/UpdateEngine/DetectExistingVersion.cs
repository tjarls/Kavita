using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Kavita.Update.UpdateEngine
{
    public interface IDetectExistingVersion
    {
        string GetExistingVersion(string targetFolder);
    }
    public class DetectExistingVersion : IDetectExistingVersion
    {
        private readonly ILogger<DetectExistingVersion> _logger;

        public DetectExistingVersion(ILogger<DetectExistingVersion> logger)
        {
            _logger = logger;
        }
        
        public string GetExistingVersion(string targetFolder)
        {
            try
            {
                var targetExecutable = Path.Combine(targetFolder, "Kavita.exe");

                if (File.Exists(targetExecutable))
                {
                    var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(targetExecutable);

                    return versionInfo.FileVersion;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get existing version from {FolderPath}", targetFolder);
            }

            return "(unknown)";
        }
    }
}