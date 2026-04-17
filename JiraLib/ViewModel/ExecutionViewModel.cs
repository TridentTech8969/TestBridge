using System.ComponentModel.DataAnnotations;

namespace JiraLib.ViewModels
{
    public class CreateExecutionViewModel
    {
        [Required]
        public string TicketNo { get; set; } = string.Empty;
        public string? Device { get; set; }
        public string? ExecutionType { get; set; }
        public string? Region { get; set; }
        public string? Username { get; set; }
    }

    public class AddSubExecutionViewModel
    {
        [Required]
        public Guid ParentExecutionId_Guid { get; set; }
        public string? ProfileLogin { get; set; }
        public string? TvProvider { get; set; }
        public string? AssetWatched { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? PrerollAd { get; set; }
        public int? MidrollAdBreaks { get; set; }
        public string? DetailedObservations { get; set; }
    }

    public class UpdateExecutionViewModel
    {
        public string? ProfileLogin { get; set; }
        public string? TvProvider { get; set; }
        public string? AssetWatched { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? PrerollAd { get; set; }
        public int? MidrollAdBreaks { get; set; }
        public string? DetailedObservations { get; set; }
    }

    public class UpdateStatusViewModel
    {
        [Required]
        public Guid ExecutionId { get; set; }
        [Required]
        public string StatusCode { get; set; } = string.Empty;
        [Required]
        public Guid ChangedByUserId { get; set; }
    }
}