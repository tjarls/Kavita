using System;
using Kavita.Common.Disk;
using Microsoft.Extensions.Logging;

namespace Kavita.Update.UpdateEngine
{
    public interface IBackupAppData
    {
        void Backup();
    }

    public class BackupAppData : IBackupAppData
    {
        private readonly IDiskTransferService _diskTransferService;
        private readonly IDiskService _diskProvider;
        private readonly ILogger<BackupAppData> _logger;

        public BackupAppData(
            IDiskService diskProvider,
            IDiskTransferService diskTransferService,
            ILogger<BackupAppData> logger)
        {
            _diskProvider = diskProvider;
            _diskTransferService = diskTransferService;
            _logger = logger;
        }

        public void Backup()
        {
            _logger.LogInformation("Backing up appdata (database/config)");
            var backupFolderAppData = BackupAndRestore.UpdateBackupDirectory;

            if (_diskProvider.FolderExists(backupFolderAppData))
            {
                _diskProvider.EmptyFolder(backupFolderAppData);
            }
            else
            {
                _diskProvider.CreateFolder(backupFolderAppData);
            }

            try
            {
                // TODO: Get this data proper
                _diskTransferService.TransferFile("appsettings.json", BackupAndRestore.UpdateBackupDirectory, TransferMode.Copy);
                _diskTransferService.TransferFile("kavita.db", BackupAndRestore.UpdateBackupDirectory, TransferMode.Copy);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Couldn't create a data backup");
            }
        }
    }
}