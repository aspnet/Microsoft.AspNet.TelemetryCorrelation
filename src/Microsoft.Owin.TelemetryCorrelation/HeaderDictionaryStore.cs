using System.Collections.ObjectModel;
using Microsoft.AspNet.TelemetryCorrelation;

namespace Microsoft.Owin.TelemetryCorrelation
{
    internal struct HeaderDictionaryStore : IHeaderStore
    {
        private readonly IHeaderDictionary inner;

        public HeaderDictionaryStore(IHeaderDictionary inner) => this.inner = inner;

        public ReadOnlyCollection<string> GetValues(string key)
        {
            var values = inner.GetValues(key);
            return values != null ? new ReadOnlyCollection<string>(values) : null;
        }
    }
}