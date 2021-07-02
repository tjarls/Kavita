using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Extensions;
using API.Interfaces.Services;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class ServerController : BaseApiController
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<ServerController> _logger;
        private readonly IConfiguration _config;
        private readonly IBackupService _backupService;
        private readonly IArchiveService _archiveService;
        private readonly IProgressService _progressService;
        private readonly IDirectoryService _directoryService;

        public ServerController(IHostApplicationLifetime applicationLifetime, ILogger<ServerController> logger, 
            IConfiguration config, IBackupService backupService, IArchiveService archiveService,
            IProgressService progressService, IDirectoryService directoryService)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _config = config;
            _backupService = backupService;
            _archiveService = archiveService;
            _progressService = progressService;
            _directoryService = directoryService;
        }
        
        [HttpPost("restart")]
        public ActionResult RestartServer()
        {
            _logger.LogInformation("{UserName} is restarting server from admin dashboard", User.GetUsername());
            
            _applicationLifetime.StopApplication();
            return Ok();
        }

        [HttpGet("logs")]
        public async Task<ActionResult> GetLogs()
        {
            var files = _backupService.LogFiles(_config.GetMaxRollingFiles(), _config.GetLoggingFileName());
            try
            {
                var (fileBytes, zipPath) = await _archiveService.CreateZipForDownload(files, "logs");
                return File(fileBytes, "application/zip", Path.GetFileName(zipPath));  
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("import-progress")]
        public async Task<ActionResult> UploadProgress()
        {
            var files = new List<string>();
            // var directory = Path.Join(DirectoryService.TempDirectory, "uploads");
            // new DirectoryInfo(directory).Delete(true);
            // foreach (var file in Request.Form.Files)
            // {
            //     var filename = Path.Join(directory, "upload_" + file.Name + ".csv");
            //     await using var fs = new FileStream(filename,
            //         FileMode.CreateNew, FileAccess.Write);
            //     fs.Seek(0, SeekOrigin.Begin);
            //     await file.OpenReadStream().CopyToAsync(fs);
            //     files.Add(filename);
            // }

            files.Add(@"C:\Users\Joe\Documents\Projects\KavitaOrg\Kavita\API\temp\upload_progress.csv");
            foreach (var filename in files)
            {
                await _progressService.LoadProgress(filename);
            }
            return Ok();
        }

        [HttpGet("export-progress")]
        public async Task<ActionResult> ExportProgress()
        {
            var exportFilePath = await _progressService.ExportProgress();
            if (string.IsNullOrEmpty(exportFilePath))
            {
                return BadRequest("No progress");
            }
            var content = await _directoryService.ReadFileAsync(exportFilePath);
            var format = Path.GetExtension(exportFilePath).Replace(".", "");
            
            // Calculates SHA1 Hash for byte[]
            Response.AddCacheHeader(content);

            return File(content, format);
        }
        
        
    }
}