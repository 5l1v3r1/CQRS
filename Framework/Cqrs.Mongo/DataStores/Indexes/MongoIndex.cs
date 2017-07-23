﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="Chinchilla Software Limited">
// // 	Copyright Chinchilla Software Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;

namespace Cqrs.Mongo.DataStores.Indexes
{
	public abstract class MongoIndex<TEntity>
	{
		/// <summary>
		/// Indicates if the index enforces unique values. Defaults to false.
		/// </summary>
		public bool IsUnique { get; protected set; }

		/// <summary>
		/// Indicates if the index is in ascending order or descending. Defaults to true meaning ascending order.
		/// </summary>
		public bool IsAcending { get; protected set; }

		/// <summary>
		/// The name of the index. Default to the class name removing any instances of "Index" and "MongoIndex" from the name.
		/// </summary>
		public string Name { get; protected set; }

		public IEnumerable<System.Linq.Expressions.Expression<Func<TEntity, object>>> Selectors { get; protected set; }

		protected MongoIndex()
		{
			IsUnique = false;
			IsAcending = true;
			Name = GetType()
				.Name
				.Replace("MongoIndex", string.Empty)
				.Replace("Index", string.Empty);
		}
	}
}