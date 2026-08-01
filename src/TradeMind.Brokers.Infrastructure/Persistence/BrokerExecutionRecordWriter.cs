using Microsoft.EntityFrameworkCore;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Infrastructure.Persistence.Entities;

namespace TradeMind.Brokers.Infrastructure.Persistence;

/// <summary>
/// Persists only the normalized execution, order, fill and position projections.
/// Connector payloads and credentials never cross this boundary.
/// </summary>
public sealed class BrokerExecutionRecordWriter(IDbContextFactory<BrokerDbContext> dbContextFactory) : IBrokerExecutionRecordWriter
{
    public async Task WriteAsync(BrokerOrderExecutionCommand command, BrokerExecutionResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(result);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var request = command.Order;
        var execution = await context.Executions.FindAsync([result.ExecutionId.Value], cancellationToken).ConfigureAwait(false);
        if (execution is null)
        {
            context.Executions.Add(new BrokerExecutionEntity
            {
                Id = result.ExecutionId.Value,
                ConnectorId = request.ConnectorId.Value,
                AccountId = request.AccountId.Value,
                TenantId = request.TenantId,
                ExecutionSessionId = request.ExecutionSessionId,
                Status = result.Status.ToString(),
                ErrorCode = result.Error?.Code,
                CreatedAtUtc = result.CompletedAtUtc
            });
        }
        else
        {
            execution.Status = result.Status.ToString();
            execution.ErrorCode = result.Error?.Code;
            execution.CreatedAtUtc = result.CompletedAtUtc;
        }

        if (result.Order is not null)
        {
            var order = await context.Orders.FindAsync([result.Order.OrderId.Value], cancellationToken).ConfigureAwait(false);
            if (order is null)
            {
                context.Orders.Add(ToEntity(result.Order, request));
            }
            else
            {
                order.Status = result.Order.Status.ToString();
                order.Quantity = result.Order.Quantity;
                order.FilledQuantity = result.Order.FilledQuantity;
                order.UpdatedAtUtc = result.Order.UpdatedAtUtc;
                order.ConcurrencyVersion++;
            }
        }

        if (result.Execution is not null)
        {
            var fillId = result.Execution.ExecutionId.Value;
            if (!await context.Fills.AnyAsync(item => item.Id == fillId, cancellationToken).ConfigureAwait(false))
            {
                context.Fills.Add(new BrokerFillEntity
                {
                    Id = fillId,
                    ExecutionId = result.Execution.ExecutionId.Value,
                    OrderId = result.Execution.OrderId.Value,
                    PositionId = result.Execution.PositionId?.Value,
                    ConnectorId = result.Execution.ConnectorId.Value,
                    AccountId = result.Execution.AccountId.Value,
                    TenantId = request.TenantId,
                    Quantity = result.Execution.FilledQuantity,
                    Price = result.Execution.AveragePrice ?? 0m,
                    FilledAtUtc = result.Execution.OccurredAtUtc
                });
            }

            if (result.Execution.PositionId is not null)
            {
                var positionId = result.Execution.PositionId.Value;
                var position = await context.Positions.FindAsync(new object[] { positionId }, cancellationToken).ConfigureAwait(false);
                if (position is null)
                {
                    context.Positions.Add(new BrokerPositionEntity
                    {
                        Id = positionId,
                        ConnectorId = result.Execution.ConnectorId.Value,
                        AccountId = result.Execution.AccountId.Value,
                        TenantId = request.TenantId,
                        Instrument = request.Instrument,
                        Quantity = result.Execution.FilledQuantity,
                        AveragePrice = result.Execution.AveragePrice ?? 0m,
                        OpenedAtUtc = result.Execution.OccurredAtUtc,
                        UpdatedAtUtc = result.Execution.OccurredAtUtc,
                        ConcurrencyVersion = 1
                    });
                }
                else
                {
                    position.UpdatedAtUtc = result.Execution.OccurredAtUtc;
                    position.ConcurrencyVersion++;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static BrokerOrderEntity ToEntity(TradeMind.Brokers.Domain.BrokerOrder order, TradeMind.Brokers.Domain.BrokerOrderRequest request) => new()
    {
        Id = order.OrderId.Value,
        ExecutionId = order.ExecutionId.Value,
        ConnectorId = order.ConnectorId.Value,
        AccountId = order.AccountId.Value,
        TenantId = request.TenantId,
        ClientOrderId = order.ClientOrderId,
        Instrument = order.Instrument,
        Status = order.Status.ToString(),
        Quantity = order.Quantity,
        FilledQuantity = order.FilledQuantity,
        CreatedAtUtc = order.CreatedAtUtc,
        UpdatedAtUtc = order.UpdatedAtUtc,
        ConcurrencyVersion = 1
    };
}
