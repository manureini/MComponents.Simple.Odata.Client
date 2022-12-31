using MComponents.Shared.Attributes;
using MComponents.Simple.Odata.Client.Provider;
using MShared;
using Simple.OData.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
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

        public async Task Update<T>(T pValue, IDictionary<string, object> pChangedValues, string pCollection = null, Func<IIdentifiable, bool> pNestedPropertyShouldBeSkipped = null, Func<object, string, List<Guid>> pNestedCollectionPropertyOldValueFunc = null) where T : class
        {
            Type tType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            var idProperty = tType.GetProperties().First(f => f.Name.ToLower() == "id");
            var keyValue = idProperty.GetValue(pValue);

            var propertyInfos = pValue.GetType().GetProperties();

            if (pChangedValues != null)
            {
                propertyInfos = propertyInfos.Where(p => pChangedValues.ContainsKey(p.Name)).ToArray();
            }

            CheckForAddedNestedProperties(pValue, propertyInfos, pNestedPropertyShouldBeSkipped, pNestedCollectionPropertyOldValueFunc, out List<(string propName, IIdentifiable propValue)> nestedValues, out List<string> ignoreProps);

            CheckForRemovedNestedProperties(pValue, propertyInfos, pNestedCollectionPropertyOldValueFunc, out List<(string propName, Guid propId)> removedValues, ref ignoreProps);

            if (nestedValues.Count == 0 && removedValues.Count == 0) //no batch magic required
            {
                var query = Client.For<T>(pCollection).Key(keyValue);

                if (pChangedValues != null)
                {
                    foreach (var ignoreprop in ignoreProps)
                    {
                        pChangedValues.Remove(ignoreprop);
                    }

                    if (!pChangedValues.Any())
                        return;

                    query = query.Set(pChangedValues);
                }
                else
                {
                    query = query.Set(pValue);
                }

                await query.UpdateEntryAsync(false);
                return;
            }

            var batch = new ODataBatch(Client, true);

            batch += c =>
            {
                if (pChangedValues != null)
                {
                    foreach (var ignoreprop in ignoreProps)
                    {
                        pChangedValues.Remove(ignoreprop);
                    }

                    if (!pChangedValues.Any())
                        return Task.CompletedTask;

                    return Client.For<T>(pCollection).Key(keyValue).Set(pChangedValues).UpdateEntryAsync(false);
                }

                var client = Client.For<T>(pCollection).Key(keyValue).Set(pValue);

                foreach (var prop in ignoreProps)
                {
                    client.IgnoreProperty(prop);
                }

                return client.UpdateEntryAsync(false);
            };

            foreach (var value in removedValues)
            {
                batch = AddToBatchRemoveNestedPropertyCollectionItem(pValue, batch, value.propName, value.propId);
            }

            foreach (var value in nestedValues)
            {
                batch = AddToBatchAddNestedProperty(pValue, batch, value.propName, value.propValue);
            }

            await batch.ExecuteAsync();
        }

        public async Task<T> Create<T>(IDictionary<string, object> pChangedValues, string pCollection = null) where T : class
        {
            var query = Client.For<T>(pCollection);
            query.Set(pChangedValues);
            return await query.InsertEntryAsync();
        }

        public async Task Create<T>(T pValue, string pCollection = null, Func<IIdentifiable, bool> pNestedPropertyShouldBeSkipped = null, Func<object, string, List<Guid>> pNestedCollectionPropertyOldValueFunc = null) where T : class
        {
            CheckForAddedNestedProperties(pValue, pValue.GetType().GetProperties(), pNestedPropertyShouldBeSkipped, pNestedCollectionPropertyOldValueFunc, out List<(string propName, IIdentifiable propValue)> pNestedValues, out List<string> pIgnoreProps);

            if (pNestedValues.Count == 0) //no batch magic required
            {
                await Client.For<T>(pCollection).Set(pValue).InsertEntryAsync(false);
                return;
            }

            var batch = new ODataBatch(Client, true);

            batch += c =>
            {
                var client = c.For<T>(pCollection).Set(pValue);

                foreach (var prop in pIgnoreProps)
                {
                    client.IgnoreProperty(prop);
                }

                return client.InsertEntryAsync(false);
            };

            foreach (var value in pNestedValues)
            {
                batch = AddToBatchAddNestedProperty(pValue, batch, value.propName, value.propValue);
            }

            await batch.ExecuteAsync();
        }

        private static void CheckForAddedNestedProperties<T>(T pValue, IEnumerable<PropertyInfo> pProperties, Func<IIdentifiable, bool> pNestedPropertyShouldBeSkipped, Func<T, string, List<Guid>> pNestedCollectionPropertyOldValueFunc, out List<(string propName, IIdentifiable propValue)> pNestedValues, out List<string> pIgnoreProps) where T : class
        {
            pNestedValues = new();
            pIgnoreProps = new List<string>();

            foreach (var prop in pProperties)
            {
                if (typeof(IIdentifiable).IsAssignableFrom(prop.PropertyType))
                {
                    var propValue = (IIdentifiable)prop.GetValue(pValue);

                    if (propValue == null || (pNestedPropertyShouldBeSkipped != null && pNestedPropertyShouldBeSkipped(propValue)))
                        continue;

                    pIgnoreProps.Add(prop.Name);
                    pNestedValues.Add((prop.Name, propValue));
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

                    var oldValues = pNestedCollectionPropertyOldValueFunc(pValue, prop.Name);

                    foreach (IIdentifiable value in enumerable)
                    {
                        if (value == null || (pNestedPropertyShouldBeSkipped != null && pNestedPropertyShouldBeSkipped(value)))
                            continue;

                        if (!pIgnoreProps.Contains(prop.Name))
                            pIgnoreProps.Add(prop.Name);

                        oldValues.Add(value.Id);
                        pNestedValues.Add((prop.Name, value));
                    }

                    continue;
                }

                SetDateToUtcDate(pValue, prop);
            }
        }

        private static void CheckForRemovedNestedProperties<T>(T pValue, IEnumerable<PropertyInfo> pProperties, Func<T, string, List<Guid>> pNestedCollectionPropertyOldValueFunc, out List<(string propName, Guid propId)> pRemovedValues, ref List<string> pIgnoreProps) where T : class
        {
            pRemovedValues = new();

            foreach (var prop in pProperties)
            {
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

                    if (!pIgnoreProps.Contains(prop.Name))
                        pIgnoreProps.Add(prop.Name);

                    var oldValueIds = pNestedCollectionPropertyOldValueFunc(pValue, prop.Name);

                    if (oldValueIds == null)
                        continue;

                    var oldValueIdsCopy = oldValueIds.ToList();

                    foreach (IIdentifiable value in enumerable)
                    {
                        if (value == null)
                            continue;

                        oldValueIdsCopy.Remove(value.Id);
                    }

                    if (oldValueIdsCopy.Any())
                    {
                        foreach (var oldValueId in oldValueIdsCopy)
                        {
                            pRemovedValues.Add((prop.Name, oldValueId));
                            oldValueIds.Remove(oldValueId);
                        }
                    }
                }

                SetDateToUtcDate(pValue, prop);
            }
        }

        private static void SetDateToUtcDate<T>(T pValue, PropertyInfo prop) where T : class
        {
            if (prop.GetCustomAttribute<DateAttribute>() == null)
                return;

            var dateTime = prop.GetValue(pValue) as DateTime?;

            if (dateTime != null && dateTime.Value.Kind != DateTimeKind.Utc)
            {
                var newValue = DateTime.SpecifyKind(dateTime.Value.Date, DateTimeKind.Utc);
                prop.SetValue(pValue, newValue);
            }
        }

        private static void SetDateToUtcDate<T>(T pValue, IDictionary<string, object> changedValue) where T : class
        {
            foreach (var changedPropertyName in changedValue.Keys.ToArray())
            {
                var prop = pValue.GetType().GetProperty(changedPropertyName);

                if (prop.GetCustomAttribute<DateAttribute>() == null)
                    return;

                var dateTime = changedValue[changedPropertyName] as DateTime?;

                if (dateTime != null && dateTime.Value.Kind != DateTimeKind.Utc)
                {
                    changedValue[changedPropertyName] = DateTime.SpecifyKind(dateTime.Value.Date, DateTimeKind.Utc);
                }
            }
        }

        private ODataBatch AddToBatchAddNestedProperty<T>(T pValue, ODataBatch pBatch, string pPropName, IIdentifiable pPropValue) where T : class
        {
            var type = pValue.GetType();
            var propValType = pPropValue.GetType();
            var property = type.GetProperty(pPropName);

            if (pPropValue.Id == Guid.Empty)
            {
                pPropValue.Id = Guid.NewGuid();
            }

            string propName = null;

            var inverseProp = property.GetCustomAttribute<InversePropertyAttribute>();

            if (inverseProp != null)
            {
                propName = propValType.GetProperties().SingleOrDefault(p => p.Name == inverseProp.Property)?.Name;
            }

            propName ??= propValType.GetProperties().SingleOrDefault(p => p.Name == type.Name && p.PropertyType == type)?.Name;
            propName ??= propValType.GetProperties().SingleOrDefault(p => p.PropertyType == type)?.Name;

            if (propName == null)
            {
                throw new Exception($"{pPropValue} does not have inverse property with type {type} or result is ambiguous");
            }

            pBatch += c => c.For(propValType.Name).Set(pPropValue).InsertEntryAsync(false);
            pBatch += c => c.For(propValType.Name).Key(pPropValue.Id).LinkEntryAsync(pValue, propName);

            return pBatch;
        }

        private ODataBatch AddToBatchRemoveNestedPropertyCollectionItem<T>(T pValue, ODataBatch pBatch, string pPropName, Guid pPropId) where T : class
        {
            var propValType = pValue.GetType().GetProperty(pPropName);

            pBatch += c => c.For(propValType.Name).Key(pPropId).DeleteEntryAsync();

            return pBatch;
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
                    query = query.Expand(expand.Replace(".", "/"));
                }
            }

            return query;
        }
    }
}
