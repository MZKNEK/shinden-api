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
        if (string.IsNullOrWhiteSpace(_token))
            throw new ArgumentException("The API token is required.", nameof(_token));
        if (string.IsNullOrWhiteSpace(_agent))
            throw new ArgumentException("The user agent is required.", nameof(_agent));

        var hasUsername = !string.IsNullOrWhiteSpace(_username);
        var hasPassword = !string.IsNullOrWhiteSpace(_password);
        if (hasUsername != hasPassword)
            throw new ArgumentException("Both username and password are required for user authentication.");

        if (hasUsername)
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