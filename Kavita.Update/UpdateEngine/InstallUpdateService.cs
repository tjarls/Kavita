using System;
using System.IO;
using Hangfire.Logging;
using Kavita.Common.Disk;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Extensions;
using Kavita.Common.Processes;
using Microsoft.Extensions.Logging;

namespace Kavita.Update.UpdateEngine
{
    public interface IInstallUpdateService
    {
        void Start(string installationFolder, int processId);
    }
    public class InstallUpdateService
    {
        private readonly IDiskProvider _diskService;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IDetectApplicationType _detectApplicationType;
        private readonly IDetectExistingVersion _detectExistingVersion;
        private readonly ITerminateNzbDrone _terminateNzbDrone;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IBackupAndRestore _backupAndRestore;
        private readonly IBackupAppData _backupAppData;
        private readonly IStartKavita _startKavita;
        private readonly IProcessProvider _processProvider;
        private readonly ILogger<InstallUpdateService> _logger;

        public InstallUpdateService(IDiskService diskService,
            IDiskTransferService diskTransferService,
            IDetectApplicationType detectApplicationType,
            IDetectExistingVersion detectExistingVersion,
            ITerminateNzbDrone terminateNzbDrone,
            IAppFolderInfo appFolderInfo,
            IBackupAndRestore backupAndRestore,
            IBackupAppData backupAppData,
            IStartKavita startKavita,
            IProcessProvider processProvider,
            ILogger<InstallUpdateService> logger)
        {
            _diskService = diskService;
            _diskTransferService = diskTransferService;
            _detectApplicationType = detectApplicationType;
            _detectExistingVersion = detectExistingVersion;
            _terminateNzbDrone = terminateNzbDrone;
            _appFolderInfo = appFolderInfo;
            _backupAndRestore = backupAndRestore;
            _backupAppData = backupAppData;
            _startKavita = startKavita;
            _processProvider = processProvider;
            _logger = logger;
        }
        
        private void Verify(string targetFolder, int processId)
        {
            _logger.LogInformation("Verifying requirements before update...");

            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                throw new ArgumentException("Target folder can not be null or empty");
            }

            if (!_diskService.FolderExists(targetFolder))
            {
                throw new DirectoryNotFoundException("Target folder doesn't exist " + targetFolder);
            }

            if (processId < 1)
            {
                throw new ArgumentException("Invalid process ID: " + processId);
            }

            if (!_processProvider.Exists(processId))
            {
                throw new ArgumentException("Process with ID doesn't exist " + processId);
            }

            _logger.LogInformation("Verifying Update Folder");
            if (!_diskService.FolderExists(_appFolderInfo.GetUpdatePackageFolder()))
            {
                throw new DirectoryNotFoundException("Update folder doesn't exist " + _appFolderInfo.GetUpdatePackageFolder());
            }
        }

        public void Start(string installationFolder, int processId)
        {
            _logger.LogInformation("Installation Folder: {0}", installationFolder);
            _logger.LogInformation("Updating Lidarr from version {0} to version {1}", _detectExistingVersion.GetExistingVersion(installationFolder), BuildInfo.Version);

            Verify(installationFolder, processId);

            if (installationFolder.EndsWith(@"\bin\Lidarr") || installationFolder.EndsWith(@"/bin/Lidarr"))
            {
                installationFolder = installationFolder.GetParentPath();
                _logger.LogInformation("Fixed Installation Folder: {0}", installationFolder);
            }

            var appType = _detectApplicationType.GetAppType();

            _processProvider.FindProcessByName(ProcessProvider.LIDARR_CONSOLE_PROCESS_NAME);
            _processProvider.FindProcessByName(ProcessProvider.LIDARR_PROCESS_NAME);

            if (OsInfo.IsWindows)
            {
                _terminateNzbDrone.Terminate(processId);
            }

            try
            {
                _backupAndRestore.Backup(installationFolder);
                _backupAppData.Backup();

                if (OsInfo.IsWindows)
                {
                    if (_processProvider.Exists(ProcessProvider.LIDARR_CONSOLE_PROCESS_NAME) || _processProvider.Exists(ProcessProvider.LIDARR_PROCESS_NAME))
                    {
                        _logger.LogError("Kavita was restarted prematurely by external process.");
                        return;
                    }
                }

                try
                {
                    _logger.LogInformation("Copying new files to target folder");
                    _diskTransferService.MirrorFolder(_appFolderInfo.GetUpdatePackageFolder(), installationFolder);

                    // Set executable flag on Lidarr app
                    if (OsInfo.IsOsx || (OsInfo.IsLinux && PlatformInfo.IsNetCore))
                    {
                        _diskService.SetFilePermissions(Path.Combine(installationFolder, "Lidarr"), "755", null);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to copy upgrade package to target folder.");
                    _backupAndRestore.Restore(installationFolder);
                    throw;
                }
            }
            finally
            {
                if (OsInfo.IsWindows)
                {
                    _startKavita.Start(appType, installationFolder);
                }
                else
                {
                    _terminateNzbDrone.Terminate(processId);

                    _logger.LogInformation("Waiting for external auto-restart.");
                    for (int i = 0; i < 10; i++)
                    {
                        System.Threading.Thread.Sleep(1000);

                        if (_processProvider.Exists(ProcessProvider.LIDARR_PROCESS_NAME))
                        {
                            _logger.LogInformation("Lidarr was restarted by external process.");
                            break;
                        }
                    }

                    if (!_processProvider.Exists(ProcessProvider.LIDARR_PROCESS_NAME))
                    {
                        _startKavita.Start(appType, installationFolder);
                    }
                }
            }
        }
    }
}