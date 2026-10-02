using System;
using System.Collections;

namespace ShindenAPI;

/// <summary>Builds site and CDN URLs for models returned by the API.</summary>
public sealed class ShindenUrlResolver
{
    private const string BigImageSize = "genuine";
    private const string SmallImageSize = "225x350";
    private const string DefaultImageServer = "https://cdn.shinden.eu/";

    /// <summary>The default resolver for the public Shinden services.</summary>
    public static ShindenUrlResolver Default { get; } = new(
        new Uri("https://shinden.pl/"),
        new Uri("https://shinden.pl/"),
        new Uri(DefaultImageServer));

    /// <summary>Site URI used for web links.</summary>
    public Uri SiteUri { get; }

    /// <summary>Image server URI used for CDN links.</summary>
    public Uri ImageServerUri { get; }

    /// <summary>Creates a resolver using optional custom site and image server URIs.</summary>
    public ShindenUrlResolver(Uri apiUri, Uri siteUri = default!, Uri imageServerUri = default!)
    {
        if (apiUri is null)
            throw new ArgumentNullException(nameof(apiUri));
        if (!apiUri.IsAbsoluteUri)
            throw new ArgumentException("The API URI must be absolute.", nameof(apiUri));

        SiteUri = Normalize(siteUri ?? new Uri($"{apiUri.Scheme}://{apiUri.Authority}/"));
        ImageServerUri = Normalize(imageServerUri ?? new Uri(DefaultImageServer));
    }

    /// <summary>Builds a user profile URL.</summary>
    public string GetUserProfileUrl(ulong userId)
        => new Uri(SiteUri, $"user/{userId}").AbsoluteUri;

    /// <summary>Builds a title URL.</summary>
    public string GetTitleUrl(ulong titleId)
        => new Uri(SiteUri, $"t/{titleId}").AbsoluteUri;

    /// <summary>Builds a staff URL.</summary>
    public string GetStaffUrl(ulong staffId)
        => new Uri(SiteUri, $"staff/{staffId}").AbsoluteUri;

    /// <summary>Builds a character URL.</summary>
    public string GetCharacterUrl(ulong characterId)
        => new Uri(SiteUri, $"character/{characterId}").AbsoluteUri;

    /// <summary>Builds a user avatar URL.</summary>
    public string GetUserAvatarUrl(ulong userId)
        => GetUserAvatarUrl(string.Empty, userId);

    /// <summary>Builds a title picture URL.</summary>
    public string GetPictureUrl(ulong imageId, bool big = true)
        => GetPictureUrl(imageId.ToString(), big);

    internal string GetUserAvatarUrl(string imageId, ulong userId)
    {
        if (string.IsNullOrEmpty(imageId) || imageId is "0")
            return new Uri(ImageServerUri, "cdn1/other/placeholders/user/100x100.jpg").AbsoluteUri;

        return new Uri(ImageServerUri,
            $"cdn1/avatars/225x350/{userId}.jpg?v{Uri.EscapeDataString(imageId)}").AbsoluteUri;
    }

    internal string GetPictureUrl(string imageId, bool big = true)
    {
        if (string.IsNullOrEmpty(imageId) || imageId is "0")
            return new Uri(ImageServerUri, "cdn1/other/placeholders/title/225x350.jpg").AbsoluteUri;

        var size = big ? BigImageSize : SmallImageSize;
        return new Uri(ImageServerUri, $"cdn1/images/{size}/{Uri.EscapeDataString(imageId)}.jpg").AbsoluteUri;
    }

    private static Uri Normalize(Uri value)
    {
        if (!value.IsAbsoluteUri)
            throw new ArgumentException("The URL must be absolute.", nameof(value));

        var builder = new UriBuilder(value)
        {
            Path = value.AbsolutePath.TrimEnd('/') + "/",
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri;
    }

    internal static void Apply(object model, ShindenUrlResolver resolver)
    {
        if (model is IShindenUrlAware urlAware)
            urlAware.UseResolver(resolver);

        if (model is IEnumerable values)
        {
            foreach (var value in values)
            {
                if (value is not null)
                    Apply(value, resolver);
            }
        }
    }
}

/// <summary>Applies a URL resolver to a model.</summary>
public interface IShindenUrlAware
{
    /// <summary>Sets the resolver used by this model.</summary>
    void UseResolver(ShindenUrlResolver resolver);
}
