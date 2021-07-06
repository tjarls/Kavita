using System.IO;
using Kavita.Common.Disk;
using Microsoft.Extensions.Logging;

namespace Kavita.Update.UpdateEngine
{
    public interface IBackupAndRestore
    {
        void Backup(string source);
        void Restore(string target);
    }
    public class BackupAndRestore : IBackupAndRestore
    {
        private readonly IDiskTransferService _diskTransferService;
        private readonly Logger<BackupAndRestore> _logger;
        public static readonly string UpdateDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp/update");
        public static readonly string UpdateBackupDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp/backup");
        
        public BackupAndRestore(IDiskTransferService diskTransferService, Logger<BackupAndRestore> logger)
        {
            _diskTransferService = diskTransferService;
            _logger = logger;
        }
        
        public void Backup(string source)
        {
            _logger.LogInformation("Creating backup of existing installation");
            _diskTransferService.MirrorFolder(source, UpdateDirectory);
        }

        public void Restore(string target)
        {
            _logger.LogInformation("Attempting to rollback upgrade");
            var count = _diskTransferService.MirrorFolder(UpdateDirectory, target);
            _logger.LogInformation("Rolled back {0} files", count);
        }
    }
}