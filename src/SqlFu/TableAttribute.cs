﻿using System;
using SqlFu.Configuration.Internals;

namespace SqlFu
{
    public class TableAttribute:Attribute,ITableInfo
    {
        public string Name { get; set; }
        public string DbSchema { get; set; }
        /// <summary>
        /// Gets or sets the name of the autoincremented column
        /// </summary>
        public string IdentityColumn { get; set; }
        public TableAttribute(string name)
        {
            Name = name;
        }
    }
}