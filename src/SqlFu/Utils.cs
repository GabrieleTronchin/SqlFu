﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using SqlFu.Builders.Expressions;
using SqlFu.Configuration;
using SqlFu.Configuration.Internals;
using SqlFu.Providers;

namespace SqlFu
{
    public static class Utils
    {
       

        /// <summary>
        /// Used to generate sql bits from expressions
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static IGenerateSqlFromExpressions GetExpressionSqlGenerator(this DbConnection db)
            => new ExpressionSqlGenerator(db.Provider().ExpressionsHelper,SqlFuManager.Config.TableInfoFactory,db.Provider());

        /// <summary>
        /// Sets table name/schema in one statement
        /// </summary>
        /// <param name="opt"></param>
        /// <param name="name"></param>
        /// <param name="schema"></param>
        /// <returns></returns>
        public static IHelperOptions SetTableName(this IHelperOptions opt, string name, string schema = null)
        {
            name.MustNotBeEmpty();
            opt.TableName = name;
            opt.DbSchema = schema;
            return opt;
        }

        /// <summary>
        /// Every type named '[something][suffix]' will use the table name 'something'
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="match"></param>
        /// <param name="schema"></param>
        /// <param name="suffix"></param>
        public static void 
            AddSuffixTableConvention(this SqlFuConfig cfg,Func<Type, bool> match=null,string schema=null,string suffix="Row")
        {
            suffix.MustNotBeNull();
            match = match ?? (t => t.Name.EndsWith(suffix));
            cfg.AddNamingConvention(match, t => 
            new TableName(t.Name.SubstringUntil(suffix),schema));
        }

        public static SqlFuConfig SqlFuConfig(this DbConnection db) => SqlFuManager.Config;

        public static TableInfo GetTableInfo(this Type type) => SqlFuManager.Config.TableInfoFactory.GetInfo(type);

        public static string GetColumnName(this TableInfo info, MemberExpression member, IEscapeIdentifier provider)
        {
            var col = info.Columns.First(d => d.PropertyInfo.Name == member.Member.Name);
            return provider.EscapeIdentifier(col.Name);
        }
        public static string GetColumnName(this TableInfo info, string property, IEscapeIdentifier provider)
        {
            var col = info.Columns.First(d => d.PropertyInfo.Name == property);
            return provider.EscapeIdentifier(col.Name);
        }

        /// <summary>
        /// Returns the underlying type, usually int. Works with nullables too
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type GetUnderlyingTypeForEnum(this Type type)
        {
            if (!type.IsNullable()) return Enum.GetUnderlyingType(type);
            return Enum.GetUnderlyingType(type.GetGenericArgument());
        }

        /// <summary>
        /// Is Enum or nullable of Enum
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsEnumType(this Type type) => type.IsEnum() || (type.IsNullable() && type.GetGenericArgument().IsEnum());

       
        public static string GetCachingId(this string data) => Convert.ToBase64String(data.MurmurHash());

        public static string FormatCommand(this DbCommand cmd) => 
            FormatCommand(cmd.CommandText,
            (cmd.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName, p => p.Value)));

        public static bool IsListParam(this object data) => 
            data is IEnumerable && !(data is string) && !(data is byte[]);

        public static string FormatCommand(string sql, IDictionary<string, object> args)
        {
            var sb = new StringBuilder();
            if (sql == null)
                return "";
            sb.Append(sql);
            if (args != null && args.Count > 0)
            {
                sb.AppendLine();
                foreach (var kv in args)
                {
                    sb.Append($"\t -> {kv.Key} [{kv.Value?.GetType().Name ?? "null"}] = \"");
                    sb.Append($"{kv.Value}\"").AppendLine();
                }

                sb.Remove(sb.Length - 1, 1);
            }
            return sb.ToString();
        }

        public static bool IsCustomObjectType(this Type t) => t.IsUserDefinedClass();

        public static bool IsCustomObject<T>(this T t)
            => t.GetType().IsCustomObjectType();
      

       
    }
}
