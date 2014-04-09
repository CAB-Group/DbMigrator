using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Transactions;
using IsolationLevel = System.Transactions.IsolationLevel;

namespace DbMigrator
{
	class Database
	{
		private readonly Func<IDbConnection> _connectionFactory;

		public Database(Func<IDbConnection> connectionFactory)
		{
			_connectionFactory = connectionFactory;
		}

		public void Execute(Action<DatabaseExpression> action)
		{
			using (var transactionScope = CreateTransactionScope())
			using (var connection = GetConnection())
			{
				var expression = new DatabaseExpression(connection);
				action(expression);
				transactionScope.Complete();
			}
		}

		private static TransactionScope CreateTransactionScope()
		{
			return new TransactionScope(TransactionScopeOption.Required,
												 new TransactionOptions
													 {
														 IsolationLevel = IsolationLevel.ReadCommitted,
														 Timeout = TimeSpan.MaxValue
													 });
		}

		public int Execute(string sql, object param = null)
		{
			using (var transactionScope = CreateTransactionScope())
			using (var connection = GetConnection())
			{
				var affectedRows = Math.Max(0, connection.Execute(sql, param, commandTimeout: 0));
				transactionScope.Complete();
				return affectedRows;
			}
		}

		public IEnumerable<T> Query<T>(string sql, object param = null)
		{
			using (var connection = GetConnection())
				return connection.Query<T>(sql, param, commandTimeout: 0);
		}

		public IEnumerable<dynamic> Query(string sql, object param = null)
		{
			using (var connection = GetConnection())
				return connection.Query(sql, param, commandTimeout: 0);
		}

		private IDbConnection GetConnection()
		{
			return _connectionFactory();
		}

		internal class DatabaseExpression
		{
			private readonly IDbConnection _connection;

			public DatabaseExpression(IDbConnection connection)
			{
				_connection = connection;
			}

			public int Execute(string sql, object param = null)
			{
				return Math.Max(0, _connection.Execute(sql, param, commandTimeout: 0));
			}
		}
	}
}