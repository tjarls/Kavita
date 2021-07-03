using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.Common.Disk;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Extensions;
using Kavita.Common.Processes;
using Kavita.Update.UpdateEngine;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Kavita.Common.Update
{
    public interface IInstallUpdateService
    {
        Task CheckForUpdates();
    }
    public class InstallUpdateService : IInstallUpdateService
    {
        private readonly ICheckUpdateService _checkUpdateService;
        private readonly IDiskService _diskService;
        private readonly HttpClient _httpClient;
        private readonly IOsInfo _osInfo;
        private readonly ILogger<InstallUpdateService> _logger;
        private readonly IVerifyUpdates _updateVerifier;
        private static readonly string DownloadDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp/downloads");
        private static readonly string UpdateDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp/update");

        public InstallUpdateService(ICheckUpdateService checkUpdateService, IDiskService diskService, 
            HttpClient httpClient, IOsInfo osInfo, ILogger<InstallUpdateService> logger, IVerifyUpdates updateVerifier, IStartKavita startKavita)
        {
            _checkUpdateService = checkUpdateService;
            _diskService = diskService;
            _httpClient = httpClient;
            _osInfo = osInfo;
            _logger = logger;
            _updateVerifier = updateVerifier;
            _startKavita = startKavita;
        }
        public async Task CheckForUpdates()
        {
            try
            {
                // TODO: Check if AutoUpdate is enabled && Not a docker user

                _logger.LogInformation("Checking for Updates");
                var latestAvailable = await _checkUpdateService.AvailableUpdate();
                if (latestAvailable == null)
                {
                    _logger.LogInformation("No update found");
                    return;
                }

                _logger.LogInformation("Found Update {Version}", latestAvailable.Version);

                var installing = await InstallUpdate(latestAvailable);

                if (installing)
                {
                    _logger.LogDebug("Install in progress, giving installer 30 seconds.");

                    var watch = Stopwatch.StartNew();

                    while (watch.Elapsed < TimeSpan.FromSeconds(30))
                    {
                        Thread.Sleep(1000);
                    }

                    _logger.LogError(
                        "Post-install update not completed within 30 seconds. Attempting to continue normal operation.");
                }
                else
                {
                    _logger.LogDebug(
                        "Post-install update cancelled for unknown reason. Attempting to continue normal operation.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform the post-install update check. Attempting to continue normal operation");
            }
        }

        private async Task<bool> InstallUpdate(UpdatePackage updatePackage)
        {
            // Download file: latestAvailable.Url
            // Extract to temp directory
            // Copy/Merge appsettings.json over and copy DB
            // Delete source directory (make sure backup, etc aren't touched)
            // Copy extracted data back over. 

            _logger.LogInformation("Downloading update {0}", updatePackage.Version);
            _logger.LogDebug("Downloading update package from [{0}] to [{1}]", updatePackage.Url, DownloadDirectory);
            var path = await updatePackage.Url
                .DownloadFileAsync(DownloadDirectory);

            _logger.LogInformation("Verifying update package");

            if (!_updateVerifier.Verify(updatePackage, path))
            {
                _logger.LogError("Update package is invalid");
                throw new UpdateVerificationFailedException("Update file '{0}' is invalid", path);
            }

            _logger.LogInformation("Update package verified successfully");

            _logger.LogInformation("Extracting Update package");
            new DirectoryInfo(UpdateDirectory).Delete(true);
            _diskService.ExistOrCreate(UpdateDirectory);
            using var archive = ArchiveFactory.Open(path);
            archive.WriteToDirectory(UpdateDirectory, new ExtractionOptions()
            {
                Overwrite = true,
                ExtractFullPath = true
            });
            
            _logger.LogInformation("Update package extracted successfully");

            EnsureValidBranch(updatePackage);

            CopyConfigToInstall(updatePackage);
            
            // TODO: Backup my own directory completely
            
            // Perform the final move and somehow stop this service and have new one start
            //_backupService.Backup(BackupType.Update);
            
            _logger.LogInformation("Preparing client");
            // _diskTransferService.TransferFolder(_appFolderInfo.GetUpdateClientFolder(), updateSandboxFolder, TransferMode.Move);
            //
            // // Set executable flag on update app
            // if (OsInfo.IsOsx || (OsInfo.IsLinux && PlatformInfo.IsNetCore))
            // {
            //     _diskProvider.SetFilePermissions(_appFolderInfo.GetUpdateClientExePath(updatePackage.Runtime), "755", null);
            // }
            //
            // _logger.LogInformation("Starting update client {0}", _appFolderInfo.GetUpdateClientExePath(updatePackage.Runtime));
            // _logger.LogInformation("Kavita will restart shortly.");
            //
            // _processProvider.Start(_appFolderInfo.GetUpdateClientExePath(updatePackage.Runtime), GetUpdaterArgs(updateSandboxFolder));
            
            return true;
        }

        /// <summary>
        /// Copies over any custom appsettings.json config to the new install
        /// </summary>
        /// <param name="package"></param>
        private void CopyConfigToInstall(UpdatePackage package)
        {
            var installConfig = Path.Join(UpdateDirectory, "Kavita/appsettings.json");
            Configuration.SetBranch(installConfig, Configuration.Branch);
            Configuration.SetPort(installConfig, Configuration.Port);
            Configuration.SetJwtToken(installConfig, Configuration.JwtToken);
            Configuration.SetLogLevel(installConfig, Configuration.LogLevel);
            
        }
        
        private void EnsureValidBranch(UpdatePackage package)
        {
            var currentBranch = Configuration.Branch;
            if (package.Branch != currentBranch)
            {
                try
                {
                    _logger.LogInformation("Branch [{0}] is being redirected to [{1}]]", currentBranch, package.Branch);
                    Configuration.Branch = package.Branch;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Couldn't change the branch from [{0}] to [{1}].", currentBranch, package.Branch);
                }
            }
        }
        
        public void Start(string installationFolder, int processId)
        {
            _logger.LogInformation("Installation Folder: {0}", installationFolder);
            _logger.LogInformation("Updating Kavita from version {0} to version {1}", _detectExistingVersion.GetExistingVersion(installationFolder), BuildInfo.Version);

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
                        _logger.Error("Lidarr was restarted prematurely by external process.");
                        return;
                    }
                }

                try
                {
                    _logger.Info("Copying new files to target folder");
                    _diskTransferService.MirrorFolder(_appFolderInfo.GetUpdatePackageFolder(), installationFolder);

                    // Set executable flag on Lidarr app
                    if (OsInfo.IsOsx || (OsInfo.IsLinux && PlatformInfo.IsNetCore))
                    {
                        _diskProvider.SetFilePermissions(Path.Combine(installationFolder, "Kavita"), "755", null);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to copy upgrade package to target folder");
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

                    _logger.Info("Waiting for external auto-restart.");
                    for (int i = 0; i < 10; i++)
                    {
                        System.Threading.Thread.Sleep(1000);

                        if (_processProvider.Exists(ProcessProvider.LIDARR_PROCESS_NAME))
                        {
                            _logger.Info("Lidarr was restarted by external process.");
                            break;
                        }
                    }

                    if (!_processProvider.Exists(ProcessProvider.LIDARR_PROCESS_NAME))
                    {
                        _startNzbDrone.Start(appType, installationFolder);
                    }
                }
            }
        }
    }
}