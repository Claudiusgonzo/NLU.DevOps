﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace LanguageUnderstanding.Luis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using LanguageUnderstanding.Luis;
    using Models;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    /// <summary>
    /// Test suite for <see cref="LuisEntity"/> class.
    /// </summary>
    [TestFixture]
    internal class LuisLanguageUnderstandingServiceTests
    {
        /// <summary>
        /// Compares two <see cref="LuisEntity"/> instances.
        /// </summary>
        private readonly LuisEntityComparer luisEntityComparer = new LuisEntityComparer();

        /// <summary>
        /// Compares two <see cref="LuisLabeledUtterance"/> instances.
        /// </summary>
        private readonly LuisLabeledUtteranceComparer luisLabeledUtteranceComparer = new LuisLabeledUtteranceComparer();

        /* LuisEntity Tets. */

        [Test]
        public void ArgumentNullChecks()
        {
            Action nullAppName = () => new LuisLanguageUnderstandingService(null, string.Empty, string.Empty, string.Empty, string.Empty);
            Action nullAppId = () => new LuisLanguageUnderstandingService(string.Empty, null, string.Empty, string.Empty, string.Empty);
            Action nullAppVersion = () => new LuisLanguageUnderstandingService(string.Empty, string.Empty, null, string.Empty, string.Empty);
            Action nullRegion = () => new LuisLanguageUnderstandingService(string.Empty, string.Empty, string.Empty, null, string.Empty);
            Action nullRegionClient = () => new LuisLanguageUnderstandingService(string.Empty, string.Empty, string.Empty, null, new MockLuisClient());
            Action nullAuthoringKey = () => new LuisLanguageUnderstandingService(string.Empty, string.Empty, string.Empty, string.Empty, default(string));
            Action nullLuisClient = () => new LuisLanguageUnderstandingService(string.Empty, string.Empty, string.Empty, string.Empty, default(ILuisClient));
            nullAppName.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("appName");
            nullAppId.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("appId");
            nullAppVersion.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("appVersion");
            nullRegion.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("region");
            nullAuthoringKey.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("authoringKey");
            nullLuisClient.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("luisClient");

            using (var luis = new LuisLanguageUnderstandingService(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty))
            {
                Func<Task> nullUtterances = () => luis.TrainAsync(null, Array.Empty<EntityType>());
                Func<Task> nullUtterance = () => luis.TrainAsync(new LabeledUtterance[] { null }, Array.Empty<EntityType>());
                Func<Task> nullEntityTypes = () => luis.TrainAsync(Array.Empty<LabeledUtterance>(), null);
                Func<Task> nullEntityType = () => luis.TrainAsync(Array.Empty<LabeledUtterance>(), new EntityType[] { null });
                Func<Task> nullTestUtterances = () => luis.TestAsync(null, Array.Empty<EntityType>());
                Func<Task> nullTestUtterance = () => luis.TestAsync(new string[] { null }, Array.Empty<EntityType>());
                Func<Task> nullTestEntityTypes = () => luis.TestAsync(Array.Empty<string>(), null);
                Func<Task> nullTestEntityType = () => luis.TestAsync(Array.Empty<string>(), new EntityType[] { null });
                nullUtterances.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("utterances");
                nullUtterance.Should().Throw<ArgumentException>().And.ParamName.Should().Be("utterances");
                nullEntityTypes.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("entityTypes");
                nullEntityType.Should().Throw<ArgumentException>().And.ParamName.Should().Be("entityTypes");
                nullTestUtterances.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("utterances");
                nullTestUtterance.Should().Throw<ArgumentException>().And.ParamName.Should().Be("utterances");
                nullTestEntityTypes.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("entityTypes");
                nullTestEntityType.Should().Throw<ArgumentException>().And.ParamName.Should().Be("entityTypes");
            }
        }

        [Test]
        public void LuisEntityInitializes()
        {
            var entityType = "Location";
            var startCharIndex = 2;
            var endCharIndex = 8;
            var luisEntity = new LuisEntity(entityType, startCharIndex, endCharIndex);
            luisEntity.EntityName.Should().Be("Location");
            luisEntity.StartCharIndex.Should().Be(startCharIndex);
            luisEntity.EndCharIndex.Should().Be(endCharIndex);
        }

        [Test]
        public void LuisEntitySerializes()
        {
            var entityType = "Location";
            var startCharIndex = 2;
            var endCharIndex = 8;
            var luisEntity = new LuisEntity(entityType, startCharIndex, endCharIndex);
            var actualString = JsonConvert.SerializeObject(luisEntity);
            var actual = JObject.Parse(actualString);
            actual.Value<string>("entity").Should().Be(entityType);
            actual.Value<int>("startIndex").Should().Be(2);
            actual.Value<int>("endIndex").Should().Be(8);
        }

        [Test]
        public void EntityConvertsToLuisEntity()
        {
            var utterance = "Engineer is the job I want!";
            var entity = new Entity("String", null, "Engineer", 0);
            var expected = new LuisEntity("String", 0, 7);
            var entityType = new SimpleEntityType("String");
            var actual = LuisEntity.FromEntity(entity, utterance, entityType);
            this.luisEntityComparer.Equals(actual, expected).Should().BeTrue();
        }

        /* LuisLabeledUtteranceTests */

        [Test]
        public void LuisLabeledUtteranceInitializes()
        {
            var text = "I want to fly from New York to Seattle";
            var intent = "bookFlight";
            var luisEntities = new List<LuisEntity>
            {
                new LuisEntity("Location::From", 20, 27),
                new LuisEntity("Location::To", 32, 38),
            };

            var luisLabeledUtterance = new LuisLabeledUtterance(text, intent, luisEntities);
            luisLabeledUtterance.Text.Should().Be(text);
            luisLabeledUtterance.Intent.Should().Be(intent);
            luisLabeledUtterance.LuisEntities.Should().BeEquivalentTo(luisEntities);
        }

        [Test]
        public void LuisLabeledUtteranceSerializes()
        {
            var text = "My name is Bill Gates.";
            var intent = "updateName";
            var luisEntities = new List<LuisEntity>
            {
                new LuisEntity("FirstName", 11, 14),
                new LuisEntity("LastName", 16, 20),
            };

            var luisLabeledUtterance = new LuisLabeledUtterance(text, intent, luisEntities);
            var actualString = JsonConvert.SerializeObject(luisLabeledUtterance);
            var actual = JObject.Parse(actualString);
            actual.Value<string>("text").Should().Be(text);
            actual.Value<string>("intent").Should().Be(intent);
            actual["entities"].As<JArray>().Count.Should().Be(2);
            actual.SelectToken(".entities[0].entity").Value<string>().Should().Be(luisEntities[0].EntityName);
            actual.SelectToken(".entities[0].startIndex").Value<int>().Should().Be(luisEntities[0].StartCharIndex);
            actual.SelectToken(".entities[0].endIndex").Value<int>().Should().Be(luisEntities[0].EndCharIndex);
            actual.SelectToken(".entities[1].entity").Value<string>().Should().Be(luisEntities[1].EntityName);
            actual.SelectToken(".entities[1].startIndex").Value<int>().Should().Be(luisEntities[1].StartCharIndex);
            actual.SelectToken(".entities[1].endIndex").Value<int>().Should().Be(luisEntities[1].EndCharIndex);
        }

        [Test]
        public void LabeledUtteranceToLuisLabeledUtterance()
        {
            var text = "My name is Bill Gates.";
            var intent = "updateName";
            List<LuisEntity> luisEntities = new List<LuisEntity>
            {
                new LuisEntity("FirstName", 11, 14),
                new LuisEntity("LastName", 16, 20),
            };

            var expected = new LuisLabeledUtterance(text, intent, luisEntities);

            var entities = new List<Entity>
            {
                new Entity("FirstName", null, "Bill", 0),
                new Entity("LastName", null, "Gates", 0),
            };

            var entityTypes = new[]
            {
                new SimpleEntityType("FirstName"),
                new SimpleEntityType("LastName"),
            };

            var labeledUtterance = new LabeledUtterance(text, intent, entities);
            var actual = LuisLabeledUtterance.FromLabeledUtterance(labeledUtterance, entityTypes);
            this.luisLabeledUtteranceComparer.Equals(actual, expected).Should().BeTrue();
        }

        [Test]
        public async Task TrainEmptyModel()
        {
            var appName = string.Empty;
            var appId = Guid.NewGuid().ToString();
            var versionId = string.Empty;
            var region = "westus";
            var importUri = $"https://{region}.api.cognitive.microsoft.com/luis/api/v2.0/apps/{appId}/versions/import?versionId={versionId}";
            var trainUri = $"https://{region}.api.cognitive.microsoft.com/luis/api/v2.0/apps/{appId}/versions/{versionId}/train";
            var mockClient = new MockLuisClient();
            using (var luis = new LuisLanguageUnderstandingService(appName, appId, versionId, region, mockClient))
            {
                var utterances = Enumerable.Empty<LabeledUtterance>();
                var entityTypes = Enumerable.Empty<EntityType>();
                await luis.TrainAsync(utterances, entityTypes);

                // Assert correct import request
                var importRequest = mockClient.Requests.FirstOrDefault(request => request.Uri == importUri);
                importRequest.Should().NotBeNull();
                var jsonBody = JObject.Parse(importRequest.Body);

                // Expects 3 intents
                var intents = jsonBody.SelectToken(".intents").As<JArray>();
                intents.Count.Should().Be(1);
                intents.FirstOrDefault(token => token.Value<string>("name") == "None").Should().NotBeNull();

                // Assert train request
                var trainRequest = mockClient.Requests.FirstOrDefault(request => request.Uri == trainUri);
                trainRequest.Should().NotBeNull();
            }
        }

        [Test]
        public void TrainFailuresThrowException()
        {
            var appName = string.Empty;
            var appId = Guid.NewGuid().ToString();
            var versionId = string.Empty;
            var region = "westus";
            var importUri = $"https://{region}.api.cognitive.microsoft.com/luis/api/v2.0/apps/{appId}/versions/import?versionId={versionId}";
            var trainUri = $"https://{region}.api.cognitive.microsoft.com/luis/api/v2.0/apps/{appId}/versions/{versionId}/train";
            var failUri = default(string);
            var mockClient = new MockLuisClient
            {
                OnHttpResponse = request =>
                {
                    if (request.Uri == failUri)
                    {
                        return MockLuisClient.FailString;
                    }

                    return null;
                },
            };

            using (var luis = new LuisLanguageUnderstandingService(appName, appId, versionId, region, mockClient))
            {
                var utterances = Enumerable.Empty<LabeledUtterance>();
                var entityTypes = Enumerable.Empty<EntityType>();
                Func<Task> callTrainAsync = () => luis.TrainAsync(utterances, entityTypes);

                failUri = importUri;
                callTrainAsync.Should().Throw<HttpRequestException>();

                failUri = trainUri;
                callTrainAsync.Should().Throw<HttpRequestException>();

                failUri = null;
                callTrainAsync.Should().NotThrow<HttpRequestException>();
            }
        }

        [Test]
        public async Task TrainModelWithUtterances()
        {
            var appName = string.Empty;
            var appId = Guid.NewGuid().ToString();
            var versionId = string.Empty;
            var region = "westus";
            var importUri = $"https://{region}.api.cognitive.microsoft.com/luis/api/v2.0/apps/{appId}/versions/import?versionId={versionId}";
            var mockClient = new MockLuisClient();
            using (var luis = new LuisLanguageUnderstandingService(appName, appId, versionId, region, mockClient))
            {
                var utterances = new[]
                {
                    new LabeledUtterance("Book me a flight.", "BookFlight", Array.Empty<Entity>()),
                    new LabeledUtterance("Cancel my flight.", "CancelFlight", Array.Empty<Entity>())
                };

                var entityTypes = Enumerable.Empty<EntityType>();
                await luis.TrainAsync(utterances, entityTypes).ConfigureAwait(false);

                // Assert correct import request
                var importRequest = mockClient.Requests.FirstOrDefault(request => request.Uri == importUri);
                importRequest.Should().NotBeNull();
                var jsonBody = JObject.Parse(importRequest.Body);

                // Expects 3 intents
                var intents = jsonBody.SelectToken(".intents").As<JArray>();
                intents.Count.Should().Be(3);
                intents.FirstOrDefault(token => token.Value<string>("name") == "None").Should().NotBeNull();
                intents.FirstOrDefault(token => token.Value<string>("name") == utterances[0].Intent).Should().NotBeNull();
                intents.FirstOrDefault(token => token.Value<string>("name") == utterances[1].Intent).Should().NotBeNull();

                // Expect 2 utterances
                var importUtterances = jsonBody.SelectToken(".utterances").As<JArray>();
                importUtterances.Count.Should().Be(2);

                var bookUtterance = importUtterances.FirstOrDefault(token => token.Value<string>("intent") == utterances[0].Intent);
                bookUtterance.Should().NotBeNull();
                bookUtterance.Value<string>("text").Should().Be(utterances[0].Text);
                bookUtterance.SelectToken(".entities").As<JArray>().Count.Should().Be(0);

                var cancelUtterance = importUtterances.FirstOrDefault(token => token.Value<string>("intent") == utterances[1].Intent);
                cancelUtterance.Should().NotBeNull();
                cancelUtterance.Value<string>("text").Should().Be(utterances[1].Text);
                cancelUtterance.SelectToken(".entities").As<JArray>().Count.Should().Be(0);
            }
        }

        [Test]
        public async Task TrainModelWithUtterancesAndSimpleEntities()
        {
            var appName = string.Empty;
            var appId = Guid.NewGuid().ToString();
            var versionId = string.Empty;
            var region = "westus";
            var importUri = $"https://{region}.api.cognitive.microsoft.com/luis/api/v2.0/apps/{appId}/versions/import?versionId={versionId}";
            var mockClient = new MockLuisClient();
            using (var luis = new LuisLanguageUnderstandingService(appName, appId, versionId, region, mockClient))
            {
                var utterances = new[]
                {
                    new LabeledUtterance(
                        "Book me a flight.",
                        "BookFlight",
                        new Entity[] { new Entity("Name", string.Empty, "me", 0) }),
                    new LabeledUtterance(
                        "Cancel my flight.",
                        "CancelFlight",
                        new Entity[] { new Entity("Subject", string.Empty, "flight", 0) })
                };

                var entityTypes = new[]
                {
                    new SimpleEntityType("Name"),
                    new SimpleEntityType("Subject")
                };

                await luis.TrainAsync(utterances, entityTypes);

                // Assert correct import request
                var importRequest = mockClient.Requests.FirstOrDefault(request => request.Uri == importUri);
                importRequest.Should().NotBeNull();
                var jsonBody = JObject.Parse(importRequest.Body);

                // Expects 3 intents
                var intents = jsonBody.SelectToken(".intents").As<JArray>();
                intents.Count.Should().Be(3);
                intents.FirstOrDefault(token => token.Value<string>("name") == "None").Should().NotBeNull();
                intents.FirstOrDefault(token => token.Value<string>("name") == utterances[0].Intent).Should().NotBeNull();
                intents.FirstOrDefault(token => token.Value<string>("name") == utterances[1].Intent).Should().NotBeNull();

                // Expect 2 utterances
                var importUtterances = jsonBody.SelectToken(".utterances").As<JArray>();
                importUtterances.Count.Should().Be(2);

                var bookUtterance = importUtterances.FirstOrDefault(token => token.Value<string>("intent") == utterances[0].Intent);
                bookUtterance.Should().NotBeNull();
                bookUtterance.Value<string>("text").Should().Be(utterances[0].Text);
                bookUtterance.SelectToken(".entities[0].entity").Value<string>().Should().Be(utterances[0].Entities[0].EntityType);
                bookUtterance.SelectToken(".entities[0].startIndex").Value<int>().Should().Be(5);
                bookUtterance.SelectToken(".entities[0].endIndex").Value<int>().Should().Be(6);

                var cancelUtterance = importUtterances.FirstOrDefault(token => token.Value<string>("intent") == utterances[1].Intent);
                cancelUtterance.Should().NotBeNull();
                cancelUtterance.Value<string>("text").Should().Be(utterances[1].Text);
                cancelUtterance.SelectToken(".entities[0].entity").Value<string>().Should().Be(utterances[1].Entities[0].EntityType);
                cancelUtterance.SelectToken(".entities[0].startIndex").Value<int>().Should().Be(10);
                cancelUtterance.SelectToken(".entities[0].endIndex").Value<int>().Should().Be(15);

                // Expect 2 entities
                var entities = jsonBody.SelectToken(".entities").As<JArray>();
                entities.Count.Should().Be(2);
                entities.FirstOrDefault(token => token.Value<string>("name") == entityTypes[0].Name).Should().NotBeNull();
                entities.FirstOrDefault(token => token.Value<string>("name") == entityTypes[1].Name).Should().NotBeNull();
            }
        }

        [Test]
        public async Task CleanupModel()
        {
            var appName = string.Empty;
            var appId = Guid.NewGuid().ToString();
            var versionId = string.Empty;
            var region = "westus";
            var cleanupUri = $"https://{region}.api.cognitive.microsoft.com/luis/api/v2.0/apps/{appId}/";
            var expectedUris = new string[] { cleanupUri };
            var mockClient = new MockLuisClient();
            using (var luis = new LuisLanguageUnderstandingService(appName, appId, versionId, region, mockClient))
            {
                await luis.CleanupAsync();
                mockClient.Requests.FirstOrDefault(request => request.Uri == cleanupUri).Should().NotBeNull();
            }
        }

        [Test]
        public async Task TestModel()
        {
            var appName = string.Empty;
            var appId = Guid.NewGuid().ToString();
            var versionId = string.Empty;
            var test = "the quick brown fox jumped over the lazy dog";
            var region = "westus";

            var mockClient = new MockLuisClient
            {
                OnHttpResponse = request =>
                {
                    if (request.Uri.Contains(test))
                    {
                        return "{\"query\":\"" + test + "\",\"topScoringIntent\":{\"intent\":\"intent\"}," +
                            "\"entities\":[{\"entity\":\"entity\",\"type\":\"type\",\"startIndex\":32," +
                            "\"endIndex\":34}]}";
                    }

                    return null;
                },
            };

            using (var luis = new LuisLanguageUnderstandingService(appName, appId, versionId, region, mockClient))
            {
                var result = await luis.TestAsync(new[] { test }, Array.Empty<EntityType>());
                result.Count().Should().Be(1);
                result.First().Text.Should().Be(test);
                result.First().Intent.Should().Be("intent");
                result.First().Entities.Count.Should().Be(1);
                result.First().Entities.First().EntityType.Should().Be("type");
                result.First().Entities.First().EntityValue.Should().Be("entity");
                result.First().Entities.First().MatchText.Should().Be("the");
                result.First().Entities.First().MatchIndex.Should().Be(1);
            }
        }

        [Test]
        public async Task TestSpeech()
        {
            var appName = string.Empty;
            var appId = Guid.NewGuid().ToString();
            var versionId = string.Empty;
            var test = "the quick brown fox jumped over the lazy dog entity";
            var region = "westus";
            var jsonString = "{\"query\":\"" + test + "\",\"topScoringIntent\":{\"intent\":\"intent\"}," +
                        "\"entities\":[{\"entity\":\"entity\",\"type\":\"type\",\"startIndex\":45," +
                        "\"endIndex\":50}]}";

            var mockClient = new MockLuisClient
            {
                OnRecognizeSpeechResponse = (speechFile) =>
                {
                    return jsonString;
                }
            };

            using (var luis = new LuisLanguageUnderstandingService(appName, appId, versionId, region, mockClient))
            {
                var result = await luis.TestSpeechAsync(new string[] { "somefile" }, new List<EntityType>(), CancellationToken.None);
                result.Count().Should().Be(1);
                result.First().Text.Should().Be(test);
                result.First().Intent.Should().Be("intent");
                result.First().Entities.Count.Should().Be(1);
                result.First().Entities.First().EntityType.Should().Be("type");

                result.First().Entities.First().MatchText.Should().Be("entity");
                result.First().Entities.First().MatchIndex.Should().Be(0);
            }
        }

        [Test]
        public async Task TestWithBuiltinEntity()
        {
            var appName = string.Empty;
            var appId = Guid.NewGuid().ToString();
            var versionId = string.Empty;
            var test = "the quick brown fox jumped over the lazy dog";
            var region = "westus";

            var mockClient = new MockLuisClient
            {
                OnHttpResponse = request =>
                {
                    if (request.Uri.Contains(test))
                    {
                        return "{\"query\":\"" + test + "\",\"topScoringIntent\":{\"intent\":\"intent\"}," +
                            "\"entities\":[{\"entity\":\"entity\",\"type\":\"builtin.test\",\"startIndex\":32," +
                            "\"endIndex\":34}]}";
                    }

                    return null;
                },
            };

            var entityType = new BuiltinEntityType("type", "test");

            using (var luis = new LuisLanguageUnderstandingService(appName, appId, versionId, region, mockClient))
            {
                var result = await luis.TestAsync(new[] { test }, new[] { entityType });
                result.Count().Should().Be(1);
                result.First().Text.Should().Be(test);
                result.First().Intent.Should().Be("intent");
                result.First().Entities.Count.Should().Be(1);
                result.First().Entities.First().EntityType.Should().Be("type");
                result.First().Entities.First().EntityValue.Should().Be("entity");
                result.First().Entities.First().MatchText.Should().Be("the");
                result.First().Entities.First().MatchIndex.Should().Be(1);
            }
        }

        [Test]
        public void TestFailedThrowsException()
        {
            var appName = string.Empty;
            var appId = Guid.NewGuid().ToString();
            var versionId = string.Empty;
            var test = "the quick brown fox jumped over the lazy dog";
            var region = "westus";

            var mockClient = new MockLuisClient
            {
                OnHttpResponse = request =>
                {
                    if (request.Uri.Contains(test))
                    {
                        return MockLuisClient.FailString;
                    }

                    return null;
                },
            };

            using (var luis = new LuisLanguageUnderstandingService(appName, appId, versionId, region, mockClient))
            {
                Func<Task> callTestAsync = () => luis.TestAsync(new[] { test }, Array.Empty<EntityType>());
                callTestAsync.Should().Throw<HttpRequestException>();
            }
        }

        /// <summary>
        /// Mock version of <see cref="ILuisClient"/> for testing.
        /// Enables the verification of LUIS http requests. Returns a
        /// successful request response when the request matches expectation.
        /// Returns a bad request response when the request does not match.
        /// </summary>
        private sealed class MockLuisClient : ILuisClient
        {
            /// <summary>
            /// Gets a string that can be returned in <see cref="OnHttpResponse"/> to return a failure.
            /// </summary>
            public static string FailString { get; } = Guid.NewGuid().ToString();

            /// <summary>
            /// Gets a collection of requests made against the LUIS client.
            /// </summary>
            public IReadOnlyList<LuisRequest> Requests => this.RequestsInternal;

            /// <summary>
            /// Gets or sets callback when a request is made against the LUIS client.
            /// </summary>
            public Action<LuisRequest> OnRequest { get; set; }

            /// <summary>
            /// Gets or sets callback when a request is made against the LUIS client.
            /// </summary>
            public Func<LuisRequest, string> OnHttpResponse { get; set; }

            /// <summary>
            /// Gets or sets callback when a recognizeSpeech request is made against the LUIS client.
            /// </summary>
            public Func<string, string> OnRecognizeSpeechResponse { get; set; }

            /// <summary>
            /// Gets a collection of requests made against the LUIS client.
            /// </summary>
            private List<LuisRequest> RequestsInternal { get; } = new List<LuisRequest>();

            /// <inheritdoc />
            public Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken)
            {
                return this.ProcessRequest(new LuisRequest
                {
                    Method = HttpMethod.Get.Method,
                    Uri = uri.OriginalString,
                });
            }

            /// <inheritdoc />
            public Task<string> RecognizeSpeechAsync(string appId, string speechFile)
            {
                return Task.FromResult(this.OnRecognizeSpeechResponse.Invoke(speechFile));
            }

            /// <inheritdoc />
            public Task<HttpResponseMessage> PostAsync(Uri uri, string requestBody, CancellationToken cancellationToken)
            {
                return this.ProcessRequest(new LuisRequest
                {
                    Method = HttpMethod.Post.Method,
                    Uri = uri.OriginalString,
                    Body = requestBody,
                });
            }

            /// <inheritdoc />
            public Task<HttpResponseMessage> DeleteAsync(Uri uri, CancellationToken cancellationToken)
            {
                return this.ProcessRequest(new LuisRequest
                {
                    Method = HttpMethod.Delete.Method,
                    Uri = uri.OriginalString,
                });
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }

            /// <summary>
            /// Process the LUIS request.
            /// </summary>
            /// <param name="request">LUIS <paramref name="request"/>.</param>
            /// <returns>HTTP response.</returns>
            private Task<HttpResponseMessage> ProcessRequest(LuisRequest request)
            {
                this.RequestsInternal.Add(request);
                this.OnRequest?.Invoke(request);
                var response = this.OnHttpResponse?.Invoke(request);
                var statusCode = response != FailString
                    ? HttpStatusCode.OK
                    : HttpStatusCode.InternalServerError;
                var httpResponse = new HttpResponseMessage(statusCode);
                if (response != null)
                {
                    httpResponse.Content = new StringContent(response);
                }

                return Task.FromResult(httpResponse);
            }
        }

        /// <summary>
        /// LUIS request data.
        /// </summary>
        private sealed class LuisRequest
        {
            /// <summary>
            /// Gets or sets the HTTP method.
            /// </summary>
            public string Method { get; set; }

            /// <summary>
            /// Gets or sets the URI of the request.
            /// </summary>
            public string Uri { get; set; }

            /// <summary>
            /// Gets or sets the request body.
            /// </summary>
            public string Body { get; set; }
        }

        /// <summary>
        /// An <see cref="IEqualityComparer{T}"/> for <see cref="LuisLabeledUtterance"/>.
        /// </summary>
        private sealed class LuisLabeledUtteranceComparer : IEqualityComparer<LuisLabeledUtterance>
        {
            /// <inheritdoc />
            public bool Equals(LuisLabeledUtterance u1, LuisLabeledUtterance u2)
            {
                return (u1.Text == u2.Text) &&
                    (u1.Intent == u2.Intent) &&
                    u1.LuisEntities.SequenceEqual(u2.LuisEntities, new LuisEntityComparer());
            }

            /// <inheritdoc />
            public int GetHashCode(LuisLabeledUtterance utterance)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// An <see cref="IEqualityComparer{T}"/> for <see cref="LuisLabeledUtterance"/>.
        /// </summary>
        private sealed class LuisEntityComparer : IEqualityComparer<LuisEntity>
        {
            /// <inheritdoc />
            public bool Equals(LuisEntity e1, LuisEntity e2)
            {
                return (e1.EntityName == e2.EntityName) &&
                    (e1.StartCharIndex == e2.StartCharIndex) &&
                    (e1.EndCharIndex == e2.EndCharIndex);
            }

            /// <inheritdoc />
            public int GetHashCode(LuisEntity entity)
            {
                throw new NotImplementedException();
            }
        }
    }
}
