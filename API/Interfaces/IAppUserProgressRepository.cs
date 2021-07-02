using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.Export;
using API.Entities.Enums;

namespace API.Interfaces
{
    public interface IAppUserProgressRepository
    {
        Task<int> CleanupAbandonedChapters();
        Task<bool> UserHasProgress(LibraryType libraryType, int userId);
        Task<IEnumerable<ProgressReportDto>> GetAllProgressDto();
    }
}