using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QPharmacy.Repositories
{
    public class MockDoctorRepository : IDoctorRepository
    {
        private static readonly DoctorKpiModel _kpi = new()
        {
            TodaysAppointments = 6,
            PendingReferrals = 2,
            PrescriptionsIssuedWeek = 31,
            AvgConsultationLengthMinutes = 14
        };

        private static readonly List<DoctorScheduleItemModel> _todaysSchedule = new()
        {
            new DoctorScheduleItemModel
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                PatientId = Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"),
                PatientName = "Thandiwe Mokoena",
                Initials = "TM",
                AvatarBgColor = "bg-emerald-600",
                Time = "09:30",
                AppointmentType = "Follow-up",
                Mode = "Video",
                Status = "Scheduled"
            },
            new DoctorScheduleItemModel
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                PatientId = Guid.Parse("b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e"),
                PatientName = "Jabu Khumalo",
                Initials = "JK",
                AvatarBgColor = "bg-blue-600",
                Time = "10:15",
                AppointmentType = "New patient",
                Mode = "Video",
                Status = "Scheduled"
            },
            new DoctorScheduleItemModel
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                PatientId = Guid.Parse("c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f"),
                PatientName = "Rose Pillay",
                Initials = "RP",
                AvatarBgColor = "bg-amber-500",
                Time = "11:00",
                AppointmentType = "Chronic review",
                Mode = "Video",
                Status = "Scheduled"
            }
        };

        private static readonly List<DoctorPendingActionModel> _pendingActions = new()
        {
            new DoctorPendingActionModel
            {
                Id = Guid.NewGuid(),
                ActionType = "Referral",
                Title = "2 referrals awaiting sign-off",
                Subtitle = "Specialist referrals",
                Count = 2,
                IconType = "Warning"
            },
            new DoctorPendingActionModel
            {
                Id = Guid.NewGuid(),
                ActionType = "Interaction",
                Title = "1 prescription flagged for interaction",
                Subtitle = "Requires review",
                Count = 1,
                IconType = "File"
            }
        };

        private static readonly List<DoctorPatientModel> _patients = new()
        {
            new DoctorPatientModel
            {
                PatientId = Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"),
                PatientName = "Thandiwe Mokoena",
                Age = 41,
                LastVisitDate = "2 Jul 2026",
                ConditionSummary = "Hypertension, T2D",
                MedicalAid = "Discovery Health",
                Initials = "TM",
                Email = "thandiwe@example.com",
                Phone = "082 123 4567"
            },
            new DoctorPatientModel
            {
                PatientId = Guid.Parse("b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e"),
                PatientName = "Jabu Khumalo",
                Age = 34,
                LastVisitDate = "29 Jun 2026",
                ConditionSummary = "New patient",
                MedicalAid = "Bonitas",
                Initials = "JK",
                Email = "jabu@example.com",
                Phone = "083 234 5678"
            },
            new DoctorPatientModel
            {
                PatientId = Guid.Parse("c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f"),
                PatientName = "Rose Pillay",
                Age = 58,
                LastVisitDate = "18 Jun 2026",
                ConditionSummary = "Chronic, cardiac",
                MedicalAid = "Discovery Health",
                Initials = "RP",
                Email = "rose@example.com",
                Phone = "084 345 6789"
            }
        };

        private static readonly Dictionary<Guid, ConsultationSessionModel> _sessions = new()
        {
            [Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d")] = new ConsultationSessionModel
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"),
                PatientName = "Thandiwe Mokoena",
                CallTimerDisplay = "14:22",
                IsLive = true,
                Assessment = "Blood pressure well controlled. HbA1c improved to 6.8%. Continue current regimen.",
                Plan = "Repeat bloods in 4 weeks. Issue digital referral to dietitian.",
                ChatMessages = new List<ConsultationChatMessageModel>
                {
                    new ConsultationChatMessageModel
                    {
                        Id = Guid.NewGuid(),
                        SenderName = "Thandiwe",
                        IsDoctor = false,
                        MessageText = "Should I keep taking the Metformin with the new dosage?",
                        Timestamp = DateTime.Now.AddMinutes(-5)
                    },
                    new ConsultationChatMessageModel
                    {
                        Id = Guid.NewGuid(),
                        SenderName = "You",
                        IsDoctor = true,
                        MessageText = "Yes, continue as prescribed, we will review in 4 weeks.",
                        Timestamp = DateTime.Now.AddMinutes(-2)
                    }
                }
            }
        };

        private static readonly List<DoctorPrescriptionModel> _prescriptions = new()
        {
            new DoctorPrescriptionModel
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"),
                PatientName = "Thandiwe Mokoena",
                PreferredPharmacy = "Clicks, Claremont",
                Medication = "Atorvastatin 20mg",
                DosageDuration = "Nightly, 30 days",
                Status = "Awaiting verification",
                CreatedAt = DateTime.Now.AddHours(-2)
            },
            new DoctorPrescriptionModel
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.Parse("c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f"),
                PatientName = "Rose Pillay",
                PreferredPharmacy = "Dis-Chem, Tokai",
                Medication = "Bisoprolol 5mg",
                DosageDuration = "Once daily, 30 days",
                Status = "Verified",
                CreatedAt = DateTime.Now.AddDays(-1)
            }
        };

        private static readonly List<DoctorCalendarSlotModel> _calendarSlots = new()
        {
            new DoctorCalendarSlotModel { DayLabel = "Mon 6 Jul", MorningStatus = "4 booked", AfternoonStatus = "2 booked" },
            new DoctorCalendarSlotModel { DayLabel = "Tue 7 Jul", MorningStatus = "3 booked", AfternoonStatus = "Open" },
            new DoctorCalendarSlotModel { DayLabel = "Wed 8 Jul", MorningStatus = "Open", AfternoonStatus = "5 booked" }
        };

        public Task<DoctorKpiModel> GetDoctorKpisAsync(Guid doctorId)
        {
            return Task.FromResult(_kpi);
        }

        public Task<IEnumerable<DoctorScheduleItemModel>> GetTodaysScheduleAsync(Guid doctorId)
        {
            return Task.FromResult<IEnumerable<DoctorScheduleItemModel>>(_todaysSchedule);
        }

        public Task<IEnumerable<DoctorPendingActionModel>> GetPendingActionsAsync(Guid doctorId)
        {
            return Task.FromResult<IEnumerable<DoctorPendingActionModel>>(_pendingActions);
        }

        public Task<IEnumerable<DoctorPatientModel>> GetDoctorPatientsAsync(Guid doctorId, string? searchQuery = null)
        {
            var query = _patients.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(p => p.PatientName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                         p.ConditionSummary.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
            }
            return Task.FromResult(query);
        }

        public Task<bool> AddPatientAsync(DoctorPatientModel patient)
        {
            if (patient.PatientId == Guid.Empty) patient.PatientId = Guid.NewGuid();
            if (string.IsNullOrEmpty(patient.Initials) && !string.IsNullOrEmpty(patient.PatientName))
            {
                var parts = patient.PatientName.Split(' ');
                patient.Initials = parts.Length > 1 ? $"{parts[0][0]}{parts[1][0]}" : parts[0][..1];
            }
            _patients.Insert(0, patient);
            return Task.FromResult(true);
        }

        public Task<ConsultationSessionModel> GetConsultationSessionAsync(Guid patientId)
        {
            if (!_sessions.TryGetValue(patientId, out var session))
            {
                var patient = _patients.FirstOrDefault(p => p.PatientId == patientId);
                session = new ConsultationSessionModel
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    PatientName = patient?.PatientName ?? "Patient",
                    CallTimerDisplay = "05:00",
                    IsLive = true,
                    Assessment = "",
                    Plan = "",
                    ChatMessages = new List<ConsultationChatMessageModel>()
                };
                _sessions[patientId] = session;
            }
            return Task.FromResult(session);
        }

        public Task<bool> SaveClinicalNoteAndReferralAsync(Guid consultationId, string assessment, string plan, bool createReferral)
        {
            var session = _sessions.Values.FirstOrDefault(s => s.Id == consultationId);
            if (session != null)
            {
                session.Assessment = assessment;
                session.Plan = plan;
            }

            if (createReferral)
            {
                _kpi.PendingReferrals++;
                var referralAction = _pendingActions.FirstOrDefault(a => a.ActionType == "Referral");
                if (referralAction != null)
                {
                    referralAction.Count = _kpi.PendingReferrals;
                    referralAction.Title = $"{_kpi.PendingReferrals} referrals awaiting sign-off";
                }
            }

            return Task.FromResult(true);
        }

        public Task<bool> CompleteConsultationAsync(Guid consultationId, string assessment, string plan, string? referralNote, DoctorPrescriptionModel? script = null)
        {
            var session = _sessions.Values.FirstOrDefault(s => s.Id == consultationId);
            if (session != null)
            {
                session.Assessment = assessment;
                session.Plan = plan;
                session.ReferralNote = referralNote ?? "";
                session.Status = "Completed";
                session.IsLive = false;
                session.EndedAt = DateTime.UtcNow;
            }

            if (!string.IsNullOrWhiteSpace(referralNote))
            {
                _kpi.PendingReferrals++;
                var referralAction = _pendingActions.FirstOrDefault(a => a.ActionType == "Referral");
                if (referralAction != null)
                {
                    referralAction.Count = _kpi.PendingReferrals;
                    referralAction.Title = $"{_kpi.PendingReferrals} referrals awaiting sign-off";
                }
            }

            if (script != null)
            {
                CreatePrescriptionAsync(script);
            }

            return Task.FromResult(true);
        }

        public Task<bool> SetConsultationLiveStatusAsync(Guid consultationId, bool isLive)
        {
            var session = _sessions.Values.FirstOrDefault(s => s.Id == consultationId);
            if (session != null)
            {
                session.IsLive = isLive;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<ConsultationSessionModel?> GetConsultationSessionByIdAsync(Guid consultationId)
        {
            var session = _sessions.Values.FirstOrDefault(s => s.Id == consultationId);
            return Task.FromResult(session);
        }

        public Task<ConsultationSessionModel?> GetConsultationSessionByAppointmentIdAsync(Guid appointmentId)
        {
            var session = _sessions.Values.FirstOrDefault(s => s.AppointmentId == appointmentId);
            return Task.FromResult(session);
        }

        public Task<bool> SendChatMessageAsync(Guid consultationId, string senderName, string text, bool isDoctor, Guid? senderId = null)
        {
            var session = _sessions.Values.FirstOrDefault(s => s.Id == consultationId);
            if (session != null)
            {
                session.ChatMessages.Add(new ConsultationChatMessageModel
                {
                    Id = Guid.NewGuid(),
                    ConsultationId = consultationId,
                    SenderId = senderId,
                    SenderName = senderName,
                    IsDoctor = isDoctor,
                    MessageText = text,
                    Timestamp = DateTime.Now
                });
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> CreatePrescriptionAsync(DoctorPrescriptionModel prescription)
        {
            _prescriptions.Insert(0, prescription);
            _kpi.PrescriptionsIssuedWeek++;

            // Push to Pharmacist verification queue automatically
            var initials = "P";
            if (!string.IsNullOrEmpty(prescription.PatientName))
            {
                var parts = prescription.PatientName.Split(' ');
                initials = parts.Length > 1 ? $"{parts[0][0]}{parts[1][0]}" : parts[0][..1];
            }

            MockPharmacistRepository.AddMockVerificationQueueItem(new VerificationQueueModel
            {
                Id = prescription.Id,
                PrescriptionNumber = new Random().Next(4825, 9999).ToString(),
                PatientName = prescription.PatientName,
                DoctorName = "Dr. N. Adams",
                MedicationDetails = $"{prescription.Medication}, {prescription.DosageDuration}",
                InteractionStatus = "No conflicts detected",
                IsFlagged = false,
                Initials = initials,
                AvatarBgColor = "bg-teal-600",
                MedicationShort = prescription.Medication,
                Status = "Pending"
            });

            return Task.FromResult(true);
        }

        public Task<IEnumerable<DoctorPrescriptionModel>> GetRecentlyIssuedPrescriptionsAsync(Guid doctorId)
        {
            return Task.FromResult<IEnumerable<DoctorPrescriptionModel>>(_prescriptions);
        }

        internal static void UpdatePrescriptionStatus(Guid id, string status)
        {
            var targetStatus = status == "Approved" ? "Verified" : status;
            var script = _prescriptions.FirstOrDefault(p => p.Id == id);
            if (script != null)
            {
                script.Status = targetStatus;
            }
        }

        public Task<IEnumerable<DoctorCalendarSlotModel>> GetCalendarSlotsAsync(Guid doctorId)
        {
            return Task.FromResult<IEnumerable<DoctorCalendarSlotModel>>(_calendarSlots);
        }

        public Task<bool> BlockCalendarTimeOffAsync(Guid slotId, string period)
        {
            var slot = _calendarSlots.FirstOrDefault(s => s.Id == slotId);
            if (slot != null)
            {
                if (period.Equals("Morning", StringComparison.OrdinalIgnoreCase))
                {
                    slot.IsMorningBlocked = true;
                    slot.MorningStatus = "Blocked";
                }
                else
                {
                    slot.IsAfternoonBlocked = true;
                    slot.AfternoonStatus = "Blocked";
                }
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<IEnumerable<WaitingLobbyPatientModel>> GetWaitingPatientsAsync(Guid doctorId)
        {
            // Mock returns an empty list; real waiting happens via DB
            return Task.FromResult(Enumerable.Empty<WaitingLobbyPatientModel>());
        }

        public Task<ConsultationSessionModel> AcceptPatientConsultationAsync(Guid appointmentId, Guid patientId, Guid doctorId)
        {
            var session = new ConsultationSessionModel
            {
                Id = Guid.NewGuid(),
                AppointmentId = appointmentId,
                PatientId = patientId,
                DoctorId = doctorId,
                PatientName = "Thandiwe Mokoena",
                IsLive = true,
                Status = "InProgress",
                StartedAt = DateTime.UtcNow,
                RoomId = $"room-{patientId.ToString().ToLowerInvariant()}"
            };
            return Task.FromResult(session);
        }

        public Task<bool> MarkMessagesAsReadAsync(Guid consultationId, bool isDoctorViewer)
        {
            return Task.FromResult(true);
        }
    }
}
