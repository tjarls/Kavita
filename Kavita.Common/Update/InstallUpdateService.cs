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
            HttpClient httpClient, IOsInfo osInfo, ILogger<InstallUpdateService> logger, IVerifyUpdates updateVerifier)
        {
            _checkUpdateService = checkUpdateService;
            _diskService = diskService;
            _httpClient = httpClient;
            _osInfo = osInfo;
            _logger = logger;
            _updateVerifier = updateVerifier;
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
        
        // public void Handle(ApplicationStartingEvent message)
        // {
        //     // Check if we have to do an application update on startup
        //     try
        //     {
        //         var updateMarker = Path.Combine(_appFolderInfo.AppDataFolder, "update_required");
        //         if (!_diskProvider.FileExists(updateMarker))
        //         {
        //             return;
        //         }
        //
        //         _logger.LogDebug("Post-install update check requested");
        //
        //         // Don't do a prestartup update check unless BuiltIn update is enabled
        //         if (!_configFileProvider.UpdateAutomatically ||
        //             _configFileProvider.UpdateMechanism != UpdateMechanism.BuiltIn ||
        //             _deploymentInfoProvider.IsExternalUpdateMechanism)
        //         {
        //             _logger.LogDebug("Built-in updater disabled, skipping post-install update check");
        //             return;
        //         }
        //
        //         var latestAvailable = _checkUpdateService.AvailableUpdate();
        //         if (latestAvailable == null)
        //         {
        //             _logger.LogDebug("No post-install update available");
        //             _diskProvider.DeleteFile(updateMarker);
        //             return;
        //         }
        //
        //         _logger.Info("Installing post-install update from {0} to {1}", BuildInfo.Version, latestAvailable.Version);
        //         _diskProvider.DeleteFile(updateMarker);
        //
        //         var installing = InstallUpdate(latestAvailable);
        //
        //         if (installing)
        //         {
        //             _logger.LogDebug("Install in progress, giving installer 30 seconds.");
        //
        //             var watch = Stopwatch.StartNew();
        //
        //             while (watch.Elapsed < TimeSpan.FromSeconds(30))
        //             {
        //                 Thread.Sleep(1000);
        //             }
        //
        //             _logger.LogError("Post-install update not completed within 30 seconds. Attempting to continue normal operation.");
        //         }
        //         else
        //         {
        //             _logger.LogDebug("Post-install update cancelled for unknown reason. Attempting to continue normal operation.");
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Failed to perform the post-install update check. Attempting to continue normal operation.");
        //     }
        // }
    }
}