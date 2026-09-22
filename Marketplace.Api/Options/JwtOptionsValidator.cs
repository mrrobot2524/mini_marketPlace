using Microsoft.Extensions.Options;

namespace Marketplace.Api.Options;

[OptionsValidator]
public partial class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
}