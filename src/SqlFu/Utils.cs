﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using SqlFu.Builders.CreateTable;
using SqlFu.Builders.Expressions;
using SqlFu.Configuration.Internals;
using SqlFu.Providers;

namespace SqlFu
{
    public static class Utils
    {
        internal static ExpressionWriterHelper CreateWriterHelper(this DbConnection db) => new ExpressionWriterHelper(db.SqlFuConfig().TableInfoFactory,db.GetProvider());

        /// <summary>
        /// Every type named '[something][suffix]' will use the table name 'something'
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="match"></param>
        /// <param name="schema"></param>
        /// <param name="suffix"></param>
        public static void 
            AddSuffixTableConvention(this SqlFuConfig cfg,Func<Type,bool> match=null,string schema=null,string suffix="Row")
        {
            suffix.MustNotBeNull();
            match = match ?? (t => t.Name.EndsWith(suffix));
            cfg.AddNamingConvention(match, t => 
            new TableName(t.Name.SubstringUntil(suffix),schema));
        }

        public static DbFunctions GetDbFunctions(this DbConnection db) => db.GetProvider().Functions;
        public static T GetDbFunctions<T>(this DbConnection db) where T:DbFunctions => db.GetProvider().Functions as T;

        public static SqlFuConfig SqlFuConfig(this DbConnection db)
        {
            return SqlFuManager.Config;
        }

        public static TableInfo GetTableInfo(this Type type)
        {
            return SqlFuManager.Config.TableInfoFactory.GetInfo(type);
        }

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

        //public static string GetColumnName(this ITableInfoFactory factory, MemberExpression member,IEscapeIdentifier provider)
        //{
        //    return factory.GetInfo(member.Expression.Type).GetColumnName(member, provider);
        //}

        /// <summary>
        /// Returns the underlying type, usually int. Works with nullables too
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type GetUnderlyingTypeForEnum(this Type type)
        {
            if (!type.IsNullable()) return type.GetEnumUnderlyingType();
            return type.GetGenericArgument().GetEnumUnderlyingType();
        }
        
        /// <summary>
        /// Is Enum or nullable of Enum
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsEnum(this Type type)
        {
            return type.IsEnum || (type.IsNullable() && type.GetGenericArgument().IsEnum);
        }
       
        public static string GetUniqueHash(this string data)
        {
            return Convert.ToBase64String(data.MurmurHash());
        }

        public static string FormatCommand(this IDbCommand cmd)
        {
            return FormatCommand(cmd.CommandText,
                                 (cmd.Parameters.Cast<IDbDataParameter>()
                                     .ToDictionary(p => p.ParameterName, p => p.Value)));
        }

        public static bool IsListParam(this object data)
        {
            if (data == null) return false;
            //var type = data.GetType();
            return data is IEnumerable && !(data is string) && !(data is byte[]);
            // return type.Implements<IEnumerable>() && typeof (string) != type && typeof (byte[]) != type;
        }

        public static string FormatCommand(string sql, IDictionary<string, object> args)
        {
            var sb = new StringBuilder();
            if (sql == null)
                return "";
            sb.Append(sql);
            if (args != null && args.Count > 0)
            {
                sb.Append("\n");
                foreach (var kv in args)
                {
                    sb.AppendFormat("\t -> {0} [{1}] = \"", kv.Key,kv.Value?.GetType().Name ?? "null");
                    sb.Append(kv.Value).Append("\"\n");
                }

                sb.Remove(sb.Length - 1, 1);
            }
            return sb.ToString();
        }

        public static bool IsCustomObjectType(this Type t)
        {
            return t.IsClass && (Type.GetTypeCode(t) == TypeCode.Object);
        }

        public static bool IsCustomObject<T>(this T t)
        {
            return !(t is ValueType) && (Type.GetTypeCode(t.GetType()) == TypeCode.Object);
        }

       
    }
}
