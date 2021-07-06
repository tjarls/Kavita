using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kavita.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Kavita.Common.Disk
{
    public interface IDiskTransferService
    {
        TransferMode TransferFolder(string sourcePath, string targetPath, TransferMode mode);
        TransferMode TransferFile(string sourcePath, string targetPath, TransferMode mode, bool overwrite = false);
        int MirrorFolder(string sourcePath, string targetPath);
    }
    
    public class DiskTransferService : IDiskTransferService
    {
        private readonly IDiskService _diskService;
        private readonly ILogger<DiskTransferService> _logger;

        public DiskTransferService(IDiskService diskService, ILogger<DiskTransferService> logger)
        {
            _diskService = diskService;
            _logger = logger;
        }
        public int MirrorFolder(string sourcePath, string targetPath)
        {
            var filesCopied = 0;
            //Ensure.That(sourcePath, () => sourcePath).IsValidPath();
            //Ensure.That(targetPath, () => targetPath).IsValidPath();

            sourcePath = ResolveRealParentPath(sourcePath);
            targetPath = ResolveRealParentPath(targetPath);

            _logger.LogDebug("Mirror Folder [{SourcePath}] > [{TargetPath}]", sourcePath, targetPath);

            _diskService.ExistOrCreate(targetPath);
            
            
            var sourceFolders =  _diskService.GetDirectoryInfos(sourcePath);
            var targetFolders =  _diskService.GetDirectoryInfos(targetPath);

            foreach (var subDir in targetFolders.Where(v => !sourceFolders.Any(d => d.Name == v.Name)))
            {
                if (ShouldIgnore(subDir))
                {
                    continue;
                }

                _diskService.DeleteFolder(subDir.FullName, true);
            }

            foreach (var subDir in sourceFolders)
            {
                if (ShouldIgnore(subDir))
                {
                    continue;
                }

                filesCopied += MirrorFolder(subDir.FullName, Path.Combine(targetPath, subDir.Name));
            }

            var sourceFiles = _diskService.GetFileInfos(sourcePath);
            //var sourceFiles = _diskService.GetFiles(sourcePath).Select(file => new FileInfo(file));
            //var targetFiles = _diskService.GetFiles(targetPath).Select(file => new FileInfo(file));
            var targetFiles = _diskService.GetFileInfos(targetPath);

            foreach (var targetFile in targetFiles.Where(v => sourceFiles.All(d => d.Name != v.Name)))
            {
                if (ShouldIgnore(targetFile))
                {
                    continue;
                }

                _diskService.DeleteFile(targetFile.FullName);
            }

            foreach (var sourceFile in sourceFiles)
            {
                if (ShouldIgnore(sourceFile))
                {
                    continue;
                }

                var targetFile = Path.Combine(targetPath, sourceFile.Name);

                if (CompareFiles(sourceFile.FullName, targetFile))
                {
                    continue;
                }

                TransferFile(sourceFile.FullName, targetFile, TransferMode.Copy, true);
                filesCopied++;
            }

            return filesCopied;
        }
        
        private bool ShouldIgnore(IDirectoryInfo folder)
        {
            if (folder.Name.StartsWith(".nfs") || folder.Name.StartsWith(".DS_Store") || folder.Name.StartsWith("@eaDir"))
            {
                _logger.LogTrace("Ignoring folder {FolderName}", folder.FullName);
                return true;
            }

            return false;
        }
        private bool ShouldIgnore(IFileInfo file)
        {
            if (file.Name.StartsWith(".nfs") || file.Name == "debug.log" || file.Name.EndsWith(".socket"))
            {
                _logger.LogTrace("Ignoring file {FileName}", file.FullName);
                return true;
            }

            return false;
        }
        
        private string ResolveRealParentPath(string path)
        {
            var parentPath = path.GetParentPath();
            if (!_diskService.FolderExists(parentPath))
            {
                return path;
            }

            var realParentPath = parentPath.GetActualCasing();

            var partialChildPath = path.Substring(parentPath.Length);

            return realParentPath + partialChildPath;
        }
        
        private bool CompareFiles(string sourceFile, string targetFile)
        {
            // if (!_diskService.FileExists(sourceFile) || !_diskService.FileExists(targetFile))
            // {
            //     return false;
            // }
            //
            // if (_diskService.GetFileSize(sourceFile) != _diskService.GetFileSize(targetFile))
            // {
            //     return false;
            // }

            var sourceBuffer = new byte[64 * 1024];
            var targetBuffer = new byte[64 * 1024];
            using (var sourceStream = _diskService.OpenReadStream(sourceFile))
            using (var targetStream = _diskService.OpenReadStream(targetFile))
            {
                while (true)
                {
                    var sourceLength = sourceStream.Read(sourceBuffer, 0, sourceBuffer.Length);
                    var targetLength = targetStream.Read(targetBuffer, 0, targetBuffer.Length);

                    if (sourceLength != targetLength)
                    {
                        return false;
                    }

                    if (sourceLength == 0)
                    {
                        return true;
                    }

                    for (var i = 0; i < sourceLength; i++)
                    {
                        if (sourceBuffer[i] != targetBuffer[i])
                        {
                            return false;
                        }
                    }
                }
            }
        }

        public TransferMode TransferFolder(string sourcePath, string targetPath, TransferMode mode)
        {
            return TransferFile(sourcePath, targetPath, mode, false);
        }

        public TransferMode TransferFile(string sourcePath, string targetPath, TransferMode mode, bool overwrite = false)
        {
            //Ensure.That(sourcePath, () => sourcePath).IsValidPath();
            //Ensure.That(targetPath, () => targetPath).IsValidPath();

            sourcePath = ResolveRealParentPath(sourcePath);
            targetPath = ResolveRealParentPath(targetPath);

            _logger.LogDebug("{0} [{1}] > [{2}]", mode, sourcePath, targetPath);

            var originalSize = _diskService.GetFileSize(sourcePath);

            if (sourcePath == targetPath)
            {
                throw new IOException($"Source and destination can't be the same {sourcePath}");
            }

            if (sourcePath.PathEquals(targetPath, StringComparison.InvariantCultureIgnoreCase))
            {
                if (mode.HasFlag(TransferMode.Copy))
                {
                    throw new IOException(string.Format("Source and destination can't be the same {0}", sourcePath));
                }

                if (mode.HasFlag(TransferMode.Move))
                {
                    var tempPath = sourcePath + ".backup~";

                    // _diskService.MoveFile(sourcePath, tempPath, true);
                    // try
                    // {
                    //     ClearTargetPath(sourcePath, targetPath, overwrite);
                    //
                    //     _diskProvider.MoveFile(tempPath, targetPath);
                    //
                    //     return TransferMode.Move;
                    // }
                    // catch
                    // {
                    //     RollbackMove(sourcePath, tempPath);
                    //     throw;
                    // }
                    return TransferMode.Move;
                }

                return TransferMode.None;
            }
            return TransferMode.None;
        }
    }
}