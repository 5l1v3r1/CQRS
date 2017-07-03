﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="Chinchilla Software Limited">
// // 	Copyright Chinchilla Software Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System;
using System.Threading.Tasks;
using cdmdotnet.Logging;
using Cqrs.Akka.Events;
using Cqrs.Domain;
using Cqrs.Domain.Factories;
using Cqrs.Events;

namespace Cqrs.Akka.Domain
{
	public class AkkaAggregateRepository<TAuthenticationToken>
		: AggregateRepository<TAuthenticationToken>
		, IAkkaAggregateRepository<TAuthenticationToken>
	{
		protected IAkkaEventPublisherProxy<TAuthenticationToken> EventPublisher { get; private set; }

		public AkkaAggregateRepository(IAggregateFactory aggregateFactory, IEventStore<TAuthenticationToken> eventStore, IEventPublisher<TAuthenticationToken> publisher, ICorrelationIdHelper correlationIdHelper, IAkkaEventPublisherProxy<TAuthenticationToken> eventPublisher)
			: base(aggregateFactory, eventStore, publisher, correlationIdHelper)
		{
			EventPublisher = eventPublisher;
		}

		#region Overrides of Repository<TAuthenticationToken>

		protected override TAggregateRoot CreateAggregate<TAggregateRoot>(Guid id)
		{
			var aggregate = AggregateFactory.Create<TAggregateRoot>();

			return aggregate;
		}

		protected override void PublishEvent(IEvent<TAuthenticationToken> @event)
		{
			Task.Factory.StartNewSafely(() =>
			{
				EventPublisher.Publish(@event);
				base.PublishEvent(@event);
			});
		}

		#endregion
	}
}