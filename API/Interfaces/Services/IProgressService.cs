using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces.Services
{
    /// <summary>
    /// Responsible for Exporting progress information and importing back into the DB
    /// </summary>
    public interface IProgressService
    {
        string ExportProgressForUser(AppUser user);
        Task<string> ExportProgress();
        string ExportRatingsForUser(AppUser user);
        string ExportRatings();
        void LoadProgressForUser(AppUser user, string csvPath);
        Task LoadProgress(string csvPath);
        void LoadRatingsForUser(AppUser user, string csvPath);
        void LoadRatings(string csvPath);
    }
}