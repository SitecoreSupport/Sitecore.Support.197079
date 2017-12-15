using Sitecore.Diagnostics;
using Sitecore.Form.Core.Utility;
using System.Collections.Specialized;

namespace Sitecore.Support.Forms.Shell.UI
{
    public class PatchHelper
    {
        internal static string Expand(string parameters, bool allowUrlDEcoding)
        {
            Assert.ArgumentNotNull(parameters, "parameters");
            NameValueCollection collection = ParametersUtil.XmlToNameValueCollection(parameters, allowUrlDEcoding);
            collection.ForEach(delegate (string k, string v) {
                if (SessionUtil.IsSessionKey(v))
                {
                    collection[k] = Sitecore.Web.WebUtil.GetSessionString(v, v);
                }
            });
            return ParametersUtil.NameValueCollectionToXml(collection);
        }
    }
}
