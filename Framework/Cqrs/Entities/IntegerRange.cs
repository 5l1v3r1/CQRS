﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="Chinchilla Software Limited">
// // 	Copyright Chinchilla Software Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

namespace Cqrs.Entities
{
	/// <summary>
	/// A <see cref="Range{TPrimitive}"/> object for collecting a <see cref="int"/> lower and upper limit.
	/// </summary>
	public class IntegerRange : Range<int>
	{
		/// <summary>
		/// Instantiates and Initialises a new instance of the <see cref="IntegerRange"/> class.
		/// </summary>
		public IntegerRange()
		{
			IsFromInclusive = true;
			IsToInclusive = true;
		}
	}
}