using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QPharmacy.Repositories
{
    public interface IPaymentWorkflowRepository
    {
        Task<ProcessPaymentResult> ProcessPaymentTransactionAsync(ProcessPaymentRequest request);
        Task<bool> RecordWebhookEventAsync(WebhookEventModel evt);
        Task<PaymentIntentModel?> GetPaymentIntentByOrderIdAsync(Guid orderId);
        Task<IEnumerable<PaymentAllocationModel>> GetAllocationsByOrderIdAsync(Guid orderId);
        Task<MedicalAidModel?> GetMedicalAidForPatientAsync(Guid patientId);
    }
}
