using System.Linq.Expressions;

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
