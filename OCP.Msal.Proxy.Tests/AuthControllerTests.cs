using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using OCP.Msal.Proxy.Web.Controllers;
using Xunit;
using Moq;
using Microsoft.Identity.Web;

namespace OCP.Msal.Proxy.Tests
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
            var configuration = CreateMockIConfiguration();

            return new AuthController(new MicrosoftIdentityOptions(), configuration);
        }

        private static IConfiguration CreateMockIConfiguration()
        {
            return new Mock<IConfiguration>().Object;
        }

    }
}
