﻿using System.Data;

namespace SqlFu.Mapping.Internals
{
    public class DynamicMapper : IMapReaderToPoco<object>
    {
        private string[] _columns;
        
        public object Map(IDataReader reader, string parentPrefix)
        {
            if (_columns == null)
            {
                _columns=new string[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    _columns[i] = reader.GetName(i);
                }
            }
            var result = new SqlFuDynamic(_columns);
            reader.GetValues(result.Values);
            return result;

        }       
    }
}