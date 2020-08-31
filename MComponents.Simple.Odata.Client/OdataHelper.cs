using MComponents.MGrid;
using Microsoft.OData.Edm;
using System;

namespace MComponents.Simple.Odata.Client
{
    internal static class OdataHelper
    {
        public static MGridColumn ConvertOdataPropertyToGridColumns(IEdmProperty pProperty)
        {
            return new MGridColumn()
            {
                Property = pProperty.Name,
                PropertyType = GetType(pProperty.Type)
            };
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
