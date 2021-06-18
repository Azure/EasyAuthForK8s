using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCP.Msal.Proxy.Web.Models
{
    public class ApiUnauthorizedMessageModel
    {
        const string _MESSAGE = "The resource requested is protected by Azure Active Directory, and the required authorization header is missing or invalid for this request.  Acquire a valid bearer token and set Authorization header in the request";
        const string _USER_GUIDE = "https://docs.microsoft.com/en-us/azure/active-directory/develop/active-directory-v2-protocols";
        public string message { get { return _MESSAGE; } }
        public string tokenAuthorityMetadata { get; set; }
        public string scope { get; set; }
        public string user_guide { get { return _USER_GUIDE; } }
    }
}
