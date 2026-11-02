using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QPharmacy.Repositories
{
    public class MockPaymentWorkflowRepository : IPaymentWorkflowRepository
    {
        private readonly List<PaymentIntentModel> _intents = new();
        private readonly List<PaymentAllocationModel> _allocations = new();
        private readonly List<WebhookEventModel> _webhooks = new();

        public Task<MedicalAidModel?> GetMedicalAidForPatientAsync(Guid patientId)
        {
            return Task.FromResult<MedicalAidModel?>(new MedicalAidModel
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                SchemeName = "Discovery Health",
                PlanName = "Classic Saver",
                MemberNumber = "3820 4471 002",
                AnnualLimit = 10000m,
                AmountUsed = 4200m,
                Status = "Active"
            });
        }

        public Task<ProcessPaymentResult> ProcessPaymentTransactionAsync(ProcessPaymentRequest request)
        {
            var intentId = Guid.NewGuid();
            _intents.Add(new PaymentIntentModel
            {
                Id = intentId,
                OrderId = request.OrderId,
                PatientId = request.PatientId,
                Amount = request.TotalAmount,
                PaymentMethod = request.PaymentType,
                Status = "Succeeded",
                GatewayTransactionId = "tx_mock_" + Guid.NewGuid().ToString("N")[..8],
                IdempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString("N")
            });

            return Task.FromResult(new ProcessPaymentResult
            {
                Success = true,
                PaymentIntentId = intentId,
                Status = "Paid",
                Message = "Payment processed successfully.",
                RemainingMedicalAidBenefit = 5800m - request.MedicalAidAmount
            });
        }

        public Task<bool> RecordWebhookEventAsync(WebhookEventModel evt)
        {
            if (_webhooks.Any(w => w.EventId == evt.EventId)) return Task.FromResult(true);
            _webhooks.Add(evt);
            return Task.FromResult(true);
        }

        public Task<PaymentIntentModel?> GetPaymentIntentByOrderIdAsync(Guid orderId)
        {
            return Task.FromResult(_intents.FirstOrDefault(i => i.OrderId == orderId));
        }

        public Task<IEnumerable<PaymentAllocationModel>> GetAllocationsByOrderIdAsync(Guid orderId)
        {
            return Task.FromResult<IEnumerable<PaymentAllocationModel>>(_allocations.Where(a => a.OrderId == orderId).ToList());
        }
    }
}
