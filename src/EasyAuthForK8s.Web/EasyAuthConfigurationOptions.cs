namespace EasyAuthForK8s.Web
{
    public class EasyAuthConfigurationOptions
    {
        public string RedirectParam { get; set; } = "rd";
        public string DataProtectionFileLocation { get; set; } = "C:\\mnt\\dp";
        public string SigninPath { get; set; } = "/easyauth/login";
        public string AuthPath { get; set; } = "/easyauth/auth";
        public bool AllowBearerToken { get; set; } = false;
        /// <summary>
        /// provides a default path to send the user after successful login where the 
        /// RedirectParam query string has no value
        /// </summary>
        public string DefaultRedirectAfterSignin { get; set; } = "/";
    }
}
