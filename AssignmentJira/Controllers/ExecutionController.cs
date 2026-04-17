using Microsoft.AspNetCore.Mvc;
using JiraLib.Interface;
using JiraLib.Models;
using JiraLib.ViewModels;

namespace AssignmentJira.Controllers
{
    public class ExecutionController : Controller
    {
        private readonly IExecutionService _executionService;

        public ExecutionController(IExecutionService executionService)
        {
            _executionService = executionService;
        }

        // GET: /Execution/Dashboard
        public IActionResult Dashboard() => View();

        // GET: /Execution/Details/{id}
        public IActionResult Details(Guid id) => View(id);

        // GET: /Execution/GetExecutions?search=&sortBy=
        [HttpGet]
        public async Task<IActionResult> GetExecutions(string? search, string? sortBy)
        {
            try
            {
                var result = await _executionService.GetExecutionsAsync(search, sortBy);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // POST: /Execution/CreateExecution
        [HttpPost]
        public async Task<IActionResult> CreateExecution([FromBody] CreateExecutionViewModel model)
        {
            try
            {
                if (!ModelState.IsValid) return Json(new { success = false, message = "Invalid data." });
                var result = await _executionService.CreateExecutionAsync(model);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // DELETE: /Execution/DeleteExecution/{id}
        [HttpDelete]
        public async Task<IActionResult> DeleteExecution(Guid id)
        {
            try
            {
                await _executionService.DeleteExecutionByGuidAsync(id);
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // GET: /Execution/GetExecutionDetails/{id}
        [HttpGet]
        public async Task<IActionResult> GetExecutionDetails(Guid id)
        {
            try
            {
                var result = await _executionService.GetExecutionDetailsByGuidAsync(id);
                if (result == null) return Json(new { success = false, message = "Execution not found." });
                return Json(new { success = true, data = result });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // POST: /Execution/AddSubExecution
        [HttpPost]
        public async Task<IActionResult> AddSubExecution([FromBody] AddSubExecutionViewModel model)
        {
            try
            {
                if (!ModelState.IsValid) return Json(new { success = false, message = "Invalid data." });
                var result = await _executionService.AddSubExecutionAsync(model);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // PUT: /Execution/UpdateExecution/{id}
        [HttpPut]
        public async Task<IActionResult> UpdateExecution(Guid id, [FromBody] UpdateExecutionViewModel model)
        {
            try
            {
                if (!ModelState.IsValid) return Json(new { success = false, message = "Invalid data." });
                await _executionService.UpdateExecutionByGuidAsync(id, model);
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // POST: /Execution/UpdateStatus
        [HttpPost]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusViewModel model)
        {
            try
            {
                if (!ModelState.IsValid) return Json(new { success = false, message = "Invalid data." });
                await _executionService.UpdateStatusByGuidAsync(model.ExecutionId, model.StatusCode, model.ChangedByUserId);
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
    }
}