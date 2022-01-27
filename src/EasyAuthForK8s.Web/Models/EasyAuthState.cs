using System.Collections.Generic;

namespace EasyAuthForK8s.Web.Models;

/// <summary>
/// This exists as a method to preserve state between the authreq subrequest in nginx
/// and the subsequent "error_page" directive that is used to produce the 302 redirect
/// in the browser.  This way users are not blindly redirected to sign in, but rather  
/// arrive with a context of why they were not authorized previously
/// </summary>
public class EasyAuthState
{
    public string? Url { get; set; }
    public AuthStatus Status { get; set; } = AuthStatus.Unauthenticated;
    /// <summary>
    /// jagged collection of required scopes.  Each array element represents 
    /// a set of allowable values.  eg, if the required scope is "foo" and the 
    /// collection contains an element ["foo", "bar"] the requirement is deemed met
    /// </summary>
    public IList<string[]> Scopes { get; set; } = new List<string[]>();
    public IList<string> GraphQueries { get; set; } = new List<string>();
    public string Msg { get; set; } = string.Empty;
    public string Scheme { get; set; } = string.Empty;

    public enum AuthStatus
    {
        Unauthenticated = 0,  //to us this means 401 unauthorized in the normal sense
        Unauthorized, // user is authenticated but requires an elevated token 
        Forbidden //user is otherwise forbidden
    }
}

