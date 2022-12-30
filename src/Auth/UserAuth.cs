namespace ShindenAPI;

public class UserAuth : Auth
{
    public required string Nickname { get; init; }
    public required string Password { get; init; }
}
