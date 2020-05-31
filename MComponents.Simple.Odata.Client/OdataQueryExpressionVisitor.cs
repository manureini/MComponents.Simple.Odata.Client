using MComponents.Simple.Odata.Client;
using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace PIS.Services
{
    public class OdataQueryExpressionVisitor<T> : ExpressionVisitor where T : class
    {
        protected ODataClient mClient;

        public OdataQueryExpressionVisitor(ODataClient pClient)
        {
            mClient = pClient;
        }

        public override Expression Visit(Expression node)
        {
            return base.Visit(node);
        }

        protected override Expression VisitExtension(Expression node)
        {
            return base.VisitExtension(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "Skip")
            {
                var innerExpression = Visit(node.Arguments[0]);

                if (innerExpression.ContainsWhereIdExpression())
                {
                    return innerExpression;
                }

                //  mClient.For<T>().Skip(5);
                var mi = typeof(IFluentClient<T, IBoundClient<T>>).GetMethod(nameof(IBoundClient<T>.Skip));
                return Expression.Call(innerExpression, mi, ToInt64Expression(node.Arguments[1]));
            }

            if (node.Method.Name == "Take")
            {
                var innerExpression = Visit(node.Arguments[0]);

                if (innerExpression.ContainsWhereIdExpression())
                {
                    return innerExpression;
                }

                //  mClient.For<T>().Skip(5);
                var mi = typeof(IFluentClient<T, IBoundClient<T>>).GetMethod(nameof(IBoundClient<T>.Top));
                return Expression.Call(innerExpression, mi, ToInt64Expression(node.Arguments[1]));
            }

            if (node.Method.Name == "Where")
            {
                //  mClient.For<T>().Filter(expr);
                if (node.Arguments[1] is UnaryExpression ue)
                {
                    Expression<Func<T, bool>> expr = ue.Operand as Expression<Func<T, bool>>;

                    var mi = typeof(IFluentClient<T, IBoundClient<T>>).GetMethods()
                        .First(m => m.Name == nameof(IBoundClient<T>.Filter) && m.GetParameters()[0].ParameterType == typeof(Expression<Func<T, bool>>));

                    return Expression.Call(Visit(node.Arguments[0]), mi, Visit(Expression.Constant(expr)));
                }
            }

            if (node.Method.Name == "OrderBy" || node.Method.Name == "OrderByDescending" || node.Method.Name == "ThenBy" || node.Method.Name == "Descending")
            {
                //  mClient.For<T>().OrderBy(expr);

                if (node.Arguments[1] is UnaryExpression ue)
                {
                    dynamic expr = ue.Operand;

                    var convertedExpr = Expression.Lambda<Func<T, object>>
                           (
                               expr.Body, expr.Parameters[0]
                           );

                    var mi = typeof(IFluentClient<T, IBoundClient<T>>).GetMethods()
                        .First(m => m.Name == node.Method.Name && m.GetParameters()[0].ParameterType == typeof(Expression<Func<T, object>>));

                    return Expression.Call(Visit(node.Arguments[0]), mi, Visit(Expression.Constant(convertedExpr)));
                }
            }

            if (node.Method.Name == "ToLowerInvariant")
            {
                var mi = typeof(string).GetMethods().Where(m => m.Name == nameof(string.ToLower) && m.GetParameters().Count() == 0).First();

                return Expression.Call(node.Object, "ToLower", null);

                //    return Expression.Call(mi, Visit(node.Object));
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node == null)
                return node;

            if (node.Type.IsGenericType && node.Type.GetGenericTypeDefinition() == typeof(EnumerableQuery<>))
            {
                var client = Expression.Constant(mClient);

                var mi = mClient.GetType().GetMethods().Last(m => m.Name == "For").MakeGenericMethod(typeof(T));

                return Expression.Call(client, mi, Expression.Constant(null, typeof(string)));
            }

            if (node.Value is Expression expr)
                return Visit(expr);

            return base.VisitConstant(node);
        }

        protected Expression ToInt64Expression(Expression pExpression)
        {
            if (pExpression is ConstantExpression ce)
            {
                return Expression.Constant(Convert.ToInt64(ce.Value));
            }

            return pExpression;
        }
    }
}
