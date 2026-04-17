using JiraLib.Models;
using JiraLib.Interface;
using JiraLib.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace JiraLib.Implementation
{
    public class ExecutionService : IExecutionService
    {
        private readonly DbContextOptions<TestBridgeDBContext> _db;

        public ExecutionService(string conn)
        {
            // Initialize the DbContextOptions with the provided connection string
            _db = new DbContextOptionsBuilder<TestBridgeDBContext>()
                .UseSqlServer(conn)
                .Options;
        }

        // ── GET ALL EXECUTIONS (Dashboard table) ──────────────────────────────
        public async Task<IEnumerable<object>> GetExecutionsAsync(string? search, string? sortBy)
        {
            using var context = new TestBridgeDBContext(_db);
            var query = context.executions
                .Include(e => e.ticket)
                .Include(e => e.profile)
                    .ThenInclude(p => p.device)
                .Include(e => e.profile)
                    .ThenInclude(p => p.region)
                .Include(e => e.status)
                .Include(e => e.executed_byNavigation)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(e =>
                    e.ticket.jira_key.ToLower().Contains(search) ||
                    e.profile.device.device_name.ToLower().Contains(search) ||
                    e.status.status_label.ToLower().Contains(search) ||
                    e.executed_byNavigation.username.ToLower().Contains(search)
                );
            }

            // Sort
            query = sortBy switch
            {
                "oldest" => query.OrderBy(e => e.created_at),
                "ticket" => query.OrderBy(e => e.ticket.jira_key),
                "status" => query.OrderBy(e => e.status.display_order),
                _ => query.OrderByDescending(e => e.created_at) // "newest" default
            };

            var list = await query.Select(e => new
            {
                executionId = e.execution_id,
                ticketNo = e.ticket.jira_key,
                ticketSummary = e.ticket.summary,
                ticketType = e.ticket.ticket_type,
                deviceName = e.profile != null ? e.profile.device.device_name : null,
                deviceType = e.profile != null ? e.profile.device.device_type : null,
                regionCode = e.profile != null && e.profile.region != null ? e.profile.region.region_code : null,
                profileName = e.profile != null ? e.profile.profile_name : null,
                statusId = e.status_id,
                statusCode = e.status.status_code,
                statusLabel = e.status.status_label,
                username = e.executed_byNavigation.username,
                fullName = e.executed_byNavigation.full_name,
                executionDate = e.execution_date,
                createdAt = e.created_at,
                isRegression = e.is_regression,
                testCycle = e.test_cycle
            }).ToListAsync();

            return list;
        }

        // ── CREATE EXECUTION ──────────────────────────────────────────────────
        public async Task<object> CreateExecutionAsync(CreateExecutionViewModel model)
        {
            // Resolve ticket by jira_key
            using var context = new TestBridgeDBContext(_db);
            var ticket = await context.jira_tickets
                .FirstOrDefaultAsync(t => t.jira_key == model.TicketNo.Trim());

            if (ticket == null)
                throw new Exception($"Ticket '{model.TicketNo}' not found in Jira tickets.");

            // Resolve user by username
            var user = await context.users
                .FirstOrDefaultAsync(u => u.username == model.Username);

            if (user == null)
                throw new Exception($"User '{model.Username}' not found.");

            // Resolve default status (first active status by display_order)
            var defaultStatus = await context.execution_status_masters
                .Where(s => s.is_active)
                .OrderBy(s => s.display_order)
                .FirstOrDefaultAsync();

            if (defaultStatus == null)
                throw new Exception("No active execution status found.");

            // Resolve or create execution_profile
            Guid? profileId = null;

            if (!string.IsNullOrWhiteSpace(model.Device) || !string.IsNullOrWhiteSpace(model.Region))
            {
                var device = await context.device_masters
                    .FirstOrDefaultAsync(d => d.device_name == model.Device && d.is_active);

                var region = await context.region_masters
                    .FirstOrDefaultAsync(r => r.region_code == model.Region && r.is_active);

                // Look for an existing matching profile
                var existingProfile = await context.execution_profiles
                    .FirstOrDefaultAsync(p =>
                        p.device_id == (device != null ? device.device_id : (Guid?)null) &&
                        p.region_id == (region != null ? region.region_id : (Guid?)null) &&
                        p.created_by == user.user_id &&
                        p.is_active);

                if (existingProfile != null)
                {
                    profileId = existingProfile.profile_id;
                }
                else
                {
                    var newProfile = new execution_profile
                    {
                        profile_id = Guid.NewGuid(),
                        profile_name = $"{model.Device} / {model.Region}",
                        device_id = device?.device_id,
                        region_id = region?.region_id,
                        environment = "QA",
                        created_by = user.user_id,
                        is_active = true,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    context.execution_profiles.Add(newProfile);
                    profileId = newProfile.profile_id;
                }
            }

            var execution = new execution
            {
                execution_id = Guid.NewGuid(),
                ticket_id = ticket.ticket_id,
                profile_id = profileId,
                executed_by = user.user_id,
                status_id = defaultStatus.status_id,
                execution_date = DateTime.UtcNow,
                is_regression = model.ExecutionType?.ToLower() == "regression",
                test_cycle = model.ExecutionType,
                notes = null,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            context.executions.Add(execution);

            // Write initial status history
            var history = new execution_status_history
            {
                history_id = Guid.NewGuid(),
                execution_id = execution.execution_id,
                from_status_id = null,
                to_status_id = defaultStatus.status_id,
                changed_by = user.user_id,
                change_reason = "Execution created.",
                changed_at = DateTime.UtcNow,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };
            context.execution_status_histories.Add(history);

            await context.SaveChangesAsync();

            return new
            {
                executionId = execution.execution_id,
                ticketNo = ticket.jira_key,
                statusLabel = defaultStatus.status_label,
                createdAt = execution.created_at
            };
        }

        // ── DELETE EXECUTION ──────────────────────────────────────────────────
        public Task DeleteExecutionAsync(int id)
        {
            // Note: execution_id is Guid — controller should pass Guid.
            // This overload accepts int for interface compatibility; see note below.
            throw new NotSupportedException("Use DeleteExecutionByGuidAsync. Execution IDs are Guid.");
        }

        // Correct implementation used internally and by controller (cast to Guid in controller)
        public async Task DeleteExecutionByGuidAsync(Guid executionId)
        {
            using var context = new TestBridgeDBContext(_db);
            var execution = await context.executions
                .Include(e => e.execution_status_histories)
                .Include(e => e.observations)
                    .ThenInclude(o => o.observation_tags)
                .Include(e => e.observations)
                    .ThenInclude(o => o.attachments)
                .Include(e => e.attachments)
                .FirstOrDefaultAsync(e => e.execution_id == executionId);

            if (execution == null)
                throw new Exception("Execution not found.");

            // Remove child records first
            foreach (var obs in execution.observations)
            {
                context.observation_tags.RemoveRange(obs.observation_tags);
                context.attachments.RemoveRange(obs.attachments);
            }
            context.observations.RemoveRange(execution.observations);
            context.attachments.RemoveRange(execution.attachments);
            context.execution_status_histories.RemoveRange(execution.execution_status_histories);
            context.executions.Remove(execution);

            await context.SaveChangesAsync();
        }

        // ── GET EXECUTION DETAILS (Details page) ──────────────────────────────
        public Task<object?> GetExecutionDetailsAsync(int id)
        {
            throw new NotSupportedException("Use GetExecutionDetailsByGuidAsync. Execution IDs are Guid.");
        }

        public async Task<object?> GetExecutionDetailsByGuidAsync(Guid executionId)
        {
            using var context = new TestBridgeDBContext(_db);
            var execution = await context.executions
                .Include(e => e.ticket)
                .Include(e => e.profile)
                    .ThenInclude(p => p.device)
                .Include(e => e.profile)
                    .ThenInclude(p => p.region)
                .Include(e => e.status)
                .Include(e => e.executed_byNavigation)
                .Include(e => e.observations)
                    .ThenInclude(o => o.obs_type)
                .Include(e => e.observations)
                    .ThenInclude(o => o.observation_tags)
                .Include(e => e.execution_status_histories)
                    .ThenInclude(h => h.to_status)
                .FirstOrDefaultAsync(e => e.execution_id == executionId);

            if (execution == null) return null;

            return new
            {
                executionId = execution.execution_id,
                ticketNo = execution.ticket.jira_key,
                ticketSummary = execution.ticket.summary,
                ticketType = execution.ticket.ticket_type,
                deviceName = execution.profile?.device?.device_name,
                deviceType = execution.profile?.device?.device_type,
                regionCode = execution.profile?.region?.region_code,
                profileName = execution.profile?.profile_name,
                environment = execution.profile?.environment,
                buildVersion = execution.profile?.build_version,
                statusCode = execution.status.status_code,
                statusLabel = execution.status.status_label,
                username = execution.executed_byNavigation.username,
                fullName = execution.executed_byNavigation.full_name,
                startedAt = execution.started_at,
                completedAt = execution.completed_at,
                executionDate = execution.execution_date,
                testCycle = execution.test_cycle,
                notes = execution.notes,
                isRegression = execution.is_regression,
                observations = execution.observations.Select(o => new
                {
                    observationId = o.observation_id,
                    title = o.title,
                    description = o.description,
                    severity = o.severity,
                    obsTypeCode = o.obs_type.type_code,
                    obsTypeLabel = o.obs_type.type_label,
                    stepsToRepro = o.steps_to_repro,
                    expectedResult = o.expected_result,
                    actualResult = o.actual_result,
                    linkedJiraKey = o.linked_jira_key,
                    isResolved = o.is_resolved,
                    resolvedAt = o.resolved_at,
                    tags = o.observation_tags.Select(t => t.tag_value),
                    createdAt = o.created_at
                }),
                statusHistory = execution.execution_status_histories
                    .OrderByDescending(h => h.changed_at)
                    .Select(h => new
                    {
                        toStatus = h.to_status.status_label,
                        changedAt = h.changed_at,
                        reason = h.change_reason
                    })
            };
        }

        // ── ADD SUB EXECUTION ─────────────────────────────────────────────────
        // The DB schema has one execution per ticket+profile. "Sub executions"
        // in the UI map to new execution rows linked to the same ticket,
        // plus an observation row for the on-screen data captured.
        public async Task<object> AddSubExecutionAsync(AddSubExecutionViewModel model)
        {
            using var context = new TestBridgeDBContext(_db);
            // Load parent to inherit ticket + profile
            var parent = await context.executions
                .Include(e => e.ticket)
                .Include(e => e.status)
                .Include(e => e.executed_byNavigation)
                .FirstOrDefaultAsync(e => e.execution_id == model.ParentExecutionId_Guid);

            if (parent == null)
                throw new Exception("Parent execution not found.");

            var defaultStatus = await context.execution_status_masters
                .Where(s => s.is_active)
                .OrderBy(s => s.display_order)
                .FirstOrDefaultAsync()
                ?? throw new Exception("No active execution status found.");

            var sub = new execution
            {
                execution_id = Guid.NewGuid(),
                ticket_id = parent.ticket_id,
                profile_id = parent.profile_id,
                executed_by = parent.executed_by,
                status_id = defaultStatus.status_id,
                execution_date = DateTime.UtcNow,
                started_at = model.StartTime,
                completed_at = model.EndTime,
                duration_seconds = (model.StartTime.HasValue && model.EndTime.HasValue)
                                    ? (int?)(model.EndTime.Value - model.StartTime.Value).TotalSeconds
                                    : null,
                test_cycle = parent.test_cycle,
                notes = model.DetailedObservations,
                is_regression = parent.is_regression,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            context.executions.Add(sub);

            // Status history for sub
            var history = new execution_status_history
            {
                history_id = Guid.NewGuid(),
                execution_id = sub.execution_id,
                from_status_id = null,
                to_status_id = defaultStatus.status_id,
                changed_by = parent.executed_by,
                change_reason = "Sub execution created.",
                changed_at = DateTime.UtcNow,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };
            context.execution_status_histories.Add(history);

            // Store profile_login, tv_provider, asset_watched as an observation
            // using a general obs_type (first active one)
            var obsType = await context.observation_type_masters
                .Where(o => o.is_active)
                .FirstOrDefaultAsync();

            if (obsType != null && (
                !string.IsNullOrWhiteSpace(model.ProfileLogin) ||
                !string.IsNullOrWhiteSpace(model.TvProvider) ||
                !string.IsNullOrWhiteSpace(model.AssetWatched) ||
                !string.IsNullOrWhiteSpace(model.DetailedObservations)))
            {
                var obs = new observation
                {
                    observation_id = Guid.NewGuid(),
                    execution_id = sub.execution_id,
                    obs_type_id = obsType.obs_type_id,
                    reported_by = parent.executed_by,
                    title = $"Profile: {model.ProfileLogin} | Asset: {model.AssetWatched}",
                    description = model.DetailedObservations,
                    severity = obsType.severity_default ?? "Medium",
                    steps_to_repro = $"TV Provider: {model.TvProvider}",
                    expected_result = null,
                    actual_result = null,
                    linked_jira_key = null,
                    is_resolved = false,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };
                context.observations.Add(obs);

                // Store preroll / midroll as tags
                if (!string.IsNullOrWhiteSpace(model.PrerollAd))
                {
                    context.observation_tags.Add(new observation_tag
                    {
                        tag_id = Guid.NewGuid(),
                        observation_id = obs.observation_id,
                        tag_value = $"preroll:{model.PrerollAd}",
                        created_by = parent.executed_by,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    });
                }

                if (model.MidrollAdBreaks.HasValue)
                {
                    context.observation_tags.Add(new observation_tag
                    {
                        tag_id = Guid.NewGuid(),
                        observation_id = obs.observation_id,
                        tag_value = $"midroll_breaks:{model.MidrollAdBreaks.Value}",
                        created_by = parent.executed_by,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    });
                }
            }

            await context.SaveChangesAsync();

            return new
            {
                subExecutionId = sub.execution_id,
                createdAt = sub.created_at
            };
        }

        // ── UPDATE EXECUTION ──────────────────────────────────────────────────
        public  Task UpdateExecutionAsync(int id, UpdateExecutionViewModel model)
        {
            throw new NotSupportedException("Use UpdateExecutionByGuidAsync. Execution IDs are Guid.");
        }

        public async Task UpdateExecutionByGuidAsync(Guid executionId, UpdateExecutionViewModel model)
        {
            using var context = new TestBridgeDBContext(_db);
            var execution = await context.executions
                .Include(e => e.observations)
                    .ThenInclude(o => o.observation_tags)
                .FirstOrDefaultAsync(e => e.execution_id == executionId)
                ?? throw new Exception("Execution not found.");

            // Update time fields
            if (model.StartTime.HasValue) execution.started_at = model.StartTime;
            if (model.EndTime.HasValue) execution.completed_at = model.EndTime;

            if (model.StartTime.HasValue && model.EndTime.HasValue)
                execution.duration_seconds = (int)(model.EndTime.Value - model.StartTime.Value).TotalSeconds;

            if (model.DetailedObservations != null)
                execution.notes = model.DetailedObservations;

            execution.updated_at = DateTime.UtcNow;

            // Update the linked observation (first one — created by AddSubExecution)
            var obs = execution.observations.FirstOrDefault();
            if (obs != null)
            {
                if (model.ProfileLogin != null || model.AssetWatched != null)
                    obs.title = $"Profile: {model.ProfileLogin} | Asset: {model.AssetWatched}";

                if (model.TvProvider != null)
                    obs.steps_to_repro = $"TV Provider: {model.TvProvider}";

                if (model.DetailedObservations != null)
                    obs.description = model.DetailedObservations;

                obs.updated_at = DateTime.UtcNow;

                // Refresh preroll tag
                if (model.PrerollAd != null)
                {
                    var prerollTag = obs.observation_tags
                        .FirstOrDefault(t => t.tag_value.StartsWith("preroll:"));
                    if (prerollTag != null)
                        prerollTag.tag_value = $"preroll:{model.PrerollAd}";
                }

                // Refresh midroll tag
                if (model.MidrollAdBreaks.HasValue)
                {
                    var midrollTag = obs.observation_tags
                        .FirstOrDefault(t => t.tag_value.StartsWith("midroll_breaks:"));
                    if (midrollTag != null)
                        midrollTag.tag_value = $"midroll_breaks:{model.MidrollAdBreaks.Value}";
                }
            }

            await context.SaveChangesAsync();
        }

        // ── UPDATE STATUS ─────────────────────────────────────────────────────
        public Task UpdateStatusAsync(int executionId, string status)
        {
            throw new NotSupportedException("Use UpdateStatusByGuidAsync. Execution IDs are Guid.");
        }

        public async Task UpdateStatusByGuidAsync(Guid executionId, string statusCode, Guid changedByUserId)
        {
            using var context = new TestBridgeDBContext(_db);
            var execution = await context.executions
                .FirstOrDefaultAsync(e => e.execution_id == executionId)
                ?? throw new Exception("Execution not found.");

            var newStatus = await context.execution_status_masters
                .FirstOrDefaultAsync(s => s.status_code == statusCode && s.is_active)
                ?? throw new Exception($"Status '{statusCode}' not found.");

            var history = new execution_status_history
            {
                history_id = Guid.NewGuid(),
                execution_id = executionId,
                from_status_id = execution.status_id,
                to_status_id = newStatus.status_id,
                changed_by = changedByUserId,
                change_reason = "Status updated by user.",
                changed_at = DateTime.UtcNow,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            execution.status_id = newStatus.status_id;
            execution.updated_at = DateTime.UtcNow;

            context.execution_status_histories.Add(history);
            await context.SaveChangesAsync();
        }
    }
}