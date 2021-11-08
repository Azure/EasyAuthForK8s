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
    }
}
