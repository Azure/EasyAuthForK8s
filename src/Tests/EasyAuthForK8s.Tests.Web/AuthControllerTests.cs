using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using EasyAuthForK8s.Web.Controllers;
using Xunit;
using Moq;
using Microsoft.Identity.Web;
using EasyAuthForK8s.Web;

namespace EasyAuthForK8s.Web.Tests
{
    public class AuthControllerTests
    {
        [Fact]
        public void ClaimIsInjectedIntoHeaderAsExpected()
        {

            var claimtype = "testclaim";
            var claimvalue = "testvalue";

            var claim = new Claim(claimtype, claimvalue);
            var headers = new HeaderDictionary();
            var headerprefix = "X-Injected-";
            AuthController.AddResponseHeadersFromClaims(new List<Claim>() { claim }, headers);

            Assert.Equal(headerprefix + claimtype, headers.First().Key);

        }

        private AuthController CreateAuthController()
        {
            return new AuthController(new MicrosoftIdentityOptions(), new EasyAuthConfigurationOptions());
        }
    }
}
