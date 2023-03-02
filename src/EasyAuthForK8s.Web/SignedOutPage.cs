using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Text;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Web
{
    public class SignedOutPage
    {
        public static async Task Render(HttpResponse response, AppManifest appManifest)
        {
            response.ContentType = "text/html";
            StringBuilder sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html>");
            sb.Append(HeadHtml());
            sb.Append("<body style=\"font-family:monospace, Verdana, sans-serif;\">");
            sb.Append("<div class=\"container\">");
            sb.Append("<div class=\"main\">");
            sb.Append(DescriptionHtml(appManifest?.appDisplayName ?? "this application"));
            sb.Append(PublisherHtml(appManifest?.publisherName ?? "No Information"));
            sb.Append(LinksHtml(appManifest?.info!));
            sb.Append("</div>"); //main
            sb.Append("<div class=\"right\">");
            sb.Append(LogoObjectHtml(appManifest?.info!));
            sb.Append("</div>"); //right
            sb.Append("</div>"); //container
            sb.Append("</body>");
            sb.Append("</html>");

            await response.WriteAsync(sb.ToString());
            await response.StartAsync();
        }

        private static string HeadingHtml(string heading)
        {
            return $"<h1>{heading}</h1>";
        }
        private static string DescriptionHtml(string appname)
        {
            return $"<p><h4>You have been signed out of {appname}.  You may close this browser window.</h4></p>";
        }

        private static string PublisherHtml(string publisher)
        {
            return $"<p>Publisher: {publisher}</p>";
        }
        private static string LogoObjectHtml(AppManifest.Info info)
        {
            //insert app's image if available
            if (info != null && !string.IsNullOrEmpty(info.logoUrl) && Uri.IsWellFormedUriString(info.logoUrl, UriKind.RelativeOrAbsolute))
                return $"<object data=\"{info.logoUrl}\"></object>";
            else
                return string.Empty;
        }
        private static string LinksHtml(AppManifest.Info info)
        {
            if (info != null
                && (!string.IsNullOrEmpty(info.supportUrl)
                || !string.IsNullOrEmpty(info.termsOfServiceUrl)
                || !string.IsNullOrEmpty(info.marketingUrl)
                || !string.IsNullOrEmpty(info.privacyStatementUrl)))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("<p>App Links: ");
                if (!string.IsNullOrEmpty(info.supportUrl) && Uri.IsWellFormedUriString(info.supportUrl, UriKind.RelativeOrAbsolute))
                    sb.Append("<a href=\"#\">Support</a> ");
                if (!string.IsNullOrEmpty(info.termsOfServiceUrl) && Uri.IsWellFormedUriString(info.termsOfServiceUrl, UriKind.RelativeOrAbsolute))
                    sb.Append("<a href=\"#\">Terms Of Service</a> ");
                if (!string.IsNullOrEmpty(info.marketingUrl) && Uri.IsWellFormedUriString(info.marketingUrl, UriKind.RelativeOrAbsolute))
                    sb.Append("<a href=\"#\">Marketing</a> ");
                if (!string.IsNullOrEmpty(info.privacyStatementUrl) && Uri.IsWellFormedUriString(info.privacyStatementUrl, UriKind.RelativeOrAbsolute))
                    sb.Append("<a href=\"#\">Privacy Statement</a> ");
                sb.Append("</p>");

                return sb.ToString();
            }
            else
                return "";
        }
        private static string TitleText()
        {
            return "Signed Out";
        }
        private static string HeadHtml()
        {
            return $"<head><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><title>{TitleText()}</title><style>{Constants.CSS}</style></head>";
        }

        private class Constants
        {
            public const string CSS = "*{box-sizing:border-box}svg,img{max-width:100%;max-height:225px}.container{overflow:auto}.main{max-width:fit-content;float:left;width:70%;padding:0 20px}.right{float:left;width:30%;padding:15px;margin-top:7px}#error_details{width:100%;padding:10px;background-color:#f5f5f5;display:none;border-left:10px solid red;font-family:monospace} a{width:100%;margin:7px}@media only screen and (max-width:620px){.main,.right{width:100%}.container{display:flex;flex-direction:column-reverse}.right{padding:0 20px;margin-top:0;padding-left:15px}svg,img{max-width:100%;max-height:150px;display:block}}";
        }
    }
}
