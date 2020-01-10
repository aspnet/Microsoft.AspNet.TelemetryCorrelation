using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    internal struct NameValueCollectionHeaderStore : IHeaderStore
    {
        private readonly NameValueCollection inner;

        public NameValueCollectionHeaderStore(NameValueCollection inner) => this.inner = inner;

        public ReadOnlyCollection<string> GetValues(string key)
        {
            var values = inner.GetValues(key);
            return values != null ? new ReadOnlyCollection<string>(values) : null;
        }
    }
}