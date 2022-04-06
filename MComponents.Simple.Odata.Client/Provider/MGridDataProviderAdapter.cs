using MComponents.MGrid;
using MComponents.Simple.Odata.Client;
using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client.Provider
{
    public class MGridDataProviderAdapter<T> : IMGridDataAdapter<T> where T : class
    {
        protected string mCollection;

        protected string[] mExpands;

        protected Expression<Func<T, bool>> mFilter;

        protected MGridOdataAdapter<T> mOdataAdapter;
        protected DataProvider mDataProvider;

        public MGridDataProviderAdapter(DataProvider pDataProvider, ODataClient pClient, string pCollection = null, string[] pExpands = null, Expression<Func<T, bool>> pFilter = null)
        {
            mDataProvider = pDataProvider;
            mOdataAdapter = new MGridOdataAdapter<T>(pClient, pCollection, pExpands, pFilter);
        }

        public async Task<IEnumerable<T>> GetData(IQueryable<T> pQueryable)
        {
            if (mDataProvider.IsOnline)
            {
                try
                {
                    var result = await mOdataAdapter.GetData(pQueryable);
                    await mDataProvider.AddToCache(result, mCollection);

                    var ids = result.Select(v => mDataProvider.GetId(v));

                    var ret = new List<T>();

                    foreach (var id in ids)
                    {
                        ret.Add(await mDataProvider.Get<T>(id));
                    }

                    return ret;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                return Enumerable.Empty<T>();
            }

            return await mDataProvider.Get<T>(mCollection);
        }

        public async Task<long> GetDataCount(IQueryable<T> pQueryable)
        {
            if (mDataProvider.IsOnline)
            {
                try
                {
                    return await mOdataAdapter.GetDataCount(pQueryable);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return (await GetData(pQueryable)).LongCount();
        }

        public async Task<long> GetTotalDataCount()
        {
            if (mDataProvider.IsOnline)
            {
                try
                {
                    return await mOdataAdapter.GetTotalDataCount();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return (await mDataProvider.Get<T>(mCollection)).LongCount();
        }

        public Task<T> Add(T pNewValue)
        {
            return mDataProvider.Create(pNewValue, mCollection);
        }

        public Task Update(T pValue)
        {
            return mDataProvider.Update(pValue, mCollection);
        }

        public Task Remove(T pValue)
        {
            return mDataProvider.Remove(pValue, mCollection);
        }
    }
}
