namespace Student_Management_System.Models;

public sealed record MfaSetupViewModel(
    bool IsEnabled,
    string SharedKey,
    string AuthenticatorUri,
    IReadOnlyList<string> RecoveryCodes);
