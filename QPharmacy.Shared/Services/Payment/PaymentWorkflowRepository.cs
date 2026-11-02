using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;

namespace QPharmacy.Repositories
{
    public class PaymentWorkflowRepository : IPaymentWorkflowRepository
    {
        private readonly string _connectionString;

        public PaymentWorkflowRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection not found in configuration.");
        }

        public async Task<MedicalAidModel?> GetMedicalAidForPatientAsync(Guid patientId)
        {
            await using var connection = new SqlConnection(_connectionString);
            const string sql = @"SELECT TOP 1 Id, PatientId, SchemeName, PlanName, MemberNumber, AnnualLimit, AmountUsed, Status, CreatedAt, UpdatedAt
                                 FROM dbo.MedicalAids
                                 WHERE PatientId = @PatientId AND Status = 'Active';";
            return await connection.QueryFirstOrDefaultAsync<MedicalAidModel>(sql, new { PatientId = patientId });
        }

        public async Task<ProcessPaymentResult> ProcessPaymentTransactionAsync(ProcessPaymentRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            decimal remainingBenefit = 0;
            MedicalAidModel? medicalAid = null;

            if (request.MedicalAidAmount > 0)
            {
                const string queryMa = @"SELECT TOP 1 Id, PatientId, SchemeName, PlanName, MemberNumber, AnnualLimit, AmountUsed, Status
                                         FROM dbo.MedicalAids WITH (UPDLOCK)
                                         WHERE PatientId = @PatientId AND Status = 'Active';";
                medicalAid = await connection.QueryFirstOrDefaultAsync<MedicalAidModel>(queryMa, new { request.PatientId });
                if (medicalAid == null)
                {
                    return new ProcessPaymentResult
                    {
                        Success = false,
                        Status = "Failed",
                        Message = "No active medical aid cover found for this patient account."
                    };
                }

                var available = medicalAid.AnnualLimit - medicalAid.AmountUsed;
                if (request.MedicalAidAmount > available)
                {
                    return new ProcessPaymentResult
                    {
                        Success = false,
                        Status = "Failed",
                        RemainingMedicalAidBenefit = available,
                        Message = $"Medical aid limit exceeded. Available balance is R {available:N2}, but R {request.MedicalAidAmount:N2} was requested. Please use split payment or card."
                    };
                }

                remainingBenefit = available - request.MedicalAidAmount;
            }

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                var paymentIntentId = Guid.NewGuid();
                var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? Guid.NewGuid().ToString("N")
                    : request.IdempotencyKey;

                // 1. Insert PaymentIntent
                const string insertIntentSql = @"
                    INSERT INTO dbo.PaymentIntents
                        (Id, OrderId, PatientId, Amount, Currency, PaymentMethod, Status, GatewayTransactionId, IdempotencyKey, CreatedAt, UpdatedAt)
                    VALUES
                        (@Id, @OrderId, @PatientId, @Amount, 'ZAR', @PaymentMethod, 'Succeeded', @GatewayTxId, @IdempotencyKey, SYSUTCDATETIME(), SYSUTCDATETIME());";

                var gatewayTxId = "tx_mock_" + Guid.NewGuid().ToString("N")[..12];
                await connection.ExecuteAsync(insertIntentSql, new
                {
                    Id = paymentIntentId,
                    request.OrderId,
                    request.PatientId,
                    Amount = request.TotalAmount,
                    PaymentMethod = request.PaymentType,
                    GatewayTxId = gatewayTxId,
                    IdempotencyKey = idempotencyKey
                }, transaction);

                // 2. If Medical Aid portion
                Guid? claimId = null;
                if (request.MedicalAidAmount > 0 && medicalAid != null)
                {
                    claimId = Guid.NewGuid();
                    const string insertClaimSql = @"
                        INSERT INTO dbo.Claims
                            (Id, PatientId, Description, Amount, Status, ClaimDate, CreatedAt)
                        VALUES
                            (@Id, @PatientId, @Description, @Amount, 'Approved', SYSUTCDATETIME(), SYSUTCDATETIME());";

                    await connection.ExecuteAsync(insertClaimSql, new
                    {
                        Id = claimId,
                        request.PatientId,
                        Description = $"Medication Order {request.OrderNumber}",
                        Amount = request.MedicalAidAmount
                    }, transaction);

                    const string updateMaSql = @"
                        UPDATE dbo.MedicalAids
                        SET AmountUsed = AmountUsed + @Deduction,
                            UpdatedAt = SYSUTCDATETIME()
                        WHERE Id = @Id;";

                    await connection.ExecuteAsync(updateMaSql, new
                    {
                        Deduction = request.MedicalAidAmount,
                        medicalAid.Id
                    }, transaction);

                    const string insertMaAllocSql = @"
                        INSERT INTO dbo.PaymentAllocations
                            (Id, PaymentIntentId, OrderId, Source, Amount, Status, ClaimId, PaymentMethodId, CreatedAt)
                        VALUES
                            (@Id, @PaymentIntentId, @OrderId, 'MedicalAid', @Amount, 'Approved', @ClaimId, NULL, SYSUTCDATETIME());";

                    await connection.ExecuteAsync(insertMaAllocSql, new
                    {
                        Id = Guid.NewGuid(),
                        PaymentIntentId = paymentIntentId,
                        request.OrderId,
                        Amount = request.MedicalAidAmount,
                        ClaimId = claimId
                    }, transaction);
                }

                // 3. If Card portion
                if (request.CardAmount > 0)
                {
                    const string insertCardAllocSql = @"
                        INSERT INTO dbo.PaymentAllocations
                            (Id, PaymentIntentId, OrderId, Source, Amount, Status, ClaimId, PaymentMethodId, CreatedAt)
                        VALUES
                            (@Id, @PaymentIntentId, @OrderId, 'Card', @Amount, 'Settled', NULL, @PaymentMethodId, SYSUTCDATETIME());";

                    await connection.ExecuteAsync(insertCardAllocSql, new
                    {
                        Id = Guid.NewGuid(),
                        PaymentIntentId = paymentIntentId,
                        request.OrderId,
                        Amount = request.CardAmount,
                        PaymentMethodId = request.CardPaymentMethodId
                    }, transaction);
                }

                // 4. Update Orders PaymentStatus and Status
                const string updateOrderSql = @"
                    UPDATE dbo.Orders
                    SET PaymentStatus = 'Paid',
                        Status = CASE WHEN Status IN ('Pending Payment', 'Processing') THEN 'Dispensed' ELSE Status END
                    WHERE Id = @OrderId OR OrderNumber = @OrderNumber;";

                await connection.ExecuteAsync(updateOrderSql, new
                {
                    request.OrderId,
                    request.OrderNumber
                }, transaction);

                // 5. Update DispensingOrders PaymentStatus and ActionLabel
                const string updateDispensingSql = @"
                    UPDATE dbo.DispensingOrders
                    SET PaymentStatus = 'Paid',
                        ActionLabel = CASE WHEN ActionLabel = 'Awaiting payment' THEN 'Mark packed' ELSE ActionLabel END,
                        HandoverRef = CASE WHEN HandoverRef = 'Awaiting payment' THEN 'Payment verified - ready to pack' ELSE HandoverRef END
                    WHERE Id = @OrderId OR OrderNumber = @OrderNumber;";

                await connection.ExecuteAsync(updateDispensingSql, new
                {
                    request.OrderId,
                    request.OrderNumber
                }, transaction);

                // 6. Log activity
                const string insertActivitySql = @"
                    INSERT INTO dbo.ActivityLogs
                        (Id, PatientId, Description, EventType, CreatedAt)
                    VALUES
                        (NEWID(), @PatientId, @Description, 'Payment', SYSUTCDATETIME());";

                await connection.ExecuteAsync(insertActivitySql, new
                {
                    request.PatientId,
                    Description = $"Payment of R {request.TotalAmount:N2} confirmed for order {request.OrderNumber} ({request.PaymentType})"
                }, transaction);

                // 6b. Sync paid prescription items into the Prescriptions table
                //     so the Patient Dashboard can auto-generate today's dose schedule from them.
                const string syncPrescriptionsSql = @"
                    INSERT INTO dbo.Prescriptions
                        (Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt, SupplyDays, RepeatsRemaining)
                    SELECT
                        wi.Id,
                        wp.PatientId,
                        wi.MedicationName,
                        wi.Frequency,
                        ISNULL(wp.DoctorName, 'Doctor'),
                        ISNULL(wp.PreferredPharmacy, 'QPharmacy'),
                        'Verified',
                        wp.IssuedAt,
                        ISNULL(wi.DurationDays, 30),
                        ISNULL(wi.RepeatsRemaining, 0)
                    FROM dbo.WorkflowPrescriptionItems wi
                    INNER JOIN dbo.WorkflowPrescriptions wp ON wp.Id = wi.PrescriptionId
                    WHERE wp.Id = @OrderId
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.Prescriptions p
                          WHERE p.Id = wi.Id
                             OR (p.PatientId = wp.PatientId
                                 AND p.MedicationName = wi.MedicationName
                                 AND p.Status = 'Verified')
                      );";

                await connection.ExecuteAsync(syncPrescriptionsSql, new { request.OrderId }, transaction);

                await transaction.CommitAsync();

                return new ProcessPaymentResult
                {
                    Success = true,
                    PaymentIntentId = paymentIntentId,
                    Status = "Paid",
                    Message = "Payment processed and confirmed successfully!",
                    RemainingMedicalAidBenefit = remainingBenefit
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ProcessPaymentResult
                {
                    Success = false,
                    Status = "Failed",
                    Message = $"Payment transaction failed: {ex.Message}"
                };
            }
        }

        public async Task<bool> RecordWebhookEventAsync(WebhookEventModel evt)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.WebhookEvents WHERE EventId = @EventId",
                new { evt.EventId });

            if (exists > 0)
            {
                // Idempotent: already processed
                return true;
            }

            const string sql = @"
                INSERT INTO dbo.WebhookEvents (Id, EventId, EventType, Signature, Payload, Status, ProcessedAt)
                VALUES (@Id, @EventId, @EventType, @Signature, @Payload, @Status, SYSUTCDATETIME());";

            await connection.ExecuteAsync(sql, evt);

            // If this is a payment intent succeeded event, update order payment status if orderId found
            if (evt.EventType.Equals("payment_intent.succeeded", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var doc = JsonDocument.Parse(evt.Payload);
                    if (doc.RootElement.TryGetProperty("orderId", out var orderIdElem) &&
                        Guid.TryParse(orderIdElem.GetString(), out var orderId))
                    {
                        await connection.ExecuteAsync(@"
                            UPDATE dbo.Orders SET PaymentStatus = 'Paid' WHERE Id = @OrderId;
                            UPDATE dbo.DispensingOrders SET PaymentStatus = 'Paid', ActionLabel = 'Mark packed' WHERE Id = @OrderId;
                        ", new { OrderId = orderId });
                    }
                }
                catch
                {
                    // Non-fatal if payload isn't json or doesn't have orderId
                }
            }

            return true;
        }

        public async Task<PaymentIntentModel?> GetPaymentIntentByOrderIdAsync(Guid orderId)
        {
            await using var connection = new SqlConnection(_connectionString);
            const string sql = @"SELECT TOP 1 * FROM dbo.PaymentIntents WHERE OrderId = @OrderId ORDER BY CreatedAt DESC;";
            return await connection.QueryFirstOrDefaultAsync<PaymentIntentModel>(sql, new { OrderId = orderId });
        }

        public async Task<IEnumerable<PaymentAllocationModel>> GetAllocationsByOrderIdAsync(Guid orderId)
        {
            await using var connection = new SqlConnection(_connectionString);
            const string sql = @"SELECT * FROM dbo.PaymentAllocations WHERE OrderId = @OrderId ORDER BY CreatedAt ASC;";
            return await connection.QueryAsync<PaymentAllocationModel>(sql, new { OrderId = orderId });
        }
    }
}
