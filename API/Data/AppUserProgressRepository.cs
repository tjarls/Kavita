using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Export;
using API.Entities.Enums;
using API.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class AppUserProgressRepository : IAppUserProgressRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public AppUserProgressRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        /// <summary>
        /// This will remove any entries that have chapterIds that no longer exists. This will execute the save as well.
        /// </summary>
        public async Task<int> CleanupAbandonedChapters()
        {
            var chapterIds = _context.Chapter.Select(c => c.Id);

            var rowsToRemove = await _context.AppUserProgresses
                .Where(progress => !chapterIds.Contains(progress.ChapterId))
                .ToListAsync();
            
            _context.RemoveRange(rowsToRemove);
            return await _context.SaveChangesAsync() > 0 ? rowsToRemove.Count : 0;
        }

        /// <summary>
        /// Checks if user has any progress against a library of passed type
        /// </summary>
        /// <param name="libraryType"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<bool> UserHasProgress(LibraryType libraryType, int userId)
        {
            var seriesIds = await _context.AppUserProgresses
                .Where(aup => aup.PagesRead > 0 && aup.AppUserId == userId)
                .AsNoTracking()
                .Select(aup => aup.SeriesId)
                .ToListAsync();

            if (seriesIds.Count == 0) return false;
            
            return await _context.Series
                .Include(s => s.Library)
                .Where(s => seriesIds.Contains(s.Id) && s.Library.Type == libraryType)
                .AsNoTracking()
                .AnyAsync();
        }
        
        public async Task<IEnumerable<RatingReviewReportDto>> GetAllRatingReviewsDto()
        {
            return await _context.AppUserProgresses
                .Join(_context.AppUser, p => p.AppUserId, user => user.Id, (p, user) => new
                {
                    user.NormalizedUserName,
                    user.UserName,
                    p.AppUserId,
                    p.PagesRead,
                    p.VolumeId,
                    p.ChapterId,
                    p.SeriesId,
                    
                })
                .Join(_context.Volume, p => p.VolumeId, volume => volume.Id, (p, volume) => new
                {
                    VolumeNumber = volume.Number,
                    p.AppUserId,
                    p.PagesRead,
                    p.VolumeId,
                    p.ChapterId,
                    p.SeriesId,
                    p.NormalizedUserName,
                    p.UserName,
                })
                .Join(_context.Chapter, p => p.ChapterId, chapter => chapter.Id, (p, chapter) => new
                {
                    p.VolumeNumber,
                    p.AppUserId,
                    p.PagesRead,
                    p.VolumeId,
                    p.ChapterId,
                    p.SeriesId,
                    chapter.Range,
                    p.NormalizedUserName,
                    p.UserName,
                    chapter.IsSpecial
                })
                .Join(_context.Series, p => p.SeriesId, series => series.Id, (p, series) => 
                    new RatingReviewReportDto()
                    {
                        VolumeNumber = p.VolumeNumber,
                        Name = series.Name,
                        PagesRead = p.PagesRead,
                        ChapterRange = p.Range,
                        LocalizedName = series.LocalizedName,
                        NormalizedName = series.NormalizedName,
                        OriginalName = series.OriginalName,
                        SeriesId = series.Id,
                        UserId = p.AppUserId,
                        NormalizedUserName = p.NormalizedUserName,
                        UserName = p.UserName,
                        IsSpecial = p.IsSpecial,
                        //UserReview = 
                    })
                .AsNoTracking()
                .Where(thing => thing.PagesRead > 0)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProgressReportDto>> GetAllProgressDto()
        {
            return await _context.AppUserProgresses
                .Join(_context.AppUser, p => p.AppUserId, user => user.Id, (p, user) => new
                {
                    user.NormalizedUserName,
                    user.UserName,
                    p.AppUserId,
                    p.PagesRead,
                    p.VolumeId,
                    p.ChapterId,
                    p.SeriesId
                })
                .Join(_context.Volume, p => p.VolumeId, volume => volume.Id, (p, volume) => new
                {
                    VolumeNumber = volume.Number,
                    p.AppUserId,
                    p.PagesRead,
                    p.VolumeId,
                    p.ChapterId,
                    p.SeriesId,
                    p.NormalizedUserName,
                    p.UserName,
                })
                .Join(_context.Chapter, p => p.ChapterId, chapter => chapter.Id, (p, chapter) => new
                {
                    p.VolumeNumber,
                    p.AppUserId,
                    p.PagesRead,
                    p.VolumeId,
                    p.ChapterId,
                    p.SeriesId,
                    chapter.Range,
                    p.NormalizedUserName,
                    p.UserName,
                    chapter.IsSpecial
                })
                .Join(_context.Series, p => p.SeriesId, series => series.Id, (p, series) => 
                    new ProgressReportDto()
                    {
                        VolumeNumber = p.VolumeNumber,
                        Name = series.Name,
                        Pages = series.Pages,
                        PagesRead = p.PagesRead,
                        ChapterRange = p.Range,
                        LocalizedName = series.LocalizedName,
                        NormalizedName = series.NormalizedName,
                        OriginalName = series.OriginalName,
                        SeriesId = series.Id,
                        UserId = p.AppUserId,
                        NormalizedUserName = p.NormalizedUserName,
                        UserName = p.UserName,
                        IsSpecial = p.IsSpecial
                    })
                .AsNoTracking()
                .Where(thing => thing.PagesRead > 0)
                .ToListAsync();
        }
    }
}