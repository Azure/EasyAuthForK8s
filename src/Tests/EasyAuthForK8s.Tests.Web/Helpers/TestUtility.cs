using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Tests.Web.Helpers
{
    public class TestUtility
    {
        public static string RandomSafeString(uint length)
        {
            var sb = new StringBuilder();
            var random = new Random();

            while (sb.Length - 1 <= length)
            {

                sb.Append(Convert.ToChar(random.NextInt64(32, 126)));
            }

            return sb.ToString();
        }
        public const string DummyGuid = "9f75d278-da60-4e2c-a75f-6a5c87caeb0d";

        public static QueryCollection ParseQuery(string query)
        {
            var keyValuePairs = new Dictionary<string, StringValues>();
            var enumerable = new QueryStringEnumerable(query);
            foreach (var q in enumerable)
            {
                var name = q.DecodeName().ToString();
                var value = q.DecodeValue().ToString();
                if (!keyValuePairs.ContainsKey(name))
                    keyValuePairs.Add(name, new(value));
                else
                    keyValuePairs[name] = StringValues.Concat(keyValuePairs[name],value);
            }
            return new QueryCollection(keyValuePairs);
        }
    }
}
