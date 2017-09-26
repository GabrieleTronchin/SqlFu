﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using SqlFu.Builders;
using SqlFu.Builders.Expressions;
#if NET13
using System.Reflection;
#endif

namespace SqlFu.Providers
{
    public class DbProviderExpressions : IDbProviderExpressions
    {
        internal const int Placeholder = 1;

        public DbProviderExpressions()
        {
            LinkMethods(()=>Placeholder.Count(),CountAll);
            LinkMethods(()=>Placeholder.Count(23),Count);
            LinkMethods(()=>Placeholder.Sum(3),Sum);
            LinkMethods(()=>Placeholder.Max(2),Max);
            LinkMethods(()=>Placeholder.Min(2),Min);
            LinkMethods(()=>Placeholder.Avg(2),Avg);
            LinkMethods(()=>Placeholder.Floor(2),Floor);
            LinkMethods(()=>Placeholder.Ceiling(2),Ceiling);
            LinkMethods(()=>Placeholder.Concat(),Concat);
            LinkMethods(()=>Placeholder.Round(2,2),Round);                     
            LinkMethods(()=>DateTime.MinValue.Between(DateTime.MinValue, DateTime.MinValue),BetweenDates);                                 
        }

        private void BetweenDates(MethodCallExpression arg1, StringBuilder sb, IGenerateSqlFromExpressions writer)
        {
            var date = writer.GetColumnsSql(arg1.Arguments[0]);
            sb.Append($"{date}>={writer.GetSql(arg1.Arguments[1])} and {date}<={writer.GetSql(arg1.Arguments[2])}");
        }

        /// <summary>
        /// Maps the extension method to the actual sql generating function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="function"></param>
        /// <param name="func"></param>
        public void LinkMethods<T>(Expression<Func<T>> function, Action<MethodCallExpression, StringBuilder, IGenerateSqlFromExpressions> func)
        {
            var metho = function.Body as MethodCallExpression;
            metho.MustNotBeNull();
            Functions[GetKey(metho)]=func;
        }

        private string GetKey(MethodCallExpression e) => e.Method.DeclaringType + e.Method.Name+e.Arguments.Count;
        
        #region string & datetime functions
        public virtual string Substring(string column, int pos, int length)
        {
            column.MustNotBeNull();
            var idx = pos + 1;
            return "substring({0},{1},{2})".ToFormat(column, idx, length);
        }

        public string ToUpper(string column)
        {
            return "upper({0})".ToFormat(column);
        }

        public string ToLower(string column)
        {
            return "lower({0})".ToFormat(column);
        }

        public virtual string Length(string column)
        {
            return "len({0})".ToFormat(column);
        }

        public virtual string Year(string column)
        {
            return "year({0})".ToFormat(column);
        }

        public virtual string Day(string column)
        {
            return "day({0})".ToFormat(column);
        }
        #endregion

        #region Db Functions
        protected Dictionary<string, Action<MethodCallExpression, StringBuilder,IGenerateSqlFromExpressions>> Functions =new Dictionary<string, Action<MethodCallExpression, StringBuilder, IGenerateSqlFromExpressions>>();

        private void Count(MethodCallExpression method, StringBuilder sb,IGenerateSqlFromExpressions manager)
            => sb.Append($"count({manager.GetColumnsSql(method.Arguments[1])})");
        
        private void CountAll(MethodCallExpression method, StringBuilder sb, IGenerateSqlFromExpressions manager)
            => sb.Append("count(*)");
       
  
        private void Sum(MethodCallExpression method, StringBuilder sb, IGenerateSqlFromExpressions w)
       => sb.Append($"sum({w.GetColumnsSql(method.Arguments[1])})");

        private void Min(MethodCallExpression method, StringBuilder sb, IGenerateSqlFromExpressions manager)
         => sb.Append($"min({manager.GetColumnsSql(method.Arguments[1])})");

        private void Max(MethodCallExpression method, StringBuilder sb, IGenerateSqlFromExpressions manager) 
            => sb.Append($"max({manager.GetColumnsSql(method.Arguments[1])})");

        private void Avg(MethodCallExpression method, StringBuilder sb, IGenerateSqlFromExpressions manager)
         => sb.Append($"avg({manager.GetColumnsSql(method.Arguments[1])})");

        private void Round(MethodCallExpression method, StringBuilder sb, IGenerateSqlFromExpressions writer)
        {
            sb.Append("round(");
            sb.Append(writer.GetColumnsSql(method.Arguments[1]));
            sb.Append(",");
            sb.Append(writer.GetSql(method.Arguments[2]));
            sb.Append(")"); 
        }
    
        private void Floor(MethodCallExpression method, StringBuilder sb, IGenerateSqlFromExpressions manager) 
            => sb.Append($"floor({manager.GetColumnsSql(method.Arguments[1])})");

        private void Ceiling(MethodCallExpression method, StringBuilder sb, IGenerateSqlFromExpressions manager)
        => sb.Append($"ceiling({manager.GetColumnsSql(method.Arguments[1])})");



        private void Concat(MethodCallExpression method,StringBuilder sb,IGenerateSqlFromExpressions writer)
        {
           sb.Append("concat(");
            foreach (var arg in method.Arguments[1].CastAs<NewArrayExpression>().Expressions)
            {
                sb.Append(writer.GetColumnsSql(arg));
                sb.Append(",");
            }
            sb.RemoveLastIfEquals(',');
            sb.Append(")");           
        }

        public string GetSql(MethodCallExpression method, IGenerateSqlFromExpressions manager)
        {
            var func = Functions.GetValueOrDefault(GetKey(method));
            if (func == null) throw new NotSupportedException("Unrecognized function {0}".ToFormat(method.Method.Name));
            var sb=new StringBuilder();
            func(method,sb, manager);
            return sb.ToString();
        } 
#endregion
    }
}