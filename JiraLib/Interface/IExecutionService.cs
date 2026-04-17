using JiraLib.ViewModels;

namespace JiraLib.Interface
{
    public interface IExecutionService
    {
        // Dashboard
        Task<IEnumerable<object>> GetExecutionsAsync(string? search, string? sortBy);

        // Create
        Task<object> CreateExecutionAsync(CreateExecutionViewModel model);

        // Delete
        Task DeleteExecutionByGuidAsync(Guid executionId);

        // Details page
        Task<object?> GetExecutionDetailsByGuidAsync(Guid executionId);

        // Sub execution (Details page accordion)
        Task<object> AddSubExecutionAsync(AddSubExecutionViewModel model);

        // Save execution card fields
        Task UpdateExecutionByGuidAsync(Guid executionId, UpdateExecutionViewModel model);

        // Inline status change (dashboard table dropdown)
        Task UpdateStatusByGuidAsync(Guid executionId, string statusCode, Guid changedByUserId);
    }
}