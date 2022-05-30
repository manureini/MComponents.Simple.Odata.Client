using MComponents.MGrid;
using Microsoft.AspNetCore.Components;

namespace MComponents.Simple.Odata.Client.ListViews
{
    public class RedirectListView<T> : ListView<T> where T : class
    {
        [Inject]
        public Navigation Navigation { get; set; }

        public virtual string DetailUrl => typeof(T).Name.ToLower() + "/";

        public void OnBeginAdd(BeginAddArgs<T> args)
        {
            args.Cancelled = true;
            Navigation.NavigateTo(DetailUrl);
        }

        public void OnBeginEdit(BeginEditArgs<T> args)
        {
            args.Cancelled = true;
            dynamic row = args.Row;
            Navigation.NavigateTo(DetailUrl + row.Id);
        }
    }
}
