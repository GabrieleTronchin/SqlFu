﻿using System;
using System.Data.Common;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SqlFu.Builders;
using SqlFu.Builders.Crud;
using SqlFu.Configuration;
using SqlFu.Configuration.Internals;

namespace SqlFu
{
    
    public static class CrudHelpers
    {

        public static InsertedId Insert<T>(this DbConnection db, T data, Action<IInsertableOptions<T>> cfg = null)
        {
            var info = db.GetPocoInfo<T>();
            var options = info.CreateInsertOptions<T>();
            cfg?.Invoke(options);

            var provider = db.Provider();
            var builder=new InsertSqlBuilder(info,data,provider,options);

            return db.GetValue<InsertedId>(builder.GetCommandConfiguration());
        }
        /// <summary>
        /// Inserts and ignores unique constraint exception.
        /// Useful when updating read models
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="db"></param>
        /// <param name="data"></param>
        /// <param name="cfg"></param>
        /// <param name="keyName">unique constraint partial name</param>
     public static void InsertIgnore<T>(this DbConnection db, T data, Action<IInsertableOptions<T>> cfg = null,string keyName=null)
        {
            try
            {
                Insert(db, data, cfg);
            }
            catch (DbException ex) when (db.Provider().IsUniqueViolation(ex,keyName))
            {
                
                //ignore this    
            }
        }
     public static async Task InsertIgnoreAsync<T>(this DbConnection db, T data, CancellationToken? token = null, Action<IInsertableOptions<T>> cfg = null,string keyName=null)
        {
            try
            {
                await InsertAsync(db, data,token,cfg).ConfigureFalse();
            }
            catch (DbException ex) when (db.Provider().IsUniqueViolation(ex,keyName))
            {
                
                //ignore this    
            }
        }

        public static Task<InsertedId> InsertAsync<T>(this DbConnection db, T data,CancellationToken? cancel=null ,Action<IInsertableOptions<T>> cfg = null)
        {
            var info = db.GetPocoInfo<T>();
            var options = info.CreateInsertOptions<T>();
            cfg?.Invoke(options);

            var provider = db.Provider();
            var builder=new InsertSqlBuilder(info,data,provider,options);

            return db.GetValueAsync<InsertedId>(builder.GetCommandConfiguration(), cancel??CancellationToken.None);
        }
        static Insertable<T> CreateInsertOptions<T>(this TableInfo info) => 
            new Insertable<T>()
            {
                
                TableName = info.TableName,
                IdentityColumn = info.GetIdentityColumnName()
            };

        public static IBuildUpdateTable<T> Update<T>(this DbConnection db,Action<IHelperOptions> cfg=null)
        {
            var opt = new HelperOptions();
            cfg?.Invoke(opt);
            var executor = new CustomSqlExecutor(db);
            opt.EnsureTableName(db.GetPocoInfo<T>());
            return new UpdateTableBuilder<T>(executor, db.GetExpressionSqlGenerator(), db.Provider(), opt);
        }

        /// <summary>
        /// Perform update table with data from an anonymous object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="db"></param>
        /// <param name="columns">Select which columns to update from an anonymous object</param>
        /// <param name="cfg">Configure name and other</param>
        /// <returns></returns>
        public static IBuildUpdateTableFrom<T> UpdateFrom<T>(this DbConnection db, Func<IUpdateColumns, IColumnsToUpdate<T>> columns, Action<IHelperOptions> cfg) where  T:class 
        {
            var options=new HelperOptions(); 
            var u = new UpdateColumns();
            cfg(options);
            options.EnsureTableName(db.GetPocoInfo<T>());
            var builder = columns(u) as UpdateColumns.CreateBuilder<T>;
            var executor=new CustomSqlExecutor(db);
            var updater=new UpdateTableBuilder<T>(executor,db.GetExpressionSqlGenerator(),db.Provider(),options);
            builder.PopulateBuilder(updater);
            return updater;
        }

        public static int CountRows<T>(this DbConnection db,Expression<Func<T,bool>> condition=null)
            => db.QueryValue(d =>
            {
                var q=d.From<T>().Where(c=>true);
                if (condition != null)
                {
                    q = q.And(condition);
                }
                return q.Select(c => c.Count());
            });
        

        public static int DeleteFromAnonymous<T>(this DbConnection db,T data,Action<IHelperOptions> opt,Expression<Func<T, bool>> criteria = null)
        {
            var options=new HelperOptions();
            opt(options);
            var name = db.Provider().EscapeTableName(new TableName(options.TableName, options.DbSchema));
            var builder = new DeleteTableBuilder(name, db.GetExpressionSqlGenerator());
            if (criteria != null) builder.WriteCriteria(criteria);
            return db.Execute(builder.GetCommandConfiguration());
        }

        public static int DeleteFrom<T>(this DbConnection db,Expression<Func<T, bool>> criteria=null)
        {
            var builder=new DeleteTableBuilder(db.GetTableName<T>(),db.GetExpressionSqlGenerator());
            if (criteria!=null) builder.WriteCriteria(criteria);
            return db.Execute(builder.GetCommandConfiguration());
        }

        public static Task<int> DeleteFromAsync<T>(this DbConnection db,CancellationToken token,Expression<Func<T, bool>> criteria=null)
        {
            var builder=new DeleteTableBuilder(db.GetTableName<T>(), db.GetExpressionSqlGenerator());
            if (criteria!=null) builder.WriteCriteria(criteria);
            return db.ExecuteAsync(builder.GetCommandConfiguration(),token);
        }
      
    }
}