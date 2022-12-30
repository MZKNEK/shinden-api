using System;

namespace ShindenAPI.Credentials;

public class AuthBuilder
{
    private string _marmolade;
    private string _username;
    private string _password;
    private string _token;
    private string _agent;

    public AuthBuilder WithUsername(string username)
    {
        _username = username;
        return this;
    }

    public AuthBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public AuthBuilder WithToken(string token)
    {
        _token = token;
        return this;
    }

    public AuthBuilder WithUserAgent(string userAgent)
    {
        _agent = userAgent;
        return this;
    }

    public AuthBuilder WithMarmolade(string marmolade)
    {
        _marmolade = marmolade;
        return this;
    }

    public Auth Build()
    {
        _ = _token ?? throw new ArgumentNullException(nameof(_token));
        _ = _agent ?? throw new ArgumentNullException(nameof(_agent));

        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            return new UserAuth
            {
                Marmolade = _marmolade ?? default!,
                Nickname = _username,
                Password = _password,
                UserAgent = _agent,
                Token = _token
            };

        return new Auth
        {
            Marmolade = _marmolade ?? default!,
            UserAgent = _agent,
            Token = _token
        };
    }
}