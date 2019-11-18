using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCP.Msal.Proxy.Web.Models
{
    public class ApiUnauthorizedMessageModel
    {
        public string message = "The resource requested is protected by Azure Active Directory, and the required authorization header is missing or invalid for this request.  Acquire a valid bearer token and set Authorization header in the request";
        public string tokenAuthorityMetadata;
        public string scope;
        public string user_guide = "https://docs.microsoft.com/en-us/azure/active-directory/develop/active-directory-v2-protocols";
    }
}
