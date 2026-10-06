namespace Fatoura.Api.Data.Entities;

/// <summary>Marker for entities whose changes are recorded in the audit log.</summary>
public interface IAudited;

public sealed class AuditLog
{
    public long Id { get; set; }

    public DateTimeOffset At { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    /// <summary>JSON object of changed properties: { "Prop": { "old": ..., "new": ... } }.</summary>
    public string Changes { get; set; } = string.Empty;
}
