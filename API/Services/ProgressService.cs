using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Export;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Services;
using CsvHelper;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class ProgressService : IProgressService
    {
        private readonly ILogger<ProgressService> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public ProgressService(ILogger<ProgressService> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        public string ExportProgressForUser(AppUser user)
        {
            throw new System.NotImplementedException();
        }

        public async Task<string> ExportProgress()
        {
            var progress = await _unitOfWork.AppUserProgressRepository.GetAllProgressDto();
            var path = Path.Join(DirectoryService.TempDirectory, "progress.csv");
            await using var writer = new StreamWriter(path) ;
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture) ;
            await csv.WriteRecordsAsync(progress);

            return !progress.Any() ? path : string.Empty;
        }

        public string ExportRatingsForUser(AppUser user)
        {
            throw new System.NotImplementedException();
        }

        public string ExportRatings()
        {
            throw new System.NotImplementedException();
        }

        public void LoadProgressForUser(AppUser user, string csvPath)
        {
            throw new System.NotImplementedException();
        }

        public async Task LoadProgress(string csvPath)
        {
            using var reader = new StreamReader(csvPath) ;
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<ProgressReportDto>();
            // TODO: Process and import into DB
            var allProgress = await _unitOfWork.AppUserProgressRepository.GetAllProgressDto();
            // Create a Map of SeriesId -> custom object
            var seriesMap = new Dictionary<string, IList<ProgressReportDto>>();
            foreach (var userprogress in allProgress)
            {
                var normalizedName = Parser.Parser.Normalize(userprogress.OriginalName);
                seriesMap.TryGetValue(normalizedName, out var dtos);
                if (dtos == null) dtos = new List<ProgressReportDto>();
                dtos.Add(userprogress);
                
                if (!seriesMap.ContainsKey(normalizedName))
                {
                    seriesMap.Add(normalizedName, dtos);    
                }
                
                
            }
            _logger.LogInformation("Mapped {SeriesCount} Series", seriesMap.Keys.Count);
            //_unitOfWork.AppUserProgressRepository.
            // We need to get all the existing user progress objects and create new ones. In order to do that, we need the new seriesIds, VolumeIds, and ChapterIds
            // var allSeries = await _unitOfWork.SeriesRepository.GetFullSeriesByIds(seriesMap.Keys.ToArray());
            // foreach (var series in allSeries)
            // {
            //     // First we need to transform the old data into references of the new data
            // }
            
            // Once we have the new data, then we need look up user progress for the users given and apply updates
                
            if (_unitOfWork.HasChanges() && Task.Run(() => _unitOfWork.CommitAsync()).Result)
            {
               // _logger.LogInformation("Updated metadata for {LibraryName} in {ElapsedMilliseconds} milliseconds", library.Name, sw.ElapsedMilliseconds);
            }
        }
        

        public void LoadRatingsForUser(AppUser user, string csvPath)
        {
            throw new System.NotImplementedException();
        }

        public void LoadRatings(string csvPath)
        {
            throw new System.NotImplementedException();
        }
    }
}