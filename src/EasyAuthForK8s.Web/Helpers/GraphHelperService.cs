using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Web.Helpers
{
    public class GraphHelperService
    {
        private readonly IOptionsMonitor<OpenIdConnectOptions> _openIdConnectOptions;
        private readonly HttpClient _httpClient;
        ConfigurationManager<AppManifest> _configurationManager;
        ILogger<GraphHelperService> _logger;

        public GraphHelperService(IOptionsMonitor<OpenIdConnectOptions> openIdConnectOptions, HttpClient httpClient, ILogger<GraphHelperService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException("httpClient");
            _openIdConnectOptions = openIdConnectOptions ?? throw new ArgumentNullException("openIdConnectOptions");
            _logger = logger ?? throw new ArgumentNullException("logger");
            _configurationManager = new ConfigurationManager<AppManifest>("noop",
                    new AppManifestRetriever(_httpClient, OidcOptions, _logger));

        }

        private OpenIdConnectOptions OidcOptions()
        {
            return _openIdConnectOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        }

        public virtual async Task<AppManifestResult> GetManifestConfigurationAsync(CancellationToken cancel)
        {
            //we don't necessarily always want to throw if we can't get the configuration
            //let the caller decide how to handle
            var result = new AppManifestResult();
            try
            {
                result.AppManifest = await _configurationManager.GetConfigurationAsync(cancel);
                result.Succeeded = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }

        public virtual async Task<List<string>> ExecuteQueryAsync(string accessToken, string[] queries, CancellationToken cancel)
        {
            List<string> data = new List<string>();

            var configResult = await GetOidcConfigurationAsync(OidcOptions(), cancel);
            if (configResult == null)
                data.Add("oidcConfiguration could not be resolved.");

            else if (queries != null && queries.Length > 0)
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{GraphEndpointFromUInfoEndpoint(configResult!.UserInfoEndpoint)}/$batch");
                request.Headers.Accept.TryParseAdd("application/json;odata.metadata=none");
                request.Headers.Add("ConsistencyLevel", "eventual");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                dynamic body = new ExpandoObject();
                body.requests = new List<dynamic>();
                for (int i = 0; i < queries.Length; i++)
                {
                    body.requests.Add(new { url = queries[i], method = "GET", id = i });
                }

                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                try
                {
                    using (HttpResponseMessage response = await _httpClient.SendAsync(request))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            await ExtractGraphResponse(data, response.Content.ReadAsStream(), _logger);
                        }
                        else
                        {
                            data.Add($"{{\"error_status\":{(int)(response.StatusCode)}," +
                                $"\"error_message\":\"Graph API failure: {JsonEncodedText.Encode(response.ReasonPhrase ?? "Unknown")}\"}}");

                            _logger.LogWarning($"An graph query resulted in an error code - HttpStatus:{(int)(response.StatusCode)}, " +
                                $"Reason:{response.ReasonPhrase}, Request:{request.RequestUri}, Body: {JsonSerializer.Serialize(body)}");
                        }
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred attempting to execute a graph query");
                    data.Add($"{{\"error_status\":500,\"error_message\":\"Graph API failure: {JsonEncodedText.Encode(ex.Message)}\"}}");
                }
            }
            return data;
        }

        internal static async Task ExtractGraphResponse(List<string> results, Stream body, ILogger logger)
        {
            JsonDocument document = await JsonDocument.ParseAsync(body);

            JsonElement responseCollection = document.RootElement.GetProperty("responses");

            //we have to order the reponses back into the order they were sent
            //since the return order is non-determistic
            foreach (JsonElement element in responseCollection.EnumerateArray()
                .OrderBy(x => x.GetProperty("id").GetString())
                .ToArray())
            {
                using MemoryStream stream = new MemoryStream();
                using Utf8JsonWriter writer = new Utf8JsonWriter(stream);
                {
                    writer.WriteStartObject();
                    JsonElement bodyElement = element.GetProperty("body");
                    JsonElement statusElement = element.GetProperty("status");

                    bool hasError = false;
                    if (!IsSuccessStatus(statusElement!.GetInt32()))
                    {
                        hasError = true;
                        writer.WritePropertyName("error_status");
                        writer.WriteNumberValue(statusElement.GetInt32());
                    }

                    if (bodyElement.ValueKind == JsonValueKind.Object || hasError)
                    {
                        //this gets a little weird when there is an error but the expected value 
                        //is not an object.  This means a raw value should have been returned,
                        //but since it wasn't the error will be encoded.
                        if (hasError && bodyElement.ValueKind == JsonValueKind.String)
                        {
                            bodyElement = JsonDocument
                                .Parse(Encoding.UTF8.GetString(Convert.FromBase64String(bodyElement.GetString() ?? "{}")))
                                .RootElement;
                        }

                        if (bodyElement.TryGetProperty("error", out JsonElement errorElement))
                        {
                            writer.WritePropertyName("error_message");
                            var error_message = errorElement.GetProperty("message").GetString();
                            writer.WriteStringValue(error_message);
                            logger.LogWarning($"An item in a graph query batch had errors - {error_message}");
                        }
                        else
                        {
                            //graph responses tend to be quite verbose, so remove metadata
                            foreach (JsonProperty property in bodyElement.EnumerateObject())
                            {
                                if (!property.Name.StartsWith("@odata"))
                                {
                                    property.WriteTo(writer);
                                }
                            }
                        }
                    }
                    //here we're dealing with a raw value from odata $value
                    else
                    {
                        writer.WritePropertyName("value");
                        //it might be encoded
                        var bodyText = bodyElement.GetString();
                        if (bodyText != null && IsBase64String(bodyText!))
                            writer.WriteStringValue(Encoding.UTF8.GetString(Convert.FromBase64String(bodyText)));
                        else
                            bodyElement.WriteTo(writer);
                    }

                    writer.WriteEndObject();
                    writer.Flush();
                    stream.Position = 0;
                    results.Add(await new StreamReader(stream, Encoding.UTF8).ReadToEndAsync());
                }
            }
        }
        private static bool IsBase64String(string base64)
        {
            base64 = base64.Trim();
            return (base64.Length % 4 == 0) && Regex.IsMatch(base64, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.Compiled);
        }
        private static bool IsSuccessStatus(int status)
        {
            return status >= 200 && status < 300;
        }
        private static string GraphEndpointFromUInfoEndpoint(string usrInfo)
        {
            return string.Concat(new Uri(usrInfo)
                .GetLeftPart(System.UriPartial.Authority),
                "/",
                Constants.GraphApiVersion);
        }

        private static string GraphResourceFromUInfoEndpoint(string usrInfo)
        {
            return string.Concat(new Uri(usrInfo)
                .GetLeftPart(System.UriPartial.Authority),
                "/.default");
        }

        private static async Task<OpenIdConnectConfiguration?> GetOidcConfigurationAsync(
            OpenIdConnectOptions options,
            CancellationToken cancel)
        {
            if (options == null || options.ConfigurationManager == null)
                return null;

            return await options.ConfigurationManager!.GetConfigurationAsync(cancel);
        }

        internal class AppManifestRetriever : IConfigurationRetriever<AppManifest>
        {
            readonly HttpClient _client;
            readonly Func<OpenIdConnectOptions> _optionsResolver;
            readonly ILogger _logger;

            public AppManifestRetriever(HttpClient client, Func<OpenIdConnectOptions> optionsResolver, ILogger logger)
            {
                _client = client;
                _optionsResolver = optionsResolver;
                _logger = logger;
            }
            async Task<AppManifest> IConfigurationRetriever<AppManifest>.GetConfigurationAsync(string ignored, IDocumentRetriever retriever, CancellationToken cancel)
            {
                _logger.LogInformation("Begin GetConfigurationAsync to aquire application manifest.");
                AppManifest? appManifest = null;
                OpenIdConnectConfiguration? configResult = null;

                try
                {
                    var options = _optionsResolver() ?? throw new ArgumentNullException("_optionsResolver");

                    try
                    {
                        configResult = await GetOidcConfigurationAsync(options, cancel);
                        if (configResult == null)
                            throw new InvalidOperationException("oidcConfiguration is empty.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error retrieving OIDC configuration.");
                        throw;
                    }

                    var tokenEndpoint = configResult!.TokenEndpoint;
                    var graphEndpoint = GraphEndpointFromUInfoEndpoint(configResult!.UserInfoEndpoint);
                    var graphResource = GraphResourceFromUInfoEndpoint(configResult!.UserInfoEndpoint);

                    string? access_token = null;
                    string? id = null;


                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
                    {
                        Content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>()
                        {
                            //rather than validating parameters, just let AAD respond
                            new("client_id", options.ClientId ?? String.Empty),
                            new("client_secret", options.ClientSecret ?? String.Empty),
                            new("grant_type", "client_credentials"),
                            new("scope", graphResource)
                        })
                    };
                    using (HttpResponseMessage response = await _client.SendAsync(request, cancel))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancel));
                            access_token = document.RootElement.GetProperty("access_token").GetString();

                            if (!string.IsNullOrEmpty(access_token))
                            {
                                id = new JwtSecurityToken(access_token).Claims.First(x => x.Type == "oid").Value;
                            }
                        }
                        else
                            throw new(await response.Content.ReadAsStringAsync(cancel));
                    }
                    if (access_token != null && id != null)
                    {
                        var url = string.Concat(graphEndpoint, "/directoryObjects/", id);
                        request = new HttpRequestMessage(HttpMethod.Get, url);
                        request.Headers.Authorization = new("Bearer", access_token);
                        using (HttpResponseMessage response = await _client.SendAsync(request, cancel))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                appManifest = await JsonSerializer.DeserializeAsync<AppManifest>(await response.Content.ReadAsStreamAsync(cancel));
                                _logger.LogInformation("Successful GetConfigurationAsync to aquire application manifest.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error retrieving application manifest configuration. {ex.Message}");
                    throw;
                }

                if (appManifest != null)
                    appManifest.oidcScopes = configResult.ScopesSupported;
                return appManifest!;
            }
        }
    }
}
