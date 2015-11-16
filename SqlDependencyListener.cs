using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Objects;
using System.Data.SqlClient;
using System.Reflection;
using System.Data;

//big thanks to http://www.codeproject.com/Articles/496484/SqlDependency-with-Entity-Framework
//big thanks to https://gkulshrestha.wordpress.com/2014/05/02/signalr-with-sql-server-query-notification/

namespace MyProject.Controllers
{
    public class SqlDependencyListener<TEntity> where TEntity : class
    {
        #region Fields
        private DbContext _context;
        private Expression<Func<TEntity, bool>> _query;
        private string _connectionString;
        private Action<string> _clientMethod;
        private string _groupName;

        public event EventHandler<EntityChangeEventArgs<TEntity>> Changed;
        public event EventHandler<NotifierErrorEventArgs> Error;

        private static List<string> _groups = new List<string>();
        #endregion

        public SqlDependencyListener(Expression<Func<TEntity, bool>> query, Action<string> clientMethod, string groupName = null)
        {
            if (_groups.Contains(groupName)) { return; } else { _groups.Add(groupName); }
            GetContext();
            _query = query;
            _connectionString = _context.Database.Connection.ConnectionString;
            _clientMethod = clientMethod;
            _groupName = groupName;
            RegisterNotification();
        }

        private void GetContext()
        {
            string ns = typeof(TEntity).Namespace;
            switch (ns)
            {
                case "Context1":
                    _context = new Model.Context1.Context1Entities();
                    break;
                case "Context2":
                    _context = new Model.Context2.Context2Entities();
                    break;
                default:
                    throw new Exception("No valid database determined from Entity given to SQL Dependency");
            }
        }

        private void RegisterNotification()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (SqlCommand command = GetCommand())
                {
                    command.Connection = connection;
                    SqlDependency sqlDependency = new SqlDependency(command);
                    if(connection.State == ConnectionState.Closed) connection.Open();

                    sqlDependency.OnChange += new OnChangeEventHandler(_sqlDependency_OnChange);
                    SqlDataReader reader = command.ExecuteReader();
                }
            }
        }

        #region LINQquiries
        //these 2 methods allow use of LINQ style queries
        private SqlCommand GetCommand()
        {
            var q = GetCurrent();

            return q.ToSqlCommand();
        }
        private DbQuery<TEntity> GetCurrent()
        {
            var query = _context.Set<TEntity>().Where(_query) as DbQuery<TEntity>;

            return query;
        }
        #endregion

        #region event handlers
        private void _sqlDependency_OnChange(object sender, SqlNotificationEventArgs e)
        {
            if (_context == null) return;
            if (e.Type == SqlNotificationType.Subscribe || e.Info == SqlNotificationInfo.Error)
            {
                var args = new NotifierErrorEventArgs { Reason = e };
                OnError(args);
            }
            else
            {
                var args = new EntityChangeEventArgs<TEntity>();
                OnChanged(args);
            }
        }

        protected virtual void OnChanged(EntityChangeEventArgs<TEntity> e)
        {
            if (Changed != null) Changed(this, e);
            _clientMethod(_groupName);
            RegisterNotification();

        }

        protected virtual void OnError(NotifierErrorEventArgs e)
        {
            if (Error != null) Error(this, e);
        }
        #endregion

        public void Dispose()
        {
            if (_context != null)
            {
                _groups.Remove(_groupName);
                _context.Dispose();
                _context = null;
            }
        }
    }


    public static class Extensions
    {
        public static SqlCommand ToSqlCommand<T>(this DbQuery<T> query)
        {
            SqlCommand command = new SqlCommand();

            command.CommandText = query.ToString();

            var objectQuery = query.ToObjectQuery();

            foreach (var param in objectQuery.Parameters)
            {
                command.Parameters.AddWithValue(param.Name, param.Value);
            }

            return command;
        }

        public static ObjectQuery<T> ToObjectQuery<T>(this DbQuery<T> query)
        {
            var internalQuery = query.GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.Name == "_internalQuery")
                .Select(field => field.GetValue(query))
                .First();

            var objectQuery = internalQuery.GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.Name == "_objectQuery")
                .Select(field => field.GetValue(internalQuery))
                .Cast<ObjectQuery<T>>()
                .First();

            return objectQuery;
        }
    }

    public class EntityChangeEventArgs<T> : EventArgs
    {
        public IEnumerable<T> Results { get; set; }
    }

    public class NotifierErrorEventArgs : EventArgs
    {
        public string Sql { get; set; }
        public SqlNotificationEventArgs Reason { get; set; }
    }

}

