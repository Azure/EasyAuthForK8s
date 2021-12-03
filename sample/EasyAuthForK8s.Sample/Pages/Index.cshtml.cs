using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Text.Json;

namespace EasyAuthForK8s.Sample.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }
        public void OnGet(string? encoding, string? format, string? prefix)
        {
            if (!string.IsNullOrEmpty(encoding))
                Encoding = encoding!;
            if (!string.IsNullOrEmpty(format))
                Format = format!;
            //if empty just ignore the prefix
            if (prefix != null)
                Prefix = prefix!;

            var headers = Request.Headers
                .Where(x => x.Key.StartsWith(Prefix, StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrWhiteSpace(Prefix))
                .Select(x => new KeyValuePair<string, string>(x.Key, x.Value));

            if (Encoding == "UrlEncode")
                headers = headers.Select(x => new KeyValuePair<string, string>(x.Key, WebUtility.UrlDecode(x.Value)));

            else if (Encoding == "Base64")
                headers = headers.Select(x => new KeyValuePair<string, string>(x.Key, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(x.Value))));

            if (Format == "Combined")
            {
                headers = headers.Select(x => new KeyValuePair<string, string>(x.Key, JsonPrettyPrint(x.Value)));
            }

            Headers.AddRange(headers.ToList());
        }
        public List<KeyValuePair<string, string>> Headers = new();
        public string Encoding = "UrlEncode";
        public string Format = "Separate";
        public string Prefix = "x-injected-";

        private static string JsonPrettyPrint(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { Indented = true });
                document.WriteTo(writer);
                writer.Flush();
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}