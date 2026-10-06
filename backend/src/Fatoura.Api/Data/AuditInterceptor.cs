using System.Text.Json;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fatoura.Api.Data;

/// <summary>
/// Writes an <see cref="AuditLog"/> row for every added, modified or deleted <see cref="IAudited"/> entity,
/// with a JSON diff of scalar properties (binary columns are summarised, not copied).
/// Rows are written after the main save so generated keys are known; both happen in the caller's transaction when one is open.
/// </summary>
internal sealed class AuditInterceptor(ICurrentUser currentUser, TimeProvider time) : SaveChangesInterceptor
{
    private List<PendingAudit>? _pending;
    private bool _writingAudit;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (Flush(eventData.Context))
        {
            eventData.Context!.SaveChanges();
            _writingAudit = false;
        }

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Flush(eventData.Context))
        {
            await eventData.Context!.SaveChangesAsync(cancellationToken);
            _writingAudit = false;
        }

        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending = null;

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = null;
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is null || _writingAudit)
        {
            return;
        }

        _pending = [];
        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.Entity is IAudited))
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var changes = Diff(entry);
            if (entry.State == EntityState.Modified && changes.Count == 0)
            {
                continue;
            }

            _pending.Add(new PendingAudit(entry, entry.State.ToString(), changes));
        }
    }

    private bool Flush(DbContext? context)
    {
        if (context is null || _writingAudit || _pending is not { Count: > 0 })
        {
            _pending = null;
            return false;
        }

        var now = time.GetUtcNow();
        foreach (var p in _pending)
        {
            var key = p.Entry.Metadata.FindPrimaryKey()!.Properties
                .Select(prop => p.Entry.Property(prop.Name).CurrentValue?.ToString() ?? string.Empty);
            context.Set<AuditLog>().Add(new AuditLog
            {
                At = now,
                UserId = currentUser.Id,
                UserName = currentUser.Name,
                Action = p.Action,
                EntityType = p.Entry.Metadata.ClrType.Name,
                EntityId = string.Join(",", key),
                Changes = JsonSerializer.Serialize(p.Changes),
            });
        }

        _pending = null;
        _writingAudit = true;
        return true;
    }

    private static Dictionary<string, object?> Diff(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            object? Summarise(object? v) => v is byte[] bytes ? $"<{bytes.Length} bytes>" : v;

            switch (entry.State)
            {
                case EntityState.Added:
                    changes[name] = new { @new = Summarise(prop.CurrentValue) };
                    break;
                case EntityState.Deleted:
                    changes[name] = new { old = Summarise(prop.OriginalValue) };
                    break;
                case EntityState.Modified when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue)
                    && !(prop.OriginalValue is byte[] a && prop.CurrentValue is byte[] c && a.AsSpan().SequenceEqual(c)):
                    changes[name] = new { old = Summarise(prop.OriginalValue), @new = Summarise(prop.CurrentValue) };
                    break;
            }
        }

        return changes;
    }

    private sealed record PendingAudit(EntityEntry Entry, string Action, Dictionary<string, object?> Changes);
}
