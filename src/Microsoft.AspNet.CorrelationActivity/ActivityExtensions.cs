using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNet.CorrelationActivity
{
    /// <summary>
    /// Extensions of Activity class
    /// </summary>
    internal static class ActivityExtensions
    {
        public const string RequestIDHeaderName = "Request-Id";
        public const string CorrelationContextHeaderName = "Correlation-Context";

        /// <summary>
        /// Read activity information from HTTP request header and restore them to the activity
        /// </summary>
        /// <param name="activity"></param>
        /// <param name="requestHeaders"></param>
        public static void RestoreActivityInfoFromRequestHeaders(this Activity activity, NameValueCollection requestHeaders)
        {
            var requestIDs = requestHeaders.GetValues(RequestIDHeaderName);
            if (requestIDs != null)
            {
                try
                {
                    // there may be several Request-Id header, but we only read the first one
                    activity.SetParentId(requestIDs[0]);

                    // Header format - Correlation-Context: key1=value1, key2=value2 
                    var baggages = requestHeaders.GetValues(CorrelationContextHeaderName);
                    if (baggages != null)
                    {
                        // there may be several Correlation-Context header 
                        foreach (var item in baggages)
                        {
                            foreach(var pair in item.Split(','))
                            {
                                NameValueHeaderValue baggageItem;
                                if (NameValueHeaderValue.TryParse(pair, out baggageItem))
                                {
                                    try
                                    {
                                        activity.AddBaggage(baggageItem.Name, baggageItem.Value);
                                    }
                                    catch (ArgumentException)
                                    { }
                                }
                            }                            
                        }
                    }
                }
                catch (ArgumentException)
                { }
            }
        }        
    }
}
