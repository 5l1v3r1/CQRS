﻿using System;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Chinchilla.Logging;
using Cqrs.Cache;
using Cqrs.Domain;
using Cqrs.Domain.Factories;
using Cqrs.Authentication;
using Cqrs.Configuration;
using Cqrs.Tests.Substitutes;
using NUnit.Framework;

namespace Cqrs.Tests.Cache
{
	public class When_saving_two_aggregates_in_parallel
	{
		private CacheRepository<ISingleSignOnToken> _rep1;
		private TestAggregate _aggregate1;
		private TestInMemoryEventStore _testStore;
		private TestAggregate _aggregate2;

		[SetUp]
		public void Setup()
		{
			// This will clear the cache between runs.
			var cacheKeys = MemoryCache.Default.Select(kvp => kvp.Key).ToList();
			foreach (var cacheKey in cacheKeys)
				MemoryCache.Default.Remove(cacheKey);

			_testStore = new TestInMemoryEventStore();
			var dependencyResolver = new TestDependencyResolver(null);
			var aggregateFactory = new AggregateFactory(dependencyResolver, dependencyResolver.Resolve<ILogger>());
			_rep1 = new CacheRepository<ISingleSignOnToken>(new AggregateRepository<ISingleSignOnToken>(aggregateFactory, _testStore, new TestEventPublisher(), new NullCorrelationIdHelper(), new ConfigurationManager()), _testStore);

			_aggregate1 = new TestAggregate(Guid.NewGuid());
			_aggregate2 = new TestAggregate(Guid.NewGuid());

			_rep1.Save(_aggregate1);
			_rep1.Save(_aggregate2);

			var t1 = new Task(() =>
								  {
									  for (var i = 0; i < 100; i++)
									  {
										  var aggregate = _rep1.Get<TestAggregate>(_aggregate1.Id);
										  aggregate.DoSomething();
										  _rep1.Save(aggregate);
									  }
								  });

			var t2 = new Task(() =>
								  {
									  for (var i = 0; i < 100; i++)
									  {
										  var aggregate = _rep1.Get<TestAggregate>(_aggregate2.Id);
										  aggregate.DoSomething();
										  _rep1.Save(aggregate);
									  }
								  });
			t1.Start();
			t2.Start();

			Task.WaitAll(new[] {t1,t2});
		}

		[Test]
		public void Should_not_get_more_than_one_event_with_same_id()
		{
			Assert.That(_testStore.Events.Select(x => x.Version).Count(), Is.EqualTo(_testStore.Events.Count));
		}

		[Test]
		public void Should_save_all_events()
		{
			Assert.That(_testStore.Events.Count(), Is.EqualTo(202));
		}

		[Test]
		public void Should_distibute_events_correct()
		{
			var aggregate1 = _rep1.Get<TestAggregate>(_aggregate2.Id);
			Assert.That(aggregate1.DidSomethingCount, Is.EqualTo(100));
			var aggregate2 = _rep1.Get<TestAggregate>(_aggregate2.Id);
			Assert.That(aggregate2.DidSomethingCount, Is.EqualTo(100));
		}
	}
}