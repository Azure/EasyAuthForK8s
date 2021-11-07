using EasyAuthForK8s.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EasyAuthForK8s.Tests.Web
{
    internal class EasyAuthOptionsConfigurationSource : IConfigurationSource, IConfigurationProvider
    {
        private readonly EasyAuthConfigurationOptions _options;
        public EasyAuthOptionsConfigurationSource(EasyAuthConfigurationOptions options)
        {
            _options = options;
        }
        IConfigurationProvider IConfigurationSource.Build(IConfigurationBuilder builder)
        {
            return this;
        }

        IEnumerable<string> IConfigurationProvider.GetChildKeys(IEnumerable<string> earlierKeys, string parentPath)
        {
            if (parentPath == Constants.EasyAuthConfigSection)
            {
                return typeof(EasyAuthConfigurationOptions)
                    .GetProperties()
                    .Select(x => $"{Constants.EasyAuthConfigSection}:{x.Name}")
                    .Union(earlierKeys)
                    .AsEnumerable();
            }
            else
            {
                return earlierKeys;
            }
        }

        IChangeToken IConfigurationProvider.GetReloadToken()
        {
            return null;
        }

        void IConfigurationProvider.Load()
        {

        }

        void IConfigurationProvider.Set(string key, string value)
        {
            throw new NotImplementedException();
        }

        bool IConfigurationProvider.TryGet(string key, out string value)
        {
            value = null;
            if (typeof(EasyAuthConfigurationOptions).GetProperties().Any(x => $"{Constants.EasyAuthConfigSection}:{x.Name}" == key))
            {
                object prop = typeof(EasyAuthConfigurationOptions)
                    .GetProperties()
                    .First(x => $"{Constants.EasyAuthConfigSection}:{x.Name}" == key)
                    .GetValue(_options, null);

                if (prop is string)
                {
                    value = prop as string;
                    return true;
                }
            }
            return false;
        }
    }
}
