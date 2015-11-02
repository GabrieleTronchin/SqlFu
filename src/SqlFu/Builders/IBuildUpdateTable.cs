﻿using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFu.Builders
{
    public interface IBuildUpdateTable<T>:IBuildUpdateTableFrom<T>
    {
        IBuildUpdateTable<T> Set(Expression<Func<T, object>> column, Expression<Func<T, object>> statement);
        IBuildUpdateTable<T> Set(Expression<Func<T, object>> column, object value);
        IBuildUpdateTable<T> Set(string propertyName, object value);
    }

    public interface IBuildUpdateTableFrom<T>:IExecuteSql
    {
     
        IExecuteSql Where(Expression<Func<T, bool>> criteria);        

    }

    public interface IUpdateColumns
    {
        /// <summary>
        /// Specifies the columns to be updated and which should be ignored
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data">Anonymous object</param>
       
        /// <returns></returns>
        IIgnoreColumns<T> FromData<T>(T data) where T : class;

    }

    public interface IIgnoreColumns<T>:IColumnsToUpdate<T>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ignore">List of columns to ignore</param>
        /// <returns></returns>
        IColumnsToUpdate<T> Ignore(params Expression<Func<T, object>>[] ignore);
    }

    public interface IExecuteSql
    {
        int Execute();
        Task<int> ExecuteAsync(CancellationToken token);
    }
}