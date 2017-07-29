﻿using System;
using Cqrs.Snapshots;

namespace Cqrs.Tests.Substitutes
{
	public class TestSnapshotStore : ISnapshotStore
	{
		public bool VerifyGet { get; private set; }

		public bool VerifySave { get; private set; }

		public int SavedVersion { get; private set; }

		public Snapshot Get<TAggregateRoot>(Guid id)
		{
			VerifyGet = true;
			return new TestSnapshotAggregateSnapshot();
		}

		public void Save(Snapshot snapshot)
		{
			VerifySave = true;
			SavedVersion = snapshot.Version;
		}
	}
}
