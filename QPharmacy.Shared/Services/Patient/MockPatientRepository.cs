using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QPharmacy.Repositories
{
    /// <summary>
    /// In-memory mock repository used when no database is available (development / mock mode).
    /// </summary>
    public class MockPatientRepository : IPatientRepository
    {
        private static readonly List<PrescriptionModel> _mockPrescriptions = new()
        {
            new PrescriptionModel { Id = Guid.NewGuid(), PatientId = Guid.Empty, MedicationName = "Amlodipine 5mg",    Dosage = "Once daily - 30 tablets",   PrescribedBy = "Dr. N. Adams", PharmacyName = "Clicks, Claremont",  Status = "Verified", CreatedAt = DateTime.Today.AddDays(-30), LastDispensedDate = DateTime.Today.AddDays(-25), SupplyDays = 30, RepeatsRemaining = 2 },
            new PrescriptionModel { Id = Guid.NewGuid(), PatientId = Guid.Empty, MedicationName = "Metformin 500mg",   Dosage = "Twice daily - 60 tablets",  PrescribedBy = "Dr. N. Adams", PharmacyName = "Clicks, Claremont",  Status = "Verified", CreatedAt = DateTime.Today.AddDays(-60), LastDispensedDate = DateTime.Today.AddDays(-10), SupplyDays = 30, RepeatsRemaining = 1 },
            new PrescriptionModel { Id = Guid.NewGuid(), PatientId = Guid.Empty, MedicationName = "Atorvastatin 20mg", Dosage = "Nightly - 30 tablets",      PrescribedBy = "Dr. N. Adams", PharmacyName = "Clicks, Claremont",  Status = "Awaiting pharmacist",  CreatedAt = DateTime.Today.AddDays(-15) },
            new PrescriptionModel { Id = Guid.NewGuid(), PatientId = Guid.Empty, MedicationName = "Cetirizine 10mg",   Dosage = "As needed - 20 tablets",   PrescribedBy = "Self-uploaded", PharmacyName = "Dis-Chem, Tokai",   Status = "Uploaded",             CreatedAt = DateTime.Today.AddDays(-5)  },
            new PrescriptionModel { Id = Guid.NewGuid(), PatientId = Guid.Empty, MedicationName = "Amoxicillin 500mg", Dosage = "3x daily - 21 capsules",   PrescribedBy = "Dr. T. Khumalo",PharmacyName = "Dis-Chem, Tokai",   Status = "Expired",             CreatedAt = DateTime.Today.AddDays(-120)},
        };

        public Task<IEnumerable<PrescriptionModel>> GetPatientPrescriptionsAsync(Guid patientId)
            => Task.FromResult<IEnumerable<PrescriptionModel>>(_mockPrescriptions);

        public Task<IEnumerable<PrescriptionModel>> GetPrescriptionsByStatusAsync(Guid patientId, string statusGroup)
        {
            var filtered = statusGroup switch
            {
                "Active"  => _mockPrescriptions.Where(p => p.Status == "Verified"),
                "Pending" => _mockPrescriptions.Where(p => p.Status == "Awaiting pharmacist" || p.Status == "Uploaded"),
                "History" => _mockPrescriptions.Where(p => p.Status == "Expired"),
                _         => _mockPrescriptions.AsEnumerable()
            };
            return Task.FromResult<IEnumerable<PrescriptionModel>>(filtered.ToList());
        }

        public Task<bool> UploadPrescriptionAsync(PrescriptionModel prescription)
        {
            _mockPrescriptions.Insert(0, prescription);

            var vqItem = new VerificationQueueModel
            {
                Id = prescription.Id,
                PrescriptionNumber = (new Random().Next(1000, 9999)).ToString(),
                PatientName = "Thandiwe Mokoena",
                DoctorName = string.IsNullOrWhiteSpace(prescription.PrescribedBy) ? "Dr. Self-Uploaded" : prescription.PrescribedBy,
                MedicationDetails = $"{prescription.MedicationName}, {prescription.Dosage}",
                InteractionStatus = "No conflicts detected",
                IsFlagged = false,
                Initials = "TM",
                AvatarBgColor = "bg-teal-600",
                MedicationShort = prescription.MedicationName,
                Status = "Pending",
                CreatedAt = prescription.CreatedAt
            };

            MockPharmacistRepository.AddMockVerificationQueueItem(vqItem);

            return Task.FromResult(true);
        }

        internal static void UpdatePrescriptionStatus(Guid prescriptionId, string newStatus)
        {
            var match = _mockPrescriptions.FirstOrDefault(p => p.Id == prescriptionId);
            if (match != null)
            {
                match.Status = newStatus;
            }
        }

        private static readonly List<MedicationScheduleModel> _mockSchedules = new()
        {
            new MedicationScheduleModel 
            { 
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 
                PatientId = Guid.Empty, 
                MedicationName = "Amlodipine 5mg", 
                Dosage = "5mg", 
                Instructions = "Take 1 tablet with water in the morning", 
                ScheduledTime = DateTime.Today.AddHours(8),  
                Status = "Taken",
                TakenAt = DateTime.Today.AddHours(8).AddMinutes(15)
            },
            new MedicationScheduleModel 
            { 
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 
                PatientId = Guid.Empty, 
                MedicationName = "Metformin 500mg", 
                Dosage = "500mg", 
                Instructions = "Take 1 tablet with breakfast", 
                ScheduledTime = DateTime.Today.AddHours(8),  
                Status = "Taken",
                TakenAt = DateTime.Today.AddHours(8).AddMinutes(30)
            },
            new MedicationScheduleModel 
            { 
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), 
                PatientId = Guid.Empty, 
                MedicationName = "Metformin 500mg", 
                Dosage = "500mg", 
                Instructions = "Take 1 tablet with dinner", 
                ScheduledTime = DateTime.Today.AddHours(20), 
                Status = "Upcoming" 
            },
            new MedicationScheduleModel 
            { 
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), 
                PatientId = Guid.Empty, 
                MedicationName = "Atorvastatin 20mg", 
                Dosage = "20mg", 
                Instructions = "Take 1 tablet at bedtime", 
                ScheduledTime = DateTime.Today.AddHours(21), 
                Status = "Upcoming" 
            }
        };

        public Task<IEnumerable<MedicationScheduleModel>> GetMedicationScheduleTodayAsync(Guid patientId)
        {
            // Sync schedules from verified prescriptions if needed
            var verified = _mockPrescriptions.Where(p => p.Status == "Verified").ToList();
            foreach (var rx in verified)
            {
                if (!_mockSchedules.Any(s => s.MedicationName.Equals(rx.MedicationName, StringComparison.OrdinalIgnoreCase)))
                {
                    _mockSchedules.Add(new MedicationScheduleModel
                    {
                        Id = Guid.NewGuid(),
                        PatientId = patientId,
                        MedicationName = rx.MedicationName,
                        Dosage = rx.Dosage,
                        Instructions = "Take as prescribed with water",
                        ScheduledTime = DateTime.Today.AddHours(9),
                        Status = "Upcoming"
                    });
                }
            }
            return Task.FromResult<IEnumerable<MedicationScheduleModel>>(_mockSchedules.OrderBy(s => s.ScheduledTime));
        }

        public Task<bool> MarkDoseAsTakenAsync(Guid scheduleId, Guid patientId)
        {
            var schedule = _mockSchedules.FirstOrDefault(s => s.Id == scheduleId);
            if (schedule != null)
            {
                schedule.Status = "Taken";
                schedule.TakenAt = DateTime.Now;
                schedule.Notes = null;
            }
            return Task.FromResult(schedule != null);
        }

        public Task<bool> MarkDoseAsSkippedAsync(Guid scheduleId, Guid patientId, string? reason = null)
        {
            var schedule = _mockSchedules.FirstOrDefault(s => s.Id == scheduleId);
            if (schedule != null)
            {
                schedule.Status = "Skipped";
                schedule.Notes = string.IsNullOrWhiteSpace(reason) ? "Skipped by patient" : reason;
                schedule.TakenAt = null;
            }
            return Task.FromResult(schedule != null);
        }

        public Task<bool> UndoDoseStatusAsync(Guid scheduleId, Guid patientId)
        {
            var schedule = _mockSchedules.FirstOrDefault(s => s.Id == scheduleId);
            if (schedule != null)
            {
                schedule.Status = "Upcoming";
                schedule.TakenAt = null;
                schedule.Notes = null;
            }
            return Task.FromResult(schedule != null);
        }

        public Task<IEnumerable<ActivityLogModel>> GetPatientActivityLogAsync(Guid patientId)
        {
            var data = new List<ActivityLogModel>
            {
                new ActivityLogModel { Description = "Prescription for Atorvastatin 20mg uploaded", EventType = "Upload", CreatedAt = DateTime.Now.AddHours(-2) },
                new ActivityLogModel { Description = "Medication order #1042 dispatched",            EventType = "Order",  CreatedAt = DateTime.Now.AddHours(-24) }
            };
            return Task.FromResult<IEnumerable<ActivityLogModel>>(data);
        }

        private static readonly List<AppointmentModel> _mockAppointments = new()
        {
            new AppointmentModel
            {
                Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                DoctorId = Guid.Parse("5685B24E-0085-4DF9-91B1-6577322E90D7"),
                DoctorName = "Dr. Sarah Jenkins",
                Specialty = "Cardiology",
                AppointmentDate = DateTime.Today.AddDays(2).AddHours(10),
                Mode = "Video",
                Status = "Confirmed",
                Notes = "Routine follow-up for blood pressure"
            },
            new AppointmentModel
            {
                Id = Guid.Parse("22222222-3333-4444-5555-666666666666"),
                DoctorId = Guid.Parse("5685B24E-0085-4DF9-91B1-6577322E90D7"),
                DoctorName = "Dr. Sarah Jenkins",
                Specialty = "Cardiology",
                AppointmentDate = DateTime.Today.AddDays(-10).AddHours(14),
                Mode = "In-Person",
                Status = "Completed",
                Notes = "Initial consultation"
            }
        };

        public Task<AppointmentModel?> GetNextAppointmentAsync(Guid patientId)
        {
            var next = _mockAppointments
                .Where(a => a.AppointmentDate >= DateTime.Now && a.Status != "Cancelled")
                .OrderBy(a => a.AppointmentDate)
                .FirstOrDefault();

            return Task.FromResult(next);
        }

        public Task<IEnumerable<AppointmentModel>> GetPatientAppointmentsAsync(Guid patientId)
        {
            return Task.FromResult<IEnumerable<AppointmentModel>>(_mockAppointments.OrderByDescending(a => a.AppointmentDate).ToList());
        }

        public Task<Guid> BookAppointmentAsync(AppointmentModel appointment, string patientName)
        {
            if (appointment.Id == Guid.Empty)
                appointment.Id = Guid.NewGuid();
            if (string.IsNullOrWhiteSpace(appointment.Status))
                appointment.Status = "Confirmed";
            if (appointment.CreatedAt == default)
                appointment.CreatedAt = DateTime.UtcNow;

            _mockAppointments.Insert(0, appointment);
            return Task.FromResult(appointment.Id);
        }

        public Task<bool> CancelAppointmentAsync(Guid appointmentId, Guid patientId)
        {
            var appt = _mockAppointments.FirstOrDefault(a => a.Id == appointmentId);
            if (appt != null)
            {
                appt.Status = "Cancelled";
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<ConsultationSessionModel?> GetActiveConsultationForPatientAsync(Guid patientId)
        {
            var appt = _mockAppointments.FirstOrDefault(a => a.Status == "Confirmed" && a.Mode == "Video");
            if (appt != null)
            {
                return Task.FromResult<ConsultationSessionModel?>(new ConsultationSessionModel
                {
                    Id = Guid.Parse("99999999-8888-7777-6666-555555555555"),
                    AppointmentId = appt.Id,
                    DoctorId = appt.DoctorId,
                    DoctorName = appt.DoctorName,
                    PatientId = patientId,
                    PatientName = "Thandiwe Mokoena",
                    RoomId = $"room-{appt.Id.ToString()[..8]}",
                    Status = "In-Progress",
                    StartedAt = DateTime.UtcNow
                });
            }
            return Task.FromResult<ConsultationSessionModel?>(null);
        }

        public Task<DashboardKpiModel?> GetDashboardKpisAsync(Guid patientId)
        {
            var taken = _mockSchedules.Count(s => s.Status == "Taken");
            var total = _mockSchedules.Count;
            var active = _mockPrescriptions.Count(p => p.Status == "Verified");

            DashboardKpiModel? kpis = new DashboardKpiModel
            {
                DosesTakenToday      = taken,
                DosesScheduledToday  = total,
                NextRefillDays       = 12,
                ActivePrescriptions  = active > 0 ? active : 3,
                FamilyMembersLinked  = _mockFamilyMembers.Count
            };
            return Task.FromResult<DashboardKpiModel?>(kpis);
        }

        private static readonly List<FamilyMemberModel> _mockFamilyMembers = new()
        {
            new FamilyMemberModel { Id = Guid.Parse("f1111111-f111-f111-f111-f11111111111"), PrimaryPatientId = Guid.Empty, FirstName = "Thandiwe", LastName = "Mokoena", Relationship = "Self", CanView = true, CanEdit = true },
            new FamilyMemberModel { Id = Guid.Parse("f2222222-f222-f222-f222-f22222222222"), PrimaryPatientId = Guid.Empty, FirstName = "Ntando", LastName = "Mokoena", Relationship = "Child", CanView = true, CanEdit = false }
        };

        private static readonly Dictionary<Guid, List<MedicalConditionModel>> _customConditions = new();
        private static readonly Dictionary<Guid, List<LabResultModel>> _customLabResults = new();
        private static readonly Dictionary<Guid, List<VaccinationModel>> _customVaccinations = new();

        public Task<IEnumerable<FamilyMemberModel>> GetFamilyMembersAsync(Guid primaryPatientId)
            => Task.FromResult<IEnumerable<FamilyMemberModel>>(_mockFamilyMembers);

        public Task<bool> CanAccessPatientAsync(Guid primaryPatientId, Guid patientId, bool requireEdit)
        {
            if (primaryPatientId == patientId) return Task.FromResult(true);
            var member = _mockFamilyMembers.FirstOrDefault(item => item.Id == patientId &&
                (item.PrimaryPatientId == primaryPatientId || item.PrimaryPatientId == Guid.Empty));
            return Task.FromResult(member is not null && (requireEdit ? member.CanEdit : member.CanView));
        }

        public Task<bool> AddFamilyMemberAsync(FamilyMemberModel member)
        {
            if (!_mockFamilyMembers.Any(m => m.Id == member.Id))
            {
                _mockFamilyMembers.Add(member);
            }

            // Create initial baseline medical records for the newly added family member
            _customConditions[member.Id] = new List<MedicalConditionModel>
            {
                new MedicalConditionModel { PatientId = member.Id, Name = $"{member.Relationship} Wellness Evaluation", Type = "Wellness", Notes = $"Initial health profile registered for {member.FirstName}." }
            };

            _customLabResults[member.Id] = new List<LabResultModel>
            {
                new LabResultModel { PatientId = member.Id, TestName = "Routine Blood & Metabolic Screen", Value = "Normal", Status = "Normal", TestDate = DateTime.Today.AddDays(-7) }
            };

            _customVaccinations[member.Id] = new List<VaccinationModel>
            {
                new VaccinationModel { PatientId = member.Id, VaccineName = member.Relationship == "Child" ? "Pediatric Immunization (DTP)" : "Annual Influenza", Status = "Up to date", Notes = "Administered at community clinic", DateAdministered = DateTime.Today.AddMonths(-2) },
                new VaccinationModel { PatientId = member.Id, VaccineName = "Hepatitis B Booster", Status = "Due", Notes = "Scheduled for next checkup", DateDue = DateTime.Today.AddMonths(3) }
            };

            return Task.FromResult(true);
        }

        public Task<bool> AddMedicalConditionAsync(MedicalConditionModel condition)
        {
            if (!_customConditions.ContainsKey(condition.PatientId))
            {
                _customConditions[condition.PatientId] = new List<MedicalConditionModel>();
            }
            _customConditions[condition.PatientId].Insert(0, condition);
            return Task.FromResult(true);
        }

        public Task<IEnumerable<MedicalConditionModel>> GetMedicalConditionsAsync(Guid patientId)
        {
            var data = new List<MedicalConditionModel>();
            if (_customConditions.TryGetValue(patientId, out var custom) && custom.Any())
            {
                data.AddRange(custom);
            }

            if (patientId == Guid.Parse("f2222222-f222-f222-f222-f22222222222") || patientId == Guid.Parse("22222222-2222-2222-2222-222222222222"))
            {
                data.Add(new MedicalConditionModel { PatientId = patientId, Name = "Childhood Asthma", Type = "Chronic", Notes = "Diagnosed 2024 - Mild intermittent" });
                data.Add(new MedicalConditionModel { PatientId = patientId, Name = "Peanut allergy", Type = "Allergy", Notes = "Severe allergy - carries EpiPen" });
            }
            else if (patientId == Guid.Parse("f3333333-f333-f333-f333-f33333333333") || patientId == Guid.Parse("33333333-3333-3333-3333-333333333333"))
            {
                data.Add(new MedicalConditionModel { PatientId = patientId, Name = "Osteoarthritis", Type = "Chronic", Notes = "Diagnosed 2018 - Mild knee pain" });
                data.Add(new MedicalConditionModel { PatientId = patientId, Name = "Sulfa allergy", Type = "Allergy", Notes = "Rash reaction - documented 2020" });
            }
            else if (!data.Any(c => c.Name == "Hypertension"))
            {
                // Thandiwe (default / primary)
                data.Add(new MedicalConditionModel { PatientId = patientId, Name = "Hypertension", Type = "Chronic", Notes = "Diagnosed 2022 - Managed" });
                data.Add(new MedicalConditionModel { PatientId = patientId, Name = "Type 2 Diabetes", Type = "Chronic", Notes = "Diagnosed 2023 - Managed" });
                data.Add(new MedicalConditionModel { PatientId = patientId, Name = "Penicillin allergy", Type = "Allergy", Notes = "Confirmed by Dr. N. Adams" });
            }

            return Task.FromResult<IEnumerable<MedicalConditionModel>>(data);
        }

        public Task<IEnumerable<LabResultModel>> GetLabResultsAsync(Guid patientId)
        {
            if (_customLabResults.TryGetValue(patientId, out var custom) && custom.Any())
            {
                return Task.FromResult<IEnumerable<LabResultModel>>(custom);
            }

            var data = new List<LabResultModel>();

            if (patientId == Guid.Parse("f2222222-f222-f222-f222-f22222222222") || patientId == Guid.Parse("22222222-2222-2222-2222-222222222222"))
            {
                data.Add(new LabResultModel { PatientId = patientId, TestName = "Allergy IgE Screen", Value = "High", Status = "Attention", TestDate = DateTime.Today.AddDays(-60) });
            }
            else if (patientId == Guid.Parse("f3333333-f333-f333-f333-f33333333333") || patientId == Guid.Parse("33333333-3333-3333-3333-333333333333"))
            {
                data.Add(new LabResultModel { PatientId = patientId, TestName = "Creatinine (Kidney)", Value = "Normal", Status = "Normal", TestDate = DateTime.Today.AddDays(-30) });
                data.Add(new LabResultModel { PatientId = patientId, TestName = "Uric Acid", Value = "High", Status = "Attention", TestDate = DateTime.Today.AddDays(-30) });
            }
            else
            {
                // Thandiwe
                data.Add(new LabResultModel { PatientId = patientId, TestName = "HbA1c", Value = "6.8%", Status = "Attention", TestDate = DateTime.Today.AddDays(-15) });
                data.Add(new LabResultModel { PatientId = patientId, TestName = "Blood pressure", Value = "128/82", Status = "Normal", TestDate = DateTime.Today.AddDays(-15) });
                data.Add(new LabResultModel { PatientId = patientId, TestName = "Cholesterol panel", Value = "Normal", Status = "Normal", TestDate = DateTime.Today.AddDays(-32) });
            }

            return Task.FromResult<IEnumerable<LabResultModel>>(data);
        }

        public Task<bool> AddVaccinationAsync(VaccinationModel vaccination)
        {
            if (!_customVaccinations.ContainsKey(vaccination.PatientId))
            {
                _customVaccinations[vaccination.PatientId] = new List<VaccinationModel>();
            }
            _customVaccinations[vaccination.PatientId].Insert(0, vaccination);
            return Task.FromResult(true);
        }

        public Task<IEnumerable<VaccinationModel>> GetVaccinationHistoryAsync(Guid patientId)
        {
            var data = new List<VaccinationModel>();
            if (_customVaccinations.TryGetValue(patientId, out var custom) && custom.Any())
            {
                data.AddRange(custom);
            }

            if (patientId == Guid.Parse("f2222222-f222-f222-f222-f22222222222") || patientId == Guid.Parse("22222222-2222-2222-2222-222222222222"))
            {
                data.Add(new VaccinationModel { PatientId = patientId, VaccineName = "Measles-Mumps-Rubella (MMR)", Status = "Up to date", Notes = "Completed 2025", DateAdministered = DateTime.Today.AddYears(-1) });
                data.Add(new VaccinationModel { PatientId = patientId, VaccineName = "Polio", Status = "Up to date", Notes = "Completed 2025", DateAdministered = DateTime.Today.AddYears(-1) });
            }
            else if (patientId == Guid.Parse("f3333333-f333-f333-f333-f33333333333") || patientId == Guid.Parse("33333333-3333-3333-3333-333333333333"))
            {
                data.Add(new VaccinationModel { PatientId = patientId, VaccineName = "Pneumococcal", Status = "Up to date", Notes = "Given 2024", DateAdministered = DateTime.Today.AddYears(-2) });
            }
            else if (!data.Any(v => v.VaccineName == "Influenza"))
            {
                // Thandiwe
                data.Add(new VaccinationModel { PatientId = patientId, VaccineName = "Influenza", Status = "Up to date", Notes = "April 2026", DateAdministered = DateTime.Today.AddMonths(-3) });
                data.Add(new VaccinationModel { PatientId = patientId, VaccineName = "Tetanus", Status = "Due", Notes = "Due 2026", DateDue = DateTime.Today.AddMonths(5) });
            }

            return Task.FromResult<IEnumerable<VaccinationModel>>(data);
        }

        private static readonly List<OrderModel> _mockOrders = new()
        {
            new OrderModel { Id = Guid.NewGuid(), OrderNumber = "QP-1082", OrderDate = DateTime.Today.AddDays(-2), TotalAmount = 450.00M, Status = "Delivered", ItemsCount = 2, DispatchedFrom = "Clicks, Claremont" },
            new OrderModel { Id = Guid.NewGuid(), OrderNumber = "QP-0994", OrderDate = DateTime.Today.AddDays(-15), TotalAmount = 1200.50M, Status = "Delivered", ItemsCount = 4, DispatchedFrom = "Dis-Chem, Tokai" }
        };

        public Task<OrderModel?> GetActiveOrderAsync(Guid patientId)
        {
            var active = _mockOrders.FirstOrDefault(o => o.Status == "Dispensed" || o.Status == "Out for delivery");
            return Task.FromResult<OrderModel?>(active);
        }

        public Task<IEnumerable<OrderModel>> GetOrderHistoryAsync(Guid patientId)
        {
            return Task.FromResult<IEnumerable<OrderModel>>(_mockOrders.OrderByDescending(o => o.OrderDate));
        }

        public Task<bool> CreateOrderAsync(OrderModel order)
        {
            _mockOrders.Add(order);
            return Task.FromResult(true);
        }

        public Task<IEnumerable<MarketplaceProductModel>> GetMarketplaceProductsAsync()
        {
            var data = new List<MarketplaceProductModel>
            {
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Vitamin D3 1000IU", Price = 89.00m, IconName = "ShoppingBag", Category = "Vitamins & Supplements", Description = "Essential high-potency vitamin D3 for strong bones and immune resilience. 60 softgels.", InStock = true, Rating = 4.9m, Badge = "Bestseller" },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Omega-3 Fish Oil 1000mg", Price = 179.00m, IconName = "ShoppingBag", Category = "Vitamins & Supplements", Description = "Molecularly distilled pure EPA/DHA essential fatty acids for cardiovascular and joint health. 90 softgels.", InStock = true, Rating = 4.8m, Badge = "Popular" },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Multivitamin Daily Active A-Z", Price = 125.00m, IconName = "ShoppingBag", Category = "Vitamins & Supplements", Description = "Comprehensive daily micronutrient formula with antioxidants and B-complex for vitality. 30 tablets.", InStock = true, Rating = 4.7m },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Effervescent Vitamin C 1000mg + Zinc", Price = 65.00m, IconName = "ShoppingBag", Category = "Vitamins & Supplements", Description = "Fast-absorbing effervescent immune shield with refreshing orange flavour. 20 tablets.", InStock = true, Rating = 4.9m, Badge = "Fast Action" },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Magnesium Complex 400mg", Price = 145.00m, IconName = "ShoppingBag", Category = "Vitamins & Supplements", Description = "High-bioavailability chelated magnesium to support deep sleep, nerve and muscle recovery. 60 capsules.", InStock = true, Rating = 4.8m },

                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Digital Blood Pressure Monitor", Price = 649.00m, IconName = "Activity", Category = "Devices & Monitors", Description = "Clinically validated automatic upper-arm cuff monitor with irregular heartbeat detector and memory.", InStock = true, Rating = 4.9m, Badge = "Top Rated" },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Fingertip Pulse Oximeter", Price = 289.00m, IconName = "Activity", Category = "Devices & Monitors", Description = "Accurate blood oxygen saturation (SpO2) and pulse rate monitor with multi-directional OLED display.", InStock = true, Rating = 4.7m, Badge = "Essential" },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Infrared Forehead Thermometer", Price = 399.00m, IconName = "Activity", Category = "Devices & Monitors", Description = "Non-contact instant 1-second temperature scanner with fever alert color display.", InStock = true, Rating = 4.8m },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Accurate Blood Glucose Monitoring Kit", Price = 485.00m, IconName = "Activity", Category = "Devices & Monitors", Description = "Complete diagnostic starter kit including test meter, painless lancing device, and 25 test strips.", InStock = true, Rating = 4.9m, Badge = "Medical Grade" },

                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "First Aid Emergency Kit (45 Pcs)", Price = 195.00m, IconName = "Package", Category = "First Aid & OTC", Description = "Complete home and travel medical kit packed with sterile dressings, bandages, tweezers and shears.", InStock = true, Rating = 4.8m, Badge = "Household Staple" },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Antiseptic Cleansing Spray 100ml", Price = 54.00m, IconName = "Package", Category = "First Aid & OTC", Description = "Non-stinging painless antiseptic formula for fast wound disinfection, minor cuts and grazes.", InStock = true, Rating = 4.9m },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Hydrocortisone Relief Cream 1%", Price = 68.00m, IconName = "Package", Category = "First Aid & OTC", Description = "Maximum strength topical soothing cream for eczema, minor skin rashes and allergic irritations. 15g.", InStock = true, Rating = 4.7m, Badge = "OTC Relief" },
                new MarketplaceProductModel { Id = Guid.NewGuid(), Name = "Paracetamol 500mg Extra Relief", Price = 38.00m, IconName = "Package", Category = "First Aid & OTC", Description = "Rapid pain and fever relief caplets for headaches, aches and flu symptoms. 24 coated caplets.", InStock = true, Rating = 4.9m, Badge = "Fast Relief" }
            };
            return Task.FromResult<IEnumerable<MarketplaceProductModel>>(data);
        }

        private static readonly List<PaymentMethodModel> _mockPaymentMethods = new()
        {
            new PaymentMethodModel
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.Empty,
                CardType = "Visa",
                LastFour = "4821",
                ExpiryDate = "09/28",
                GatewayToken = "tok_mock123",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            }
        };

        private static readonly List<ClaimModel> _mockClaims = new()
        {
            new ClaimModel
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.Empty,
                Description = "Chronic medication - July",
                Amount = 486.50m,
                Status = "Approved",
                ClaimDate = DateTime.Today.AddDays(-5),
                CreatedAt = DateTime.Today.AddDays(-5)
            },
            new ClaimModel
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.Empty,
                Description = "Consultation - Dr. Adams",
                Amount = 650.00m,
                Status = "Pending",
                ClaimDate = DateTime.Today.AddDays(-10),
                CreatedAt = DateTime.Today.AddDays(-10)
            }
        };

        public Task<MedicalAidModel?> GetMedicalAidDetailsAsync(Guid patientId)
        {
            var data = new MedicalAidModel
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                SchemeName = "Discovery Health",
                PlanName = "Classic Saver",
                MemberNumber = "3820 4471 002",
                AnnualLimit = 10000.00m,
                AmountUsed = 4200.00m,
                Status = "Active",
                CreatedAt = DateTime.Today.AddYears(-2)
            };
            return Task.FromResult<MedicalAidModel?>(data);
        }

        public Task<IEnumerable<ClaimModel>> GetRecentClaimsAsync(Guid patientId)
        {
            return Task.FromResult<IEnumerable<ClaimModel>>(_mockClaims);
        }

        public Task<IEnumerable<PaymentMethodModel>> GetSavedPaymentMethodsAsync(Guid patientId)
        {
            return Task.FromResult<IEnumerable<PaymentMethodModel>>(_mockPaymentMethods);
        }

        public Task<bool> AddPaymentMethodAsync(PaymentMethodModel paymentMethod)
        {
            if (paymentMethod.IsDefault)
            {
                foreach (var pm in _mockPaymentMethods)
                {
                    pm.IsDefault = false;
                }
            }
            _mockPaymentMethods.Add(paymentMethod);
            return Task.FromResult(true);
        }

        public Task<IEnumerable<DummyModel>> GetDummiesAsync()
        {
            var list = new List<DummyModel>
            {
                new DummyModel { Id = 1, Title = "Mock Item 1", Description = "Mock Description 1" }
            };
            return Task.FromResult<IEnumerable<DummyModel>>(list);
        }

        public Task<bool> JoinLobbyAsync(Guid appointmentId, Guid patientId)
            => Task.FromResult(true);

        public Task<bool> LeaveLobbyAsync(Guid appointmentId, Guid patientId)
            => Task.FromResult(true);
    }
}
