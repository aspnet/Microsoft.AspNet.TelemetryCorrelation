using System.Collections.ObjectModel;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    /// <summary>
    /// Represents a common abstraction to unify the access to Microsoft.Owin.IHeaderDictionary and System.Collections.Specialized.NameValueCollection.
    /// </summary>
    public interface IHeaderStore
    {
        /// <summary>
        /// Get the associated values from the collection in their original format.
        /// Returns null if the key is not present.
        /// </summary>
        /// <param name="key">The key, e.g. HTTP header name.</param>
        /// <returns>Values for the specified key.</returns>
        ReadOnlyCollection<string> GetValues(string key);
    }
}