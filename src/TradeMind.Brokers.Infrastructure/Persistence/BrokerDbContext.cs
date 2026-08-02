using Microsoft.EntityFrameworkCore;
using TradeMind.Brokers.Infrastructure.Persistence.Entities;

namespace TradeMind.Brokers.Infrastructure.Persistence;

public sealed class BrokerDbContext(DbContextOptions<BrokerDbContext> options) : DbContext(options)
{
    public DbSet<BrokerExecutionEntity> Executions => Set<BrokerExecutionEntity>();
    public DbSet<BrokerOrderEntity> Orders => Set<BrokerOrderEntity>();
    public DbSet<BrokerPositionEntity> Positions => Set<BrokerPositionEntity>();
    public DbSet<BrokerFillEntity> Fills => Set<BrokerFillEntity>();
    public DbSet<BrokerIdempotencyEntity> Idempotency => Set<BrokerIdempotencyEntity>();
    public DbSet<BrokerAuditEntity> Audit => Set<BrokerAuditEntity>();
    public DbSet<BrokerReconciliationEntity> Reconciliations => Set<BrokerReconciliationEntity>();
    public DbSet<BrokerReconciliationMismatchEntity> ReconciliationMismatches => Set<BrokerReconciliationMismatchEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BrokerExecutionEntity>(entity =>
        {
            entity.ToTable("broker_executions");
            entity.HasKey(item => item.Id).HasName("pk_broker_executions");
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(item => item.ConnectorId).HasColumnName("connector_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.AccountId).HasColumnName("account_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
            entity.Property(item => item.ExecutionSessionId).HasColumnName("execution_session_id").HasMaxLength(128);
            entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(item => item.ErrorCode).HasColumnName("error_code").HasMaxLength(128);
            entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.HasIndex(item => new { item.TenantId, item.AccountId, item.CreatedAtUtc }).HasDatabaseName("ix_broker_executions_tenant_account_created");
            entity.HasIndex(item => item.ExecutionSessionId).HasDatabaseName("ix_broker_executions_execution_session");
        });
        modelBuilder.Entity<BrokerOrderEntity>(entity =>
        {
            entity.ToTable("broker_orders");
            entity.HasKey(item => item.Id).HasName("pk_broker_orders");
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(item => item.ExecutionId).HasColumnName("execution_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.ConnectorId).HasColumnName("connector_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.AccountId).HasColumnName("account_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
            entity.Property(item => item.ClientOrderId).HasColumnName("client_order_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Instrument).HasColumnName("instrument").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(item => item.Quantity).HasColumnName("quantity");
            entity.Property(item => item.FilledQuantity).HasColumnName("filled_quantity");
            entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc");
            entity.Property(item => item.ConcurrencyVersion).HasColumnName("concurrency_version").IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.AccountId, item.ClientOrderId }).IsUnique().HasDatabaseName("ux_broker_orders_tenant_account_client_order");
            entity.HasIndex(item => item.ExecutionId).HasDatabaseName("ix_broker_orders_execution");
        });
        modelBuilder.Entity<BrokerPositionEntity>(entity =>
        {
            entity.ToTable("broker_positions");
            entity.HasKey(item => item.Id).HasName("pk_broker_positions");
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(item => item.ConnectorId).HasColumnName("connector_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.AccountId).HasColumnName("account_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
            entity.Property(item => item.Instrument).HasColumnName("instrument").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Quantity).HasColumnName("quantity");
            entity.Property(item => item.AveragePrice).HasColumnName("average_price");
            entity.Property(item => item.OpenedAtUtc).HasColumnName("opened_at_utc");
            entity.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc");
            entity.Property(item => item.ConcurrencyVersion).HasColumnName("concurrency_version").IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.AccountId, item.Instrument }).HasDatabaseName("ix_broker_positions_tenant_account_instrument");
        });
        modelBuilder.Entity<BrokerFillEntity>(entity =>
        {
            entity.ToTable("broker_fills");
            entity.HasKey(item => item.Id).HasName("pk_broker_fills");
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(item => item.ExecutionId).HasColumnName("execution_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.OrderId).HasColumnName("order_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.PositionId).HasColumnName("position_id").HasMaxLength(128);
            entity.Property(item => item.ConnectorId).HasColumnName("connector_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.AccountId).HasColumnName("account_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
            entity.Property(item => item.Quantity).HasColumnName("quantity");
            entity.Property(item => item.Price).HasColumnName("price");
            entity.Property(item => item.FilledAtUtc).HasColumnName("filled_at_utc");
            entity.HasIndex(item => new { item.TenantId, item.AccountId, item.FilledAtUtc }).HasDatabaseName("ix_broker_fills_tenant_account_filled");
            entity.HasIndex(item => item.ExecutionId).IsUnique().HasDatabaseName("ux_broker_fills_execution");
        });
        modelBuilder.Entity<BrokerIdempotencyEntity>(entity =>
        {
            entity.ToTable("broker_idempotency");
            entity.HasKey(item => item.KeyHash).HasName("pk_broker_idempotency");
            entity.Property(item => item.KeyHash).HasColumnName("key_hash").HasMaxLength(128);
            entity.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(item => item.ResultJson).HasColumnName("result_json").HasColumnType("jsonb");
            entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(item => item.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(item => item.ConcurrencyVersion).HasColumnName("concurrency_version");
            entity.HasIndex(item => item.ExpiresAtUtc).HasDatabaseName("ix_broker_idempotency_expires");
        });
        modelBuilder.Entity<BrokerAuditEntity>(entity =>
        {
            entity.ToTable("broker_audit");
            entity.HasKey(item => item.Id).HasName("pk_broker_audit");
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Operation).HasColumnName("operation").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Outcome).HasColumnName("outcome").HasMaxLength(32).IsRequired();
            entity.Property(item => item.Mode).HasColumnName("mode").HasMaxLength(32).IsRequired();
            entity.Property(item => item.ActorId).HasColumnName("actor_id").HasMaxLength(128);
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id").HasMaxLength(128);
            entity.Property(item => item.ConnectorId).HasColumnName("connector_id").HasMaxLength(128);
            entity.Property(item => item.AccountId).HasColumnName("account_id").HasMaxLength(128);
            entity.Property(item => item.ExecutionSessionId).HasColumnName("execution_session_id").HasMaxLength(128);
            entity.Property(item => item.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128);
            entity.Property(item => item.TimestampUtc).HasColumnName("timestamp_utc");
            entity.Property(item => item.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
            entity.HasIndex(item => new { item.TenantId, item.TimestampUtc }).HasDatabaseName("ix_broker_audit_tenant_timestamp");
            entity.HasIndex(item => item.ExecutionSessionId).HasDatabaseName("ix_broker_audit_execution_session");
        });
        modelBuilder.Entity<BrokerReconciliationEntity>(entity =>
        {
            entity.ToTable("broker_reconciliation_runs");
            entity.HasKey(item => item.Id).HasName("pk_broker_reconciliation_runs");
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(item => item.ConnectorId).HasColumnName("connector_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.AccountId).HasColumnName("account_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
            entity.Property(item => item.IsConsistent).HasColumnName("is_consistent");
            entity.Property(item => item.ErrorCode).HasColumnName("error_code").HasMaxLength(128);
            entity.Property(item => item.StartedAtUtc).HasColumnName("started_at_utc");
            entity.Property(item => item.CompletedAtUtc).HasColumnName("completed_at_utc");
            entity.HasIndex(item => new { item.TenantId, item.AccountId, item.StartedAtUtc }).HasDatabaseName("ix_broker_reconciliation_tenant_account_started");
        });
        modelBuilder.Entity<BrokerReconciliationMismatchEntity>(entity =>
        {
            entity.ToTable("broker_reconciliation_items");
            entity.HasKey(item => item.Id).HasName("pk_broker_reconciliation_items");
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.ReconciliationId).HasColumnName("reconciliation_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Type).HasColumnName("type").HasMaxLength(64).IsRequired();
            entity.Property(item => item.Reference).HasColumnName("reference").HasMaxLength(256).IsRequired();
            entity.Property(item => item.Description).HasColumnName("description").HasMaxLength(512).IsRequired();
            entity.HasIndex(item => item.ReconciliationId).HasDatabaseName("ix_broker_reconciliation_items_run");
        });
    }
}
