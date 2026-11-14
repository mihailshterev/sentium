using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Features.Account;

public sealed record RegisterRequest(
    [Required, MinLength(3), MaxLength(64)] string UserName,
    [Required] string Password);
