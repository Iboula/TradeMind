using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeMind.ExecutionSessions.Infrastructure.Persistence.Entities;

namespace TradeMind.ExecutionSessions.Infrastructure.Persistence.Configurations;

public sealed class ExecutionSessionConfiguration : IEntityTypeConfiguration<ExecutionSessionEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionSessionEntity> entity)
    {
        entity.ToTable("execution_sessions");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        entity.Property(item => item.IdempotencyKeyHash).HasColumnName("idempotency_key_hash").HasMaxLength(128);
        entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
        entity.Property(item => item.UserId).HasColumnName("user_id").HasMaxLength(128);
        entity.Property(item => item.OrganizationId).HasColumnName("organization_id").HasMaxLength(128);
        entity.Property(item => item.CreatedByActorId).HasColumnName("created_by_actor_id").HasMaxLength(128);
        entity.Property(item => item.CreatedByActorType).HasColumnName("created_by_actor_type").HasMaxLength(32);
        entity.Property(item => item.Instrument).HasColumnName("instrument").HasMaxLength(128).IsRequired();
        entity.Property(item => item.Timeframe).HasColumnName("timeframe").HasMaxLength(32).IsRequired();
        entity.Property(item => item.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        entity.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        entity.Property(item => item.CompletedAtUtc).HasColumnName("completed_at_utc");
        entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        entity.Property(item => item.CurrentStage).HasColumnName("current_stage").HasMaxLength(32).IsRequired();
        entity.Property(item => item.SchemaVersion).HasColumnName("schema_version").IsRequired();
        entity.Property(item => item.CoreVersion).HasColumnName("core_version").HasMaxLength(64).IsRequired();
        entity.Property(item => item.ApiVersion).HasColumnName("api_version").HasMaxLength(64).IsRequired();
        entity.Property(item => item.TriggerType).HasColumnName("trigger_type").HasMaxLength(32).IsRequired();
        entity.Property(item => item.Source).HasColumnName("source").HasMaxLength(128).IsRequired();
        entity.Property(item => item.FailureCode).HasColumnName("failure_code").HasMaxLength(128);
        entity.Property(item => item.FailureMessage).HasColumnName("failure_message").HasMaxLength(2000);
        entity.Property(item => item.FailureDetails).HasColumnName("failure_details").HasMaxLength(4000);
        entity.Property(item => item.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb").IsRequired();
        entity.Property(item => item.ConcurrencyVersion).HasColumnName("concurrency_version").IsConcurrencyToken().IsRequired();
        entity.HasIndex(item => item.CorrelationId).HasDatabaseName("ix_execution_sessions_correlation_id");
        entity.HasIndex(item => new { item.Status, item.StartedAtUtc }).HasDatabaseName("ix_execution_sessions_status_started");
        entity.HasIndex(item => new { item.Instrument, item.StartedAtUtc }).HasDatabaseName("ix_execution_sessions_instrument_started");
        entity.HasIndex(item => item.CurrentStage).HasDatabaseName("ix_execution_sessions_current_stage");
        entity.HasIndex(item => item.IdempotencyKeyHash).HasDatabaseName("ix_execution_sessions_idempotency_hash").HasFilter("idempotency_key_hash IS NOT NULL");
        entity.HasIndex(item => new { item.OrganizationId, item.TenantId, item.StartedAtUtc }).HasDatabaseName("ix_execution_sessions_organization_tenant_started");
        entity.HasIndex(item => item.CreatedByActorId).HasDatabaseName("ix_execution_sessions_created_by_actor");
        entity.HasMany(item => item.Artifacts).WithOne().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(item => item.Timeline).WithOne().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ExecutionSessionArtifactConfiguration : IEntityTypeConfiguration<ExecutionSessionArtifactEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionSessionArtifactEntity> entity)
    {
        entity.ToTable("execution_session_artifacts");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.SessionId).HasColumnName("session_id");
        entity.Property(item => item.ArtifactType).HasColumnName("artifact_type").HasMaxLength(64).IsRequired();
        entity.Property(item => item.ArtifactId).HasColumnName("artifact_id").HasMaxLength(256).IsRequired();
        entity.Property(item => item.Stage).HasColumnName("stage").HasMaxLength(32).IsRequired();
        entity.Property(item => item.SchemaVersion).HasColumnName("schema_version").IsRequired();
        entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        entity.Property(item => item.ContentHash).HasColumnName("content_hash").HasMaxLength(128).IsRequired();
        entity.Property(item => item.StorageReference).HasColumnName("storage_reference").HasMaxLength(512);
        entity.Property(item => item.IsReplayable).HasColumnName("is_replayable").IsRequired();
        entity.Property(item => item.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb").IsRequired();
        entity.HasIndex(item => item.ArtifactId).HasDatabaseName("ix_execution_session_artifacts_artifact_id");
        entity.HasIndex(item => new { item.SessionId, item.ArtifactType, item.ArtifactId }).IsUnique().HasDatabaseName("ux_execution_session_artifacts_identity");
    }
}

public sealed class ExecutionSessionTimelineConfiguration : IEntityTypeConfiguration<ExecutionSessionTimelineEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionSessionTimelineEntity> entity)
    {
        entity.ToTable("execution_session_timeline");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.SessionId).HasColumnName("session_id");
        entity.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        entity.Property(item => item.Stage).HasColumnName("stage").HasMaxLength(32).IsRequired();
        entity.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        entity.Property(item => item.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb").IsRequired();
        entity.HasIndex(item => new { item.SessionId, item.OccurredAtUtc }).HasDatabaseName("ix_execution_session_timeline_session_occurred");
    }
}

public sealed class ExecutionSessionAuditConfiguration : IEntityTypeConfiguration<ExecutionSessionAuditEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionSessionAuditEntity> entity)
    {
        entity.ToTable("execution_session_audit");
        entity.HasKey(item => item.AuditId);
        entity.Property(item => item.AuditId).HasColumnName("audit_id");
        entity.Property(item => item.SessionId).HasColumnName("session_id");
        entity.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
        entity.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        entity.Property(item => item.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        entity.Property(item => item.ActorType).HasColumnName("actor_type").HasMaxLength(32).IsRequired();
        entity.Property(item => item.ActorId).HasColumnName("actor_id").HasMaxLength(128);
        entity.Property(item => item.PreviousStatus).HasColumnName("previous_status").HasMaxLength(32);
        entity.Property(item => item.NewStatus).HasColumnName("new_status").HasMaxLength(32);
        entity.Property(item => item.PreviousStage).HasColumnName("previous_stage").HasMaxLength(32);
        entity.Property(item => item.NewStage).HasColumnName("new_stage").HasMaxLength(32);
        entity.Property(item => item.ArtifactId).HasColumnName("artifact_id").HasMaxLength(256);
        entity.Property(item => item.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb").IsRequired();
        entity.Property(item => item.SchemaVersion).HasColumnName("schema_version").IsRequired();
        entity.HasIndex(item => new { item.SessionId, item.OccurredAtUtc }).HasDatabaseName("ix_execution_session_audit_session_occurred");
    }
}

public sealed class ExecutionSessionOutboxConfiguration : IEntityTypeConfiguration<ExecutionSessionOutboxEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionSessionOutboxEntity> entity)
    {
        entity.ToTable("execution_session_outbox");
        entity.HasKey(item => item.OutboxMessageId);
        entity.Property(item => item.OutboxMessageId).HasColumnName("outbox_message_id");
        entity.Property(item => item.SessionId).HasColumnName("session_id");
        entity.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        entity.Property(item => item.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        entity.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        entity.Property(item => item.ProcessedAtUtc).HasColumnName("processed_at_utc");
        entity.Property(item => item.AttemptCount).HasColumnName("attempt_count").IsRequired();
        entity.Property(item => item.LastError).HasColumnName("last_error").HasMaxLength(4000);
        entity.Property(item => item.ConcurrencyVersion).HasColumnName("concurrency_version").IsRequired();
        entity.HasIndex(item => new { item.ProcessedAtUtc, item.OccurredAtUtc }).HasDatabaseName("ix_execution_session_outbox_pending");
    }
}

public sealed class DurableIdempotencyConfiguration : IEntityTypeConfiguration<DurableIdempotencyRecordEntity>
{
    public void Configure(EntityTypeBuilder<DurableIdempotencyRecordEntity> entity)
    {
        entity.ToTable("execution_idempotency_records");
        entity.HasKey(item => item.IdempotencyKey);
        entity.Property(item => item.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128);
        entity.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(128).IsRequired();
        entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        entity.Property(item => item.ExecutionSessionId).HasColumnName("execution_session_id");
        entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        entity.Property(item => item.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        entity.Property(item => item.ResponseStatusCode).HasColumnName("response_status_code");
        entity.Property(item => item.ResponseContentType).HasColumnName("response_content_type").HasMaxLength(256);
        entity.Property(item => item.ResponseBody).HasColumnName("response_body").HasColumnType("bytea");
        entity.Property(item => item.ConcurrencyVersion).HasColumnName("concurrency_version").IsConcurrencyToken().IsRequired();
        entity.HasIndex(item => item.ExpiresAtUtc).HasDatabaseName("ix_execution_idempotency_expires");
    }
}
