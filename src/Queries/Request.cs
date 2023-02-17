using System;
using System.Net.Http;
using Newtonsoft.Json;

namespace ShindenAPI.Queries
{
    public abstract class Request<TReturn>
    {
        internal abstract HttpRequestMessage Build(Uri uri, string token);

        internal virtual bool RequireHttps() => false;

        internal virtual TReturn Parse(string response)
            => JsonConvert.DeserializeObject<TReturn>(response);
    }
}