namespace Modules.Identity.Features.Users.ResetCredential;

public sealed record ResetCredentialCommand(string? NewPassword, string? NewPin);
