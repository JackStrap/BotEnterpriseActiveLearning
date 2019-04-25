// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using devBolseBotEnterprise.Middleware;
using devBolseBotEnterprise.Middleware.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Integration.ApplicationInsights.Core;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace devBolseBotEnterprise
{
	public class Startup
	{
		private readonly ILoggerFactory _loggerFactory;
		private readonly bool _isProduction = false;

		public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			_isProduction = env.IsProduction();
			_loggerFactory = loggerFactory;

			var builder = new ConfigurationBuilder()
				.SetBasePath(env.ContentRootPath)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
				.AddEnvironmentVariables();

			if (env.IsDevelopment())
			{
				builder.AddUserSecrets<Startup>();
			}
			Configuration = builder.Build();
		}

		public IConfiguration Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			// Load the connected services from .bot file.
			string botFilePath = Configuration.GetSection("botFilePath")?.Value;
			string botFileSecret = Configuration.GetSection("botFileSecret")?.Value;
			BotConfiguration botConfig = BotConfiguration.Load(botFilePath, botFileSecret);
			services.AddSingleton(sp => botConfig ?? throw new InvalidOperationException($"The .bot config file could not be loaded."));

			// Get default locale from appsettings.json
			string defaultLocale = Configuration.GetSection("defaultLocale").Get<string>();

			// Use Application Insights
			services.AddBotApplicationInsights(botConfig);

			// Initializes your bot service clients and adds a singleton that your Bot can access through dependency injection.
			BotServices connectedServices = new BotServices(botConfig);
			services.AddSingleton(sp => connectedServices);

			// Initialize Bot State
			ConnectedService cosmosDbService = botConfig.Services.FirstOrDefault(s => s.Type == ServiceTypes.CosmosDB) ?? throw new Exception("Please configure your CosmosDb service in your .bot file.");
			CosmosDbService cosmosDb = cosmosDbService as CosmosDbService;
			CosmosDbStorageOptions cosmosOptions = new CosmosDbStorageOptions()
			{
				CosmosDBEndpoint = new Uri(cosmosDb.Endpoint),
				AuthKey = cosmosDb.Key,
				CollectionId = cosmosDb.Collection,
				DatabaseId = cosmosDb.Database,
			};
			CosmosDbStorage dataStore = new CosmosDbStorage(cosmosOptions);
			UserState userState = new UserState(dataStore);
			ConversationState conversationState = new ConversationState(dataStore);

			services.AddSingleton(dataStore);
			services.AddSingleton(userState);
			services.AddSingleton(conversationState);
			services.AddSingleton(new BotStateSet(userState, conversationState));

			// Add the bot with options
			services.AddBot<Bot>(options =>
			{
				// Load the connected services from .bot file.
				string environment = _isProduction ? "production" : "development";
				ConnectedService service = botConfig.Services.FirstOrDefault(s => s.Type == ServiceTypes.Endpoint && s.Name == environment);
				if (!(service is EndpointService endpointService))
				{
					throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
				}

				options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);

				// Telemetry Middleware (logs activity messages in Application Insights)
				ServiceProvider sp = services.BuildServiceProvider();
				IBotTelemetryClient telemetryClient = sp.GetService<IBotTelemetryClient>();

				TelemetryLoggerMiddleware appInsightsLogger = new TelemetryLoggerMiddleware(telemetryClient, logPersonalInformation: true);
				options.Middleware.Add(appInsightsLogger);

				// Catches any errors that occur during a conversation turn and logs them to AppInsights.
				options.OnTurnError = async (context, exception) =>
				{
					telemetryClient.TrackException(exception);
					//await context.SendActivityAsync(MainStrings.ERROR);
					await context.SendActivityAsync(exception.ToString());
				};

				// Transcript Middleware (saves conversation history in a standard format)
				ConnectedService storageService = botConfig.Services.FirstOrDefault(s => s.Type == ServiceTypes.BlobStorage) ?? throw new Exception("Please configure your Azure Storage service in your .bot file.");
				BlobStorageService blobStorage = storageService as BlobStorageService;
				AzureBlobTranscriptStore transcriptStore = new AzureBlobTranscriptStore(blobStorage.ConnectionString, blobStorage.Container);
				TranscriptLoggerMiddleware transcriptMiddleware = new TranscriptLoggerMiddleware(transcriptStore);
				options.Middleware.Add(transcriptMiddleware);

				// Typing Middleware (automatically shows typing when the bot is responding/working)
				options.Middleware.Add(new ShowTypingMiddleware());

				// Locale Middleware (sets UI culture based on Activity.Locale)
				options.Middleware.Add(new SetLocaleMiddleware(defaultLocale ?? "en-us"));
				
				// Autosave State Middleware (saves bot state after each turn)
				options.Middleware.Add(new AutoSaveStateMiddleware(userState, conversationState));
			});
		}

		/// <summary>
		/// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		/// </summary>
		/// <param name="app">Application Builder.</param>
		/// <param name="env">Hosting Environment.</param>
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseBotApplicationInsights()
				.UseDefaultFiles()
				.UseStaticFiles()
				.UseBotFramework();
		}
	}
}
