using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace QPharmacy.Repositories
{
    public class PatientRepository : IPatientRepository
    {
        private readonly string _connectionString;

        public PatientRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
        }

        // ─────────────────────────────────────────────────────────────────
        // PRESCRIPTIONS
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<PrescriptionModel>> GetPatientPrescriptionsAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);

                return await connection.QueryAsync<PrescriptionModel>(
                    "sp_GetPatientPrescriptions",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
            }
            catch
            {
                await EnsurePrescriptionsTableAsync(connection);
                var sql = @"SELECT Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt, LastDispensedDate, SupplyDays, RepeatsRemaining
                            FROM Prescriptions
                            WHERE PatientId = @PatientId OR @PatientId = '00000000-0000-0000-0000-000000000000'";
                return await connection.QueryAsync<PrescriptionModel>(sql, new { PatientId = patientId });
            }
        }

        public async Task<IEnumerable<PrescriptionModel>> GetPrescriptionsByStatusAsync(Guid patientId, string statusGroup)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);
                parameters.Add("@StatusGroup", statusGroup); // "Active" | "History" | "Pending"

                return await connection.QueryAsync<PrescriptionModel>(
                    "sp_GetPrescriptionsByStatus",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
            }
            catch
            {
                await EnsurePrescriptionsTableAsync(connection);
                var filterSql = statusGroup switch
                {
                    "Active"  => "WHERE Status = 'Verified'",
                    "Pending" => "WHERE Status IN ('Awaiting pharmacist', 'Uploaded')",
                    "History" => "WHERE Status = 'Expired'",
                    _         => ""
                };

                var sql = $@"SELECT Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt, LastDispensedDate, SupplyDays, RepeatsRemaining
                             FROM Prescriptions
                             {filterSql}";
                return await connection.QueryAsync<PrescriptionModel>(sql);
            }
        }

        public async Task<bool> UploadPrescriptionAsync(PrescriptionModel prescription)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@Id", prescription.Id);
                parameters.Add("@PatientId", prescription.PatientId);
                parameters.Add("@MedicationName", prescription.MedicationName);
                parameters.Add("@Dosage", prescription.Dosage);
                parameters.Add("@PrescribedBy", prescription.PrescribedBy);
                parameters.Add("@PharmacyName", prescription.PharmacyName);
                parameters.Add("@Status", prescription.Status);

                var rows = await connection.ExecuteAsync(
                    "sp_UploadPrescription",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
                return rows > 0;
            }
            catch
            {
                await EnsurePrescriptionsTableAsync(connection);
                var sqlRx = @"
                    INSERT INTO Prescriptions (Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt, SupplyDays, RepeatsRemaining)
                    VALUES (@Id, @PatientId, @MedicationName, @Dosage, @PrescribedBy, @PharmacyName, @Status, GETUTCDATE(), 30, 2);";

                var sqlVq = @"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'VerificationQueue')
                    BEGIN
                        INSERT INTO VerificationQueue (Id, PrescriptionNumber, PatientName, DoctorName, MedicationDetails, InteractionStatus, IsFlagged, Initials, AvatarBgColor, MedicationShort, Status, CreatedAt)
                        VALUES (@Id, RIGHT(CAST(ABS(CHECKSUM(@Id)) AS VARCHAR(50)), 4), 'Patient', @PrescribedBy, @MedicationName + ', ' + @Dosage, 'No conflicts detected', 0, 'PT', 'bg-teal-600', @MedicationName, 'Pending', GETUTCDATE());
                    END;";

                await connection.ExecuteAsync(sqlRx, prescription);
                await connection.ExecuteAsync(sqlVq, new { Id = prescription.Id, PrescribedBy = prescription.PrescribedBy, MedicationName = prescription.MedicationName, Dosage = prescription.Dosage });
                return true;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // MEDICATION SCHEDULE
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<MedicationScheduleModel>> GetMedicationScheduleTodayAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            await EnsureMedicationSchedulesTableAsync(connection);

            try
            {
                // First check if schedules exist for today
                var selectTodaySql = @"
                    SELECT Id, PatientId, MedicationName, Dosage, Instructions, ScheduledTime, Status, TakenAt, Notes
                    FROM MedicationSchedules
                    WHERE (PatientId = @PatientId OR @PatientId = '00000000-0000-0000-0000-000000000000')
                      AND CAST(ScheduledTime AS DATE) = CAST(GETDATE() AS DATE)
                    ORDER BY ScheduledTime ASC;";
                var todayResults = (await connection.QueryAsync<MedicationScheduleModel>(selectTodaySql, new { PatientId = patientId })).ToList();

                if (todayResults.Any())
                {
                    return todayResults;
                }

                // If none for today, auto-generate today's schedule from active verified prescriptions
                // Only include:
                //   (a) Old-style prescriptions not linked to a WorkflowPrescriptionItem (pharmacist verified via VerificationQueue)
                //   (b) WorkflowPrescription-linked prescriptions whose order was actually Paid
                var verifiedPrescriptions = await connection.QueryAsync<PrescriptionModel>(@"
                    SELECT p.Id, p.PatientId, p.MedicationName, p.Dosage, Instructions = p.Dosage, p.Status
                    FROM Prescriptions p
                    WHERE (p.PatientId = @PatientId OR @PatientId = '00000000-0000-0000-0000-000000000000')
                      AND p.Status = 'Verified'
                      AND (
                          -- Legacy verification queue prescriptions (not linked to any WorkflowPrescriptionItem)
                          NOT EXISTS (SELECT 1 FROM WorkflowPrescriptionItems wi WHERE wi.Id = p.Id)
                          OR
                          -- WorkflowPrescription-linked: only if the linked order was paid
                          EXISTS (
                              SELECT 1
                              FROM WorkflowPrescriptionItems wi
                              INNER JOIN WorkflowPrescriptions wp ON wp.Id = wi.PrescriptionId
                              INNER JOIN Orders o ON o.Id = wp.Id
                              WHERE wi.Id = p.Id AND o.PaymentStatus = 'Paid'
                          )
                      );", new { PatientId = patientId });

                if (verifiedPrescriptions != null && verifiedPrescriptions.Any())
                {
                    var insertDoseSql = @"
                        INSERT INTO MedicationSchedules (Id, PatientId, MedicationName, Dosage, Instructions, ScheduledTime, Status)
                        VALUES (@Id, @PatientId, @MedicationName, @Dosage, @Instructions, @ScheduledTime, 'Upcoming');";

                    foreach (var rx in verifiedPrescriptions)
                    {
                        var dosage = rx.Dosage ?? "";
                        if (dosage.IndexOf("Twice", StringComparison.OrdinalIgnoreCase) >= 0 || dosage.IndexOf("2x", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            await connection.ExecuteAsync(insertDoseSql, new { Id = Guid.NewGuid(), PatientId = patientId, MedicationName = rx.MedicationName, Dosage = rx.Dosage, Instructions = "Take 1 dose with food", ScheduledTime = DateTime.Today.AddHours(8) });
                            await connection.ExecuteAsync(insertDoseSql, new { Id = Guid.NewGuid(), PatientId = patientId, MedicationName = rx.MedicationName, Dosage = rx.Dosage, Instructions = "Take 1 dose with food", ScheduledTime = DateTime.Today.AddHours(20) });
                        }
                        else if (dosage.IndexOf("3x", StringComparison.OrdinalIgnoreCase) >= 0 || dosage.IndexOf("Three", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            await connection.ExecuteAsync(insertDoseSql, new { Id = Guid.NewGuid(), PatientId = patientId, MedicationName = rx.MedicationName, Dosage = rx.Dosage, Instructions = "Take 1 dose with water", ScheduledTime = DateTime.Today.AddHours(8) });
                            await connection.ExecuteAsync(insertDoseSql, new { Id = Guid.NewGuid(), PatientId = patientId, MedicationName = rx.MedicationName, Dosage = rx.Dosage, Instructions = "Take 1 dose with water", ScheduledTime = DateTime.Today.AddHours(14) });
                            await connection.ExecuteAsync(insertDoseSql, new { Id = Guid.NewGuid(), PatientId = patientId, MedicationName = rx.MedicationName, Dosage = rx.Dosage, Instructions = "Take 1 dose with water", ScheduledTime = DateTime.Today.AddHours(20) });
                        }
                        else if (dosage.IndexOf("Night", StringComparison.OrdinalIgnoreCase) >= 0 || dosage.IndexOf("Bedtime", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            await connection.ExecuteAsync(insertDoseSql, new { Id = Guid.NewGuid(), PatientId = patientId, MedicationName = rx.MedicationName, Dosage = rx.Dosage, Instructions = "Take 1 dose at bedtime", ScheduledTime = DateTime.Today.AddHours(21) });
                        }
                        else
                        {
                            await connection.ExecuteAsync(insertDoseSql, new { Id = Guid.NewGuid(), PatientId = patientId, MedicationName = rx.MedicationName, Dosage = rx.Dosage, Instructions = "Take 1 dose in the morning", ScheduledTime = DateTime.Today.AddHours(8) });
                        }
                    }

                    return await connection.QueryAsync<MedicationScheduleModel>(selectTodaySql, new { PatientId = patientId });
                }

                // Fallback to any schedules in table
                var fallbackSql = @"SELECT Id, PatientId, MedicationName, Dosage, Instructions, ScheduledTime, Status, TakenAt, Notes
                                    FROM MedicationSchedules
                                    WHERE PatientId = @PatientId OR @PatientId = '00000000-0000-0000-0000-000000000000'
                                    ORDER BY ScheduledTime ASC;";
                return await connection.QueryAsync<MedicationScheduleModel>(fallbackSql, new { PatientId = patientId });
            }
            catch
            {
                return await new MockPatientRepository().GetMedicationScheduleTodayAsync(patientId);
            }
        }

        public async Task<bool> MarkDoseAsTakenAsync(Guid scheduleId, Guid patientId)
        {
            await new MockPatientRepository().MarkDoseAsTakenAsync(scheduleId, patientId);

            using var connection = new SqlConnection(_connectionString);
            try
            {
                var updateSql = @"UPDATE MedicationSchedules SET Status = 'Taken', TakenAt = GETDATE(), Notes = NULL WHERE Id = @ScheduleId;";
                var rows = await connection.ExecuteAsync(updateSql, new { ScheduleId = scheduleId });
                return rows > 0;
            }
            catch
            {
                return true;
            }
        }

        public async Task<bool> MarkDoseAsSkippedAsync(Guid scheduleId, Guid patientId, string? reason = null)
        {
            await new MockPatientRepository().MarkDoseAsSkippedAsync(scheduleId, patientId, reason);

            using var connection = new SqlConnection(_connectionString);
            try
            {
                var updateSql = @"UPDATE MedicationSchedules SET Status = 'Skipped', TakenAt = NULL, Notes = @Notes WHERE Id = @ScheduleId;";
                var rows = await connection.ExecuteAsync(updateSql, new { ScheduleId = scheduleId, Notes = string.IsNullOrWhiteSpace(reason) ? "Skipped by patient" : reason });
                return rows > 0;
            }
            catch
            {
                return true;
            }
        }

        public async Task<bool> UndoDoseStatusAsync(Guid scheduleId, Guid patientId)
        {
            await new MockPatientRepository().UndoDoseStatusAsync(scheduleId, patientId);

            using var connection = new SqlConnection(_connectionString);
            try
            {
                var updateSql = @"UPDATE MedicationSchedules SET Status = 'Upcoming', TakenAt = NULL, Notes = NULL WHERE Id = @ScheduleId;";
                var rows = await connection.ExecuteAsync(updateSql, new { ScheduleId = scheduleId });
                return rows > 0;
            }
            catch
            {
                return true;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // DASHBOARD
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<ActivityLogModel>> GetPatientActivityLogAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);

                var results = await connection.QueryAsync<ActivityLogModel>(
                    "sp_GetPatientActivityLog",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
                if (results != null && results.Any()) return results;
            }
            catch { }

            await EnsureActivityLogsTableAsync(connection);
            var sql = @"SELECT Id, PatientId, Description, EventType, CreatedAt FROM ActivityLogs ORDER BY CreatedAt DESC";
            return await connection.QueryAsync<ActivityLogModel>(sql);
        }

        public async Task<AppointmentModel?> GetNextAppointmentAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);

                var appt = await connection.QueryFirstOrDefaultAsync<AppointmentModel>(
                    "sp_GetNextAppointment",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
                if (appt != null) return appt;
            }
            catch { }

            await EnsureAppointmentsTableAsync(connection);
            try
            {
                var sql = @"SELECT TOP 1 Id, PatientId, DoctorName, Specialty, AppointmentDate, Status FROM Appointments WHERE AppointmentDate >= GETUTCDATE() ORDER BY AppointmentDate ASC";
                var dbAppt = await connection.QueryFirstOrDefaultAsync<AppointmentModel>(sql);
                if (dbAppt != null) return dbAppt;
            }
            catch { }

            return new AppointmentModel
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                DoctorName = "Dr. Sarah Jenkins",
                Specialty = "Cardiologist",
                AppointmentDate = DateTime.Now.AddMinutes(30),
                Status = "Scheduled"
            };
        }

        public async Task<IEnumerable<AppointmentModel>> GetPatientAppointmentsAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            await EnsureAppointmentsTableAsync(connection);
            try
            {
                var sql = @"SELECT Id, PatientId, DoctorId, DoctorName, Specialty, AppointmentDate, Status, Mode, Notes, CreatedAt 
                            FROM Appointments 
                            WHERE PatientId = @PatientId 
                            ORDER BY AppointmentDate DESC";
                var list = await connection.QueryAsync<AppointmentModel>(sql, new { PatientId = patientId });
                if (list.Any()) return list;
            }
            catch { }

            return new List<AppointmentModel>
            {
                new AppointmentModel
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    DoctorName = "Dr. Sarah Jenkins",
                    Specialty = "Cardiology & Primary Care",
                    AppointmentDate = DateTime.Now.AddDays(1).Date.AddHours(10),
                    Status = "Scheduled",
                    Mode = "Video",
                    Notes = "Routine follow-up & blood pressure management"
                }
            };
        }

        public async Task<Guid> BookAppointmentAsync(AppointmentModel appointment, string patientName)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                if (appointment.Id == Guid.Empty) appointment.Id = Guid.NewGuid();

                // 1. Insert into Appointments
                const string insertAppt = @"
                    INSERT INTO Appointments (Id, PatientId, DoctorId, DoctorName, Specialty, AppointmentDate, Status, Mode, Notes, CreatedAt)
                    VALUES (@Id, @PatientId, @DoctorId, @DoctorName, @Specialty, @AppointmentDate, @Status, @Mode, @Notes, SYSUTCDATETIME());";
                await connection.ExecuteAsync(insertAppt, appointment, transaction);

                // 2. Compute Initials
                var initials = "PT";
                if (!string.IsNullOrWhiteSpace(patientName))
                {
                    var parts = patientName.Trim().Split(' ');
                    initials = parts.Length > 1 ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant() : patientName[..Math.Min(2, patientName.Length)].ToUpperInvariant();
                }

                // 3. Insert into DoctorSchedules
                const string insertSched = @"
                    INSERT INTO DoctorSchedules (Id, DoctorId, PatientId, PatientName, Initials, AvatarBgColor, [Time], AppointmentType, Mode, Status, AppointmentDate, AppointmentId)
                    VALUES (NEWID(), @DoctorId, @PatientId, @PatientName, @Initials, 'bg-teal-600', @Time, @AppointmentType, @Mode, 'Scheduled', @AppointmentDate, @AppointmentId);";
                await connection.ExecuteAsync(insertSched, new
                {
                    DoctorId = appointment.DoctorId,
                    appointment.PatientId,
                    PatientName = patientName,
                    Initials = initials,
                    Time = appointment.AppointmentDate.ToString("HH:mm"),
                    AppointmentType = string.IsNullOrWhiteSpace(appointment.Notes) ? "Telehealth Consult" : appointment.Notes,
                    appointment.Mode,
                    appointment.AppointmentDate,
                    AppointmentId = appointment.Id
                }, transaction);

                // 4. Create DoctorConsultations record
                var consultId = Guid.NewGuid();
                const string insertConsult = @"
                    INSERT INTO DoctorConsultations (Id, AppointmentId, PatientId, DoctorId, DoctorName, PatientName, CallTimerDisplay, IsLive, Status, Assessment, [Plan], CreatedAt)
                    VALUES (@Id, @AppointmentId, @PatientId, @DoctorId, @DoctorName, @PatientName, '00:00', 0, 'Scheduled', '', '', SYSUTCDATETIME());";
                await connection.ExecuteAsync(insertConsult, new
                {
                    Id = consultId,
                    AppointmentId = appointment.Id,
                    appointment.PatientId,
                    DoctorId = appointment.DoctorId,
                    appointment.DoctorName,
                    PatientName = patientName
                }, transaction);

                transaction.Commit();
                return appointment.Id;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> CancelAppointmentAsync(Guid appointmentId, Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                await connection.ExecuteAsync("UPDATE Appointments SET Status = 'Cancelled' WHERE Id = @Id AND PatientId = @PatientId", new { Id = appointmentId, PatientId = patientId }, transaction);
                await connection.ExecuteAsync("UPDATE DoctorSchedules SET Status = 'Cancelled' WHERE AppointmentId = @Id", new { Id = appointmentId }, transaction);
                await connection.ExecuteAsync("UPDATE DoctorConsultations SET Status = 'Cancelled', IsLive = 0 WHERE AppointmentId = @Id", new { Id = appointmentId }, transaction);
                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                return false;
            }
        }

        public async Task<ConsultationSessionModel?> GetActiveConsultationForPatientAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                const string sql = @"
                    SELECT TOP 1 Id, AppointmentId, PatientId, DoctorId, DoctorName, PatientName, CallTimerDisplay, IsLive, Status, Assessment, [Plan], ReferralNote, StartedAt, EndedAt, CreatedAt
                    FROM DoctorConsultations
                    WHERE PatientId = @PatientId AND Status IN ('InProgress', 'WaitingInLobby', 'Scheduled')
                    ORDER BY CASE WHEN Status = 'InProgress' THEN 1 WHEN Status = 'WaitingInLobby' THEN 2 ELSE 3 END, CreatedAt DESC";
                var session = await connection.QueryFirstOrDefaultAsync<ConsultationSessionModel>(sql, new { PatientId = patientId });
                if (session == null)
                {
                    var appt = await GetNextAppointmentAsync(patientId);
                    if (appt != null)
                    {
                        session = new ConsultationSessionModel
                        {
                            Id = Guid.NewGuid(),
                            AppointmentId = appt.Id,
                            PatientId = patientId,
                            DoctorId = appt.DoctorId,
                            DoctorName = appt.DoctorName,
                            PatientName = "Patient",
                            CallTimerDisplay = "00:00",
                            IsLive = false,
                            Status = "Scheduled",
                            StartedAt = null,
                            CreatedAt = DateTime.UtcNow
                        };
                        await connection.ExecuteAsync(@"
                            INSERT INTO DoctorConsultations (Id, AppointmentId, PatientId, DoctorId, DoctorName, PatientName, CallTimerDisplay, IsLive, Status, StartedAt, CreatedAt)
                            VALUES (@Id, @AppointmentId, @PatientId, @DoctorId, @DoctorName, @PatientName, @CallTimerDisplay, 0, 'Scheduled', NULL, GETUTCDATE())", session);
                    }
                }

                if (session != null)
                {
                    session.RoomId = session.AppointmentId.HasValue && session.AppointmentId.Value != Guid.Empty
                        ? $"room-consultation-{session.AppointmentId.Value.ToString().ToLowerInvariant()}"
                        : $"room-{session.PatientId.ToString().ToLowerInvariant()}";

                    if (session.Status == "InProgress" && session.StartedAt == null)
                    {
                        session.StartedAt = DateTime.UtcNow;
                        await connection.ExecuteAsync("UPDATE DoctorConsultations SET StartedAt = @StartedAt WHERE Id = @Id", new { StartedAt = session.StartedAt, session.Id });
                    }

                    var msgSql = "SELECT Id, ConsultationId, SenderId, SenderName, IsDoctor, MessageText, Timestamp, ISNULL(IsRead, 0) AS IsRead FROM DoctorConsultationMessages WHERE ConsultationId = @ConsultationId ORDER BY Timestamp ASC";
                    var messages = await connection.QueryAsync<ConsultationChatMessageModel>(msgSql, new { ConsultationId = session.Id });
                    session.ChatMessages = messages.ToList();
                }

                return session;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> JoinLobbyAsync(Guid appointmentId, Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var rows = await connection.ExecuteAsync(@"
                    UPDATE Appointments 
                    SET Status = 'WaitingInLobby' 
                    WHERE (Id = @Id OR (@Id = '00000000-0000-0000-0000-000000000000' AND PatientId = @PatientId))
                      AND (@PatientId = '00000000-0000-0000-0000-000000000000' OR PatientId = @PatientId);

                    UPDATE DoctorSchedules 
                    SET Status = 'WaitingInLobby' 
                    WHERE (AppointmentId = @Id OR (@Id = '00000000-0000-0000-0000-000000000000' AND PatientId = @PatientId));

                    UPDATE DoctorConsultations 
                    SET Status = 'WaitingInLobby' 
                    WHERE (AppointmentId = @Id OR (@Id = '00000000-0000-0000-0000-000000000000' AND PatientId = @PatientId))
                      AND Status IN ('Scheduled', 'WaitingInLobby');",
                    new { Id = appointmentId, PatientId = patientId });

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Handles patient leaving consultation lobby
        public async Task<bool> LeaveLobbyAsync(Guid appointmentId, Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var rows = await connection.ExecuteAsync(@"
                    UPDATE Appointments 
                    SET Status = 'Confirmed' 
                    WHERE (Id = @Id OR (@Id = '00000000-0000-0000-0000-000000000000' AND PatientId = @PatientId))
                      AND Status = 'WaitingInLobby';

                    UPDATE DoctorSchedules 
                    SET Status = 'Scheduled' 
                    WHERE (AppointmentId = @Id OR (@Id = '00000000-0000-0000-0000-000000000000' AND PatientId = @PatientId))
                      AND Status = 'WaitingInLobby';

                    UPDATE DoctorConsultations 
                    SET Status = 'Scheduled' 
                    WHERE (AppointmentId = @Id OR (@Id = '00000000-0000-0000-0000-000000000000' AND PatientId = @PatientId))
                      AND Status = 'WaitingInLobby';",
                    new { Id = appointmentId, PatientId = patientId });

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<DashboardKpiModel?> GetDashboardKpisAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);

                var kpis = await connection.QueryFirstOrDefaultAsync<DashboardKpiModel>(
                    "sp_GetDashboardKpis",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
                if (kpis != null) return kpis;
            }
            catch { }

            try
            {
                var dosesSql = @"
                    SELECT 
                        ISNULL(SUM(CASE WHEN Status = 'Taken' THEN 1 ELSE 0 END), 0) AS DosesTakenToday,
                        COUNT(*) AS DosesScheduledToday
                    FROM MedicationSchedules
                    WHERE (PatientId = @PatientId OR @PatientId = '00000000-0000-0000-0000-000000000000')
                      AND CAST(ScheduledTime AS DATE) = CAST(GETDATE() AS DATE);";
                var doseStats = await connection.QueryFirstOrDefaultAsync<(int DosesTakenToday, int DosesScheduledToday)>(dosesSql, new { PatientId = patientId });

                var activeRxCount = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM Prescriptions
                    WHERE (PatientId = @PatientId OR @PatientId = '00000000-0000-0000-0000-000000000000')
                      AND Status = 'Verified';", new { PatientId = patientId });

                var familyCount = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM FamilyMembers
                    WHERE (PrimaryPatientId = @PatientId OR @PatientId = '00000000-0000-0000-0000-000000000000');", new { PatientId = patientId });

                return new DashboardKpiModel
                {
                    DosesTakenToday = doseStats.DosesTakenToday,
                    DosesScheduledToday = doseStats.DosesScheduledToday > 0 ? doseStats.DosesScheduledToday : 4,
                    NextRefillDays = 12,
                    ActivePrescriptions = activeRxCount > 0 ? activeRxCount : 3,
                    FamilyMembersLinked = familyCount > 0 ? familyCount : 2
                };
            }
            catch
            {
                return await new MockPatientRepository().GetDashboardKpisAsync(patientId);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // SQL TABLE AUTO-CREATION & SEEDING HELPERS
        // ─────────────────────────────────────────────────────────────────

        private static async Task EnsurePrescriptionsTableAsync(SqlConnection connection)
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Prescriptions')
                BEGIN
                    CREATE TABLE Prescriptions (
                        Id UNIQUEIDENTIFIER PRIMARY KEY,
                        PatientId UNIQUEIDENTIFIER NOT NULL,
                        MedicationName NVARCHAR(200) NOT NULL,
                        Dosage NVARCHAR(200) NOT NULL,
                        PrescribedBy NVARCHAR(100) NOT NULL,
                        PharmacyName NVARCHAR(100) NOT NULL,
                        Status NVARCHAR(50) NOT NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        LastDispensedDate DATETIME2 NULL,
                        SupplyDays INT NOT NULL DEFAULT 30,
                        RepeatsRemaining INT NOT NULL DEFAULT 2
                    );
                    INSERT INTO Prescriptions (Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt, LastDispensedDate, SupplyDays, RepeatsRemaining)
                    VALUES ('p1111111-1111-1111-1111-111111111111', '00000000-0000-0000-0000-000000000000', 'Amlodipine 5mg', 'Once daily - 30 tablets', 'Dr. N. Adams', 'Clicks, Claremont', 'Verified', DATEADD(day, -30, GETUTCDATE()), DATEADD(day, -25, GETUTCDATE()), 30, 2),
                           ('p2222222-2222-2222-2222-222222222222', '00000000-0000-0000-0000-000000000000', 'Metformin 500mg', 'Twice daily - 60 tablets', 'Dr. N. Adams', 'Clicks, Claremont', 'Verified', DATEADD(day, -60, GETUTCDATE()), DATEADD(day, -10, GETUTCDATE()), 30, 1),
                           ('p3333333-3333-3333-3333-333333333333', '00000000-0000-0000-0000-000000000000', 'Atorvastatin 20mg', 'Nightly - 30 tablets', 'Dr. N. Adams', 'Clicks, Claremont', 'Awaiting pharmacist', DATEADD(day, -15, GETUTCDATE()), NULL, 30, 3),
                           ('p4444444-4444-4444-4444-444444444444', '00000000-0000-0000-0000-000000000000', 'Cetirizine 10mg', 'As needed - 20 tablets', 'Self-uploaded', 'Dis-Chem, Tokai', 'Uploaded', DATEADD(day, -5, GETUTCDATE()), NULL, 30, 0),
                           ('p5555555-5555-5555-5555-555555555555', '00000000-0000-0000-0000-000000000000', 'Amoxicillin 500mg', '3x daily - 21 capsules', 'Dr. T. Khumalo', 'Dis-Chem, Tokai', 'Expired', DATEADD(day, -120, GETUTCDATE()), NULL, 7, 0);
                END;";
            await connection.ExecuteAsync(sql);
        }

        private static async Task EnsureMedicationSchedulesTableAsync(SqlConnection connection)
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicationSchedules')
                BEGIN
                    CREATE TABLE MedicationSchedules (
                        Id UNIQUEIDENTIFIER PRIMARY KEY,
                        PatientId UNIQUEIDENTIFIER NOT NULL,
                        MedicationName NVARCHAR(200) NOT NULL,
                        ScheduledTime DATETIME2 NOT NULL,
                        Status NVARCHAR(50) NOT NULL DEFAULT 'Upcoming'
                    );
                    INSERT INTO MedicationSchedules (Id, PatientId, MedicationName, ScheduledTime, Status)
                    VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '00000000-0000-0000-0000-000000000000', 'Amlodipine 5mg', DATEADD(hour, 8, CAST(CAST(GETDATE() AS DATE) AS DATETIME2)), 'Taken'),
                           ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '00000000-0000-0000-0000-000000000000', 'Metformin 500mg', DATEADD(hour, 8, CAST(CAST(GETDATE() AS DATE) AS DATETIME2)), 'Taken'),
                           ('cccccccc-cccc-cccc-cccc-cccccccccccc', '00000000-0000-0000-0000-000000000000', 'Metformin 500mg', DATEADD(hour, 20, CAST(CAST(GETDATE() AS DATE) AS DATETIME2)), 'Upcoming'),
                           ('dddddddd-dddd-dddd-dddd-dddddddddddd', '00000000-0000-0000-0000-000000000000', 'Atorvastatin 20mg', DATEADD(hour, 21, CAST(CAST(GETDATE() AS DATE) AS DATETIME2)), 'Upcoming');
                END;";
            await connection.ExecuteAsync(sql);
        }

        private static async Task EnsureActivityLogsTableAsync(SqlConnection connection)
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ActivityLogs')
                BEGIN
                    CREATE TABLE ActivityLogs (
                        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        PatientId UNIQUEIDENTIFIER NOT NULL,
                        Description NVARCHAR(500) NOT NULL,
                        EventType NVARCHAR(50) NOT NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                    INSERT INTO ActivityLogs (PatientId, Description, EventType, CreatedAt)
                    VALUES ('00000000-0000-0000-0000-000000000000', 'Prescription for Atorvastatin 20mg uploaded', 'Upload', DATEADD(hour, -2, GETUTCDATE())),
                           ('00000000-0000-0000-0000-000000000000', 'Medication order #1042 dispatched', 'Order', DATEADD(hour, -24, GETUTCDATE()));
                END;";
            await connection.ExecuteAsync(sql);
        }

        private static async Task EnsureAppointmentsTableAsync(SqlConnection connection)
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Appointments')
                BEGIN
                    CREATE TABLE Appointments (
                        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        PatientId UNIQUEIDENTIFIER NOT NULL,
                        DoctorName NVARCHAR(100) NOT NULL,
                        Specialty NVARCHAR(100) NOT NULL,
                        AppointmentDate DATETIME2 NOT NULL,
                        Status NVARCHAR(50) NOT NULL DEFAULT 'Scheduled'
                    );
                    INSERT INTO Appointments (PatientId, DoctorName, Specialty, AppointmentDate, Status)
                    VALUES ('00000000-0000-0000-0000-000000000000', 'Dr. Sarah Jenkins', 'Cardiologist', DATEADD(day, 3, GETUTCDATE()), 'Scheduled');
                END;";
            await connection.ExecuteAsync(sql);
        }

        // ─────────────────────────────────────────────────────────────────
        // RECORDS
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<FamilyMemberModel>> GetFamilyMembersAsync(Guid primaryPatientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PrimaryPatientId", primaryPatientId);

                return await connection.QueryAsync<FamilyMemberModel>(
                    "sp_GetFamilyMembers",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
            }
            catch
            {
                try
                {
                    var createSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyMembers')
                        BEGIN
                            CREATE TABLE FamilyMembers (
                                Id UNIQUEIDENTIFIER PRIMARY KEY,
                                PrimaryPatientId UNIQUEIDENTIFIER NOT NULL,
                                FirstName NVARCHAR(100) NOT NULL,
                                LastName NVARCHAR(100) NOT NULL,
                                Relationship NVARCHAR(50) NOT NULL,
                                CanView BIT NOT NULL DEFAULT 1,
                                CanEdit BIT NOT NULL DEFAULT 0,
                                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                            );
                            INSERT INTO FamilyMembers (Id, PrimaryPatientId, FirstName, LastName, Relationship, CanView, CanEdit)
                            VALUES ('f1111111-f111-f111-f111-f11111111111', '00000000-0000-0000-0000-000000000000', 'Thandiwe', 'Mokoena', 'Self', 1, 1),
                                   ('f2222222-f222-f222-f222-f22222222222', '00000000-0000-0000-0000-000000000000', 'Ntando', 'Mokoena', 'Child', 1, 0);
                        END;";
                    await connection.ExecuteAsync(createSql);

                    var selectSql = @"SELECT Id, PrimaryPatientId, FirstName, LastName, Relationship, CanView, CanEdit
                                      FROM FamilyMembers
                                      WHERE PrimaryPatientId = @PrimaryPatientId OR @PrimaryPatientId = '00000000-0000-0000-0000-000000000000'";
                    return await connection.QueryAsync<FamilyMemberModel>(selectSql, new { PrimaryPatientId = primaryPatientId });
                }
                catch
                {
                    return await new MockPatientRepository().GetFamilyMembersAsync(primaryPatientId);
                }
            }
        }

        public async Task<bool> CanAccessPatientAsync(Guid primaryPatientId, Guid patientId, bool requireEdit)
        {
            if (primaryPatientId == patientId) return true;
            using var connection = new SqlConnection(_connectionString);
            const string sql = """
                SELECT CAST(CASE WHEN EXISTS
                (
                    SELECT 1 FROM FamilyMembers
                    WHERE Id = @PatientId AND PrimaryPatientId = @PrimaryPatientId
                      AND CanView = 1 AND (@RequireEdit = 0 OR CanEdit = 1)
                ) THEN 1 ELSE 0 END AS bit);
                """;
            return await connection.ExecuteScalarAsync<bool>(sql, new { PrimaryPatientId = primaryPatientId, PatientId = patientId, RequireEdit = requireEdit });
        }

        public async Task<bool> AddFamilyMemberAsync(FamilyMemberModel member)
        {
            // Sync in-memory fallback
            await new MockPatientRepository().AddFamilyMemberAsync(member);

            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@Id", member.Id);
                parameters.Add("@PrimaryPatientId", member.PrimaryPatientId);
                parameters.Add("@FirstName", member.FirstName);
                parameters.Add("@LastName", member.LastName);
                parameters.Add("@Relationship", member.Relationship);
                parameters.Add("@CanView", member.CanView);
                parameters.Add("@CanEdit", member.CanEdit);

                var rows = await connection.ExecuteAsync(
                    "sp_AddFamilyMember",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
                return rows > 0;
            }
            catch
            {
                try
                {
                    var createTableSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyMembers')
                        BEGIN
                            CREATE TABLE FamilyMembers (
                                Id UNIQUEIDENTIFIER PRIMARY KEY,
                                PrimaryPatientId UNIQUEIDENTIFIER NOT NULL,
                                FirstName NVARCHAR(100) NOT NULL,
                                LastName NVARCHAR(100) NOT NULL,
                                Relationship NVARCHAR(50) NOT NULL,
                                CanView BIT NOT NULL DEFAULT 1,
                                CanEdit BIT NOT NULL DEFAULT 0,
                                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                            );
                        END;";
                    await connection.ExecuteAsync(createTableSql);

                    var insertSql = @"
                        INSERT INTO FamilyMembers (Id, PrimaryPatientId, FirstName, LastName, Relationship, CanView, CanEdit)
                        VALUES (@Id, @PrimaryPatientId, @FirstName, @LastName, @Relationship, @CanView, @CanEdit);";
                    
                    var rows = await connection.ExecuteAsync(insertSql, member);
                    return rows > 0;
                }
                catch
                {
                    return true;
                }
            }
        }

        public async Task<IEnumerable<MedicalConditionModel>> GetMedicalConditionsAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);

                var results = await connection.QueryAsync<MedicalConditionModel>(
                    "sp_GetMedicalConditions",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
                if (results != null && results.Any())
                {
                    return results;
                }
                return await new MockPatientRepository().GetMedicalConditionsAsync(patientId);
            }
            catch
            {
                return await new MockPatientRepository().GetMedicalConditionsAsync(patientId);
            }
        }

        public async Task<bool> AddMedicalConditionAsync(MedicalConditionModel condition)
        {
            await new MockPatientRepository().AddMedicalConditionAsync(condition);
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var condId = condition.Id == Guid.Empty ? Guid.NewGuid() : condition.Id;
                var recordedAt = condition.RecordedAt == default ? DateTime.Now : condition.RecordedAt;
                var sql = @"
INSERT INTO dbo.MedicalConditions (Id, PatientId, Name, Type, Notes, RecordedAt)
VALUES (@Id, @PatientId, @Name, @Type, @Notes, @RecordedAt)";
                await connection.ExecuteAsync(sql, new
                {
                    Id = condId,
                    condition.PatientId,
                    condition.Name,
                    condition.Type,
                    condition.Notes,
                    RecordedAt = recordedAt
                });
                return true;
            }
            catch
            {
                return true;
            }
        }

        public async Task<IEnumerable<LabResultModel>> GetLabResultsAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);

                var results = await connection.QueryAsync<LabResultModel>(
                    "sp_GetLabResults",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
                if (results != null && results.Any())
                {
                    return results;
                }
                return await new MockPatientRepository().GetLabResultsAsync(patientId);
            }
            catch
            {
                return await new MockPatientRepository().GetLabResultsAsync(patientId);
            }
        }

        public async Task<IEnumerable<VaccinationModel>> GetVaccinationHistoryAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);

                var results = await connection.QueryAsync<VaccinationModel>(
                    "sp_GetVaccinationHistory",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
                if (results != null && results.Any())
                {
                    return results;
                }
                return await new MockPatientRepository().GetVaccinationHistoryAsync(patientId);
            }
            catch
            {
                return await new MockPatientRepository().GetVaccinationHistoryAsync(patientId);
            }
        }

        public async Task<bool> AddVaccinationAsync(VaccinationModel vaccination)
        {
            await new MockPatientRepository().AddVaccinationAsync(vaccination);
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var vaccId = vaccination.Id == Guid.Empty ? Guid.NewGuid() : vaccination.Id;
                var sql = @"
INSERT INTO dbo.Vaccinations (Id, PatientId, VaccineName, Status, Notes, DateAdministered, DateDue)
VALUES (@Id, @PatientId, @VaccineName, @Status, @Notes, @DateAdministered, @DateDue)";
                await connection.ExecuteAsync(sql, new
                {
                    Id = vaccId,
                    vaccination.PatientId,
                    vaccination.VaccineName,
                    vaccination.Status,
                    vaccination.Notes,
                    vaccination.DateAdministered,
                    vaccination.DateDue
                });
                return true;
            }
            catch
            {
                return true;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ORDERS
        // ─────────────────────────────────────────────────────────────────

        public async Task<OrderModel?> GetActiveOrderAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);

                return await connection.QueryFirstOrDefaultAsync<OrderModel>(
                    "sp_GetActiveOrder",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
            }
            catch
            {
                try
                {
                    var sql = @"SELECT TOP 1 Id, OrderNumber, PatientId, OrderDate, TotalAmount, Status, ItemsCount, DispatchedFrom, ISNULL(PaymentStatus, 'Unpaid') AS PaymentStatus
                                FROM Orders
                                WHERE (PatientId = @PatientId OR @PatientId = '00000000-0000-0000-0000-000000000000')
                                  AND Status IN ('Dispensed', 'In transit', 'Out for delivery', 'Processing', 'Pending Payment')
                                ORDER BY OrderDate DESC";
                    return await connection.QueryFirstOrDefaultAsync<OrderModel>(sql, new { PatientId = patientId });
                }
                catch
                {
                    return await new MockPatientRepository().GetActiveOrderAsync(patientId);
                }
            }
        }

        public async Task<IEnumerable<OrderModel>> GetOrderHistoryAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@PatientId", patientId);

                return await connection.QueryAsync<OrderModel>(
                    "sp_GetOrderHistory",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
            }
            catch
            {
                try
                {
                    var sql = @"SELECT Id, OrderNumber, PatientId, OrderDate, TotalAmount, Status, ItemsCount, DispatchedFrom
                                FROM Orders
                                WHERE PatientId = @PatientId OR @PatientId = '00000000-0000-0000-0000-000000000000'
                                ORDER BY OrderDate DESC";
                    return await connection.QueryAsync<OrderModel>(sql, new { PatientId = patientId });
                }
                catch
                {
                    return await new MockPatientRepository().GetOrderHistoryAsync(patientId);
                }
            }
        }

        public async Task<IEnumerable<MarketplaceProductModel>> GetMarketplaceProductsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                return await connection.QueryAsync<MarketplaceProductModel>(
                    "sp_GetMarketplaceProducts",
                    commandType: CommandType.StoredProcedure
                );
            }
            catch
            {
                return await new MockPatientRepository().GetMarketplaceProductsAsync();
            }
        }

        public async Task<bool> CreateOrderAsync(OrderModel order)
        {
            // Sync with mock in-memory list for instant reflection
            await new MockPatientRepository().CreateOrderAsync(order);

            using var connection = new SqlConnection(_connectionString);
            var orderId = order.Id == Guid.Empty ? Guid.NewGuid() : order.Id;
            var orderDate = order.OrderDate == default ? DateTime.Now : order.OrderDate;

            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@Id", orderId);
                parameters.Add("@OrderNumber", order.OrderNumber);
                parameters.Add("@PatientId", order.PatientId);
                parameters.Add("@OrderDate", orderDate);
                parameters.Add("@TotalAmount", order.TotalAmount);
                parameters.Add("@Status", order.Status);
                parameters.Add("@ItemsCount", order.ItemsCount);
                parameters.Add("@DispatchedFrom", order.DispatchedFrom);

                var rows = await connection.ExecuteAsync(
                    "sp_CreateOrder",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
                return rows > 0;
            }
            catch
            {
                try
                {
                    var createTableSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
                        BEGIN
                            CREATE TABLE Orders (
                                Id UNIQUEIDENTIFIER PRIMARY KEY,
                                OrderNumber NVARCHAR(50) NOT NULL,
                                PatientId UNIQUEIDENTIFIER NOT NULL,
                                OrderDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                                TotalAmount DECIMAL(18,2) NOT NULL,
                                Status NVARCHAR(50) NOT NULL,
                                ItemsCount INT NOT NULL DEFAULT 1,
                                DispatchedFrom NVARCHAR(100) NOT NULL,
                                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                            );
                        END;";
                    await connection.ExecuteAsync(createTableSql);

                    var insertSql = @"
                        INSERT INTO Orders (Id, OrderNumber, PatientId, OrderDate, TotalAmount, Status, ItemsCount, DispatchedFrom)
                        VALUES (@Id, @OrderNumber, @PatientId, @OrderDate, @TotalAmount, @Status, @ItemsCount, @DispatchedFrom);";
                    
                    var rows = await connection.ExecuteAsync(insertSql, new {
                        Id = orderId,
                        OrderNumber = order.OrderNumber,
                        PatientId = order.PatientId,
                        OrderDate = orderDate,
                        TotalAmount = order.TotalAmount,
                        Status = order.Status,
                        ItemsCount = order.ItemsCount,
                        DispatchedFrom = order.DispatchedFrom
                    });
                    return rows > 0;
                }
                catch
                {
                    return true;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PAYMENTS
        // ─────────────────────────────────────────────────────────────────

        public async Task<MedicalAidModel?> GetMedicalAidDetailsAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            var parameters = new DynamicParameters();
            parameters.Add("@PatientId", patientId);

            return await connection.QueryFirstOrDefaultAsync<MedicalAidModel>(
                "sp_GetMedicalAidDetails",
                parameters,
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<ClaimModel>> GetRecentClaimsAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            var parameters = new DynamicParameters();
            parameters.Add("@PatientId", patientId);

            return await connection.QueryAsync<ClaimModel>(
                "sp_GetRecentClaims",
                parameters,
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<PaymentMethodModel>> GetSavedPaymentMethodsAsync(Guid patientId)
        {
            using var connection = new SqlConnection(_connectionString);
            var parameters = new DynamicParameters();
            parameters.Add("@PatientId", patientId);

            return await connection.QueryAsync<PaymentMethodModel>(
                "sp_GetSavedPaymentMethods",
                parameters,
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<bool> AddPaymentMethodAsync(PaymentMethodModel paymentMethod)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                INSERT INTO PaymentMethods (Id, PatientId, CardType, LastFour, ExpiryDate, GatewayToken, IsDefault, CreatedAt)
                VALUES (@Id, @PatientId, @CardType, @LastFour, @ExpiryDate, @GatewayToken, @IsDefault, @CreatedAt)";
            var rows = await connection.ExecuteAsync(sql, paymentMethod);
            return rows > 0;
        }

        // ─────────────────────────────────────────────────────────────────
        // DUMMY
        // ─────────────────────────────────────────────────────────────────
        public Task<IEnumerable<DummyModel>> GetDummiesAsync()
        {
            var dummyData = new List<DummyModel>
            {
                new DummyModel { Id = 1, Title = "First Dummy", Description = "This came from the Repository!" },
                new DummyModel { Id = 2, Title = "Second Dummy", Description = "Blazor makes this easy." },
                new DummyModel { Id = 3, Title = "Third Dummy", Description = "Look mom, no API endpoints!" }
            };

            return Task.FromResult<IEnumerable<DummyModel>>(dummyData);
        }
    }
}
