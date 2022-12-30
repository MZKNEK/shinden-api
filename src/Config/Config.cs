using System;
using ShindenAPI.Credentials;

namespace ShindenAPI;

public class Config
{
    public required Uri Uri { get; init; }
    public required Auth Auth { get; init; }
}
