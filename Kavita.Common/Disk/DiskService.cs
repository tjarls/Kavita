using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Kavita.Common.Disk
{
    public interface IDiskService
    {
        List<IDirectoryInfo> GetDirectoryInfos(string path);
        List<IFileInfo> GetFileInfos(string path, SearchOption searchOption = SearchOption.TopDirectoryOnly);
        FileStream OpenReadStream(string path);
        bool ExistOrCreate(string path);
        bool FolderExists(string path);
        long GetFolderSize(string path);
        long GetFileSize(string path);
        void DeleteFile(string path);
        void DeleteFolder(string path, bool recursive);
        
    }
    public class DiskService : IDiskService
    {
        protected IFileSystem _fileSystem;
        private readonly ILogger<DiskService> _logger;

        public DiskService(IFileSystem fileSystem, ILogger<DiskService> logger)
        {
            _fileSystem = fileSystem;
            _logger = logger;
        }
        
        public static StringComparison PathStringComparison
        {
            get
            {
                if (OsInfo.IsWindows)
                {
                    return StringComparison.OrdinalIgnoreCase;
                }

                return StringComparison.Ordinal;
            }
        }
        
        public List<IDirectoryInfo> GetDirectoryInfos(string path)
        {
            var di = _fileSystem.DirectoryInfo.FromDirectoryName(path);

            return di.GetDirectories().ToList();
        }

        public List<IFileInfo> GetFileInfos(string path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            //TODO: Ensure.That(path, () => path).IsValidPath();

            var di = _fileSystem.DirectoryInfo.FromDirectoryName(path);

            return di.GetFiles("*", searchOption).ToList();
        }

        public FileStream OpenReadStream(string path)
        {
            if (!FileExists(path))
            {
                throw new FileNotFoundException("Unable to find file: " + path, path);
            }

            return (FileStream)_fileSystem.FileStream.Create(path, FileMode.Open, FileAccess.Read);
        }

        /// <summary>
        /// Returns true if the path exists and is a directory. If path does not exist, this will create it. Returns false in all fail cases.
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        public bool ExistOrCreate(string directoryPath)
        {
            var di = new DirectoryInfo(directoryPath);
            if (di.Exists) return true;
            try
            {
                Directory.CreateDirectory(directoryPath);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public bool FolderExists(string path)
        {
            //Ensure.That(path, () => path).IsValidPath();
            return _fileSystem.Directory.Exists(path);
        }

        public long GetFolderSize(string path)
        {
            //Ensure.That(path, () => path).IsValidPath();

            return GetFiles(path, SearchOption.AllDirectories).Sum(e => _fileSystem.FileInfo.FromFileName(e).Length);
        }

        public long GetFileSize(string path)
        {
            //Ensure.That(path, () => path).IsValidPath();

            if (!FileExists(path))
            {
                throw new FileNotFoundException("File doesn't exist: " + path);
            }

            var fi = _fileSystem.FileInfo.FromFileName(path);
            return fi.Length;
        }

        public void DeleteFile(string path)
        {
            throw new NotImplementedException();
        }

        public void DeleteFolder(string path, bool recursive)
        {
            //Ensure.That(path, () => path).IsValidPath();
            //Logger.LogDebug("Deleting file: {0}", path);

            RemoveReadOnly(path);

            _fileSystem.File.Delete(path);
        }
        
        private static void RemoveReadOnly(string path)
        {
            if (File.Exists(path))
            {
                var attributes = File.GetAttributes(path);

                if (attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    var newAttributes = attributes & ~FileAttributes.ReadOnly;
                    File.SetAttributes(path, newAttributes);
                }
            }
        }

        public bool FileExists(string path)
        {
            //TODO: Ensure.That(path, () => path).IsValidPath();
            return FileExists(path, PathStringComparison);
        }
        
        public bool FileExists(string path, StringComparison stringComparison)
        {
            //TODO: Ensure.That(path, () => path).IsValidPath();

            switch (stringComparison)
            {
                case StringComparison.CurrentCulture:
                case StringComparison.InvariantCulture:
                case StringComparison.Ordinal:
                {
                    return _fileSystem.File.Exists(path) && path == path.GetActualCasing();
                }

                default:
                {
                    return _fileSystem.File.Exists(path);
                }
            }
        }
        public string[] GetFiles(string path, SearchOption searchOption)
        {
            //Ensure.That(path, () => path).IsValidPath();

            return _fileSystem.Directory.GetFiles(path, "*.*", searchOption);
        }
    }
}