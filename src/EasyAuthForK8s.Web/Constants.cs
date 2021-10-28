namespace EasyAuthForK8s.Web
{
    public class Constants
    {
        public const string AzureAdConfigSection = "AzureAd";
        public const string EasyAuthConfigSection = "EasyAuthForK8s";
        public const string CookieName = "AzAD.EasyAuthForK8s";
        public const string RoleParameterName = "role";
        public const string ScopeParameterName = "scope";
        public const string RedirectParameterName = "rd";
        public const string StateCookieName = "EasyAuthState";
        public const int StateTtlMinutes = 5;
    }
}
