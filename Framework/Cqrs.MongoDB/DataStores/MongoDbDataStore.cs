﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="Chinchilla Software Limited">
// // 	Copyright Chinchilla Software Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Cqrs.DataStores;
using cdmdotnet.Logging;
using MongoDB.Driver;
using Cqrs.Entities;

namespace Cqrs.MongoDB.DataStores
{
	public class MongoDbDataStore<TData> : IDataStore<TData>
		where TData : Entity
	{
		protected IMongoCollection<TData> MongoCollection { get; private set; }

		protected ILogger Logger { get; private set; }

		public MongoDbDataStore(ILogger logger, IMongoCollection<TData> mongoCollection)
		{
			Logger = logger;
			MongoCollection = mongoCollection;
			// MongoCollection.Database.RequestStart();
		}

		#region Implementation of IEnumerable

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
		/// </returns>
		/// <filterpriority>1</filterpriority>
		public IEnumerator<TData> GetEnumerator()
		{
			return MongoCollection.AsQueryable().GetEnumerator();
		}

		/// <summary>
		/// Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion

		#region Implementation of IQueryable

		/// <summary>
		/// Gets the expression tree that is associated with the instance of <see cref="T:System.Linq.IQueryable"/>.
		/// </summary>
		/// <returns>
		/// The <see cref="T:System.Linq.Expressions.Expression"/> that is associated with this instance of <see cref="T:System.Linq.IQueryable"/>.
		/// </returns>
		public Expression Expression
		{
			get { return MongoCollection.AsQueryable().Expression; }
		}

		/// <summary>
		/// Gets the type of the element(s) that are returned when the expression tree associated with this instance of <see cref="T:System.Linq.IQueryable"/> is executed.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.Type"/> that represents the type of the element(s) that are returned when the expression tree associated with this object is executed.
		/// </returns>
		public Type ElementType
		{
			get { return MongoCollection.AsQueryable().ElementType; }
		}

		/// <summary>
		/// Gets the singleResultQuery provider that is associated with this data source.
		/// </summary>
		/// <returns>
		/// The <see cref="T:System.Linq.IQueryProvider"/> that is associated with this data source.
		/// </returns>
		public IQueryProvider Provider
		{
			get { return MongoCollection.AsQueryable().Provider; }
		}

		#endregion

		#region Implementation of IDataStore<TData>

		public virtual void Add(TData data)
		{
			Logger.LogDebug("Adding data to the Mongo database", "MongoDataStore\\Add");
			try
			{
				DateTime start = DateTime.Now;
				MongoCollection.InsertOne(data);
				DateTime end = DateTime.Now;
				Logger.LogDebug(string.Format("Adding data in the Mongo database took {0}.", end - start), "MongoDataStore\\Add");
			}
			finally
			{
				Logger.LogDebug("Adding data to the Mongo database... Done", "MongoDataStore\\Add");
			}
		}

		public virtual void Add(IEnumerable<TData> data)
		{
			Logger.LogDebug("Adding data collection to the Mongo database", "MongoDataStore\\Add");
			try
			{
				MongoCollection.InsertMany(data);
			}
			finally
			{
				Logger.LogDebug("Adding data collection to the Mongo database... Done", "MongoDataStore\\Add");
			}
		}

		/// <summary>
		/// Will mark the <paramref name="data"/> as logically (or soft) by setting <see cref="Entity.IsLogicallyDeleted"/> to true
		/// </summary>
		public virtual void Remove(TData data)
		{
			Logger.LogDebug("Removing data from the Mongo database", "MongoDataStore\\Remove");
			try
			{
				data.IsLogicallyDeleted = true;
				Update(data);
			}
			finally
			{
				Logger.LogDebug("Removing data from the Mongo database... Done", "MongoDataStore\\Remove");
			}
		}

		public virtual void Destroy(TData data)
		{
			Logger.LogDebug("Removing data from the Mongo database", "MongoDataStore\\Destroy");
			try
			{
				DateTime start = DateTime.Now;
				MongoCollection.DeleteOne(x => x.Rsn == data.Rsn);
				DateTime end = DateTime.Now;
				Logger.LogDebug(string.Format("Updating data in the Mongo database took {0}.", end - start), "MongoDataStore\\Update");
			}
			finally
			{
				Logger.LogDebug("Removing data from the Mongo database... Done", "MongoDataStore\\Destroy");
			}
		}

		public virtual void RemoveAll()
		{
			Logger.LogDebug("Removing all from the Mongo database", "MongoDataStore\\RemoveAll");
			try
			{
				MongoCollection.DeleteMany(x => true);
			}
			finally
			{
				Logger.LogDebug("Removing all from the Mongo database... Done", "MongoDataStore\\RemoveAll");
			}
		}

		public virtual void Update(TData data)
		{
			Logger.LogDebug("Updating data in the Mongo database", "MongoDataStore\\Update");
			try
			{
				DateTime start = DateTime.Now;
				MongoCollection.ReplaceOne(x => x.Rsn == data.Rsn, data);
				DateTime end = DateTime.Now;
				Logger.LogDebug(string.Format("Updating data in the Mongo database took {0}.", end - start), "MongoDataStore\\Update");
			}
			finally
			{
				Logger.LogDebug("Updating data to the Mongo database... Done", "MongoDataStore\\Update");
			}
		}

		#endregion

		#region Implementation of IDisposable

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public virtual void Dispose()
		{
			// MongoCollection.Database.RequestDone();
		}

		#endregion
	}
}