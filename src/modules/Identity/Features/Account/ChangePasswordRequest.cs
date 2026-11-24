using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Features.Account;

internal sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MaxLength(256)] string NewPassword);
