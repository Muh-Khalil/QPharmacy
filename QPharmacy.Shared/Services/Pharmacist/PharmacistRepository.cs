using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace QPharmacy.Repositories
{
    public class PharmacistRepository : IPharmacistRepository
    {
        private readonly string _connectionString;

        public PharmacistRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
        }

        // ─────────────────────────────────────────────────────────────────
        // INVENTORY
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<PharmacyInventoryModel>> GetInventoryAsync(string branchName = "Clicks, Claremont")
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@BranchName", branchName);
                return await connection.QueryAsync<PharmacyInventoryModel>(
                    "sp_GetPharmacyInventory",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
            }
            catch
            {
                // Fallback to direct SQL query if SP doesn't exist yet
                var sql = "SELECT Id, BranchName, MedicationName, InStock, ReorderLevel, Status, UpdatedAt FROM PharmacyInventory WHERE BranchName = @BranchName ORDER BY MedicationName ASC";
                return await connection.QueryAsync<PharmacyInventoryModel>(sql, new { BranchName = branchName });
            }
        }

        public async Task<bool> AddInventoryStockAsync(PharmacyInventoryModel item)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                INSERT INTO PharmacyInventory (Id, BranchName, MedicationName, InStock, ReorderLevel, Status, UpdatedAt)
                VALUES (@Id, @BranchName, @MedicationName, @InStock, @ReorderLevel, @Status, @UpdatedAt)";
            var rows = await connection.ExecuteAsync(sql, item);
            return rows > 0;
        }

        public async Task<bool> RestockItemAsync(Guid id, int addQuantity)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                UPDATE PharmacyInventory
                SET InStock = InStock + @AddQuantity,
                    Status = CASE WHEN (InStock + @AddQuantity) <= ReorderLevel THEN (CASE WHEN (InStock + @AddQuantity) <= 20 THEN 'Reorder now' ELSE 'Low' END) ELSE 'Healthy' END,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, AddQuantity = addQuantity });
            return rows > 0;
        }

        // ─────────────────────────────────────────────────────────────────
        // VERIFICATION QUEUE
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<VerificationQueueModel>> GetVerificationQueueAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                return await connection.QueryAsync<VerificationQueueModel>(
                    "sp_GetVerificationQueue",
                    commandType: CommandType.StoredProcedure
                );
            }
            catch
            {
                var sql = "SELECT Id, PrescriptionNumber, PatientName, DoctorName, MedicationDetails, InteractionStatus, IsFlagged, Initials, AvatarBgColor, MedicationShort, Status, CreatedAt FROM VerificationQueue WHERE Status = 'Pending' ORDER BY CreatedAt ASC";
                return await connection.QueryAsync<VerificationQueueModel>(sql);
            }
        }
        public async Task<bool> UpdateVerificationStatusAsync(Guid id, string status, string? queryNote = null)
        {
            using var connection = new SqlConnection(_connectionString);
            var targetStatus = status == "Approved" ? "Verified" : status;
            var sql = @"UPDATE VerificationQueue SET Status = @Status WHERE Id = @Id;
                        UPDATE Prescriptions SET Status = @TargetStatus WHERE Id = @Id;
                        UPDATE DoctorPrescriptions SET Status = @TargetStatus WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, Status = status, TargetStatus = targetStatus });
            return rows > 0;
        }

        // ─────────────────────────────────────────────────────────────────
        // DISPENSING ORDERS
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<DispensingOrderModel>> GetDispensingOrdersAsync(string branchName)
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = """
                SELECT d.Id, d.OrderNumber, d.PatientName, d.ItemCount, d.Fulfilment,
                       d.HandoverRef, d.ActionLabel, d.CreatedAt, ISNULL(d.PaymentStatus, 'Unpaid') AS PaymentStatus
                FROM DispensingOrders d
                LEFT JOIN Orders o ON o.Id = d.Id OR o.OrderNumber = d.OrderNumber
                WHERE (o.DispatchedFrom = @BranchName OR o.DispatchedFrom IS NULL)
                ORDER BY d.CreatedAt DESC;
                """;
            return await connection.QueryAsync<DispensingOrderModel>(sql, new { BranchName = branchName });
        }

        public async Task<bool> UpdateDispensingOrderAsync(Guid id, string actionLabel, string handoverRef)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            var orderStatus = actionLabel switch
            {
                "Dispatched to Courier" => "In transit",
                "Patient Notified" => "Ready for collection",
                "Archived" => "Delivered",
                _ => "Dispensed"
            };
            var rows = await connection.ExecuteAsync("""
                UPDATE DispensingOrders SET ActionLabel = @ActionLabel, HandoverRef = @HandoverRef WHERE Id = @Id;
                UPDATE Orders SET Status = @OrderStatus WHERE Id = @Id;
                """, new { Id = id, ActionLabel = actionLabel, HandoverRef = handoverRef, OrderStatus = orderStatus }, transaction);
            if (actionLabel is "Dispatched to Courier" or "Patient Notified")
            {
                await connection.ExecuteAsync("""
                    UPDATE InventoryReservations SET Status = 'Consumed'
                    WHERE OrderId = @Id AND Status = 'Reserved';
                    """, new { Id = id }, transaction);
            }
            transaction.Commit();
            return rows > 0;
        }

        public async Task<bool> AddDispensingOrderAsync(DispensingOrderModel order)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                INSERT INTO DispensingOrders (Id, OrderNumber, PatientName, ItemCount, Fulfilment, HandoverRef, ActionLabel, CreatedAt)
                VALUES (@Id, @OrderNumber, @PatientName, @ItemCount, @Fulfilment, @HandoverRef, @ActionLabel, @CreatedAt)";
            var rows = await connection.ExecuteAsync(sql, order);
            return rows > 0;
        }

        // ─────────────────────────────────────────────────────────────────
        // KPIS
        // ─────────────────────────────────────────────────────────────────

        public async Task<PharmacistKpiModel> GetPharmacistKpisAsync(string? branchName = null)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var kpi = await connection.QueryFirstOrDefaultAsync<PharmacistKpiModel>(
                    "sp_GetPharmacistKpis",
                    new { BranchName = branchName },
                    commandType: CommandType.StoredProcedure
                );
                return kpi ?? new PharmacistKpiModel();
            }
            catch
            {
                var awaiting = await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*) FROM dbo.WorkflowPrescriptions 
                    WHERE Status IN ('AwaitingVerification', 'Queried')
                      AND (@BranchName IS NULL OR @BranchName = '' OR PreferredPharmacy = @BranchName)
                """, new { BranchName = branchName });

                var ready = await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*) FROM dbo.DispensingOrders d
                    LEFT JOIN dbo.Orders o ON o.Id = d.Id OR o.OrderNumber = d.OrderNumber
                    WHERE (@BranchName IS NULL OR @BranchName = '' OR o.DispatchedFrom = @BranchName OR o.DispatchedFrom IS NULL)
                      AND (d.ActionLabel IN ('Mark ready', 'Patient Notified', 'Ready for collection') OR o.Status = 'Ready for collection')
                """, new { BranchName = branchName });

                var lowStock = await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*) FROM dbo.PharmacyInventory 
                    WHERE (@BranchName IS NULL OR @BranchName = '' OR BranchName = @BranchName)
                      AND (Status <> 'Healthy' OR InStock <= ReorderLevel)
                """, new { BranchName = branchName });

                var dispensed = await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*) FROM dbo.DispensingOrders d
                    LEFT JOIN dbo.Orders o ON o.Id = d.Id OR o.OrderNumber = d.OrderNumber
                    WHERE (@BranchName IS NULL OR @BranchName = '' OR o.DispatchedFrom = @BranchName OR o.DispatchedFrom IS NULL)
                      AND (d.ActionLabel = 'Archived' OR o.Status = 'Delivered')
                      AND CAST(d.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
                """, new { BranchName = branchName });

                return new PharmacistKpiModel
                {
                    AwaitingVerificationCount = awaiting,
                    ReadyForCollectionCount = ready,
                    LowStockCount = lowStock,
                    DispensedTodayCount = dispensed
                };
            }
        }
    }
}
