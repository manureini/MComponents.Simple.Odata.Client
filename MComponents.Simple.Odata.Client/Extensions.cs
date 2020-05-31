using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace MComponents.Simple.Odata.Client
{
    public static class Extensions
    {
        internal static bool ContainsWhereIdExpression(this Expression pExpression)
        {
            return pExpression.ToString().Contains(".Id =="); //Hihi this is very ugly
        }
    }
}
