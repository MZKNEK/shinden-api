namespace ShindenAPI.Credentials;

public class Auth
{
    /// <summary>Optional application token. Public endpoints work without a token.</summary>
    public string Token { get; init; }
    public required string UserAgent { get; init; }
    public string Marmolade { get; init; }
}
