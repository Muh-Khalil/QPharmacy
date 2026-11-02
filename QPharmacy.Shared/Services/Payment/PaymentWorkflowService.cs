using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QPharmacy.Repositories;
using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace QPharmacy.Services
{
    public class PaymentWorkflowService : IPaymentWorkflowService
    {
        private readonly IPaymentWorkflowRepository _repository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentWorkflowService> _logger;

        public PaymentWorkflowService(
            IPaymentWorkflowRepository repository,
            IConfiguration configuration,
            ILogger<PaymentWorkflowService> logger)
        {
            _repository = repository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest request)
        {
            if (request.TotalAmount <= 0)
            {
                return new ProcessPaymentResult
                {
                    Success = false,
                    Status = "Failed",
                    Message = "Payment amount must be greater than zero."
                };
            }

            if (request.PaymentType == "Split")
            {
                if (request.MedicalAidAmount + request.CardAmount != request.TotalAmount)
                {
                    return new ProcessPaymentResult
                    {
                        Success = false,
                        Status = "Failed",
                        Message = "Split payment portions (Medical Aid + Card) must sum exactly to the total order amount."
                    };
                }
            }
            else if (request.PaymentType == "MedicalAid")
            {
                request.MedicalAidAmount = request.TotalAmount;
                request.CardAmount = 0;
            }
            else if (request.PaymentType == "Card")
            {
                request.CardAmount = request.TotalAmount;
                request.MedicalAidAmount = 0;
            }

            return await _repository.ProcessPaymentTransactionAsync(request);
        }

        public async Task<bool> ProcessWebhookAsync(string payload, string signatureHeader, string eventId, string eventType)
        {
            var secret = _configuration["PaymentGateway:WebhookSecret"] ?? "qpharmacy_webhook_secret_key_2026";

            if (!VerifyHmacSignature(payload, signatureHeader, secret))
            {
                _logger.LogWarning("Webhook signature verification failed for event {EventId}", eventId);
                return false;
            }

            var evt = new WebhookEventModel
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                EventType = eventType,
                Signature = signatureHeader,
                Payload = payload,
                Status = "Processed",
                ProcessedAt = DateTime.UtcNow
            };

            return await _repository.RecordWebhookEventAsync(evt);
        }

        public Task<PaymentIntentModel?> GetPaymentIntentAsync(Guid orderId) =>
            _repository.GetPaymentIntentByOrderIdAsync(orderId);

        public Task<IEnumerable<PaymentAllocationModel>> GetAllocationsAsync(Guid orderId) =>
            _repository.GetAllocationsByOrderIdAsync(orderId);

        public Task<MedicalAidModel?> GetMedicalAidForPatientAsync(Guid patientId) =>
            _repository.GetMedicalAidForPatientAsync(patientId);

        private static bool VerifyHmacSignature(string payload, string signatureHeader, string secret)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader)) return false;

            try
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();

                var cleanHeader = signatureHeader.Trim().ToLowerInvariant();
                if (cleanHeader.StartsWith("sha256="))
                    cleanHeader = cleanHeader.Substring(7);

                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computedSignature),
                    Encoding.UTF8.GetBytes(cleanHeader)
                );
            }
            catch
            {
                return false;
            }
        }
    }
}
