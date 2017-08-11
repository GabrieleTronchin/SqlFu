﻿using System;
using System.Data.Common;
using SqlFu.Builders;
using SqlFu.Builders.CreateTable;
using SqlFu.Configuration;
using SqlFu.Configuration.Internals;

namespace SqlFu
{
    public static class DDLExtensions
    {
        [Obsolete("All db/table create/drop related code will be removed next major version")]
        public static void DropTable<T>(this DbConnection cnx)
        {
          var info = cnx.GetPocoInfo<T>();
           cnx.DropTable(info.Table.Name, info.Table.Schema);          
        }
        [Obsolete("All db/table create/drop related code will be removed next major version")]
        public static void DropTable(this DbConnection cnx, string name, string schema = "") 
            => cnx.Provider().DatabaseTools.DropTableIfExists(cnx,new TableName(name,schema));
        [Obsolete("All db/table create/drop related code will be removed next major version")]
        public static void Truncate<T>(this DbConnection db)
        {
            var info = db.GetPocoInfo<T>();
            var name = info.EscapeName(db.Provider());
            db.Execute($"truncate {name}");
        }
        [Obsolete("All db/table create/drop related code will be removed next major version")]
        public static bool TableExists(this DbConnection cnx, string name, string schema = null) 
            => cnx.Provider().DatabaseTools.TableExists(cnx,new TableName(name,schema));

        [Obsolete("All db/table create/drop related code will be removed next major version")]
        public static bool TableExists<T>(this DbConnection cnx)
        {
            var info = cnx.GetPocoInfo<T>();
            return cnx.Provider().DatabaseTools.TableExists(cnx,info.Table);
        }

        /// <summary>
        /// Generates and execute the table creation sql using the specified poco as the table representation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="db"></param>
        /// <param name="cfg"></param>
        [Obsolete("All db/table create/drop related code will be removed next major version")]
        public static void CreateTableFrom<T>(this DbConnection db, Action<IConfigureTable<T>> cfg)
        {
            var provider = db.Provider();
            var tcfg = new TableConfigurator<T>(provider);
            cfg(tcfg);

            var data = tcfg.Data;

            var info = db.GetPocoInfo<T>();
           data.Update(info);

            var builder = new CreateTableBuilder(provider);
            
            if (db.TableExists<T>())
            {
                switch (tcfg.Data.CreationOptions)
                {
                    case TableExistsAction.Throw:
                        throw new TableExistsException(tcfg.Data.TableName);
                    case TableExistsAction.DropIt:
                        db.DropTable<T>();
                        break;
                      case TableExistsAction.Ignore:
                        return;
                }          
            }


            db.Execute(c => c.Sql(builder.GetSql(tcfg.Data)));
        }

      
    }
}