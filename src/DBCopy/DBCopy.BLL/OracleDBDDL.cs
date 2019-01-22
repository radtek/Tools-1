﻿using System;
using System.Data.Common;
using Oracle.ManagedDataAccess.Client;

namespace DBCopy.BLL
{
    public class OracleDBDDL : BasicDBDDL
    {
        public override DbProviderFactory DbProviderFactory => OracleClientFactory.Instance;

        public OracleDBDDL(String connectionString) : base(connectionString) { }

        /// <summary>
        /// 获取数据库的表空间名
        /// </summary>
        /// <returns></returns>
        public virtual String GetTableSpaceName()
        {
            //select tablespace_name,file_id,bytes/1024/1024,file_name  from dba_data_files order by file_id;
            throw new NotImplementedException();
        }

        public override string Get_GetAllTablesSql()
        {
            return "select * from user_tables";
        }

        public override string Get_GetTablePrimaryKeysSql(string tableName)
        {
            return String.Format(@"select col.column_name 
                                    from user_constraints con,  user_cons_columns col
                                    where con.constraint_name = col.constraint_name
                                    and con.constraint_type = 'P'
                                    and col.table_name = '{0}'", tableName);
        }

        public override string Get_CreateTablePrimaryKeySql(string tableName, string colName)
        {
            return String.Format(@"alter table {0}
                                    add constraint {0}_PK primary key({1})", tableName, colName);

        }

        public override string Get_GetTableAllColsSql(string tableName)
        {
            return String.Format(@"select col.column_name , uc.constraint_type,
                                    case uc.constraint_type when 'P' then 1 else 0 end ""IsPrimaryKey""
                                    from user_tab_columns col left join user_cons_columns ucc on ucc.table_name = col.table_name and ucc.column_name = col.column_name
                                    left join user_constraints uc on uc.constraint_name = ucc.constraint_name and uc.constraint_type = 'P'
                                    where col.table_name = '{0}'", tableName);
        }

        public override string Get_GetTableIndexSql(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string Get_CreateTableIndexSql(TableIndex tbIndex)
        {
            var cols = String.Join(",", tbIndex.ColumnNames);
            return $"create unique index {tbIndex.Name} on {tbIndex.TableName} ({cols}) tablespace APSD";
        }
    }
}