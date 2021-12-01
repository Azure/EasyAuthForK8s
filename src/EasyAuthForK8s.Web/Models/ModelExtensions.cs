using MessagePack;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace EasyAuthForK8s.Web.Models;

internal static class ModelExtensions
{
    public static EasyAuthState EasyAuthStateFromHttpContext(this HttpContext context)
    {
        //see if state exists in property bag, and return it
        if (context.Items.ContainsKey(Constants.StateCookieName))
        {
            return (context.Items[Constants.StateCookieName]! as EasyAuthState)!;
        }

        //see if cookie exists, read, delete cookie, and save to property bag
        else
        {
            EasyAuthState easyAuthState = new();

            if (context.Request.Cookies.ContainsKey(Constants.StateCookieName))
            {
                string? encodedString = context.Request.Cookies[Constants.StateCookieName]!;
                if (encodedString != null)
                {
                    IDataProtector dp = context.RequestServices.GetDataProtector(Constants.StateCookieName);

                    easyAuthState = JsonSerializer.Deserialize<EasyAuthState>(dp.Unprotect(encodedString!)) ?? easyAuthState;
                }
                //remove the cookie, since this is a one-time use in the current request
                context.Response.Cookies.Delete(Constants.StateCookieName);
            }

            context.Items.Add(Constants.StateCookieName, easyAuthState);
            return easyAuthState;
        }
    }
    public static void AddCookieToResponse(this EasyAuthState state, HttpContext httpContext)
    {
        IDataProtector dp = httpContext.RequestServices.GetDataProtector(Constants.StateCookieName);
        string cookieValue = dp.Protect(state.ToJsonString());

        RequestPathBaseCookieBuilder cookieBuilder = new RequestPathBaseCookieBuilder
        {
            SameSite = SameSiteMode.Lax,
            HttpOnly = true,
            SecurePolicy = CookieSecurePolicy.Always,
            IsEssential = true,
            Expiration = TimeSpan.FromMinutes(Constants.StateTtlMinutes)
        };

        CookieOptions options = cookieBuilder.Build(httpContext, DateTimeOffset.Now);

        httpContext.Response.Cookies.Append(Constants.StateCookieName, cookieValue, options);
    }
    public static string ToJsonString(this EasyAuthState state)
    {
        return JsonSerializer.Serialize<EasyAuthState>(state);
    }

    /// <summary>
    /// serializes the object into a claim.  This is a little bit experimentatal as it uses Latin-1 encoding instead of Base64.
    /// Conventional wisdom would suggest Base64 is more reliable, but it loses some of the size reductions we get from compression.
    /// Latin-1 results in 1:1 conversion.
    /// </summary>
    /// <param name="payload"></param>
    /// <returns></returns>
    public static Claim ToPayloadClaim(this UserInfoPayload payload, EasyAuthConfigurationOptions options)
    {
        if (options.CompressCookieClaims)
        {
            byte[] bytes = MessagePackSerializer.Serialize<UserInfoPayload>(payload,
                MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

            return new Claim(
                Constants.UserInfoClaimType,
                Encoding.Latin1.GetString(bytes),
                "", "", ""
                );
        }
        else
        {
            return new Claim(Constants.UserInfoClaimType, JsonSerializer.Serialize<UserInfoPayload>(payload));
        }
    }
    public static UserInfoPayload? UserInfoPayloadFromPrincipal(this ClaimsPrincipal principal, EasyAuthConfigurationOptions options)
    {
        //see if info claim exists, and return empty if not
        if (principal.Claims == null || !principal.HasClaim(x => x.Type == Constants.UserInfoClaimType))
        {
            return new UserInfoPayload().PopulateFromClaims(principal.Claims!);
        }

        //re-hydrate the info object
        else
        {
            Claim claim = principal.Claims.First(x => x.Type == Constants.UserInfoClaimType);

            if (options.CompressCookieClaims)
            {
                return MessagePackSerializer.Deserialize<UserInfoPayload>(
                    Encoding.Latin1.GetBytes(claim.Value),
                    MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
            }
            else
            {
                return JsonSerializer.Deserialize<UserInfoPayload>(claim.Value);
            }
        }
    }
    public static UserInfoPayload PopulateFromClaims(this UserInfoPayload payload, IEnumerable<Claim> claims)
    {
        if (claims != null)
            foreach (Claim claim in claims)
            {
                switch (claim.Type)
                {
                    case ClaimConstants.Name:
                        payload.name = claim.Value;
                        break;
                    case ClaimConstants.Oid:
                    case ClaimConstants.ObjectId:
                        payload.oid = claim.Value;
                        break;
                    case ClaimConstants.PreferredUserName:
                        payload.preferred_username = claim.Value;
                        break;
                    case ClaimConstants.Roles:
                    case ClaimConstants.Role:
                        payload.roles.Add(claim.Value);
                        break;
                    case ClaimConstants.Sub:
                        payload.sub = claim.Value;
                        break;
                    case ClaimConstants.Tid:
                    case ClaimConstants.TenantId:
                        payload.tid = claim.Value;
                        break;
                    case "email":
                        payload.email = claim.Value;
                        break;
                    case ClaimConstants.Scp:
                    case ClaimConstants.Scope:
                        payload.email = claim.Value;
                        break;
                    default:
                        {
                            if (!Constants.IgnoredClaims.Any(x => x == claim.Type))
                            {
                                payload.otherClaims.Add(new() { name = claim.Type, value = claim.Value });
                            }

                            break;
                        }
                }
            }
        return payload;
    }
    internal static void AppendResponseHeaders(this UserInfoPayload payload, IHeaderDictionary headers, EasyAuthConfigurationOptions configOptions)
    {
        if (headers == null)
        {
            return;
        }

        void addHeader(string name, string value)
        {
            string headerName = SanitizeHeaderName($"{configOptions.ResponseHeaderPrefix}{name}");
            string encodedValue = EncodeValue(value, configOptions.ClaimEncodingMethod);

            //nginx will only forward the first header of a given name,
            //so we must combine them into a single comma-delimited value
            if (headers.ContainsKey(headerName))
            {
                headers[headerName] = string.Concat(headers[headerName], "|", encodedValue);
            }
            else
            {
                headers.Add(headerName, encodedValue);
            }
        };

        if (configOptions.HeaderFormatOption == EasyAuthConfigurationOptions.HeaderFormat.Combined)
        {
            string serialized = JsonSerializer.Serialize<UserInfoPayload>(payload);
            addHeader("userinfo", serialized);
        }

        else
        {
            addHeader("name", payload.name);
            addHeader("oid", payload.oid);
            addHeader("preferred-username", payload.preferred_username);
            addHeader("sub", payload.sub);
            addHeader("tid", payload.tid);
            addHeader("email", payload.email);
            addHeader("groups", payload.groups);
            addHeader("scp", payload.scp);

            foreach (ClaimValue claim in payload.otherClaims)
            {
                addHeader(claim.name, claim.value);
            }

            foreach (string role in payload.roles)
            {
                addHeader("roles", role);
            }

            foreach (string graph in payload.graph)
            {
                addHeader("graph", graph);
            }
        }
    }
    private static string EncodeValue(string value, EasyAuthConfigurationOptions.EncodingMethod method)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return method switch
        {
            EasyAuthConfigurationOptions.EncodingMethod.UrlEncode => WebUtility.UrlEncode(value),
            EasyAuthConfigurationOptions.EncodingMethod.Base64 => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
            EasyAuthConfigurationOptions.EncodingMethod.NoneWithReject => value.All(c => c >= 32 && c < 127) ? value : "encoding_error",
            //None
            _ => value,
        };
    }
    private static string SanitizeHeaderName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException("name");
        }

        string clean = new string(name.Where(c => c >= 32 && c < 127).ToArray());

        return clean.Replace('_', '-').ToLower();
    }
}

