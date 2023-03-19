using Simple.OData.Client;

namespace MComponents.Simple.Odata.Client
{
    public interface IOdataClientProvider
    {
        public ODataClient Client { get; }
    }
}
