#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="Chinchilla Software Limited">
// // 	Copyright Chinchilla Software Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System.Runtime.Serialization;

namespace Cqrs.Events
{
	public interface ISagaEvent<TAuthenticationToken>
		: IEvent<TAuthenticationToken>
	{
		[DataMember]
		IEvent<TAuthenticationToken> Event { get; set; }
	}
}