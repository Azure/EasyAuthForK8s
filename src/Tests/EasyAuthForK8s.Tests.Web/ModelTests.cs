using EasyAuthForK8s.Web.Helpers;
using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace EasyAuthForK8s.Tests.Web
{

    public class ModelTests
    {
        [Theory]
        [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "httpschemas.xmlsoap.orgws200505identityclaimsnameidentifier")]
        [InlineData("(),/:;<=>?@[\\]{}\"foo", "foo")]
        //input contains no legal characters, default is returned
        [InlineData("(),/:;<=>?@[\\]{}\"", "illegal-name")]
        //input is legal, converted to lowercase
        [InlineData("FOO", "foo")]
        //complete list of valid characters should all remain, all lower case and underscore converted to "-"
        [InlineData(" !#$%&'*+-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ^_`abcdefghijklmnopqrstuvwxyz|~", " !#$%&'*+-.0123456789abcdefghijklmnopqrstuvwxyz^-`abcdefghijklmnopqrstuvwxyz|~")]
        public void Sanitize_Header_Names(string input, string expected)
        {
            var sanitized = ModelExtensions.SanitizeHeaderName(input);
            Assert.Equal(expected, sanitized);
        }

        [Theory]
        [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "nameidentifier")]
        [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier/", "nameidentifier")]
        [InlineData("///////", "illegal-name")]
        public void Uri_Claim_Names(string input, string expected)
        {
            var nameFromUri = ModelExtensions.ClaimNameFromUri(input);
            Assert.Equal(expected, nameFromUri);
        }

    }
}
