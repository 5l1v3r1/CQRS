﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="Chinchilla Software Limited">
// // 	Copyright Chinchilla Software Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cqrs.Authentication;
using Cqrs.Commands;
using cdmdotnet.Logging;
using Cqrs.Configuration;
using Cqrs.Exceptions;
using Cqrs.Messages;

namespace Cqrs.Bus
{
	public abstract class QueuedCommandBusReceiver<TAuthenticationToken> : ICommandReceiver<TAuthenticationToken>
	{
		protected static ConcurrentDictionary<string, ConcurrentQueue<ICommand<TAuthenticationToken>>> QueueTracker { get; private set; }

		protected ReaderWriterLockSlim QueueTrackerLock { get; private set; }

		protected IAuthenticationTokenHelper<TAuthenticationToken> AuthenticationTokenHelper { get; private set; }

		protected ICorrelationIdHelper CorrelationIdHelper { get; private set; }

		protected ILogger Logger { get; private set; }

		protected IConfigurationManager ConfigurationManager { get; private set; }

		protected IBusHelper BusHelper { get; private set; }

		protected abstract IDictionary<Type, IList<Action<IMessage>>> Routes { get; }

		protected QueuedCommandBusReceiver(IAuthenticationTokenHelper<TAuthenticationToken> authenticationTokenHelper, ICorrelationIdHelper correlationIdHelper, ILogger logger, IConfigurationManager configurationManager, IBusHelper busHelper)
		{
			QueueTracker = new ConcurrentDictionary<string, ConcurrentQueue<ICommand<TAuthenticationToken>>>();
			QueueTrackerLock = new ReaderWriterLockSlim();
			AuthenticationTokenHelper = authenticationTokenHelper;
			CorrelationIdHelper = correlationIdHelper;
			Logger = logger;
			ConfigurationManager = configurationManager;
			BusHelper = busHelper;
		}

		protected virtual void EnqueueCommand(string targetQueueName, ICommand<TAuthenticationToken> command)
		{
			var queue = QueueTracker.GetOrAdd(targetQueueName, new ConcurrentQueue<ICommand<TAuthenticationToken>>());
			queue.Enqueue(command);
		}

		protected virtual void CreateQueueAndAttachListenerIfNotExist(string queueName)
		{
			if (!QueueTracker.ContainsKey(queueName))
			{
				QueueTrackerLock.EnterWriteLock();
				try
				{
					if (!QueueTracker.ContainsKey(queueName))
					{
						QueueTracker.TryAdd(queueName, new ConcurrentQueue<ICommand<TAuthenticationToken>>());
						new Thread(() =>
						{
							Thread.CurrentThread.Name = queueName;
							DequeuAndProcessCommand(queueName);
						}).Start();
					}
				}
				catch (Exception exception)
				{
					Logger.LogError(string.Format("Processing a request to start a thread for the queue '{0}' failed.", queueName), exception: exception);
				}
				finally
				{
					QueueTrackerLock.ExitWriteLock();
				}
			}
		}

		protected virtual void DequeuAndProcessCommand(string queueName)
		{
			long loop = long.MinValue;
			while (true)
			{
				try
				{
					ConcurrentQueue<ICommand<TAuthenticationToken>> queue;
					if (QueueTracker.TryGetValue(queueName, out queue))
					{
						while (!queue.IsEmpty)
						{
							ICommand<TAuthenticationToken> command;
							if (queue.TryDequeue(out command))
							{
								try
								{
									CorrelationIdHelper.SetCorrelationId(command.CorrelationId);
								}
								catch (Exception exception)
								{
									Logger.LogError(string.Format("Trying to set the CorrelationId from the command type {1} for a request for the queue '{0}' failed.", queueName, command.GetType()), exception: exception);
								}
								try
								{
									AuthenticationTokenHelper.SetAuthenticationToken(command.AuthenticationToken);
								}
								catch (Exception exception)
								{
									Logger.LogError(string.Format("Trying to set the AuthenticationToken from the command type {1} for a request for the queue '{0}' failed.", queueName, command.GetType()), exception: exception);
								}
								try
								{
									ReceiveCommand(command);
								}
								catch (Exception exception)
								{
									Logger.LogError(string.Format("Processing the command type {1} for a request for the queue '{0}' failed.", queueName, command.GetType()), exception: exception);
									queue.Enqueue(command);
								}
							}
							else
								Logger.LogDebug(string.Format("Trying to dequeue a command from the queue '{0}' failed.", queueName));
						}
					}
					else
						Logger.LogDebug(string.Format("Trying to find the queue '{0}' failed.", queueName));

					if (loop++ % 5 == 0)
						Thread.Yield();
					else
						Thread.Sleep(100);
					if (loop == long.MaxValue)
						loop = long.MinValue;
				}
				catch (Exception exception)
				{
					Logger.LogError(string.Format("Dequeuing and processing a request for the queue '{0}' failed.", queueName), exception: exception);
				}
			}
		}

		public int QueueCount
		{
			get
			{
				QueueTrackerLock.EnterReadLock();
				try
				{
					return QueueTracker.Count;
				}
				finally
				{
					QueueTrackerLock.ExitReadLock();
				}
			}
		}

		public ICollection<string> QueueNames
		{
			get
			{
				QueueTrackerLock.EnterReadLock();
				try
				{
					return QueueTracker.Keys;
				}
				finally
				{
					QueueTrackerLock.ExitReadLock();
				}
			}
		}

		public virtual bool? ReceiveCommand(ICommand<TAuthenticationToken> command)
		{
			CorrelationIdHelper.SetCorrelationId(command.CorrelationId);
			AuthenticationTokenHelper.SetAuthenticationToken(command.AuthenticationToken);

			Type commandType = command.GetType();

			bool response = true;
			bool isRequired = BusHelper.IsEventRequired(commandType);

			IList<Action<IMessage>> handlers;
			if (Routes.TryGetValue(commandType, out handlers))
			{
				if (handlers != null)
				{
					if (handlers.Count != 1)
						throw new MultipleCommandHandlersRegisteredException(commandType);
					if (handlers.Count == 1)
						handlers.Single()(command);
					else if (isRequired)
						throw new NoCommandHandlerRegisteredException(commandType);
					else
						response = false;
				}
				else if (isRequired)
					throw new NoCommandHandlerRegisteredException(commandType);
				else
					response = false;
			}
			else if (isRequired)
				throw new NoCommandHandlerRegisteredException(commandType);
			else
				response = false;
			return response;
		}

		#region Implementation of ICommandReceiver

		public abstract void Start();

		#endregion
	}
}