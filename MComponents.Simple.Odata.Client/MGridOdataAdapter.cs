using MComponents.MGrid;
using Microsoft.OData.Edm;
using PIS.Services;
using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client
{
    public class MGridOdataAdapter : IMGridDataAdapter<IDictionary<string, object>>
    {
        protected ODataClient mClient { get; }

        public string CollectionName { get; protected set; }

        public string Namespace { get; protected set; }

        public string[] Expands { get; protected set; }

        public MGridOdataAdapter(ODataClient pClient, string pNamespace, string pCollection, string[] pExpands = null)
        {
            mClient = pClient;
            CollectionName = pCollection;
            Namespace = pNamespace;
            Expands = pExpands;

            if (!Namespace.EndsWith("."))
                Namespace += ".";
        }

        public virtual async Task<IEnumerable<IDictionary<string, object>>> GetData(IQueryable<IDictionary<string, object>> pQueryable)
        {
            try
            {
                var client = await GetFilteredClient(pQueryable);

                if (Expands != null)
                {
                    client = client.Expand(Expands);
                }

                // var result = (await client.FindEntriesAsync()).ToArray();
                return await client.FindEntriesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task<long> GetDataCount(IQueryable<IDictionary<string, object>> pQueryable)
        {
            try
            {
                if (pQueryable.Expression.ContainsWhereIdExpression())
                {
                    return (await GetData(pQueryable)).Count();
                }

                var client = await GetFilteredClient(pQueryable);

                return await client.Count().FindScalarAsync<long>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task<long> GetTotalDataCount()
        {
            try
            {
                return await mClient.For(CollectionName).Count().FindScalarAsync<long>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        protected virtual Task<IBoundClient<IDictionary<string, object>>> GetFilteredClient(IQueryable<IDictionary<string, object>> data)
        {
            OdataQueryExpressionVisitor<IDictionary<string, object>> visitor = new OdataQueryExpressionVisitor(mClient, CollectionName);
            var newExpressionTree = visitor.Visit(data.Expression);
            //Console.WriteLine(newExpressionTree);

            var lambda = Expression.Lambda(newExpressionTree);
            var compiled = lambda.Compile();

            return Task.FromResult((IBoundClient<IDictionary<string, object>>)compiled.DynamicInvoke());
        }

        public virtual async Task<IDictionary<string, object>> Add(IDictionary<string, object> pNewValue)
        {
            try
            {
                await mClient.For(CollectionName).Set(pNewValue).InsertEntryAsync(false).ConfigureAwait(false);
                return pNewValue;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task Remove(IDictionary<string, object> pValue)
        {
            try
            {
                var id = (Guid)pValue["Id"];
                await mClient.For(CollectionName).Key(id).Set(pValue).DeleteEntryAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task Update(IDictionary<string, object> pValue)
        {
            try
            {
                var id = (Guid)pValue["Id"];
                await mClient.For(CollectionName).Key(id).Set(pValue).UpdateEntryAsync(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }


        public async Task<IEnumerable<MGridColumn>> GetMGridColumnsFromOdataModel()
        {
            var model = await mClient.GetMetadataAsync<IEdmModel>();
            var type = model.FindDeclaredType(CollectionName) as IEdmEntityType;

            if (type == null)
                return Enumerable.Empty<MGridColumn>();

            List<MGridColumn> ret = new List<MGridColumn>();
            OdataHelper.ConvertOdataPropertyToGridColumns(type, ref ret, 1);

            return ret;
        }
    }


    public class MGridOdataAdapter<T> : IMGridDataAdapter<T> where T : class
    {
        protected ODataClient mClient;

        protected string mCollection;

        protected string[] mExpands;

        protected Expression<Func<T, bool>> mFilter;

        public MGridOdataAdapter(ODataClient pClient, string pCollection = null, string[] pExpands = null, Expression<Func<T, bool>> pFilter = null)
        {
            mClient = pClient;
            mCollection = pCollection;
            mExpands = pExpands;
            mFilter = pFilter;
        }

        public virtual async Task<IEnumerable<T>> GetData(IQueryable<T> pQueryable)
        {
            try
            {
                var client = await GetFilteredClient(pQueryable);

                if (mExpands != null)
                {
                    client = client.Expand(mExpands);
                }

                return await client.FindEntriesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task<long> GetDataCount(IQueryable<T> pQueryable)
        {
            try
            {
                if (pQueryable.Expression.ContainsWhereIdExpression())
                {
                    return (await GetData(pQueryable)).Count();
                }

                var client = await GetFilteredClient(pQueryable);

                return await client.Count().FindScalarAsync<long>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task<long> GetTotalDataCount()
        {
            try
            {
                var client = mClient.For<T>(mCollection);

                if (mFilter != null)
                {
                    client = client.Filter(mFilter);
                }

                return await client.Count().FindScalarAsync<long>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        protected virtual Task<IBoundClient<T>> GetFilteredClient(IQueryable<T> data)
        {
            if (mFilter != null)
            {
                data = data.Where(mFilter).AsQueryable();
            }

            var visitor = new OdataQueryExpressionVisitor<T>(mClient, mCollection);
            var newExpressionTree = visitor.Visit(data.Expression);

            var lambda = Expression.Lambda(newExpressionTree);
            var compiled = lambda.Compile();

            return Task.FromResult((IBoundClient<T>)compiled.DynamicInvoke());
        }

        public virtual async Task<T> Add(T pNewValue)
        {
            try
            {
                await mClient.For<T>(mCollection).Set(pNewValue).InsertEntryAsync(false).ConfigureAwait(false);
                return pNewValue;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task Remove(T pValue)
        {
            try
            {
                var id = (Guid)pValue.GetType().GetProperty("Id").GetValue(pValue);
                await mClient.For<T>(mCollection).Key(id).Set(pValue).DeleteEntryAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task Update(T pValue)
        {
            try
            {
                var id = (Guid)pValue.GetType().GetProperty("Id").GetValue(pValue);
                await mClient.For<T>(mCollection).Key(id).Set(pValue).UpdateEntryAsync(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

    }
}
