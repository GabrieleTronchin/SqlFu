﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using SqlFu.Configuration.Internals;

namespace SqlFu.Mapping.Internals
{
    public class Mapper<T> : IMapReaderToPoco<T>
    {
       
        public Mapper(TableInfo info, IMapToPoco customMapper, string queryId)
        {
            _info = info;
      
            _converter = info.Converter;
            _customMapper = customMapper;
            _queryId = queryId;
            _columns = _info.Columns.ToArray();
        }

      
        private void InitColumnsIndexes()
        {
            _indexes = new int[_columns.Length];
            _columns.ForEach((i, s) => _indexes[i] = -1);
        }

        object _sync=new object();
      
        public T Map(IDataReader reader, string parentPrefix="")
        {
            lock (_sync)
            {
                if (_indexes == null)
                {
                    ConfigureIndexes(reader, parentPrefix);
                }
            }            
            var result = (T)typeof (T).GetFactory()();
   
           Populate(result, reader,parentPrefix);
            return result;
        }

        private void ConfigureIndexes(IDataReader reader,string prefix)
        {
            InitColumnsIndexes();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var colIdx = Array.Find(_columns, s => (prefix+s.Name).ToUpper() == name.ToUpper());
                if (colIdx !=null)
                {
                    _indexes[colIdx.PocoIdx] = i;
                }
            }
        }

        private int[] _indexes;
        private Action<T, IDataReader, IManageConverters, IMapToPoco> _mapper;
        void Populate(T poco, IDataReader reader, string parentPrefix)
        {
            if (_mapper == null)
            {
                //this.LogDebug("Creating mapping delegate for {0}", poco.GetType());
                var populator = new PopulatePocoGenerator<T>();
                var items = new List<Expression>();
                items.AddRange(populator.ReadValuesIntoArray(reader.FieldCount));
                for (var i = 0; i < _columns.Length; i++)
                {
                    var column = _columns[i];

                    if (CanBeMappedFromValue(column) && _indexes[i] == -1) continue;

                    var i1 = _indexes[i];

                    //one value to map
                    if (CanBeMappedFromValue(column))
                    {
                        var justCast = column.Type.IsAssignableFrom(reader.GetFieldType(i1)) || column.Type==typeof(object);
                        items.Add(populator.AssignPropertyValue(column, i1, justCast));
                        continue;
                    }

                items.Add(populator.AssignCustomMappedValue(column, parentPrefix, _queryId));

                }
                
                var body = Expression.Block(new[] {populator.ValuesVar},items);

  //              var name = "Map"+typeof(T);
//                var methBldr = _typeBuilder.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static);
               
                //typeof(T).get
                _mapper=Expression.Lambda<Action<T, IDataReader, IManageConverters, IMapToPoco>>
                    (body, populator.PocoExpr, populator.ReaderExpr, populator.Converter, populator.CustomMapExpr)
                    .Compile();
                //var t = _typeBuilder.CreateType();
                

                //_mapper =Delegate.CreateDelegate(typeof (Action<T, IDataReader, IManageConverters, IMapToPoco>),t.GetMethod(name,BindingFlags.Static|BindingFlags.Public)) as
                //        Action<T, IDataReader, IManageConverters, IMapToPoco>;

            }
            _mapper(poco, reader, _converter, _customMapper);
        } 
        
     

        private static bool CanBeMappedFromValue(ColumnInfo column)
        {
            return !column.IsComplex || column.HasConverter;
        }


        private TableInfo _info;
        private readonly TypeBuilder _typeBuilder;
        private readonly IManageConverters _converter;
        private readonly IMapToPoco _customMapper;
        private readonly string _queryId;
        private ColumnInfo[] _columns;
    }

    
}