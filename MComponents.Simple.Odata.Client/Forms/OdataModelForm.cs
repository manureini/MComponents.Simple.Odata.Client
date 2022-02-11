using MComponents.MForm;
using MComponents.Simple.Odata.Client.Provider;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client.Forms
{
    public class OdataModelForm<T> : ComponentBase where T : class, new()
    {
        public T Model { get; protected set; }

        [Parameter]
        public Guid? Id { get; set; }

        [Parameter]
        public EventCallback OnModelLoaded { get; set; }

        [Inject]
        public IServiceProvider ServiceProvider { get; set; }

        [Inject]
        public DataProvider DataProvider { get; set; }

        [Inject]
        public IOdataClientProvider OdataClientProvider { get; set; }

        [Inject]
        public Navigation Navigation { get; set; }

        public MFormContainer FormContainer { get; set; }

        public virtual string Collection => typeof(T).Name;

        public virtual string[] Expands => null;

        protected readonly SemaphoreSlim mSemaphore = new(1, 1);

        override async protected Task OnInitializedAsync()
        {
            if (Id == null)
            {
                await CreateModel();
                return;
            }

            await LoadModel();
        }

        public virtual Task CreateModel()
        {
            Model = new T();
            return Task.CompletedTask;
        }

        public virtual async Task LoadModel()
        {
            Model = await DataProvider.Get<T>(Id.Value, Collection, Expands);
            _ = OnModelLoaded.InvokeAsync(this);
        }

        public async virtual Task OnSubmit(MFormSubmitArgs pArgs)
        {
            await mSemaphore.WaitAsync();

            try
            {
                if (Id == null)
                {
                    Model = await DataProvider.Create(Model, Collection);
                    Id = DataProvider.GetId(Model);
                }
                else
                {
                    await DataProvider.Update(Model, Collection, pArgs.ChangedValues);
                }
            }
            finally
            {
                mSemaphore.Release();
            }
        }

        public virtual void OnAfterAllFormsSubmitted()
        {
            string url = Navigation.Uri;

            if (url.Contains(Id.ToString()))
                return;

            if (!url.EndsWith("/"))
                url += "/";

            Navigation.NavigateTo(url + Id, false, true);
        }

        public Task<bool> TrySubmit()
        {
            return FormContainer.TrySubmit();
        }
    }
}
