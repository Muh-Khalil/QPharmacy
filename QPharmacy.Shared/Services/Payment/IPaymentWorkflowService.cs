using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QPharmacy.Services
{
    public interface IPaymentWorkflowService
    {
        Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest request);
        Task<bool> ProcessWebhookAsync(string payload, string signatureHeader, string eventId, string eventType);
        Task<PaymentIntentModel?> GetPaymentIntentAsync(Guid orderId);
        Task<IEnumerable<PaymentAllocationModel>> GetAllocationsAsync(Guid orderId);
        Task<MedicalAidModel?> GetMedicalAidForPatientAsync(Guid patientId);
    }
}
