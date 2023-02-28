namespace EasyAuthForK8s.Web;

public class Constants
{
    public const string AzureAdConfigSection = "AzureAd";
    public const string EasyAuthConfigSection = "EasyAuthForK8s";
    public const string CookieName = "AzAD.EasyAuthForK8s";
    public const string RoleParameterName = "role";
    public const string ScopeParameterName = "scope";
    public const string GraphParameterName = "graph";
    public const string RedirectParameterName = "rd";
    public const string StateCookieName = "EasyAuthState";
    public const string OidcGraphQueryStateBag = ".EasyAuthState.GraphQueries";
    public const string OidcScopesStateBag = ".EasyAuthState.Scopes";
    public const int StateTtlMinutes = 5;
    public const string UserInfoClaimType = "ea4k";
    public const string GraphApiVersion = "beta";
    public const string OriginalUriHeader = "X-Original-URI";
    public class Claims
    {
        public const string Name = "n";
        public const string Subject = "s";
        public const string Role = "r";
    }

    public static readonly string[] IgnoredClaims = {
        "aud","iss","iat","idp","nbf","exp","c_hash","at_hash","aio","nonce","rh","unique_name","uti","ver"
    };

    public const string NoOpRedirectUri = "_blank";
}

