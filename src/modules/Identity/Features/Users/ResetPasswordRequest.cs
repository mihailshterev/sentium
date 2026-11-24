using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Features.Users;

internal sealed record ResetPasswordRequest([Required, MaxLength(256)] string NewPassword);
