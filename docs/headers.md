# Injected Headers
When a caller is authenticated, the EasyAuth service will inject claims about the user's identity as HTTP Headers when forwarding the request to the protected service.

>**Warning!!**\
Headers contain information that may be classified as PII data.  This data is transmitted in clear text within the cluster, and in most cases can be safely ignored if you don't wish to use it.  However, if you do make use of these headers please take precautions to protect this data and the privacy of your end users.

![Headers](media/headers.jpg)

> *Note!* Claims in OAuth tokens sometimes contain characters that are not allowed in HTTP Headers.  These claims are ignored and won't be forwarded to the protected application.

## Common/Useful Headers
| Name  | Notes  |
| - | - |
| X-Injected-name | A human-readable value that identifies the subject of the token. The value is not guaranteed to be unique, it is mutable, and it's designed to be used only for display purposes.|
| X-Injected-objectidentifier | The immutable identifier for an object in the Microsoft identity system, in this case, a user account. This ID uniquely identifies the user across applications - two different applications signing in the same user will receive the same value in the oid claim.|
| X-Injected-preferred_username | The primary username that represents the user. It could be an email address, phone number, or a generic username without a specified format. Its value is mutable and might change over time.|
| X-Injected-nameidentifier | The OAuth subject claim or principal about which the token asserts information, such as the user of an app. This value is immutable and is specific to an Azure AD application.|
| X-Injected-role | The set of permissions exposed by your application registration that the user has been given permission to call. This header may contain multiple values as a comma delimited string, a single value, or not be present depending on your application's configuration|
| X-OriginalIdToken | (Web Applications only) The raw id_token.|
| X-OriginalBearerToken | (API Applications only) The raw bearer token that was presented by the client application in the Authorization header.|