using MComponents.MGrid;
using PIS.Services;
using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client
{
    public class MGridOdataAdapter<T> : IMGridDataAdapter<T> where T : class
    {
        protected ODataClient mClient { get; }

        public MGridOdataAdapter(ODataClient pClient)
        {
            mClient = pClient;
        }

        public virtual async Task<IEnumerable<T>> GetData(IQueryable<T> pQueryable)
        {
            try
            {
                var client = await GetFilteredClient(pQueryable);
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
                return await mClient.For<T>().Count().FindScalarAsync<long>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        protected virtual Task<IBoundClient<T>> GetFilteredClient(IQueryable<T> data)
        {
            OdataQueryExpressionVisitor<T> visitor = new OdataQueryExpressionVisitor<T>(mClient);
            var newExpressionTree = visitor.Visit(data.Expression);

            var lambda = Expression.Lambda(newExpressionTree);
            var compiled = lambda.Compile();

            return Task.FromResult((IBoundClient<T>)compiled.DynamicInvoke());
        }

        public virtual async Task Add(Guid pId, T pNewValue)
        {
            try
            {
                await mClient.For<T>().Set(pNewValue).InsertEntryAsync(false).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task Remove(Guid pId, T pValue)
        {
            try
            {
                await mClient.For<T>().Key(pId).Set(pValue).DeleteEntryAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public virtual async Task Update(Guid pId, T pValue)
        {
            try
            {
                await mClient.For<T>().Key(pId).Set(pValue).UpdateEntryAsync(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
