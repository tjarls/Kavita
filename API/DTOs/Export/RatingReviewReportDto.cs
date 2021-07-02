namespace API.DTOs.Export
{
    public class RatingReviewReportDto
    {
        public int SeriesId { get; set; }
        public string Name { get; set; }
        public string LocalizedName { get; set; }
        public string OriginalName { get; set; }
        public string NormalizedName { get; set; }
        public int VolumeNumber { get; set; }
        public string ChapterRange { get; set; }
        public int UserId { get; set; }
        public int PagesRead { get; set; }
        public string NormalizedUserName { get; set; }
        public string UserName { get; set; }
        public bool IsSpecial { get; set; }
        public int UserRating { get; set; }
        public string UserReview { get; set; }
    }
}