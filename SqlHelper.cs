using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

public class SqlHelper
    {
        #region Public Methods

        public async static Task<List<TResult>> ExecuteReaderAsync<TResult>(string connectionString, string command, CommandType commandType = CommandType.StoredProcedure, bool isFunc = false, object sqlParams = null)
        {
            var results = new List<TResult>();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    SqlCommand cmd = new SqlCommand(command, conn)
                    {
                        CommandTimeout = 0,
                        CommandType = commandType
                    };

                    AddParams(cmd, sqlParams, isFunc);

                    var resultType = typeof(TResult);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var result = Activator.CreateInstance(resultType);

                            var properties = resultType.GetProperties();

                            foreach (var property in properties)
                            {
                                if (property.GetCustomAttributes(typeof(SqlNotMappedAttribute), false).Any())
                                    continue;

                                var propertyType = property.PropertyType;

                                string parameterName = "";

                                var sqlParamererAttribute = property.GetCustomAttributes(typeof(SqlParameterAttribute), true).FirstOrDefault();

                                if (sqlParamererAttribute != null)
                                    parameterName = ((SqlParameterAttribute)sqlParamererAttribute).Name;
                                else
                                    parameterName = property.Name;

                                try
                                {
                                    resultType.GetProperty(property.Name).SetValue(result, ConvertValue(propertyType, reader[parameterName]));
                                }
                                catch (IndexOutOfRangeException) { }
                            }

                            results.Add((TResult)result);
                        }
                    }

                    return results;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async static Task<TResult> ExecuteScalarAsync<TResult>(string connectionString, string command, CommandType commandType = CommandType.StoredProcedure, bool isFunc = false, object sqlParams = null)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    SqlCommand cmd = new SqlCommand(command, conn)
                    {
                        CommandTimeout = 0,
                        CommandType = commandType
                    };

                    AddParams(cmd, sqlParams, isFunc);

                    var result = await cmd.ExecuteScalarAsync();

                    ReadOutParam(cmd, sqlParams);

                    return (TResult)ConvertValue(typeof(TResult), result);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async static Task<int> ExecuteAsync(string connectionString, string command, CommandType commandType = CommandType.StoredProcedure, bool isFunc = false, object sqlParams = null)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    SqlCommand cmd = new SqlCommand(command, conn)
                    {
                        CommandTimeout = 0,
                        CommandType = commandType
                    };

                    AddParams(cmd, sqlParams, isFunc);

                    var result = await cmd.ExecuteNonQueryAsync();

                    ReadOutParam(cmd, sqlParams);

                    return result;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion

        #region Private Methods

        private static object ConvertValue(Type type, object value)
        {
            if (value == null)
                return value;

            if (value == DBNull.Value)
                return type.IsValueType ? Activator.CreateInstance(type) : null;

            if (type.GenericTypeArguments.Any())
                return ConvertValue(type.GenericTypeArguments[0], value);

            switch (type.Name)
            {
                case "Int32":
                    return Convert.ToInt32(value);
                case "Boolean":
                    return Convert.ToBoolean(value);
                case "DateTime":
                    return Convert.ToDateTime(value);
                case "String":
                    return value.ToString();
                case "Decimal":
                    return Convert.ToDecimal(value);
                default:
                    return value;
            }
        }

        private static void AddFunctionQueryParams(SqlCommand cmd, object sqlParams)
        {
            var type = sqlParams.GetType();

            var properties = type.GetProperties();

            cmd.CommandText += " (";

            foreach (var property in properties)
            {
                if (property.GetCustomAttributes(typeof(SqlNotMappedAttribute), true).Any())
                    continue;

                var sqlParameter = new SqlParameter();

                if (property.GetCustomAttributes(typeof(SqlParameterAttribute), true).Any())
                {
                    var sqlParamererAttribute = property.GetCustomAttributes(typeof(SqlParameterAttribute), true).First() as SqlParameterAttribute;
                    sqlParameter.ParameterName = $"@{sqlParamererAttribute.Name}";
                    if (sqlParamererAttribute.DbType != DbType.AnsiString)
                        sqlParameter.DbType = sqlParamererAttribute.DbType;
                }
                else
                    sqlParameter.ParameterName = $"@{property.Name}";

                sqlParameter.Value = property.GetValue(sqlParams, null) ?? DBNull.Value;

                if (cmd.CommandText.Contains("@"))
                    cmd.CommandText += $", {sqlParameter.ParameterName}";
                else
                    cmd.CommandText += $" {sqlParameter.ParameterName}";

                cmd.Parameters.Add(sqlParameter);
            }
            cmd.CommandText += ")";
        }

        private static void AddQueryParams(SqlCommand cmd, object sqlParams)
        {
            var type = sqlParams.GetType();

            var properties = type.GetProperties();

            foreach (var property in properties)
            {
                if (property.GetCustomAttributes(typeof(SqlNotMappedAttribute), true).Any())
                    continue;

                var sqlParameter = new SqlParameter();

                if (property.GetCustomAttributes(typeof(SqlParameterAttribute), true).Any())
                {
                    var sqlParamererAttribute = property.GetCustomAttributes(typeof(SqlParameterAttribute), true).First() as SqlParameterAttribute;

                    sqlParameter.ParameterName = $"@{sqlParamererAttribute.Name}";
                    sqlParameter.Direction = sqlParamererAttribute.ParameterDirection;
                    if (sqlParamererAttribute.DbType != DbType.AnsiString)
                        sqlParameter.DbType = sqlParamererAttribute.DbType;
                }
                else
                    sqlParameter.ParameterName = property.Name;

                sqlParameter.Value = property.GetValue(sqlParams, null) ?? DBNull.Value;

                cmd.Parameters.Add(sqlParameter);
            }
        }

        private static void AddParams(SqlCommand cmd, object sqlParams, bool isFunc)
        {
            if (sqlParams == null)
                return;

            if (isFunc)
                AddFunctionQueryParams(cmd, sqlParams);
            else
                AddQueryParams(cmd, sqlParams);
        }

        private static void ReadOutParam(SqlCommand cmd, object sqlParams)
        {
            if (sqlParams != null &&
                sqlParams.GetType().GetProperties().Any(p => p.GetCustomAttributes(typeof(SqlParameterAttribute), false)
                .Any(o => ((SqlParameterAttribute)o).ParameterDirection == ParameterDirection.Output)))
            {
                var sqlParameters = new SqlParameter[cmd.Parameters.Count];

                var outParam = sqlParams.GetType().GetProperties()
                    .First(p => p.GetCustomAttributes(typeof(SqlParameterAttribute), true).Any(o => ((SqlParameterAttribute)o).ParameterDirection == ParameterDirection.Output));

                var outParamAttribute = outParam.GetCustomAttributes(typeof(SqlParameterAttribute), true).First() as SqlParameterAttribute;

                cmd.Parameters.CopyTo(sqlParameters, 0);

                var sqlParameter = sqlParameters.First(p => p.ParameterName == $"@{outParamAttribute.Name}");

                outParam.SetValue(sqlParams, sqlParameter.Value);
            }
        }

        #endregion
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SqlParameterAttribute : Attribute
    {
        public SqlParameterAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; set; }

        public int Order { get; set; }

        public DbType DbType { get; set; }

        public ParameterDirection ParameterDirection { get; set; }
    }


    public class SqlNotMappedAttribute : Attribute
    {

    }