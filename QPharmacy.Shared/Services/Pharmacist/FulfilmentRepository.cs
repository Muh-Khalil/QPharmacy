using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Repositories;

public sealed class FulfilmentRepository : IFulfilmentRepository
{
    private readonly string _connectionString;
    public FulfilmentRepository(IConfiguration configuration) =>
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

    public async Task<string?> GetOrderBranchAsync(Guid orderId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<string>("SELECT DispatchedFrom FROM Orders WHERE Id = @OrderId", new { OrderId = orderId });
    }

    public async Task<IEnumerable<FulfilmentItemModel>> GetItemsAsync(Guid orderId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<FulfilmentItemModel>("SELECT * FROM FulfilmentItems WHERE OrderId = @OrderId ORDER BY RequestedMedication", new { OrderId = orderId });
    }

    public async Task<bool> SetFulfilledQuantityAsync(Guid itemId, int quantity, Guid actorUserId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var item = await connection.QuerySingleOrDefaultAsync<dynamic>("""
                SELECT f.PrescriptionId, f.OrderId, f.RequestedQuantity, f.FulfilledQuantity,
                       r.Id ReservationId, r.InventoryItemId
                FROM FulfilmentItems f WITH (UPDLOCK)
                INNER JOIN InventoryReservations r WITH (UPDLOCK)
                    ON r.PrescriptionId = f.PrescriptionId AND r.OrderId = f.OrderId
                INNER JOIN PharmacyInventory i ON i.Id = r.InventoryItemId AND i.MedicationName = f.DispensedMedication
                WHERE f.Id = @ItemId AND f.Status <> 'Cancelled' AND r.Status = 'Reserved';
                """, new { ItemId = itemId }, transaction);
            if (item is null || quantity < 0 || quantity > (int)item.RequestedQuantity)
            {
                await transaction.RollbackAsync();
                return false;
            }

            var difference = (int)item.FulfilledQuantity - quantity;
            if (difference != 0)
            {
                if (difference < 0)
                {
                    var available = await connection.ExecuteScalarAsync<int>(
                        "SELECT InStock FROM PharmacyInventory WITH (UPDLOCK) WHERE Id = @InventoryItemId",
                        new { item.InventoryItemId }, transaction);
                    if (available < -difference) { await transaction.RollbackAsync(); return false; }
                }
                await connection.ExecuteAsync("""
                    UPDATE PharmacyInventory SET InStock = InStock + @Difference, UpdatedAt = SYSUTCDATETIME()
                    WHERE Id = @InventoryItemId;
                    UPDATE InventoryReservations SET Quantity = Quantity - @Difference WHERE Id = @ReservationId;
                    INSERT INTO InventoryTransactions
                        (Id, InventoryItemId, PrescriptionId, TransactionType, QuantityChange, PerformedBy, Notes, CreatedAt)
                    VALUES
                        (@TransactionId, @InventoryItemId, @PrescriptionId, @TransactionType, @Difference,
                         @ActorUserId, 'Fulfilment quantity adjusted', SYSUTCDATETIME());
                    """, new
                    {
                        Difference = difference, item.InventoryItemId, item.ReservationId, item.PrescriptionId,
                        TransactionId = Guid.NewGuid(), TransactionType = difference > 0 ? "PartialRelease" : "AdditionalReserve",
                        ActorUserId = actorUserId
                    }, transaction);
            }
            var rows = await connection.ExecuteAsync("""
                UPDATE FulfilmentItems
                SET FulfilledQuantity = @Quantity,
                    Status = CASE WHEN @Quantity = 0 THEN 'Unavailable' WHEN @Quantity < RequestedQuantity THEN 'Partial' ELSE 'Reserved' END
                WHERE Id = @ItemId;
                """, new { ItemId = itemId, Quantity = quantity }, transaction);
            await transaction.CommitAsync();
            return rows == 1;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task<bool> SubstituteAsync(Guid itemId, string medicationName, string reason, Guid actorUserId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var item = await connection.QuerySingleOrDefaultAsync<dynamic>("""
                SELECT f.PrescriptionId, f.OrderId, f.FulfilledQuantity, o.DispatchedFrom,
                       r.Id ReservationId, r.InventoryItemId
                FROM FulfilmentItems f WITH (UPDLOCK)
                INNER JOIN Orders o ON o.Id = f.OrderId
                INNER JOIN InventoryReservations r WITH (UPDLOCK)
                    ON r.PrescriptionId = f.PrescriptionId AND r.OrderId = f.OrderId
                INNER JOIN PharmacyInventory i ON i.Id = r.InventoryItemId AND i.MedicationName = f.DispensedMedication
                WHERE f.Id = @ItemId AND f.Status <> 'Cancelled' AND r.Status = 'Reserved';
                """, new { ItemId = itemId }, transaction);
            if (item is null) { await transaction.RollbackAsync(); return false; }

            var replacement = await connection.QuerySingleOrDefaultAsync<dynamic>("""
                SELECT TOP (1) Id, InStock FROM PharmacyInventory WITH (UPDLOCK)
                WHERE BranchName = @Branch AND MedicationName = @MedicationName
                ORDER BY InStock DESC;
                """, new { Branch = (string)item.DispatchedFrom, MedicationName = medicationName }, transaction);
            var fulfilledQuantity = (int)item.FulfilledQuantity;
            if (replacement is null || (int)replacement.InStock < fulfilledQuantity)
            {
                await transaction.RollbackAsync();
                return false;
            }

            await connection.ExecuteAsync("""
                UPDATE PharmacyInventory SET InStock = InStock + @Quantity, UpdatedAt = SYSUTCDATETIME() WHERE Id = @OldInventoryItemId;
                UPDATE InventoryReservations SET Status = 'Released', ReleasedAt = SYSUTCDATETIME() WHERE Id = @OldReservationId;
                UPDATE PharmacyInventory SET InStock = InStock - @Quantity, UpdatedAt = SYSUTCDATETIME() WHERE Id = @NewInventoryItemId;
                INSERT INTO InventoryReservations (Id, PrescriptionId, OrderId, InventoryItemId, Quantity, Status, ReservedAt)
                VALUES (@NewReservationId, @PrescriptionId, @OrderId, @NewInventoryItemId, @Quantity, 'Reserved', SYSUTCDATETIME());
                INSERT INTO InventoryTransactions (Id, InventoryItemId, PrescriptionId, TransactionType, QuantityChange, PerformedBy, Notes, CreatedAt)
                VALUES (@ReleaseTransactionId, @OldInventoryItemId, @PrescriptionId, 'SubstitutionRelease', @Quantity, @ActorUserId, @Reason, SYSUTCDATETIME()),
                       (@ReserveTransactionId, @NewInventoryItemId, @PrescriptionId, 'SubstitutionReserve', -@Quantity, @ActorUserId, @Reason, SYSUTCDATETIME());
                UPDATE FulfilmentItems SET DispensedMedication = @MedicationName, IsSubstitution = 1, SubstitutionReason = @Reason
                WHERE Id = @ItemId;
                """, new
                {
                    ItemId = itemId, MedicationName = medicationName, Reason = reason, Quantity = fulfilledQuantity,
                    OldInventoryItemId = (Guid)item.InventoryItemId, OldReservationId = (Guid)item.ReservationId,
                    NewInventoryItemId = (Guid)replacement.Id, NewReservationId = Guid.NewGuid(),
                    item.PrescriptionId, item.OrderId, ActorUserId = actorUserId,
                    ReleaseTransactionId = Guid.NewGuid(), ReserveTransactionId = Guid.NewGuid()
                }, transaction);
            await transaction.CommitAsync();
            return true;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task<bool> CancelAndReleaseAsync(Guid orderId, string reason, Guid actorUserId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var prescriptionId = await connection.QuerySingleOrDefaultAsync<Guid?>("SELECT PrescriptionId FROM FulfilmentItems WHERE OrderId = @OrderId", new { OrderId = orderId }, transaction);
            if (prescriptionId is null) { await transaction.RollbackAsync(); return false; }
            var reservations = await connection.QueryAsync<(Guid InventoryItemId, int Quantity)>("""
                SELECT InventoryItemId, Quantity FROM InventoryReservations WITH (UPDLOCK)
                WHERE PrescriptionId = @PrescriptionId AND OrderId = @OrderId AND Status = 'Reserved';
                """, new { PrescriptionId = prescriptionId.Value, OrderId = orderId }, transaction);
            foreach (var reservation in reservations)
            {
                await connection.ExecuteAsync("""
                    UPDATE PharmacyInventory SET InStock = InStock + @Quantity, UpdatedAt = SYSUTCDATETIME() WHERE Id = @InventoryItemId;
                    INSERT INTO InventoryTransactions (Id, InventoryItemId, PrescriptionId, TransactionType, QuantityChange, PerformedBy, Notes, CreatedAt)
                    VALUES (@Id, @InventoryItemId, @PrescriptionId, 'Release', @Quantity, @ActorUserId, @Reason, SYSUTCDATETIME());
                    """, new { Id = Guid.NewGuid(), reservation.InventoryItemId, reservation.Quantity, PrescriptionId = prescriptionId.Value, ActorUserId = actorUserId, Reason = reason }, transaction);
            }
            await connection.ExecuteAsync("""
                UPDATE InventoryReservations SET Status = 'Released', ReleasedAt = SYSUTCDATETIME() WHERE PrescriptionId = @PrescriptionId AND OrderId = @OrderId AND Status = 'Reserved';
                UPDATE FulfilmentItems SET Status = 'Cancelled' WHERE OrderId = @OrderId;
                UPDATE DispensingOrders SET ActionLabel = 'Cancelled', HandoverRef = @Reason WHERE Id = @OrderId;
                UPDATE Orders SET Status = 'Cancelled' WHERE Id = @OrderId;
                UPDATE WorkflowPrescriptions SET Status = 'Cancelled', UpdatedAt = SYSUTCDATETIME(), Version = Version + 1 WHERE Id = @PrescriptionId;
                """, new { OrderId = orderId, PrescriptionId = prescriptionId.Value, Reason = reason }, transaction);
            await transaction.CommitAsync();
            return true;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }
}
