﻿using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MComponents.Simple.Odata.Client
{
    public class MGridOdataAdapterOneToMany<T> : MGridOdataAdapter<T> where T : class
    {

        public Regex mFilterRegex = new Regex("filter=(.*?)&");


        protected Guid mOneId;
        protected string mPropertyToMany;
        protected object mOneModel;

        public MGridOdataAdapterOneToMany(ODataClient pClient, object pOneModel, Guid pOneId, string pPropertyToMany) : base(pClient)
        {
            mOneId = pOneId;
            mPropertyToMany = pPropertyToMany;
            mOneModel = pOneModel;
        }

        /*
        public override async Task<IEnumerable<T>> GetData(IQueryable<T> pQueryable)
        {
            try
            {
                var client = GetFilteredClient(pQueryable);
                client = client.Expand(mPropertyToMany);

                var ret = (await client.FindEntriesAsync()).ToArray();


                return ret;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }*/


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

            filter += mPropertyToMany + "/Id eq " + mOneId;

            RemoveFilterExpression(client);

            client = client.Filter(filter);

            return client;
        }

        private static void RemoveFilterExpression(IBoundClient<T> client)
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
            return await mClient.For<T>().Filter(mPropertyToMany + "/Id eq " + mOneId).Count().FindScalarAsync<long>();
        }

        /*

        public override async Task Add(Guid pId, T pNewValue)
        {
            try
            {
                //      IMember member = new DummyMember(Guid.Empty);

                var batch = new ODataBatch(mClient, true);

                //  async Task actionAdd(IODataClient c)
                //     async Task actionLinkGroup(IODataClient c) => 

                batch += c => c.For<T>().Set(pNewValue).InsertEntryAsync(false);
                batch += c => c.For<T>().Key(pId).LinkEntryAsync(mOneModel);

                //    batch += c => c.For<GroupMember>().Key(pId).LinkEntryAsync<IMember>(member, nameof(GroupMember.Member));

                await batch.ExecuteAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        */


    }
}
