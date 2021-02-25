using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MComponents.Simple.Odata.Client
{
    public class MGridOdataAdapterOneToMany : MGridOdataAdapter
    {

        public Regex mFilterRegex = new Regex("filter=(.*?)&");


        protected Guid mOneId;
        protected string mPropertyToMany;
        protected object mOneModel;

        public MGridOdataAdapterOneToMany(ODataClient pClient, string pNamespace, string pCollection, object pOneModel, Guid pOneId, string pPropertyToMany, string[] pExpands = null) : base(pClient, pNamespace, pCollection, pExpands)
        {
            mOneId = pOneId;
            mPropertyToMany = pPropertyToMany;
            mOneModel = pOneModel;
        }


        protected override async Task<IBoundClient<IDictionary<string, object>>> GetFilteredClient(IQueryable<IDictionary<string, object>> data)
        {
            var client = await base.GetFilteredClient(data);

            //https://github.com/simple-odata-client/Simple.OData.Client/issues/239

            var cmd = HttpUtility.UrlDecode(await client.GetCommandTextAsync());

            var filter = mFilterRegex.Match(cmd).Groups[1].Value;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                filter += " and ";
            }

            filter += mPropertyToMany + "/Id eq " + mOneId;

            RemoveFilterExpression(client);

            client = client.Filter(filter);

            return client;
        }

        private static void RemoveFilterExpression(IBoundClient<IDictionary<string, object>> client)
        {
            var cmdprop = client.GetType().GetProperty("Command", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object fluentcmd = cmdprop.GetValue(client);
            var detailsProp = fluentcmd.GetType().GetProperty("Details", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cmdDetails = detailsProp.GetValue(fluentcmd);
            var filterExpressionProp = cmdDetails.GetType().GetProperty("FilterExpression", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            filterExpressionProp.SetValue(cmdDetails, null);
        }

        public override async Task<long> GetTotalDataCount()
        {
            return await mClient.For(CollectionName).Filter(mPropertyToMany + "/Id eq " + mOneId).Count().FindScalarAsync<long>();
        }

        public override async Task Add(IDictionary<string, object> pNewValue)
        {
            var id = (Guid)pNewValue.GetType().GetProperty("Id").GetValue(pNewValue);

            var batch = new ODataBatch(mClient, true);

            batch += c => c.For(CollectionName).Set(pNewValue).InsertEntryAsync(false);
            batch += c => c.For(CollectionName).Key(id).LinkEntryAsync(mOneModel, mPropertyToMany);

            await batch.ExecuteAsync();
        }
    }



    public class MGridOdataAdapterOneToMany<T> : MGridOdataAdapter<T> where T : class
    {

        public Regex mFilterRegex = new Regex("filter=(.*?)&");


        protected Guid mOneId;
        protected string mPropertyToModel;
        protected object mOneModel;

        public MGridOdataAdapterOneToMany(ODataClient pClient, object pOneModel, Guid pOneId, string pPropertyToModel, string pCollection = null, string[] pExpands = null) : base(pClient, pCollection, pExpands)
        {
            mOneId = pOneId;
            mPropertyToModel = pPropertyToModel;
            mOneModel = pOneModel;
        }


        protected override async Task<IBoundClient<T>> GetFilteredClient(IQueryable<T> data)
        {
            var client = await base.GetFilteredClient(data);

            //https://github.com/simple-odata-client/Simple.OData.Client/issues/239

            /*
            var param = Expression.Parameter(typeof(T), "p");

            var prop = Expression.Property(param, mPropertyToMany);
            prop = Expression.Property(prop, "Id");

            var expr = Expression.Equal(prop, Expression.Constant(mOneId));

            var expression = Expression.Lambda<Func<T, bool>>(expr, param);

            string exprstr = expression.ToString();
            Console.WriteLine(exprstr);

            data = data.Where(expression);

            var client = await base.GetFilteredClient(data);
            */

            var cmd = HttpUtility.UrlDecode(await client.GetCommandTextAsync());

            var filter = mFilterRegex.Match(cmd).Groups[1].Value;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                filter += " and ";
            }

            filter += mPropertyToModel + "/Id eq " + mOneId;

            RemoveFilterExpression(client);

            client = client.Filter(filter);

            return client;
        }

        private static void RemoveFilterExpression(IBoundClient<T> client)
        {
            var cmdprop = client.GetType().GetProperty("Command", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (cmdprop == null)
                return;

            object fluentcmd = cmdprop.GetValue(client);
            var detailsProp = fluentcmd.GetType().GetProperty("Details", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cmdDetails = detailsProp.GetValue(fluentcmd);
            var filterExpressionProp = cmdDetails.GetType().GetProperty("FilterExpression", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            filterExpressionProp.SetValue(cmdDetails, null);
        }

        public override async Task<long> GetTotalDataCount()
        {
            return await mClient.For<T>().Filter(mPropertyToModel + "/Id eq " + mOneId).Count().FindScalarAsync<long>();
        }

        public override async Task Add(T pNewValue)
        {
            try
            {
                var id = (Guid)pNewValue.GetType().GetProperty("Id").GetValue(pNewValue);

                var batch = new ODataBatch(mClient, true);

                batch += c => c.For<T>().Set(pNewValue).InsertEntryAsync(false);
                batch += c => c.For<T>().Key(id).LinkEntryAsync(mOneModel, mPropertyToModel);

                await batch.ExecuteAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
