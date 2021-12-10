# Graph Queries
EasyAuth has the ability to query the Microsoft Graph to get additional information about the user.  This can be great for providing additional data beyond what would normally be provided in the id token.  However, there are some limitations to be aware of:
-   Graph queries only work with cookie authentication, since cookies are used to store that user data.  Bearer tokens are limited to the data that's actually in the token itself.
-   Graph queries add additional overhead.  For that reason, queries are only run one -- when the user logs in.  The data remains static from that point until the user logs in again.
-   Data returned from queries can get quite large if you aren't careful.  It must then be encoded and encrypted, which makes it even larger.  This can cause problems with header sizes in the ingress controller, as well as increase latency as the client must return the cookie with each request.  To mitigate this, [Cookie Compression](configuration.md#compressCookieClaims) is turned on by default.  In addition, it is strongly recommended that you limit the fields returned by the query to fields that you actually will use by using [$select and $filter](https://docs.microsoft.com/en-us/graph/query-parameters).

## Usage
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