using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using System.IO.Compression;
using System.IO;
using MessagePack;
using System.Security.Claims;

namespace EasyAuthForK8s.Web.Models
{
    internal static class ModelExtensions
    {
        public static EasyAuthState EasyAuthStateFromHttpContext(this HttpContext context)
        {
            //see if state exists in property bag, and return it
            if (context.Items.ContainsKey(Constants.StateCookieName))
                return context.Items[Constants.StateCookieName] as EasyAuthState;

            //see if cookie exists, read, delete cookie, and save to property bag
            else
            {
                EasyAuthState easyAuthState = new();

                if (context.Request.Cookies.ContainsKey(Constants.StateCookieName))
                {
                    var encodedString = context.Request.Cookies[Constants.StateCookieName];
                    if (encodedString != null)
                    {
                        var dp = context.RequestServices.GetDataProtector(Constants.StateCookieName);

                        easyAuthState = JsonSerializer.Deserialize<EasyAuthState>(dp.Unprotect(encodedString));
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
            var dp = httpContext.RequestServices.GetDataProtector(Constants.StateCookieName);
            var cookieValue = dp.Protect(state.ToJsonString());

            var cookieBuilder = new RequestPathBaseCookieBuilder
            {
                SameSite = SameSiteMode.Lax,
                HttpOnly = true,
                SecurePolicy = CookieSecurePolicy.Always,
                IsEssential = true,
                Expiration = TimeSpan.FromMinutes(Constants.StateTtlMinutes)
            };

            var options = cookieBuilder.Build(httpContext, DateTimeOffset.Now);

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
                var bytes = MessagePackSerializer.Serialize<UserInfoPayload>(payload,
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
        public static UserInfoPayload UserInfoPayloadFromPrincipal(this ClaimsPrincipal principal, EasyAuthConfigurationOptions options)
        {
            //see if info claim exists, and return empty if not
            if (principal.Claims == null || !principal.HasClaim(x => x.Type == Constants.UserInfoClaimType))
                return new();

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
                    return JsonSerializer.Deserialize<UserInfoPayload> (claim.Value);
            }
        }
        public static void PopulateFromClaims(this UserInfoPayload payload, IEnumerable<Claim> claims)
        {
            foreach(var claim in claims)
            {
                switch(claim.Type)
                {
                    case "name":
                        payload.name = claim.Value;
                        break;
                    case "oid":
                        payload.oid = claim.Value;
                        break;
                    //case "preferred_username":
                    //    payload.preferred_username = claim.Value;
                    //    break;
                    case "roles":
                        payload.roles.Add(claim.Value);
                        break;
                    case "sub":
                        payload.sub = claim.Value;
                        break;
                    case "tid":
                        payload.tid = claim.Value;
                        break;
                    case "email":
                        payload.email = claim.Value;
                        break;
                    default:
                        {
                            if(!Constants.IgnoredClaims.Any(x => x == claim.Type))
                                payload.otherClaims.Add(new KeyValuePair<string, string>(claim.Type, claim.Value));
                            break;
                        }
                }
            }
            
        }

    }
}
