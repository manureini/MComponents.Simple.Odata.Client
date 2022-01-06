using MComponents.MGrid;
using MComponents.Simple.Odata.Client.Provider;
using Microsoft.AspNetCore.Components;

namespace MComponents.Simple.Odata.Client.ListViews
{
    public class ListView<T> : ComponentBase where T : class
    {
        [Inject]
        public IOdataClientProvider OdataClientProvider { get; set; }

        [Inject]
        public DataProvider DataProvider { get; set; }

        public virtual IMGridDataAdapter<T> DataAdapter => new MGridDataProviderAdapter<T>(DataProvider, OdataClientProvider.Client);
    }
}
