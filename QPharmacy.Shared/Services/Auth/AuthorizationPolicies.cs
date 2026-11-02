namespace QPharmacy.Shared.Service;

public static class AuthorizationPolicies
{
    public const string ViewOwnPatientData = "Patient.ViewOwnData";
    public const string IssuePrescription = "Prescription.Issue";
    public const string VerifyPrescription = "Prescription.Verify";
    public const string DispenseMedication = "Medication.Dispense";
    public const string ManageUsers = "Users.Manage";
    public const string ViewAuditLogs = "Audit.View";
}
