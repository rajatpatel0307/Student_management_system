namespace Student_Management_System.Models;

public sealed record InvitationDeliveryStatus(string State, DateTime? ExpiresUtc, DateTime? LastSentUtc,
    int FailedAttempts, DateTime? NextAttemptUtc, bool CanResend);
