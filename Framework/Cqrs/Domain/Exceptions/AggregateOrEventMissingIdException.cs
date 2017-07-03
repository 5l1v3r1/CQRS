#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="Chinchilla Software Limited">
// // 	Copyright Chinchilla Software Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System;

namespace Cqrs.Domain.Exceptions
{
	[Serializable]
	public class AggregateOrEventMissingIdException : Exception
	{
		public AggregateOrEventMissingIdException(Type aggregateType, Type eventType)
			: base(string.Format("An event of type {0} tried to be saved from {1} but no id was set on either", eventType.FullName, aggregateType.FullName))
		{
		}
	}
}