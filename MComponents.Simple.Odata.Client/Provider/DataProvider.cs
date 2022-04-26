using MComponents.Simple.Odata.Client.Services;
using MShared;
using Polly;
using Polly.Timeout;
using Simple.OData.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client.Provider
{
    public class DataProvider
    {
        protected Dictionary<Guid, object> mCache = new();
        protected Dictionary<string, List<Guid>> mCollectionCache = new();

        protected OdataService mOdataService;

        protected SemaphoreSlim mSemaphore = new(1, 1);
        protected INetworkStateService mNetworkStateService;
        protected AsyncTimeoutPolicy mTimeoutPolicy;

        public bool IsOnline => mNetworkStateService.IsOnline;

        public DataProvider(OdataService pOdataService, INetworkStateService pNetworkStateService)
        {
            mOdataService = pOdataService;
            mNetworkStateService = pNetworkStateService;
            mTimeoutPolicy = Policy.TimeoutAsync(20, TimeoutStrategy.Pessimistic);
        }

        public async Task<T> Get<T>(Guid pKey, string pCollection = null, params string[] pExpands) where T : class
        {
            pCollection ??= typeof(T).Name;

            try
            {
                await mSemaphore.WaitAsync();

                var result = await mTimeoutPolicy.ExecuteAsync(async () =>
                {
                    try
                    {
                        bool forceCheckNestedProperties = false;

                        if (mCache.ContainsKey(pKey))
                        {
                            var existingValue = (T)mCache[pKey];

                            if (HasAllExpands(existingValue, pExpands))
                                return existingValue;

                            forceCheckNestedProperties = true;
                        }

                        var value = await mOdataService.Get<T>(pKey, pCollection, pExpands);

                        if (value == null)
                        {
                            return null;
                        }

                        AddToCache(value, forceCheckNestedProperties);

                        var ret = (T)mCache[pKey];

                        ReverseSetParentValue(ret);

                        return ret;
                    }
                    finally
                    {
                        mSemaphore.Release();
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        private static void ReverseSetParentValue(object parentValue)
        {
            var parentType = parentValue.GetType();

            foreach (var prop in parentType.GetProperties())
            {
                var propValue = prop.GetValue(parentValue);

                if (propValue == null || !prop.PropertyType.IsClass && !prop.PropertyType.IsInterface)
                    continue;

                if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition().IsAssignableTo(typeof(ICollection<>)))
                {
                    var collection = (IEnumerable)propValue;

                    foreach (var entry in collection)
                    {
                        var reverseProp = entry.GetType().GetProperties().SingleOrDefault(p => p.PropertyType.IsAssignableTo(parentType));

                        if (reverseProp == null)
                            continue;

                        reverseProp.SetValue(entry, parentValue);
                    }
                }

                //    ReverseSetParentValue(propValue);
            }
        }

        public Task<List<T>> Get<T>(Expression<Func<T, bool>> pFilter, params string[] pExpands) where T : class
        {
            return Get<T>(null, pFilter, pExpands);
        }

        public async Task<List<T>> Get<T>(string pCollection = null, Expression<Func<T, bool>> pFilter = null, params string[] pExpands) where T : class
        {
            pCollection ??= typeof(T).Name;

            try
            {
                await mSemaphore.WaitAsync();

                var result = await mTimeoutPolicy.ExecuteAsync(async () =>
                {
                    try
                    {
                        if (mCollectionCache.ContainsKey(pCollection) && pFilter == null && pExpands == null)
                        {
                            return GetFromCache<T>(pCollection);
                        }

                        var odataValues = await mOdataService.Get<T>(pCollection, pFilter, pExpands);

                        AddToCacheInternal(odataValues, pCollection, pExpands != null);

                        var result = GetFromCache<T>(pCollection);

                        if (pFilter != null || pExpands != null)
                        {
                            mCollectionCache.Remove(pCollection); //todo implement filter and expand cache
                        }

                        return result;
                    }
                    finally
                    {
                        mSemaphore.Release();
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<T> Create<T>(T pValue, string pCollection = null) where T : class
        {
            pCollection ??= typeof(T).Name;

            try
            {
                await mSemaphore.WaitAsync();

                var id = GetId(pValue);

                if (id == Guid.Empty)
                {
                    pValue.GetType().GetProperty("Id").SetValue(pValue, Guid.NewGuid());
                    id = GetId(pValue);
                }

                await mOdataService.Create<T>(pValue, pCollection, v => IsInCache(v));

                var ret = await mOdataService.Get<T>(id, pCollection); // Create will not expand stuff in the current implementation

                AddToCache(pValue, false); //store the reference from parameter
                AddToCache(ret, true); //store expands

                if (mCollectionCache.ContainsKey(pCollection))
                {
                    mCollectionCache[pCollection].Add(id);
                }

                return ret;
            }
            finally
            {
                mSemaphore.Release();
            }
        }

        public async Task<T> Update<T>(T pValue, string pCollection = null, IDictionary<string, object> pChangedValues = null) where T : class
        {
            pCollection ??= typeof(T).Name;

            try
            {
                await mSemaphore.WaitAsync();

                await mOdataService.Update<T>(pValue, pChangedValues, pCollection);
            }
            finally
            {
                mSemaphore.Release();
            }

            var id = GetId(pValue);

            if (!mCache.ContainsKey(id))
                throw new InvalidOperationException($"Not registered entity {id} {typeof(T)}");

            if (mCache[id] != pValue)
                throw new InvalidOperationException($"Duplicate instance of entity {id} {typeof(T)}");

            return pValue;
        }

        public async Task Remove<T>(Guid pId, string pCollection = null) where T : class
        {
            var value = await Get<T>(pId, pCollection);
            await Remove(value, pCollection);
        }

        public async Task Remove<T>(T pValue, string pCollection = null) where T : class
        {
            if (pValue == null)
                return;

            pCollection ??= typeof(T).Name;

            try
            {
                await mSemaphore.WaitAsync();

                RemoveFromCache(pValue);

                var id = GetId(pValue);
                await mOdataService.Delete<T>(id, pCollection);
            }
            finally
            {
                mSemaphore.Release();
            }
        }

        public async Task AddToCache<T>(IEnumerable<T> pValues, string pCollection)
        {
            pCollection ??= typeof(T).Name;

            try
            {
                await mSemaphore.WaitAsync();
                AddToCacheInternal(pValues, pCollection, false);
            }
            finally
            {
                mSemaphore.Release();
            }
        }

        public async Task AddToCache<T>(T pValue)
        {
            try
            {
                await mSemaphore.WaitAsync();

                if (pValue is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        AddToCache(item, false);
                    }
                    return;
                }

                AddToCache(pValue, false);
            }
            finally
            {
                mSemaphore.Release();
            }
        }

        public bool IsInCache(object value)
        {
            var id = GetId(value);
            return mCache.ContainsKey(id);
        }

        protected void AddToCacheInternal<T>(IEnumerable<T> pValues, string pCollection, bool pForceCheckNestedProperties)
        {
            var ids = new List<Guid>();

            foreach (var value in pValues)
            {
                AddToCache(value, pForceCheckNestedProperties);
                ids.Add(GetId(value));
            }

            if (mCollectionCache.ContainsKey(pCollection))
            {
                var idList = mCollectionCache[pCollection];

                foreach (var id in ids)
                {
                    if (!idList.Contains(id))
                        idList.Add(id);
                }
            }
            else
            {
                mCollectionCache.Add(pCollection, ids);
            }
        }

        protected List<T> GetFromCache<T>(string pCollection)
        {
            var ids = mCollectionCache[pCollection];

            var values = new List<T>();

            foreach (var id in ids)
            {
                values.Add((T)mCache[id]);
            }

            return values;
        }

        protected void AddToCache(object pValue, bool pForceCheckNestedProperties)
        {
            var id = GetId(pValue);

            if (!pForceCheckNestedProperties && mCache.ContainsKey(id))
                return;

            if (!mCache.ContainsKey(id))
                mCache.Add(id, pValue);

            foreach (var prop in pValue.GetType().GetProperties())
            {
                var propValue = prop.GetValue(pValue);

                if (propValue == null)
                    continue;

                if (propValue is DateTime dtValue && dtValue.Kind == DateTimeKind.Unspecified)
                {
                    propValue = DateTime.SpecifyKind(dtValue, DateTimeKind.Utc).ToLocalTime();
                    prop.SetValue(pValue, propValue);
                }

                if (!prop.PropertyType.IsClass && !prop.PropertyType.IsInterface)
                    continue;

                if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition().IsAssignableTo(typeof(ICollection<>)))
                {
                    var entryType = prop.PropertyType.GetGenericArguments()[0];

                    var method = typeof(DataProvider).GetMethod(nameof(AddToCacheListInternal), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(entryType);

                    try
                    {
                        method.Invoke(this, new object[] { id, prop, propValue, pValue, pForceCheckNestedProperties });
                    }
                    catch (Exception ex)
                    {
                    }
                    continue;
                }

                var propId = GetId(propValue);

                if (propId == Guid.Empty)
                    continue;

                AddToCache(propValue, pForceCheckNestedProperties);

                if (prop.SetMethod != null)
                {
                    prop.SetValue(mCache[id], mCache[propId]);
                }
            }
        }


        protected void AddToCacheListInternal<T>(Guid pId, PropertyInfo pPropertyInfo, IEnumerable<T> pPropValue, object pValue, bool pForceCheckNestedProperties)
        {
            var collection = pPropValue.ToList();

            var entryIds = new List<Guid>();

            foreach (var entry in collection.ToArray())
            {
                var entryId = GetId(entry);

                if (entryId == Guid.Empty)
                    continue;

                //fix odata NHibernate bug - duplicate entries
                if (entryIds.Contains(entryId))
                {
                    collection.Remove(entry);
                    continue;
                }

                entryIds.Add(entryId);

                AddToCache(entry, pForceCheckNestedProperties);

                var cacheEntry = (T)mCache[entryId];

                if (!collection.Contains(cacheEntry))
                {
                    var oldInstance = collection.FirstOrDefault(c => GetId(c) == entryId);

                    /*
                    if (collection.GetType().IsArray)
                    {
                        var collectionArray = (Array)propValue;
                        int index = Array.IndexOf(collectionArray, oldInstance);
                        collectionArray.SetValue(cacheEntry, index);
                    }
                    */

                    collection.Remove(oldInstance);
                    collection.Add(cacheEntry);
                }
            }

            pPropertyInfo.SetValue(pValue, collection);

            var cacheValue = mCache[pId];

            var cachepropValue = pPropertyInfo.GetValue(cacheValue);

            if (cachepropValue == null)
            {
                pPropertyInfo.SetValue(mCache[pId], collection);
            }
        }

        protected void RemoveFromCache(object pValue)
        {
            var id = GetId(pValue);

            if (!mCache.ContainsKey(id))
                return;

            mCache.Remove(id);

            foreach (var collection in mCollectionCache)
            {
                if (collection.Value.Contains(id))
                {
                    collection.Value.Remove(id);
                }
            }
        }

        protected bool HasAllExpands(object pValue, string[] pExpands)
        {
            var expandableProperties = pValue.GetType().GetProperties().Where(p => p.GetCustomAttribute<ExpandAttribute>() != null);

            foreach (var property in expandableProperties)
            {
                if (property.GetValue(pValue) == null)
                    return false;
            }

            if (pExpands != null)
            {
                foreach (var expand in pExpands)
                {
                    var prop = ReflectionHelper.GetIMPropertyInfo(pValue.GetType(), expand, null);

                    if (prop.GetValue(pValue) == null)
                        return false;
                }
            }

            return true;
        }

        public Guid GetId(object pValue)
        {
            if (pValue is IIdentifiable identifiable)
                return identifiable.Id;

            var type = pValue.GetType();

            if (!type.IsClass)
                return Guid.Empty;

            var idProp = type.GetProperty("Id");

            if (idProp == null)
                return Guid.Empty;

            return (Guid)idProp.GetValue(pValue);
        }
    }
}
