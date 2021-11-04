namespace EasyAuthForK8s.Web
{
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

        //endpoint should be updated for other clouds as required, or to specify a different 
        //api version
        public string GraphEndpoint { get; set; } = "https://graph.microsoft.com/beta";
    }
}
