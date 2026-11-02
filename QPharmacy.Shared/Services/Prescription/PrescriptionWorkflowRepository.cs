using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using QPharmacy.Shared.Models.Database;
using System.Data;

namespace QPharmacy.Repositories;

public sealed class PrescriptionWorkflowRepository : IPrescriptionWorkflowRepository
{
    private readonly string _connectionString;

    public PrescriptionWorkflowRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");
    }

    public async Task<Guid> IssueAsync(WorkflowPrescriptionModel prescription)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            const string insertPrescription = """
                INSERT INTO dbo.WorkflowPrescriptions
                    (Id, PrescriptionNumber, PatientId, DoctorId, PatientName, DoctorName, PreferredPharmacy, Status, IssuedAt, SignedAt, SignatureHash, UpdatedAt, Version)
                VALUES
                    (@Id, @PrescriptionNumber, @PatientId, @DoctorId, @PatientName, @DoctorName, @PreferredPharmacy, @Status, @IssuedAt, @SignedAt, @SignatureHash, @UpdatedAt, @Version);
                """;
            await connection.ExecuteAsync(insertPrescription, new
            {
                prescription.Id,
                prescription.PrescriptionNumber,
                prescription.PatientId,
                prescription.DoctorId,
                prescription.PatientName,
                prescription.DoctorName,
                prescription.PreferredPharmacy,
                Status = prescription.Status.ToString(),
                prescription.IssuedAt,
                prescription.SignedAt,
                prescription.SignatureHash,
                prescription.UpdatedAt,
                prescription.Version
            }, transaction);

            const string insertItem = """
                INSERT INTO dbo.WorkflowPrescriptionItems
                    (Id, PrescriptionId, MedicationName, Dosage, Frequency, DurationDays, Quantity, RepeatsAllowed, RepeatsRemaining)
                VALUES
                    (@Id, @PrescriptionId, @MedicationName, @Dosage, @Frequency, @DurationDays, @Quantity, @RepeatsAllowed, @RepeatsAllowed);
                """;
            foreach (var item in prescription.Items)
            {
                item.PrescriptionId = prescription.Id;
                await connection.ExecuteAsync(insertItem, item, transaction);
            }

            await InsertHistoryAsync(connection, transaction, prescription.Id, PrescriptionWorkflowStatus.Draft,
                prescription.Status, prescription.DoctorId == Guid.Empty ? prescription.PatientId : prescription.DoctorId,
                prescription.DoctorId == Guid.Empty ? "Prescription uploaded by patient" : "Prescription issued");
            await InsertAuditAsync(connection, transaction, prescription.Id, prescription.DoctorId == Guid.Empty ? prescription.PatientId : prescription.DoctorId,
                "Prescription submitted", prescription.Status.ToString());
            await transaction.CommitAsync();
            return prescription.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<WorkflowPrescriptionModel?> GetAsync(Guid prescriptionId)
    {
        var results = await QueryAsync("p.Id = @PrescriptionId", new { PrescriptionId = prescriptionId });
        return results.SingleOrDefault();
    }

    public Task<IEnumerable<WorkflowPrescriptionModel>> GetForPatientAsync(Guid patientId) =>
        QueryAsync("p.PatientId = @PatientId", new { PatientId = patientId });

    public Task<IEnumerable<WorkflowPrescriptionModel>> GetForDoctorAsync(Guid doctorId) =>
        QueryAsync("p.DoctorId = @DoctorId", new { DoctorId = doctorId });

    public Task<IEnumerable<WorkflowPrescriptionModel>> GetVerificationQueueAsync(string pharmacyBranch) =>
        QueryAsync("p.PreferredPharmacy = @PharmacyBranch AND p.Status IN ('AwaitingVerification', 'Queried')", new { PharmacyBranch = pharmacyBranch });

    public async Task<bool> TransitionAsync(Guid prescriptionId, PrescriptionWorkflowStatus expectedStatus,
        PrescriptionWorkflowStatus newStatus, Guid actorUserId, string reason)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            const string update = """
                UPDATE dbo.WorkflowPrescriptions
                SET Status = @NewStatus, UpdatedAt = SYSUTCDATETIME(), Version = Version + 1
                WHERE Id = @PrescriptionId AND Status = @ExpectedStatus;
                """;
            var changed = await connection.ExecuteAsync(update, new
            {
                PrescriptionId = prescriptionId,
                ExpectedStatus = expectedStatus.ToString(),
                NewStatus = newStatus.ToString()
            }, transaction);
            if (changed == 0) { await transaction.RollbackAsync(); return false; }

            if (newStatus == PrescriptionWorkflowStatus.Approved)
                await ReserveInventoryAndCreateOrdersAsync(connection, transaction, prescriptionId, prescriptionId, actorUserId);

            await InsertHistoryAsync(connection, transaction, prescriptionId, expectedStatus, newStatus, actorUserId, reason);
            await InsertAuditAsync(connection, transaction, prescriptionId, actorUserId,
                $"Prescription {newStatus}", reason);
            await connection.ExecuteAsync("""
                INSERT INTO dbo.PrescriptionReviews (Id, PrescriptionId, ActorUserId, Decision, Note, CreatedAt)
                VALUES (@Id, @PrescriptionId, @ActorUserId, @Decision, @Note, SYSUTCDATETIME());
                """, new { Id = Guid.NewGuid(), PrescriptionId = prescriptionId, ActorUserId = actorUserId, Decision = newStatus.ToString(), Note = reason }, transaction);
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task ReserveInventoryAndCreateOrdersAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid prescriptionId,
        Guid orderId,
        Guid actorUserId)
    {
        var prescription = await connection.QuerySingleAsync<(Guid PatientId, string PatientName, string PreferredPharmacy, string PrescriptionNumber)>("""
            SELECT PatientId, PatientName, PreferredPharmacy, PrescriptionNumber
            FROM dbo.WorkflowPrescriptions WHERE Id = @PrescriptionId;
            """, new { PrescriptionId = prescriptionId }, transaction);
        var items = (await connection.QueryAsync<WorkflowPrescriptionItemModel>("""
            SELECT Id, PrescriptionId, MedicationName, Dosage, Frequency, DurationDays, Quantity, RepeatsAllowed
            FROM dbo.WorkflowPrescriptionItems WHERE PrescriptionId = @PrescriptionId;
            """, new { PrescriptionId = prescriptionId }, transaction)).ToList();

        foreach (var item in items)
        {
            var inventoryId = await connection.QuerySingleOrDefaultAsync<Guid?>("""
                SELECT TOP 1 Id FROM dbo.PharmacyInventory WITH (UPDLOCK, ROWLOCK)
                WHERE BranchName = @BranchName AND MedicationName = @MedicationName AND InStock >= @Quantity
                ORDER BY InStock DESC;
                """, new { BranchName = prescription.PreferredPharmacy, item.MedicationName, item.Quantity }, transaction);
            if (inventoryId is null)
                throw new InvalidOperationException($"Insufficient stock for '{item.MedicationName}' at '{prescription.PreferredPharmacy}'.");

            await connection.ExecuteAsync("""
                UPDATE dbo.PharmacyInventory
                SET InStock = InStock - @Quantity,
                    Status = CASE WHEN InStock - @Quantity <= ReorderLevel THEN 'Low' ELSE 'Healthy' END,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @InventoryItemId;
                INSERT INTO dbo.InventoryReservations
                    (Id, PrescriptionId, OrderId, InventoryItemId, Quantity, Status, CreatedAt)
                VALUES
                    (@ReservationId, @PrescriptionId, @OrderId, @InventoryItemId, @Quantity, 'Reserved', SYSUTCDATETIME());
                INSERT INTO dbo.InventoryTransactions
                    (Id, InventoryItemId, PrescriptionId, TransactionType, QuantityChange, PerformedBy, Notes, CreatedAt)
                VALUES
                    (@TransactionId, @InventoryItemId, @PrescriptionId, 'Reserve', -@Quantity, @ActorUserId, 'Prescription approved', SYSUTCDATETIME());
                """, new { ReservationId = Guid.NewGuid(), TransactionId = Guid.NewGuid(), PrescriptionId = prescriptionId, OrderId = orderId, InventoryItemId = inventoryId.Value, item.Quantity, ActorUserId = actorUserId }, transaction);
        }

        decimal totalAmount = 0m;
        foreach (var item in items)
        {
            var medName = item.MedicationName.ToLowerInvariant();
            decimal unitPrice = medName switch
            {
                var s when s.Contains("cetirizine") => 65.00m,
                var s when s.Contains("metformin") => 85.00m,
                var s when s.Contains("atorvastatin") => 120.00m,
                var s when s.Contains("amlodipine") => 75.00m,
                var s when s.Contains("amoxicillin") => 95.00m,
                _ => 80.00m
            };
            totalAmount += unitPrice * Math.Max(1, item.Quantity);
        }
        if (totalAmount <= 0) totalAmount = 150.00m;

        var orderNumber = $"QP-{DateTime.UtcNow:yyyyMMdd}-{orderId.ToString("N")[..6].ToUpperInvariant()}";
        await connection.ExecuteAsync("""
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @PatientId)
            BEGIN
                INSERT INTO dbo.Users (Id, FirstName, LastName, PhoneNumber, Email, PasswordHash, PasswordSalt, IsVerified, IsActive, CreatedAt)
                VALUES (@PatientId, @PatientName, '', CONVERT(nvarchar(15), ABS(CHECKSUM(NEWID()))), CONVERT(nvarchar(36), @PatientId) + '@patient.local', 'HASH', 'SALT', 1, 1, SYSUTCDATETIME());
            END;
            INSERT INTO dbo.DispensingOrders
                (Id, OrderNumber, PatientName, ItemCount, Fulfilment, HandoverRef, ActionLabel, CreatedAt, PaymentStatus)
            VALUES
                (@Id, @OrderNumber, @PatientName, @ItemCount, 'Click & collect', 'Awaiting payment', 'Awaiting payment', SYSUTCDATETIME(), 'Unpaid');
            INSERT INTO dbo.Orders
                (Id, OrderNumber, PatientId, OrderDate, TotalAmount, Status, ItemsCount, DispatchedFrom, PaymentStatus)
            VALUES
                (@Id, @OrderNumber, @PatientId, SYSUTCDATETIME(), @TotalAmount, 'Processing', @ItemCount, @BranchName, 'Unpaid');
            """, new { Id = orderId, OrderNumber = orderNumber, prescription.PatientName, prescription.PatientId, ItemCount = items.Count, BranchName = prescription.PreferredPharmacy, TotalAmount = totalAmount }, transaction);

        foreach (var item in items)
        {
            await connection.ExecuteAsync("""
                INSERT INTO dbo.FulfilmentItems
                    (Id, OrderId, PrescriptionId, PrescriptionItemId, RequestedMedication, DispensedMedication,
                     RequestedQuantity, FulfilledQuantity, IsSubstitution, SubstitutionReason, Status)
                VALUES
                    (@Id, @OrderId, @PrescriptionId, @PrescriptionItemId, @MedicationName, @MedicationName,
                     @Quantity, @Quantity, 0, '', 'Reserved');
                """, new { Id = Guid.NewGuid(), OrderId = orderId, PrescriptionId = prescriptionId, PrescriptionItemId = item.Id, item.MedicationName, item.Quantity }, transaction);
        }
    }

    public async Task<Guid> CreateRefillAsync(Guid prescriptionId, Guid patientId, Guid actorUserId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var allowed = await connection.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM WorkflowPrescriptions p WITH (UPDLOCK)
                WHERE p.Id = @PrescriptionId AND p.PatientId = @PatientId AND p.Status = 'Approved'
                  AND EXISTS (SELECT 1 FROM WorkflowPrescriptionItems i WHERE i.PrescriptionId = p.Id AND i.RepeatsRemaining > 0)
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM FulfilmentItems fi JOIN Orders o ON o.Id = fi.OrderId
                      WHERE fi.PrescriptionId = p.Id AND o.Status NOT IN ('Delivered', 'Cancelled')
                  );
                """, new { PrescriptionId = prescriptionId, PatientId = patientId }, transaction);
            if (allowed == 0) throw new InvalidOperationException("No repeat is available or another fulfilment is still active.");

            var orderId = Guid.NewGuid();
            await ReserveInventoryAndCreateOrdersAsync(connection, transaction, prescriptionId, orderId, actorUserId);
            await connection.ExecuteAsync("""
                UPDATE WorkflowPrescriptionItems SET RepeatsRemaining = RepeatsRemaining - 1
                WHERE PrescriptionId = @PrescriptionId AND RepeatsRemaining > 0;
                """, new { PrescriptionId = prescriptionId }, transaction);
            await InsertAuditAsync(connection, transaction, prescriptionId, actorUserId, "Prescription refill requested", $"Order {orderId}");
            await transaction.CommitAsync();
            return orderId;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    private async Task<IEnumerable<WorkflowPrescriptionModel>> QueryAsync(string where, object parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        var lookup = new Dictionary<Guid, WorkflowPrescriptionModel>();
        var sql = $"""
            SELECT p.Id, p.PrescriptionNumber, p.PatientId, p.DoctorId, p.PatientName, p.DoctorName,
                   p.PreferredPharmacy, p.Status, p.IssuedAt, p.SignedAt, p.SignatureHash, p.UpdatedAt, p.Version,
                   i.Id, i.PrescriptionId, i.MedicationName, i.Dosage, i.Frequency,
                   i.DurationDays, i.Quantity, i.RepeatsAllowed, i.RepeatsRemaining
            FROM dbo.WorkflowPrescriptions p
            LEFT JOIN dbo.WorkflowPrescriptionItems i ON i.PrescriptionId = p.Id
            WHERE {where}
            ORDER BY p.IssuedAt DESC;
            """;
        await connection.QueryAsync<WorkflowPrescriptionRow, WorkflowPrescriptionItemModel, WorkflowPrescriptionModel>(
            sql,
            (row, item) =>
            {
                if (!lookup.TryGetValue(row.Id, out var prescription))
                {
                    prescription = row.ToModel();
                    lookup.Add(row.Id, prescription);
                }
                if (item is not null && item.Id != Guid.Empty) prescription.Items.Add(item);
                return prescription;
            }, parameters, splitOn: "Id");
        return lookup.Values;
    }

    private static Task InsertHistoryAsync(SqlConnection connection, SqlTransaction transaction, Guid prescriptionId,
        PrescriptionWorkflowStatus from, PrescriptionWorkflowStatus to, Guid actorUserId, string reason) =>
        connection.ExecuteAsync("""
            INSERT INTO dbo.PrescriptionStatusHistory
                (Id, PrescriptionId, FromStatus, ToStatus, ChangedByUserId, Reason, ChangedAt)
            VALUES
                (@Id, @PrescriptionId, @FromStatus, @ToStatus, @ChangedByUserId, @Reason, SYSUTCDATETIME());
            """, new { Id = Guid.NewGuid(), PrescriptionId = prescriptionId, FromStatus = from.ToString(), ToStatus = to.ToString(), ChangedByUserId = actorUserId, Reason = reason }, transaction);

    private static Task InsertAuditAsync(SqlConnection connection, SqlTransaction transaction, Guid prescriptionId,
        Guid actorUserId, string eventType, string details) => connection.ExecuteAsync("""
            IF OBJECT_ID('dbo.AdminAuditLogs', 'U') IS NOT NULL
                INSERT INTO dbo.AdminAuditLogs
                    (Id, Timestamp, EventType, Category, Actor, Details, Status, OriginIp, CreatedAt)
                VALUES
                    (@Id, CONVERT(nvarchar(50), SYSUTCDATETIME(), 126), @EventType, 'Prescription',
                     CONVERT(nvarchar(36), @ActorUserId), @Details, 'Success', '', SYSUTCDATETIME());
            """, new { Id = Guid.NewGuid(), EventType = eventType, ActorUserId = actorUserId, Details = $"Prescription {prescriptionId}: {details}" }, transaction);

    private sealed class WorkflowPrescriptionRow
    {
        public Guid Id { get; init; }
        public string PrescriptionNumber { get; init; } = string.Empty;
        public Guid PatientId { get; init; }
        public Guid DoctorId { get; init; }
        public string PatientName { get; init; } = string.Empty;
        public string DoctorName { get; init; } = string.Empty;
        public string PreferredPharmacy { get; init; } = string.Empty;
        public PrescriptionWorkflowStatus Status { get; init; }
        public DateTime IssuedAt { get; init; }
        public DateTime? SignedAt { get; init; }
        public string SignatureHash { get; init; } = string.Empty;
        public DateTime UpdatedAt { get; init; }
        public int Version { get; init; }
        public WorkflowPrescriptionModel ToModel() => new() { Id = Id, PrescriptionNumber = PrescriptionNumber, PatientId = PatientId, DoctorId = DoctorId, PatientName = PatientName, DoctorName = DoctorName, PreferredPharmacy = PreferredPharmacy, Status = Status, IssuedAt = IssuedAt, SignedAt = SignedAt, SignatureHash = SignatureHash, UpdatedAt = UpdatedAt, Version = Version };
    }
}
