using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;

namespace EasyAuthForK8s.Web.Authorization;

public class ScopeRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Implements scope authorization, in a way that is more predictable than IDWeb.ScopeAuthorizationRequirement
    /// </summary>
    /// <param name="allowedValues">The optional list of scope values.</param>
    public ScopeRequirement(IEnumerable<string>? allowedValues = null) => AllowedValues = allowedValues;
    public IEnumerable<string>? AllowedValues { get; }
    public override string ToString() =>
        $"{nameof(ScopeRequirement)}: Consented scope must contain one of the following values: " +
        $"({string.Join(", ", AllowedValues ?? Array.Empty<string>())})";

}

