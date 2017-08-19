﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="Chinchilla Software Limited">
// // 	Copyright Chinchilla Software Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cqrs.Authentication;
using Cqrs.Bus;
using Cqrs.Commands;
using Cqrs.Configuration;
using cdmdotnet.Logging;
using Cqrs.Messages;
using Microsoft.ServiceBus.Messaging;

namespace Cqrs.Azure.ServiceBus
{
	/// <summary>
	/// A <see cref="ICommandReceiver{TAuthenticationToken}"/> that receives network messages, resolves handlers and executes the handler.
	/// </summary>
	/// <typeparam name="TAuthenticationToken">The <see cref="Type"/> of the authentication token.</typeparam>
	public class AzureCommandBusReceiver<TAuthenticationToken>
		: AzureCommandBus<TAuthenticationToken>
		, ICommandHandlerRegistrar
		, ICommandReceiver<TAuthenticationToken>
	{
		// ReSharper disable StaticMemberInGenericType
		/// <summary>
		/// Gets the <see cref="RouteManager"/>.
		/// </summary>
		public static RouteManager Routes { get; private set; }

		/// <summary>
		/// The number of handles currently being executed.
		/// </summary>
		protected static long CurrentHandles { get; set; }
		// ReSharper restore StaticMemberInGenericType

		static AzureCommandBusReceiver()
		{
			Routes = new RouteManager();
		}

		/// <summary>
		/// Instantiates a new instance of <see cref="AzureCommandBusReceiver{TAuthenticationToken}"/>.
		/// </summary>
		public AzureCommandBusReceiver(IConfigurationManager configurationManager, IMessageSerialiser<TAuthenticationToken> messageSerialiser, IAuthenticationTokenHelper<TAuthenticationToken> authenticationTokenHelper, ICorrelationIdHelper correlationIdHelper, ILogger logger, IAzureBusHelper<TAuthenticationToken> azureBusHelper)
			: base(configurationManager, messageSerialiser, authenticationTokenHelper, correlationIdHelper, logger, azureBusHelper, false)
		{
			TelemetryHelper = configurationManager.CreateTelemetryHelper("Cqrs.Azure.EventHub.CommandBus.Receiver.UseApplicationInsightTelemetryHelper", correlationIdHelper);
		}

		/// <summary>
		/// Register a command handler that will listen and respond to commands.
		/// </summary>
		/// <remarks>
		/// In many cases the <paramref name="targetedType"/> will be the handler class itself, what you actually want is the target of what is being updated.
		/// </remarks>
		public virtual void RegisterHandler<TMessage>(Action<TMessage> handler, Type targetedType, bool holdMessageLock = true)
			where TMessage : IMessage
		{
			AzureBusHelper.RegisterHandler(TelemetryHelper, Routes, handler, targetedType, holdMessageLock);
		}

		/// <summary>
		/// Register a command handler that will listen and respond to commands.
		/// </summary>
		public void RegisterHandler<TMessage>(Action<TMessage> handler, bool holdMessageLock = true)
			where TMessage : IMessage
		{
			RegisterHandler(handler, null, holdMessageLock);
		}

		/// <summary>
		/// Receives <see cref="EventData"/> from the command bus.
		/// </summary>
		protected virtual void ReceiveCommand(PartitionContext context, EventData eventData)
		{
			DateTimeOffset startedAt = DateTimeOffset.UtcNow;
			Stopwatch mainStopWatch = Stopwatch.StartNew();
			string responseCode = "200";
			// Null means it was skipped
			bool? wasSuccessfull = true;
			string telemetryName = string.Format("Cqrs/Handle/Command/{0}", eventData.SequenceNumber);
			ISingleSignOnToken authenticationToken = null;

			IDictionary<string, string> telemetryProperties = new Dictionary<string, string> { { "Type", "Azure/EventHub" } };
			object value;
			if (eventData.Properties.TryGetValue("Type", out value))
				telemetryProperties.Add("MessageType", value.ToString());
			TelemetryHelper.TrackMetric("Cqrs/Handle/Command", CurrentHandles++, telemetryProperties);
			// Do a manual 10 try attempt with back-off
			for (int i = 0; i < 10; i++)
			{
				try
				{
					Logger.LogDebug(string.Format("A command message arrived with the partition key '{0}', sequence number '{1}' and offset '{2}'.", eventData.PartitionKey, eventData.SequenceNumber, eventData.Offset));
					string messageBody = Encoding.UTF8.GetString(eventData.GetBytes());

					ICommand<TAuthenticationToken> command = AzureBusHelper.ReceiveCommand(messageBody, ReceiveCommand,
						string.Format("partition key '{0}', sequence number '{1}' and offset '{2}'", eventData.PartitionKey, eventData.SequenceNumber, eventData.Offset),
						() =>
						{
							wasSuccessfull = null;
							telemetryName = string.Format("Cqrs/Handle/Command/Skipped/{0}", eventData.SequenceNumber);
							responseCode = "204";
							// Remove message from queue
							context.CheckpointAsync(eventData);
							Logger.LogDebug(string.Format("A command message arrived with the partition key '{0}', sequence number '{1}' and offset '{2}' but processing was skipped due to command settings.", eventData.PartitionKey, eventData.SequenceNumber, eventData.Offset));
							TelemetryHelper.TrackEvent("Cqrs/Handle/Command/Skipped", telemetryProperties);
						}
					);

					if (wasSuccessfull != null)
					{
						if (command != null)
						{
							telemetryName = string.Format("{0}/{1}", command.GetType().FullName, command.Id);
							authenticationToken = command.AuthenticationToken as ISingleSignOnToken;

							var telemeteredMessage = command as ITelemeteredMessage;
							if (telemeteredMessage != null)
								telemetryName = telemeteredMessage.TelemetryName;

							telemetryName = string.Format("Cqrs/Handle/Command/{0}", telemetryName);
						}
						// Remove message from queue
						context.CheckpointAsync(eventData);
					}
					Logger.LogDebug(string.Format("A command message arrived and was processed with the partition key '{0}', sequence number '{1}' and offset '{2}'.", eventData.PartitionKey, eventData.SequenceNumber, eventData.Offset));

					wasSuccessfull = true;
					responseCode = "200";
					return;
				}
				catch (Exception exception)
				{
					// Indicates a problem, unlock message in queue
					Logger.LogError(string.Format("A command message arrived with the partition key '{0}', sequence number '{1}' and offset '{2}' but failed to be process.", eventData.PartitionKey, eventData.SequenceNumber, eventData.Offset), exception: exception);

					switch (i)
					{
						case 0:
						case 1:
							// 10 seconds
							Thread.Sleep(10 * 1000);
							break;
						case 2:
						case 3:
							// 30 seconds
							Thread.Sleep(30 * 1000);
							break;
						case 4:
						case 5:
						case 6:
							// 1 minute
							Thread.Sleep(60 * 1000);
							break;
						case 7:
						case 8:
							// 3 minutes
							Thread.Sleep(3 * 60 * 1000);
							break;
						case 9:
							telemetryProperties.Add("ExceptionType", exception.GetType().FullName);
							telemetryProperties.Add("ExceptionMessage", exception.Message);
							break;
					}
					wasSuccessfull = false;
					responseCode = "500";
				}
				finally
				{
					// Eventually just accept it
					context.CheckpointAsync(eventData);

					TelemetryHelper.TrackMetric("Cqrs/Handle/Command", CurrentHandles--, telemetryProperties);

					mainStopWatch.Stop();
					TelemetryHelper.TrackRequest
					(
						telemetryName,
						authenticationToken,
						startedAt,
						mainStopWatch.Elapsed,
						responseCode,
						wasSuccessfull == null || wasSuccessfull.Value,
						telemetryProperties
					);

					TelemetryHelper.Flush();
				}
			}
		}

		/// <summary>
		/// Receives a <see cref="ICommand{TAuthenticationToken}"/> from the command bus.
		/// </summary>
		public virtual bool? ReceiveCommand(ICommand<TAuthenticationToken> command)
		{
			return AzureBusHelper.DefaultReceiveCommand(command, Routes, "Azure-EventHub");
		}

		#region Implementation of ICommandReceiver

		/// <summary>
		/// Starts listening and processing instances of <see cref="ICommand{TAuthenticationToken}"/> from the command bus.
		/// </summary>
		public void Start()
		{
			InstantiateReceiving();

			// Callback to handle received messages
			RegisterReceiverMessageHandler(ReceiveCommand);
		}

		#endregion
	}
}