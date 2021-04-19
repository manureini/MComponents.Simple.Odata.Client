using MComponents.MGrid;
using Microsoft.OData.Edm;
using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client
{
    public static class OdataHelper
    {
        public static void ConvertOdataPropertyToGridColumns(IEdmEntityType pType, ref List<MGridColumn> pColumns, int pMaxDepth, string pPath = "", int pDepth = 0)
        {
            if (pDepth > pMaxDepth)
                return;

            foreach (var property in pType.Properties())
            {
                if (property is IEdmNavigationProperty navProp)
                {
                    var pi = navProp.GetType().GetProperty("TargetEntityType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                    var targetType = pi.GetValue(navProp) as IEdmEntityType;

                    if (targetType != null)
                        ConvertOdataPropertyToGridColumns(targetType, ref pColumns, pMaxDepth, pPath + navProp.Name + ".", pDepth + 1);
                    continue;
                }

                pColumns.Add(new MGridColumn()
                {
                    Property = pPath + property.Name,
                    PropertyType = GetType(property.Type)
                });
            }
        }

        public static async Task<string[]> GetNagivationPropertyNames(ODataClient pClient, string pTypeName)
        {
            var model = await pClient.GetMetadataAsync<IEdmModel>();

            var edmType = model.FindDeclaredType(pTypeName) as IEdmEntityType;

            List<string> ret = new List<string>();

            foreach (var property in edmType.Properties())
            {
                if (property is IEdmNavigationProperty navProp)
                {
                    ret.Add(property.Name);
                }
            }

            return ret.ToArray();
        }

        public static Type GetType(IEdmTypeReference pType)
        {
            var primitiveType = pType.PrimitiveKind();

            switch (primitiveType)
            {
                case EdmPrimitiveTypeKind.String: return typeof(string);
                case EdmPrimitiveTypeKind.Double: return typeof(double);
                case EdmPrimitiveTypeKind.Int32: return typeof(int);
                case EdmPrimitiveTypeKind.Int64: return typeof(long);
                case EdmPrimitiveTypeKind.Guid: return typeof(Guid);
                case EdmPrimitiveTypeKind.Date: return typeof(DateTime);
                case EdmPrimitiveTypeKind.DateTimeOffset: return typeof(DateTimeOffset);
                case EdmPrimitiveTypeKind.Decimal: return typeof(decimal);
                case EdmPrimitiveTypeKind.Boolean: return typeof(bool);

                default:
                    return typeof(object);
            }
        }
    }
}
