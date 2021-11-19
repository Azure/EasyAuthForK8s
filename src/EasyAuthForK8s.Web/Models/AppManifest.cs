using System;
using System.Collections.Generic;
using System.Linq;

namespace EasyAuthForK8s.Web.Models
{
    /// <summary>
    /// This is a "light" version of the application manifest (a.k.a app registration)
    /// </summary>
    public class AppManifest
    {
        public string? id { get; set; }
        public string? appDescription { get; set; }
        public string? appDisplayName { get; set; }
        public string? appId { get; set; }
        public string? appOwnerOrganizationId { get; set; }
        public string? errorUrl { get; set; }
        public string? homepage { get; set; }
        public bool? isAuthorizationServiceEnabled { get; set; }
        public string? loginUrl { get; set; }
        public string? logoutUrl { get; set; }
        public string? notes { get; set; }
        public string? publisherName { get; set; }
        public List<string?> replyUrls { get; set; } = new List<string?>();
        public List<string?> servicePrincipalNames { get; set; } = new List<string?>();
        public string? signInAudience { get; set; }
        public List<string?> tags { get; set; } = new List<string?>();
        public List<AppRole?> appRoles { get; set; } = new List<AppRole?>();
        public Info? info { get; set; }
        public List<PublishedPermissionScope?> publishedPermissionScopes { get; set; } = new List<PublishedPermissionScope?>();

        /// <summary>
        /// this is not part of the actual manifest, but for the sake of completeness
        /// we'll hold on to the list of oidc scopes that the AAD instance supports
        /// so we can tell these apart from any other non-application scopes
        /// </summary>
        public ICollection<string>? oidcScopes { get; set; } = null;
        public class AppRole
        {
            public List<string> allowedMemberTypes { get; set; } = new List<string>();
            public string? description { get; set; }
            public string? displayName { get; set; }
            public string? id { get; set; }
            public bool? isEnabled { get; set; }
            public string? origin { get; set; }
            public string? value { get; set; }
        }

        public class Info
        {
            public string? termsOfServiceUrl { get; set; }
            public string? supportUrl { get; set; }
            public string? privacyStatementUrl { get; set; }
            public string? marketingUrl { get; set; }
            public string? logoUrl { get; set; }
        }

        public class PublishedPermissionScope
        {
            public string? adminConsentDescription { get; set; }
            public string? adminConsentDisplayName { get; set; }
            public string? id { get; set; }
            public bool? isEnabled { get; set; }
            public string? type { get; set; }
            public string? userConsentDescription { get; set; }
            public string? userConsentDisplayName { get; set; }
            public string? value { get; set; }
        }
        /// <summary>
        /// Creates an ordered list of scopes and formats the scope name with resource
        /// identifier where required.
        /// </summary>
        /// <param name="requestedScopes">scopes requested by an previous auth attempt</param>
        /// <param name="scopeString">The scope string from the current OIDC message, which will be replaced</param>
        /// <returns></returns>
        public string FormattedScopeString(IEnumerable<string> requestedScopes, string scopeString)
        {
            //the scope list must be formatted in the correct order for the audience of the 
            //multi-resource token to be correct:
            // 1. OIDC scopes
            // 2. Local scopes for this application
            // 3. MS Graph scopes or anything else

            if (requestedScopes == null || requestedScopes.Count() == 0)
                return scopeString!;

            var knownScopes = (publishedPermissionScopes ?? new List<PublishedPermissionScope?>())
                .Where(x => x != null && !string.IsNullOrEmpty(x!.value))
                .Select(x => x!.value!)
                .ToList();

            if (knownScopes.Count == 0)
                return scopeString!;

            //combine into one working list
            var workingScopes = (scopeString == null ?
                    Array.Empty<string>() :
                    scopeString.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
                .Union(requestedScopes);

            //create an ordered list
            // 1.  Add anything known to be an OIDC scope
            var result = (this.oidcScopes != null && this.oidcScopes.Count > 0) ?
                workingScopes
                    .Where(x => this.oidcScopes.Contains(x, StringComparer.InvariantCultureIgnoreCase))
                    .ToList() :
                new List<string>();

            // 2. Add application-specific scopes
            result.AddRange(workingScopes.Where(x => knownScopes.Contains(x, StringComparer.InvariantCultureIgnoreCase))
                .Except(result, StringComparer.InvariantCultureIgnoreCase));

            // 3. Add back anything else
            result.AddRange(workingScopes.Except(result, StringComparer.InvariantCultureIgnoreCase));

            //format
            return string.Join(' ', result.Select(x => 
                knownScopes.Contains(x, StringComparer.InvariantCultureIgnoreCase) ? $"{this.appId}/{x}" : x));
        }
    }
    public struct AppManifestResult
    {
        public bool Succeeded { get; set; } = false;
        public Exception? Exception { get; set; } = null;
        public AppManifest? AppManifest { get; set; } = null;
    }




}
