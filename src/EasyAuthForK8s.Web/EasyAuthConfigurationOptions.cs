namespace EasyAuthForK8s.Web;
public class EasyAuthConfigurationOptions
{
    public string DataProtectionFileLocation { get; set; } = "C:\\mnt\\dp";
    public string SigninPath { get; set; } = "/msal/index";
    public string AuthPath { get; set; } = "/msal/auth";
    public bool AllowBearerToken { get; set; } = false;
    /// <summary>
    /// provides a default path to send the user after successful login where the 
    /// RedirectParam query string has no value
    /// </summary>
    public string DefaultRedirectAfterSignin { get; set; } = "/";

    /// <summary>
    /// Experimental feature that serializes, compresses, and encodes the payload
    /// of non-essential claims to keep the cookie size as small as possible.  This 
    /// helps to avoid increasing the nginx header buffers beyond the default settings
    /// and reduces the size of the data sent from the client with each request.
    /// 
    /// WARNING: This feature may introduce a security vulnerability, although no specific 
    /// vulnerability is known at this time.  CRIME, a well-known exploit, takes advantage 
    /// of compressed streams to decrypt data, however it requires the attacker to be able
    /// to introduce arbitrary data into the stream and observe its compressed state.  For
    /// this feature we are only compressing a portion of the payload which an attacker should
    /// not be able to manipulate, so it should be safe in theory.  To mitigate any potential
    /// concerns, avoid sending sensitive data to the back-end service, or disable this
    /// feature.
    /// </summary>
    public bool CompressCookieClaims { get; set; } = true;

    /// <summary>
    /// Default assumes public Azure cloud and beta API version.  Endpoint should be updated for other clouds 
    /// as required, or to specify a different api version
    /// </summary>

    public string GraphEndpoint { get; set; } = "https://graph.microsoft.com/beta";

    /// <summary>
    /// Prefix of the header names sent in the reponse after authorization.  Any unsafe
    /// characters are removed, and "_" is replaced with "-" to avoid problems with nginx.
    /// Default is "x-injected-", but this can be changed to avoid name collisions
    /// or if multiple ingresses are used and you need to discern where the headers
    /// came from.  Header names are always sent as lower case.
    /// </summary>
    public string ResponseHeaderPrefix { get; set; } = "x-injected-";

    /// <summary>
    /// Sets the encoding of the response header values.  The unfortunate reality is that 
    /// claim values may contain characters that are not valid in HTTP headers.  Encoding
    /// ensures that values are transmitted using valid characters, with the consequence that 
    /// they values must be decoded by the app.  Default is UrlEncoding, but choose the type
    /// works best for your application.
    /// </summary>
    public EncodingMethod ClaimEncodingMethod { get; set; } = EncodingMethod.UrlEncode;

    public enum EncodingMethod
    {
        /// <summary>
        /// Invalid characters are escaped according to IETF RFC 3986
        /// </summary>
        UrlEncode,
        /// <summary>
        /// The full string value is encoded from UTF-8 bytes to base64 text
        /// </summary>
        Base64,
        /// <summary>
        /// Value is not encoded, and the original string value is sent.
        /// This may cause errors for downstream web servers, especially 
        /// on older platforms
        /// </summary>
        None,
        /// <summary>
        /// No encoding is applied, but any value containing an unsafe 
        /// character is rejected, and the value "encoding_error" is sent 
        /// in its place.
        /// </summary>
        NoneWithReject
    }
    public HeaderFormat HeaderFormatOption { get; set; } = HeaderFormat.Separate;

    public enum HeaderFormat
    {
        /// <summary>
        /// Default. Each claim is sent in a separate response header
        /// </summary>
        Separate,
        /// <summary>
        /// The entire claim object graph is serialized as JSON 
        /// and sent as a single header "{ResponseHeaderPrefix}userinfo".  This would be most useful
        /// combined with the Base64 encoding method where the application
        /// would be better suited to decoding and deserializing the
        /// entire object in one step
        /// </summary>
        Combined
    }
}


