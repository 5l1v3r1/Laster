﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;

namespace Laster.Process.Helpers
{
    /// <summary>
    /// X86 / x64 Warning
    /// </summary>
    public class ExcelHelper
    {
        const string ExcelOleDbConnectionStringTemplate = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0};Extended Properties=\"Excel 8.0;HDR=YES\";";

        /// <summary>
        /// Creates the Excel file from items in DataTable and writes them to specified output file.
        /// </summary>
        public static void CreateXlsFromDataTable(string fullFilePath, params DataTable[] dataTable)
        {
            if (string.IsNullOrEmpty(fullFilePath)) return;

            if (File.Exists(fullFilePath))
                File.Delete(fullFilePath);

            using (OleDbConnection conn = new OleDbConnection(String.Format(ExcelOleDbConnectionStringTemplate, fullFilePath)))
            {
                if (conn.State != ConnectionState.Open)
                    conn.Open();

                foreach (DataTable dt in dataTable)
                {
                    string createTableWithHeaderScript = GenerateCreateTableCommand(dt);
                    OleDbCommand cmd = new OleDbCommand(createTableWithHeaderScript, conn);
                    cmd.ExecuteNonQuery();

                    foreach (DataRow row in dt.Rows) AddNewRow(conn, row);
                }
            }
        }
        static void AddNewRow(OleDbConnection conn, DataRow dataRow)
        {
            string insertCmd = GenerateInsertRowCommand(dataRow);

            using (OleDbCommand cmd = new OleDbCommand(insertCmd, conn))
            {
                AddParametersWithValue(cmd, dataRow);
                cmd.ExecuteNonQuery();
            }
        }
        /// <summary>
        /// Generates the insert row command.
        /// </summary>
        static string GenerateInsertRowCommand(DataRow dataRow)
        {
            StringBuilder stringBuilder = new StringBuilder();
            List<DataColumn> columns = dataRow.Table.Columns.Cast<DataColumn>().ToList();
            string columnNamesCommaSeparated = string.Join(",", columns.Select(x => x.Caption));
            string questionmarkCommaSeparated = string.Join(",", columns.Select(x => "?"));

            stringBuilder.AppendFormat("INSERT INTO [{0}] (", dataRow.Table.TableName);
            stringBuilder.Append(columnNamesCommaSeparated);
            stringBuilder.Append(") VALUES(");
            stringBuilder.Append(questionmarkCommaSeparated);
            stringBuilder.Append(")");
            return stringBuilder.ToString();
        }
        /// <summary>
        /// Adds the parameters with value.
        /// </summary>
        static void AddParametersWithValue(OleDbCommand cmd, DataRow dataRow)
        {
            int paramNumber = 1;

            for (int i = 0; i <= dataRow.Table.Columns.Count - 1; i++)
            {
                if (!ReferenceEquals(dataRow.Table.Columns[i].DataType, typeof(int)) && !ReferenceEquals(dataRow.Table.Columns[i].DataType, typeof(decimal)))
                {
                    cmd.Parameters.AddWithValue("@p" + paramNumber, dataRow[i].ToString().Replace("'", "''"));
                }
                else
                {
                    object value = GetParameterValue(dataRow[i]);
                    OleDbParameter parameter = cmd.Parameters.AddWithValue("@p" + paramNumber, value);
                    if (value is decimal)
                    {
                        parameter.OleDbType = OleDbType.Currency;
                    }
                }

                paramNumber = paramNumber + 1;
            }
        }
        /// <summary>
        /// Gets the formatted value for the OleDbParameter.
        /// </summary>
        static object GetParameterValue(object value)
        {
            if (value is string)
            {
                return value.ToString().Replace("'", "''");
            }
            return value;
        }
        static string GenerateCreateTableCommand(DataTable tableDefination)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool firstcol = true;

            stringBuilder.AppendFormat("CREATE TABLE [{0}] (", tableDefination.TableName);

            foreach (DataColumn tableColumn in tableDefination.Columns)
            {
                if (!firstcol)
                {
                    stringBuilder.Append(", ");
                }
                firstcol = false;

                string columnDataType = "CHAR(255)";

                switch (tableColumn.DataType.Name)
                {
                    case "String":
                        columnDataType = "CHAR(255)";
                        break;
                    case "Int32":
                        columnDataType = "INTEGER";
                        break;
                    case "Decimal":
                        // Use currency instead of decimal because of bug described at 
                        // http://social.msdn.microsoft.com/Forums/vstudio/en-US/5d6248a5-ef00-4f46-be9d-853207656bcc/localization-trouble-with-oledbparameter-and-decimal?forum=csharpgeneral
                        columnDataType = "CURRENCY";
                        break;
                }

                stringBuilder.AppendFormat("[{0}] {1}", tableColumn.ColumnName, columnDataType);
            }
            stringBuilder.Append(")");

            return stringBuilder.ToString();
        }
    }
}