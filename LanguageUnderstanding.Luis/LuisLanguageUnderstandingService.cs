﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace LanguageUnderstanding.Luis
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Models;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Train, test, and cleanup a LUIS model.
    /// Implementation of <see cref="ILanguageUnderstandingService"/>
    /// </summary>
    public sealed class LuisLanguageUnderstandingService : ILanguageUnderstandingService, IDisposable
    {
        /// <summary> The protocol used in LUIS http requests. </summary>
        private const string Protocol = "https://";

        /// <summary> All the static domains/subdomains to construct LUIS host address. </summary>
        private const string Domain = ".api.cognitive.microsoft.com";

        /// <summary> Base path for LUIS API calls. </summary>
        private const string BasePath = "/luis/api/v2.0/apps/";

        /// <summary> Base path for LUIS queries. </summary>
        private const string QueryBasePath = "/luis/v2.0/apps/";

        /// <summary>
        /// Initializes a new instance of the <see cref="LuisLanguageUnderstandingService"/> class.
        /// </summary>
        /// <param name="appName">LUIS application name.</param>
        /// <param name="region">LUIS region.</param>
        /// <param name="authoringKey">LUIS authoring key.</param>
        public LuisLanguageUnderstandingService(string appName, string region, string authoringKey)
            : this(appName, region, new LuisClient(authoringKey != null ? authoringKey : throw new ArgumentNullException(nameof(authoringKey))))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LuisLanguageUnderstandingService"/> class.
        /// </summary>
        /// <param name="appName">LUIS application name.</param>
        /// <param name="region">LUIS region.</param>
        /// <param name="luisClient">LUIS client.</param>
        public LuisLanguageUnderstandingService(string appName, string region, ILuisClient luisClient)
        {
            this.AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            this.Region = region ?? throw new ArgumentNullException(nameof(region));
            this.LuisClient = luisClient ?? throw new ArgumentNullException(nameof(luisClient));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LuisLanguageUnderstandingService"/> class.
        /// </summary>
        /// <param name="appName">LUIS application name.</param>
        /// <param name="appId">LUIS application id.</param>
        /// <param name="appVersion">LUIS application version.</param>
        /// <param name="region">LUIS region.</param>
        /// <param name="authoringKey">LUIS authoring key.</param>
        public LuisLanguageUnderstandingService(string appName, string appId, string appVersion, string region, string authoringKey)
            : this(appName, appId, appVersion, region, new LuisClient(authoringKey != null ? authoringKey : throw new ArgumentNullException(nameof(authoringKey))))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LuisLanguageUnderstandingService"/> class.
        /// </summary>
        /// <param name="appName">LUIS application name.</param>
        /// <param name="appId">LUIS application id.</param>
        /// <param name="appVersion">LUIS application version.</param>
        /// <param name="region">LUIS region.</param>
        /// <param name="luisClient">LUIS client.</param>
        public LuisLanguageUnderstandingService(string appName, string appId, string appVersion, string region, ILuisClient luisClient)
        {
            this.AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            this.AppId = appId ?? throw new ArgumentNullException(nameof(appId));
            this.AppVersion = appVersion ?? throw new ArgumentNullException(nameof(appVersion));
            this.Region = region ?? throw new ArgumentNullException(nameof(region));
            this.LuisClient = luisClient ?? throw new ArgumentNullException(nameof(luisClient));
        }

        /// <summary>
        /// Gets the name of the LUIS app.
        /// </summary>
        public string AppName { get; }

        /// <summary>
        /// Gets the LUIS app ID.
        /// </summary>
        public string AppId { get; private set; }

        /// <summary>
        /// Gets the LUIS app version.
        /// </summary>
        public string AppVersion { get; private set; }

        /// <summary> Gets the LUIS application region. </summary>
        private string Region { get; }

        /// <summary> Gets the client to make HTTP requests to LUIS. </summary>
        private ILuisClient LuisClient { get; }

        /// <summary> Gets host for LUIS API calls.</summary>
        private string Host => $"{Protocol}{this.Region}{Domain}";

        /// <summary> Gets full path for LUIS API calls. Contains appId.</summary>
        private string AppIdPath => $"{BasePath}{this.AppId}/";

        /// <summary> Gets path for LUIS API calls. Contains the appId and appVersion.</summary>
        private string AppVersionPath => $"{this.AppIdPath}versions/{this.AppVersion}/";

        /// <inheritdoc />
        public async Task TrainAsync(
            IEnumerable<LabeledUtterance> utterances,
            IEnumerable<EntityType> entityTypes,
            CancellationToken cancellationToken)
        {
            if (utterances == null)
            {
                throw new ArgumentNullException(nameof(utterances));
            }

            if (entityTypes == null)
            {
                throw new ArgumentNullException(nameof(entityTypes));
            }

            // Create application if not passed in.
            if (this.AppId == null)
            {
                Debug.Assert(this.AppVersion == null, "App version must be null if app ID is not set.");
                this.AppId = await this.CreateAppAsync(cancellationToken);
                this.AppVersion = "0.2";
            }

            // Get boilerplate JObject
            var model = this.GetModelStarter();

            // Add intents to model
            var intents = new HashSet<string> { "None" };
            foreach (var utterance in utterances)
            {
                if (utterance == null)
                {
                    throw new ArgumentException("Utterance must not be null.", nameof(utterances));
                }

                intents.Add(utterance.Intent);
            }

            var intentArray = (JArray)model["intents"];
            foreach (var intent in intents)
            {
                intentArray.Add(new JObject(new JProperty("name", intent)));
            }

            // Add utterances to model
            var luisUtterances = utterances.Select(item => LuisLabeledUtterance.FromLabeledUtterance(item, entityTypes));
            var utteranceArray = (JArray)model["utterances"];
            foreach (var luisUtterance in luisUtterances)
            {
                utteranceArray.Add(JObject.FromObject(luisUtterance));
            }

            // Add entities to model
            var entitiesArray = (JArray)model["entities"];
            var prebuiltEntitiesArray = (JArray)model["prebuiltEntities"];
            foreach (var entityType in entityTypes)
            {
                if (entityType == null)
                {
                    throw new ArgumentException("Entity types must not be null.", nameof(entityTypes));
                }

                switch (entityType.Kind)
                {
                    case EntityTypeKind.Simple:
                        entitiesArray.Add(new JObject(
                            new JProperty("name", entityType.Name),
                            new JProperty("children", new JArray()),
                            new JProperty("roles", new JArray())));
                        break;
                    case EntityTypeKind.Builtin:
                        var builtinEntityType = (BuiltinEntityType)entityType;
                        prebuiltEntitiesArray.Add(new JObject(
                            new JProperty("name", builtinEntityType.BuiltinId),
                            new JProperty("roles", new JArray())));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            // This creates a new version (using the versionId passed on initialization)
            var importResponse = await this.ImportVersionAsync(model, cancellationToken);
            importResponse.EnsureSuccessStatusCode();

            // Train
            var uri = new Uri($"{this.Host}{this.AppVersionPath}train");
            var trainResponse = await this.LuisClient.PostAsync(uri, null, cancellationToken);
            trainResponse.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task<IEnumerable<LabeledUtterance>> TestAsync(IEnumerable<string> utterances, CancellationToken cancellationToken)
        {
            if (utterances == null)
            {
                throw new ArgumentNullException(nameof(utterances));
            }

            var labeledUtterances = new List<LabeledUtterance>();
            foreach (var utterance in utterances)
            {
                var uri = new Uri($"{this.Host}{QueryBasePath}{this.AppId}?q={utterance}");
                var response = await this.LuisClient.GetAsync(uri, cancellationToken);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var labeledUtterance = PredictionToLabeledUtterance(utterance, json);
                labeledUtterances.Add(labeledUtterance);
            }

            return labeledUtterances;
        }

        /// <inheritdoc />
        public Task<IEnumerable<LabeledUtterance>> TestSpeechAsync(IEnumerable<string> speechFiles, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task CleanupAsync(CancellationToken cancellationToken)
        {
            var uri = new Uri($"{this.Host}{this.AppIdPath}");
            var cleanupResponse = await this.LuisClient.DeleteAsync(uri, cancellationToken);
            cleanupResponse.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.LuisClient.Dispose();
        }

        /// <summary>
        /// Converts a prediction request response from Luis into a <see cref="LabeledUtterance"/>.
        /// </summary>
        /// <returns>A <see cref="LabeledUtterance"/>.</returns>
        /// <param name="utterance">The utterance that Luis evaluated.</param>
        /// <param name="json">The prediction request response.</param>
        private static LabeledUtterance PredictionToLabeledUtterance(string utterance, string json)
        {
            var jsonObject = JObject.Parse(json);
            var text = jsonObject.Value<string>("query");
            var intent = jsonObject.SelectToken(".topScoringIntent.intent").Value<string>();

            var array = (JArray)jsonObject["entities"];
            var entities = new List<Entity>(array.Count);
            foreach (var item in array)
            {
                var entityType = item.Value<string>("type");
                string entityValue = item.Value<string>("entity");
                int startCharIndex = item.Value<int>("startCharIndex");
                int endCharIndex = item.Value<int>("endCharIndex");

                var matchText = text.Substring(startCharIndex, endCharIndex - startCharIndex + 1);
                var matches = Regex.Matches(utterance, matchText);
                int matchIndex = -1;
                for (var i = 0; i < matches.Count; ++i)
                {
                    if (matches[i].Index == startCharIndex)
                    {
                        matchIndex = i;
                        break;
                    }
                }

                Debug.Assert(matchIndex >= 0, "Invalid LUIS response.");
                entities.Add(new Entity(entityType, entityValue, matchText, matchIndex));
            }

            return new LabeledUtterance(text, intent, entities);
        }

        /// <summary>
        /// Create skeleton JSON for a LUIS model.
        /// </summary>
        /// <returns>A JSON object with all necessary properties for a LUIS model.</returns>
        private JObject GetModelStarter()
        {
            return new JObject
            {
                { "luis_schema_version", "3.0.0" },
                { "versionId", this.AppVersion },
                { "name", this.AppName },
                { "desc", string.Empty },
                { "culture", "en-us" },
                { "intents", new JArray() },
                { "entities", new JArray() },
                { "composites", new JArray() },
                { "closedLists", new JArray() },
                { "patternAnyEntities", new JArray() },
                { "regex_entities", new JArray() },
                { "prebuiltEntities", new JArray() },
                { "model_features", new JArray() },
                { "regex_features", new JArray() },
                { "patterns", new JArray() },
                { "utterances", new JArray() },
            };
        }

        /// <summary>
        /// Creates a new app for LUIS.
        /// </summary>
        /// <returns>A task to wait on the completion of the async operation.</returns>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task<string> CreateAppAsync(CancellationToken cancellationToken)
        {
            var requestJson = new JObject
            {
                { "name", this.AppName },
                { "culture", "en-us" },
            };

            var uri = new Uri($"{this.Host}{BasePath}");
            var requestBody = requestJson.ToString(Formatting.None);
            var httpResponse = await this.LuisClient.PostAsync(uri, requestBody, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();
            var jsonString = await httpResponse.Content.ReadAsStringAsync();
            var json = JToken.Parse(jsonString);
            return json.ToString();
        }

        /// <summary>
        /// Import a LUIS model as a new version.
        /// </summary>
        /// <returns>A task to wait on the completion of the async operation.</returns>
        /// <param name="model">LUIS model as a json object.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private Task<HttpResponseMessage> ImportVersionAsync(JObject model, CancellationToken cancellationToken)
        {
            var uri = new Uri($"{this.Host}{this.AppIdPath}versions/import?versionId={this.AppVersion}");
            var requestBody = model.ToString(Formatting.None);
            return this.LuisClient.PostAsync(uri, requestBody, cancellationToken);
        }
    }
}