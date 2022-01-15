using MComponents.Shared.Attributes;
using MComponents.Simple.Odata.Client.Provider;
using MShared;
using Simple.OData.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client.Services
{
    public class OdataService
    {
        public IOdataClientProvider ClientProvider { get; set; }

        public ODataClient Client => ClientProvider.Client;

        public OdataService(IOdataClientProvider pOdataManager)
        {
            ClientProvider = pOdataManager;
        }

        public async Task<T> Get<T>(Guid pKey, string pCollection = null, params string[] pExpands) where T : class
        {
            var query = Client.For<T>(pCollection).Key(pKey);

            query = AddExpands(pExpands, query);

            return await query.FindEntryAsync();
        }

        public async Task<IEnumerable<T>> Get<T>(string pCollection = null, Expression<Func<T, bool>> pFilter = null, params string[] pExpands) where T : class
        {
            var query = Client.For<T>(pCollection);

            query = AddExpands(pExpands, query);

            if (pFilter != null)
            {
                query = query.Filter(pFilter);
            }

            return await query.FindEntriesAsync();
        }

        public async Task Update<T>(T pObject, IDictionary<string, object> pChangedValues, string pCollection = null) where T : class
        {
            Type tType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            var idProperty = tType.GetProperties().First(f => f.Name.ToLower() == "id");
            var keyValue = idProperty.GetValue(pObject);

            var query = Client.For<T>(pCollection).Key(keyValue);

            /*
            foreach (var entry in pChangedValues)
            {
                Console.WriteLine(entry.Key + "  " + entry.Value);
            }*/

            if (pChangedValues != null)
            {
                query = query.Set(pChangedValues);
            }
            else
            {
                foreach (var prop in tType.GetProperties())
                {
                    SetDateToUtcDate(pObject, prop);
                }

                query = query.Set(pObject);
            }

            await query.UpdateEntryAsync(false);
        }

        public async Task<T> Create<T>(IDictionary<string, object> pChangedValues, string pCollection = null) where T : class
        {
            var query = Client.For<T>(pCollection);
            query.Set(pChangedValues);
            return await query.InsertEntryAsync();
        }

        public async Task Create<T>(T pValue, string pCollection = null, Func<IIdentifiable, bool> pNestedPropertyShouldBeSkipped = null) where T : class
        {
            var valueType = pValue.GetType();

            List<IIdentifiable> nestedValues = new();

            List<string> ignoreProps = new List<string>();

            foreach (var prop in valueType.GetProperties())
            {
                if (typeof(IIdentifiable).IsAssignableFrom(prop.PropertyType))
                {
                    var propValue = (IIdentifiable)prop.GetValue(pValue);

                    if (propValue == null || (pNestedPropertyShouldBeSkipped != null && pNestedPropertyShouldBeSkipped(propValue)))
                        continue;

                    ignoreProps.Add(prop.Name);
                    nestedValues.Add(propValue);
                    continue;
                }

                if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType.IsGenericType)
                {
                    var genericArgType = prop.PropertyType.GetGenericArguments()[0];

                    if (!typeof(IIdentifiable).IsAssignableFrom(genericArgType))
                    {
                        continue;
                    }

                    var enumerable = (IEnumerable)prop.GetValue(pValue);

                    if (enumerable == null)
                        continue;

                    foreach (IIdentifiable value in enumerable)
                    {
                        if (value == null || (pNestedPropertyShouldBeSkipped != null && pNestedPropertyShouldBeSkipped(value)))
                            continue;

                        if (!ignoreProps.Contains(prop.Name))
                            ignoreProps.Add(prop.Name);

                        nestedValues.Add(value);
                    }

                    continue;
                }

                SetDateToUtcDate(pValue, prop);
            }

            if (nestedValues.Count == 0) //no batch magic required
            {
                await Client.For<T>(pCollection).Set(pValue).InsertEntryAsync(false);
                return;
            }

            var batch = new ODataBatch(Client, true);

            batch += c =>
            {
                var client = c.For<T>(pCollection).Set(pValue);

                foreach (var prop in ignoreProps)
                {
                    client.IgnoreProperty(prop);
                }

                return client.InsertEntryAsync(false);
            };

            foreach (var value in nestedValues)
            {
                batch = AddToBatch(pValue, batch, value);
            }

            await batch.ExecuteAsync();
        }

        private static void SetDateToUtcDate<T>(T pValue, PropertyInfo prop) where T : class
        {
            if (prop.GetCustomAttribute<DateAttribute>() == null)
                return;

            var dateTime = prop.GetValue(pValue) as DateTime?;

            if (dateTime != null && dateTime.Value.Kind != DateTimeKind.Utc)
            {
                var newValue = DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc);
                prop.SetValue(pValue, newValue);
            }
        }

        private ODataBatch AddToBatch<T>(T pValue, ODataBatch batch, IIdentifiable propValue) where T : class
        {
            var type = pValue.GetType();
            var prop = propValue.GetType().GetProperties().Single(p => p.PropertyType == type);

            batch += c => c.For(propValue.GetType().Name).Set(propValue).InsertEntryAsync(false);
            batch += c => c.For(propValue.GetType().Name).Key(propValue.Id).LinkEntryAsync(pValue, prop.Name);

            return batch;
        }

        public async Task Delete<T>(Guid pKey, string pCollection = null) where T : class
        {
            await Client.For<T>(pCollection).Key(pKey).DeleteEntryAsync();
        }

        private static IBoundClient<T> AddExpands<T>(string[] pExpands, IBoundClient<T> query) where T : class
        {
            foreach (var property in typeof(T).GetProperties().Where(p => p.GetCustomAttribute(typeof(ExpandAttribute)) != null))
            {
                if (pExpands != null && pExpands.Contains(property.Name))
                    continue;

                query = query.Expand(property.Name);
            }

            if (pExpands != null)
            {
                foreach (var expand in pExpands)
                {
                    query = query.Expand(expand);
                }
            }

            return query;
        }
    }
}
