using System.Collections.Generic;

namespace EasyAuthForK8s.Web.Models
{
    /// <summary>
    /// This is a "light" version of the application manifest (a.k.a app registration)
    /// </summary>
    public class AppManifest
    {
        public string id { get; set; }
        public string appDescription { get; set; }
        public string appDisplayName { get; set; }
        public string appId { get; set; }
        public string appOwnerOrganizationId { get; set; }
        public string errorUrl { get; set; }
        public string homepage { get; set; }
        public bool isAuthorizationServiceEnabled { get; set; }
        public string loginUrl { get; set; }
        public string logoutUrl { get; set; }
        public string notes { get; set; }
        public string publisherName { get; set; }
        public List<string> replyUrls { get; set; } = new List<string>();
        public List<string> servicePrincipalNames { get; set; } = new List<string>();
        public string signInAudience { get; set; }
        public List<string> tags { get; set; } = new List<string>();
        public List<AppRole> appRoles { get; set; } = new List<AppRole>();
        public Info info { get; set; }
        public List<PublishedPermissionScope> publishedPermissionScopes { get; set; } = new List<PublishedPermissionScope>();
        public class AppRole
        {
            public List<string> allowedMemberTypes { get; set; } = new List<string>();
            public string description { get; set; }
            public string displayName { get; set; }
            public string id { get; set; }
            public bool isEnabled { get; set; }
            public string origin { get; set; }
            public string value { get; set; }
        }

        public class Info
        {
            public string termsOfServiceUrl { get; set; }
            public string supportUrl { get; set; }
            public string privacyStatementUrl { get; set; }
            public string marketingUrl { get; set; }
            public string logoUrl { get; set; }
        }

        public class PublishedPermissionScope
        {
            public string adminConsentDescription { get; set; }
            public string adminConsentDisplayName { get; set; }
            public string id { get; set; }
            public bool isEnabled { get; set; }
            public string type { get; set; }
            public string userConsentDescription { get; set; }
            public string userConsentDisplayName { get; set; }
            public string value { get; set; }
        }
    }

    
}
