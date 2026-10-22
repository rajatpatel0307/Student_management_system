using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;
using System.Text.Json;

namespace Student_Management_System.Services;

public interface IApprovalWorkflowService
{
    Task<ApprovalRequest> RequestAsync(
        string workflowType,
        string entityType,
        string entityId,
        string reason,
        string requestedByUserId,
        IReadOnlyList<string> approverRoles,
        DateTime? dueUtc = null,
        CancellationToken cancellationToken = default);

    Task<ApprovalRequest> ActAsync(
        long requestId,
        ApprovalStatus decision,
        string? comment,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);
}

public sealed class ApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly ApplicationDbContext _db;

    public ApprovalWorkflowService(ApplicationDbContext db) => _db = db;

    public async Task<ApprovalRequest> RequestAsync(
        string workflowType,
        string entityType,
        string entityId,
        string reason,
        string requestedByUserId,
        IReadOnlyList<string> approverRoles,
        DateTime? dueUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (approverRoles.Count == 0)
            throw new InvalidOperationException("At least one approval step is required.");
        if (await _db.ApprovalRequests.AnyAsync(r =>
                r.WorkflowType == workflowType &&
                r.EntityType == entityType &&
                r.EntityId == entityId &&
                r.Status == ApprovalStatus.Pending, cancellationToken))
            throw new InvalidOperationException("A pending approval request already exists.");

        var request = new ApprovalRequest
        {
            WorkflowType = workflowType.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId.Trim(),
            Reason = reason.Trim(),
            RequestedByUserId = requestedByUserId,
            DueUtc = dueUtc,
            Steps = approverRoles.Select((role, index) => new ApprovalStep
            {
                Sequence = index + 1,
                ApproverRole = role,
                DueUtc = dueUtc
            }).ToList()
        };
        _db.ApprovalRequests.Add(request);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = requestedByUserId,
            Action = $"Requested {workflowType} approval",
            EntityName = entityType,
            EntityId = entityId
        });
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "ApprovalRequested",
            PayloadJson = JsonSerializer.Serialize(new
            {
                workflowType,
                entityType,
                entityId,
                ApproverRole = approverRoles[0]
            })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<ApprovalRequest> ActAsync(
        long requestId,
        ApprovalStatus decision,
        string? comment,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, cancellationToken);
                var result = await ActCoreAsync(
                    requestId, decision, comment, actor, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Another approver completed this step. Refresh to see the current decision.");
        }
    }

    private async Task<ApprovalRequest> ActCoreAsync(
        long requestId,
        ApprovalStatus decision,
        string? comment,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        if (decision is not (ApprovalStatus.Approved or ApprovalStatus.Rejected))
            throw new InvalidOperationException("Select approve or reject.");
        var actorUserId = actor.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        var request = await _db.ApprovalRequests
            .Include(r => r.Steps)
            .SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Approval request not found.");
        if (request.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("This request is no longer pending.");
        var step = request.Steps
            .Where(s => s.Status == ApprovalStatus.Pending)
            .OrderBy(s => s.Sequence)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No pending approval step exists.");
        var delegated = step.DelegatedToUserId == actorUserId;
        var assigned = step.ApproverUserId == actorUserId;
        if (!actor.IsInRole("SuperAdmin") &&
            !actor.IsInRole(step.ApproverRole) &&
            !delegated &&
            !assigned)
            throw new UnauthorizedAccessException();

        step.Status = decision;
        step.ActedByUserId = actorUserId;
        step.ActedUtc = DateTime.UtcNow;
        step.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (decision == ApprovalStatus.Rejected)
        {
            request.Status = ApprovalStatus.Rejected;
            request.CompletedUtc = DateTime.UtcNow;
        }
        else if (request.Steps.All(s =>
                     s.Id == step.Id || s.Status == ApprovalStatus.Approved))
        {
            request.Status = ApprovalStatus.Approved;
            request.CompletedUtc = DateTime.UtcNow;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = $"{decision} {request.WorkflowType} approval step {step.Sequence}",
            EntityName = request.EntityType,
            EntityId = request.EntityId
        });
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "ApprovalActed",
            PayloadJson = JsonSerializer.Serialize(new
            {
                request.Id,
                request.WorkflowType,
                request.EntityType,
                request.EntityId,
                Decision = decision.ToString()
            })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return request;
    }
}
