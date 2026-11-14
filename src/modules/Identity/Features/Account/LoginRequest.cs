using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Features.Account;

internal sealed record LoginRequest(
    [Required] string UserName,
    [Required] string Password);
