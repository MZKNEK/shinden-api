using System;
using ShindenAPI.Credentials;

namespace ShindenAPI;

public class ConfigBuilder
{
    private Auth _auth;
    private Uri _uri;

    public ConfigBuilder WithUri(string uri)
    {
        _uri = new(uri);
        return this;
    }

    public ConfigBuilder WithUri(Uri uri)
    {
        _uri = uri;
        return this;
    }

    public ConfigBuilder WithAuth(Func<AuthBuilder, Auth> auth)
        => WithAuth(auth(new AuthBuilder()));

    public ConfigBuilder WithAuth(Auth auth)
    {
        _auth = auth;
        return this;
    }

    public Config Build() => new Config() { Uri = _uri, Auth = _auth };
}