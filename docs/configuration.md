# Proxy Configuration
Each deployment of an EasyAuth Proxy is specific to:
- A single AAD tenant 
- A single App Registration within that tenant, which means a single set of scopes, roles, etc.
- A single Signup/Signin policy (if using Azure AD B2C)
- A single set of EasyAuth configuration options

You may protect multiple backend applications with a single proxy if, practically speaking, you are ok with managing access to these applications as a single "logical" application.  A common example of this scenario is a composite API that is served by multiple backend microservices.

For most stand-alone applications, however, you will probably elect to deploy a single EasyAuth proxy for each application

Here's a list of possible configuration options for the EasyAuth Proxy, which you can set in the *values.yaml* file of the helm chart or by providing an external configuration file:
| Parent | Name  | Usage  |
| - | - | - |
| azureAd | instance | The Azure Ad instance that will be used to authenticate users.  The default value of 'https://login.microsoftonline.com/' won't need to be changed in most cases, but you will need to provide the appropriate value if you are using a sovereign or government Azure tenant or using a custom url for Azure AD B2C.|
| azureAd | domain | Optional.  If your users are internal organizational accounts from a single tenant domain, this can be helpful by providing a "hint" during login to help ensure that the user logs in with the appropriate user account|
| azureAd | tenantId | If you are using the setup script, this value will be determined at runtime and filled in for you.  Otherwise, this is the GUID tenant identifier for the Azure AD tenant you want to use.  See [How to find my tenant id](https://docs.microsoft.com/en-us/azure/active-directory/fundamentals/active-directory-how-to-find-tenant)|
| azureAd | clientId | If you are using the setup script, this value will be determined at runtime and filled in for you.  Otherwise, this is the GUID application identifier for the Azure AD app registration you want to use.  See [App Registrations](https://docs.microsoft.com/en-us/graph/auth-register-app-v2)|
| azureAd | signUpSignInPolicyId | For B2C only.  This is the name of the policy that should be used.  Otherwise, leave blank.|
| azureAd | callbackPath | The path that Open Id Connect messages will be returned from Azure AD.  In the majority of cases, you should never need to change this. This configurationoption may be removed in the future.  See [Advanced Scenarios](docs/scenarios.md)|
| azureAd | signedOutCallbackPath | The path that the user will be redirected after clearing the session with Azure AD.  It is not recommended that you change this. This configuration option may be removed in the future.  See [Advanced Scenarios](docs/scenarios.md)|
| azureAd | clientSecretKeyRefName, clientSecretKeyRefKey | Secret container and key for the client secret.  Do not change these or set them directly or store the secret in a yaml file.  Rather, provide your secret to helm via the command line via *--set secret.azureclientsecret=$CLIENT_SECRET*  |
| easyAuthForK8s | dataProtectionFileLocation | data protection key ring location.  |
| easyAuthForK8s | signinPath | The path that the proxy host will respond to sign-in requests.  The default should not need to be changed, except for in [Advanced Scenarios](docs/scenarios.md).  Note that when changing this value, you must also update the *nginx.ingress.kubernetes.io/auth-signin* annotation in your ingresses to match.  |
| easyAuthForK8s | signoutPath | The path that the proxy use to sign out a user.  The default should not need to be changed, except for in [Advanced Scenarios](docs/scenarios.md).  |
| easyAuthForK8s | authPath | The path that the proxy host will respond to auth requests.  The default should not need to be changed, except for in [Advanced Scenarios](docs/scenarios.md).  Note that when changing this value, you must also update the *nginx.ingress.kubernetes.io/auth-url* annotation in your ingresses to match.  |
| easyAuthForK8s | allowBearerToken | Default is "false".  If "true" this will allow bearer tokens to be used in addition to cookies.  Primarily for API callers. |
| easyAuthForK8s | defaultRedirectAfterSignin |  This is the final fallback url that the user will be routed to after succesfully logging in.  Depending on your nginx configuration, the primary redirect preference will be the path provided by the "rd" query parameter, followed by the url that the user was originally trying to access.  This option provides a tertiary and final fallback, with "/" being the default |
| easyAuthForK8s | defaultRedirectAfterSignout |  This is the fallback url that the user will be routed to after logging out.  This should be a page that allows anonymous access, otherwise it will result in another log in challenge that results in the user being signed in again.  If you don't have page that allows anonymous access, you can remove this variable or set it to a value of "_blank" to configure EasyAuth to render a basic page for you. |
| easyAuthForK8s | compressCookieClaims | Option is "true" by default, set "false" to disable. Experimental feature that serializes, compresses, and encodes the payload of non-essential claims to keep the cookie size as small as possible.  This helps to avoid increasing the nginx header buffers beyond the default settings and reduces the size of the data sent from the client with each request. <br/><br/>**WARNING!**: *This feature may introduce a security vulnerability, although no specific vulnerability is known at this time.  CRIME, a well-known exploit, takes advantage of compressed streams to decrypt data, however it requires the attacker to be able to introduce arbitrary data into the stream and observe its compressed state.  For this feature we are only compressing a portion of the payload which an attacker should not be able to manipulate, so it should be safe in theory.  To mitigate any potential concerns, avoid sending sensitive data to the back-end service, or disable this feature.*  |
| easyAuthForK8s | responseHeaderPrefix | Prefix for all user information headers.  Default is *"x-injected-"*.  There is no reason to change this unless you have multiple EasyAuth proxies protecting the same backend and need to discern the source of the headers.  |
| easyAuthForK8s | claimEncodingMethod | Default is *UrlEncode*, which should work for most situations.  Valid values are: <ul><li>*UrlEncode*: Invalid characters are escaped according to IETF RFC 3986</li><li>*Base64*: The full string value is encoded from UTF-8 bytes to base64 text</li><li>*None*: Value is not encoded, and the original string value is sent. This may cause errors for downstream web servers, especially on older platforms</li><li>*NoneWithReject*: No encoding is applied, but any header value containing an unsafe character is rejected, and the value "encoding_error" is sent in its place</li></ul> |
| easyAuthForK8s | headerFormatOption | How headers should be packaged.  Default is *Separate*. Note that regardless of the option you choose, only headers defined in the *nginx.ingress.kubernetes.io/auth-response-headers* ingress annotation will be sent to the back end.<ul><li>*Separate*: A separate header for each claim.</li><li>*Combined*: A single header *x-injected-userinfo* with a JSON object containing all claims.  See [Headers](docs/headers.md#Sample UserInfo Header) for an example.  If you choose this option, a common pattern is to use the Base64 encoding method as well since most applications that work with JSON data can also easily decode Base64 at the same time. </li></ul>|
# Ingress Configuration
Each kubernetes you configure should generally be in the form of:

```
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: easyauth-sample-ingress-default
  annotations:
    nginx.ingress.kubernetes.io/auth-url: "https://$host/easyauth/auth"
    nginx.ingress.kubernetes.io/auth-signin: "https://$host/easyauth/login"
    nginx.ingress.kubernetes.io/auth-response-headers: "x-injected-userinfo,x-injected-name,x-injected-oid,x-injected-preferred-username,x-injected-sub,x-injected-tid,x-injected-email,x-injected-groups,x-injected-scp,x-injected-roles,x-injected-graph"
    cert-manager.io/cluster-issuer: letsencrypt-prod
```

Except for [advanced scenarios](advanced.md), you don't need to change any of these values.  You can, however, extend the behavior of EasyAuth by adding parameters to the `auth-url`.  For several examples of this, see [sample-ingress.yaml](../sample/templates/sample-ingress.yaml)

## Roles
Roles are a common way to define a group of users that have their own set of permissions.  For example, if your application has a page that's only for administrators and not normal users, you can define an [application role](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-add-app-roles-in-azure-ad-apps) in Azure AD, and create an ingress requiring that role for the page.  To do this, add a `role` query parameter to the `nginx.ingress.kubernetes.io/auth-url`.  You can request multiple roles or combinations of roles by using a pipe delimiter (`role=A|B` -- must have role A or role B) or multiple `role` parameters (`role=A&role=B`  => must have role A and role B).

For example: 

>`nginx.ingress.kubernetes.io/auth-url: "https://$host/easyauth/auth?role=A|B&role=C"`

reads as:
> "requires role (A OR B) AND role C"

One thing to be aware that is unique to roles (versus scopes below), when a token is requested from Azure AD the token will contain all of the roles that the user is assigned to.  So in the example above, if the user only has role "A" and not "C" EasyAuth will present them with an "Access Denied" error page.  This is because roles (which are assigned) are pre-determined, whereas scopes (which are consented to) can be appended during the log in process.

## Scopes
Scopes behave similar to roles, by using the `scope` query parameter.  The main difference is that a user can consent to additional scopes at run time, so if the `auth-url` requires a scope the user does not currently have, they will be redirected back to Azure AD to acquire the scope.  Note that if you are using graph queries (below), **you must also request the corresponding graph scopes required by the query**.  More on that below.

## Graph Queries
EasyAuth has the ability to query the Microsoft Graph to get additional information about the user.  This can be great for providing additional data beyond what would normally be provided in the id token.  However, there are some limitations to be aware of:
-   Graph queries only work with cookie authentication, since cookies are used to store that user data.  Bearer tokens are limited to the data that's actually in the token itself.
-   Graph queries add additional overhead.  For that reason, queries are only run one -- when the user logs in.  The data remains static from that point until the user logs in again.
-   Data returned from queries can get quite large if you aren't careful.  It must then be encoded and encrypted, which makes it even larger.  This can cause problems with header sizes in the ingress controller, as well as increase latency as the client must return the cookie with each request.  To mitigate this, [Cookie Compression](configuration.md#compressCookieClaims) is turned on by default.  In addition, it is strongly recommended that you limit the fields returned by the query to fields that you actually will use by using [$select and $filter](https://docs.microsoft.com/en-us/graph/query-parameters).

### Usage
To add graph query results to your configuration, update the appropriate ingress annotations to include the 'graph' parameter:
```
nginx.ingress.kubernetes.io/auth-url: "https://$host/easyauth/auth?scope=User.Read&graph=/me?$select=displayName,jobTitle"
```
Notes:
-   The example includes the request for the Graph scope "User.Read", to ensure that the query has permission to run.  If the user has not previously consented to this scope, they will be prompted to do so during the log in process.
-   You only need to provide the relative url of the query itself.  The graph host and API version are inferred from the discovery process.
-   You may include multiple `graph` query parameters (`?graph=foo&graph=bar`) to return multiple result sets.  The results will be returned in the order they appear in the query string
-   The example above shows the query text in its original form, but its a good idea to always url encode parameter values.  For example, `/me?$select=displayName,jobTitle` becomes `%2Fme%3F%24select%3DdisplayName%2CjobTitle`.
-   The resulting `x-injected-graph` header from the sample will look like: `[{"displayName": "Some User","jobTitle": "Some Title"}]`
-   Not all graph queries work with all types of users, since many graph resources are dependent on the various product licenses that are assigned to the user.  If a query raises errors, an `error` property will be returned along with a message.
-   Finally, graph queries are run against the Azure AD tenant that EasyAuth is configured to use, which is not necessarily a particular user's home tenant.  For example, let's say EasyAuth is configured to use the "Contoso" tenant, which contains a B2B Guest user from the "Fabrikam" tenant. You utilize a graph query that looks for groups users belong to. In this case the results returned for the B2B user will be their "Contoso" group memberships, not groups they belong to in their home "Fabrikam" tenant.

# Implementing Sign-out functionality for your protected applications
For web applications, EasyAuth can sign a user out of both the cookie session and Azure AD.  To sign out a user, you'll need to redirect them to the `signoutPath`, for which the default value is `"/easyauth/logout"`, by providing a link or a button in your application.  You may optionally provide a url within your application to return them to after signing out by either setting the `defaultRedirectAfterSignout` value in the helm chart, or by setting the `rd` query string parameter.  An example of a link you direct the user to my look like `"/easyauth/logout?rd=/signedout.html"`.  If you don't provide a redirect url by setting either of these options, EasyAuth will render a basic page for you.
