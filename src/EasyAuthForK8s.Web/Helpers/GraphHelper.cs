using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace EasyAuthForK8s.Web.Helpers
{
    internal class GraphHelper
    {
        static Lazy<HttpClient> _httpClient = new Lazy<HttpClient>(() =>
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Accept.TryParseAdd("application/json;odata.metadata=none");
                client.DefaultRequestHeaders.Add("ConsistencyLevel", "eventual");
                return client;
            });

        public static async Task<List<string>> ExecuteQueryAsync(string endpoint, string accessToken, string[] queries)
        {
            var data = new List<string>();
            if (queries != null && queries.Length > 0)
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/$batch");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                dynamic body = new ExpandoObject();
                body.requests = new List<dynamic>();
                for (var i = 0; i < queries.Length; i++)
                    body.requests.Add(new { url = queries[i], method = "GET", id = i });

                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                try
                {
                    using (var response = await _httpClient.Value.SendAsync(request))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            var document = await JsonDocument.ParseAsync(response.Content.ReadAsStream());
                            
                            var responseCollection = document.RootElement.GetProperty("responses");
                            
                            //we have to order the reponses back into the order they were sent
                            //since the order is non-determistic
                            foreach (JsonElement element in responseCollection.EnumerateArray()
                                .OrderBy(x => x.GetProperty("id").GetString())
                                .ToArray())
                            {
                                using MemoryStream stream = new MemoryStream();
                                using var writer = new Utf8JsonWriter(stream);
                                {
                                    writer.WriteStartObject();
                                    JsonElement bodyElement = element.GetProperty("body");
                                    JsonElement statusElement = element.GetProperty("status");

                                    bool hasError = false;
                                    if (!IsSuccessStatus(statusElement.GetInt32()))
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
                                            var s = bodyElement.GetRawText();
                                            bodyElement = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(bodyElement.GetString()))).RootElement;
                                        }

                                        if (bodyElement.TryGetProperty("error", out JsonElement errorElement))
                                        {
                                            writer.WritePropertyName("error_message");
                                            writer.WriteStringValue(errorElement.GetProperty("message").GetString());
                                        }
                                        else
                                        {
                                            //graph responses tend to be quite verbose, so remove metadata
                                            foreach (var property in bodyElement.EnumerateObject())
                                            {
                                                if (!property.Name.StartsWith("@odata"))
                                                    property.WriteTo(writer);
                                            }
                                        }
                                    }
                                    //here we're dealing with a raw value from odata $value
                                    else
                                    {
                                        writer.WritePropertyName("$value");
                                        var foo = bodyElement.GetRawText();
                                        bodyElement.WriteTo(writer);
                                    }

                                    writer.WriteEndObject();
                                    writer.Flush();
                                    stream.Position = 0;
                                    data.Add(await new StreamReader(stream, Encoding.UTF8).ReadToEndAsync());
                                }
                            }
                            
                        }                        
                        else
                            data.Add($"{{\"error_status\":{(int)(response.StatusCode)},\"error_message\":\"Graph API failure: {JsonEncodedText.Encode(response.ReasonPhrase)}\"}}");
                    };
                }
                catch (Exception ex)
                {
                    //We don't really want to the signin process to fail, especially for a transient issue
                    //so just return the error message as data
                    data.Add($"{{\"error_status\":500,\"error_message\":\"Graph API failure: {JsonEncodedText.Encode(ex.Message)}\"}}");
                }  
            }
            return data;
        }

        static bool IsSuccessStatus(int status)
        {
            return status >= 200 && status < 300;
        }
    }
}
