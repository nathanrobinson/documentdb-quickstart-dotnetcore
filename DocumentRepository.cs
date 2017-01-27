using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using todo.Models;
using AzureDocument = Microsoft.Azure.Documents.Document;

namespace todo
{
    public interface IDocumentRepository<T> where T : class, IId
    {
        Task<IQueryable<T>> QueryAsync();
        Task<IEnumerable<T>> RunQueryAsync(IQueryable<T> query);
        Task<int> RunCountAsync(IQueryable<T> query);
        Task<T> GetAsync(Guid id);
        Task<T> AddAsync(T entity);
        Task<T> UpdateAsync(T entity);
        Task<T> DeleteAsync(Guid entityId);
    }

    public class DocumentRepository<T> : IDocumentRepository<T> where T : class, IId
    {
        private readonly DocumentDbSettings _settings;
        private readonly Task<IDocumentClient> _clientTask;
        private readonly ILogger _logger;

        protected DocumentRepository(DocumentDbSettings settings,
                                     IDocumentClientFactory clientFactory,
                                     ILogger logger)
        {
            _settings = settings;
            _clientTask = clientFactory.GetClientAsync();
            _logger = logger;
        }

        public async Task<IQueryable<T>> QueryAsync()
        {
            var client = await _clientTask.ConfigureAwait(false);

            var query = client
                .CreateDocumentQuery<T>(UriFactory.CreateDocumentCollectionUri(_settings.DatabaseName,
                                                                               _settings.CollectionName));

            return query;
        }

        public async Task<IEnumerable<T>> RunQueryAsync(IQueryable<T> query)
        {
            var results = new List<T>();
            var docQuery = query.AsDocumentQuery();
            _logger.LogDebug("Running query for \"{SQL}\"", docQuery.ToString());
            while (docQuery.HasMoreResults)
            {
                var response = await docQuery.ExecuteNextAsync<T>().ConfigureAwait(false);
                results.AddRange(response);
            }
            return results;
        }

        public async Task<int> RunCountAsync(IQueryable<T> query)
        {
            var count = 0;

            var docQuery = query.Select(x => x.Id).AsDocumentQuery();
            _logger.LogDebug("Running count for \"{SQL}\"", docQuery.ToString());
            while (docQuery.HasMoreResults)
            {
                var response = await docQuery.ExecuteNextAsync<Guid>().ConfigureAwait(false);
                count += response.Count;
            }
            return count;
        }

        public async Task<T> GetAsync(Guid id)
        {
            return await WrapCallAndHandleExceptionAsync(id,
                                                   (client, uri) => client.ReadDocumentAsync(uri),
                                                   nameof(IDocumentClient.ReadDocumentAsync))
                                                   .ConfigureAwait(false);
        }

        public async Task<T> AddAsync(T entity)
        {
            var collectionUri = UriFactory.CreateDocumentCollectionUri(_settings.DatabaseName,
                                                                       _settings.CollectionName);

            entity.Id = Guid.NewGuid();
            
            var document =
                await WrapCallAndHandleExceptionAsync(entity.Id,
                                                      (client, uri) => client.CreateDocumentAsync(collectionUri, entity),
                                                      nameof(IDocumentClient.CreateDocumentAsync))
                    .ConfigureAwait(false);
            return document;
        }

        public async Task<T> UpdateAsync(T entity)
        {
            if (entity.Id == Guid.Empty)
                return null;

            var existing = await GetAsync(entity.Id).ConfigureAwait(false);
            if (existing != null)
            {
                await WrapCallAndHandleExceptionAsync(entity.Id,
                                                      (client, uri) => client.ReplaceDocumentAsync(uri, entity),
                                                      nameof(IDocumentClient.ReplaceDocumentAsync))
                    .ConfigureAwait(false);
            }

            return null;
        }

        public async Task<T> DeleteAsync(Guid entityId)
        {
            if (entityId != Guid.Empty)
            {
                var existing = await GetAsync(entityId).ConfigureAwait(false);
                if (existing != null)
                {
                    return
                        await WrapCallAndHandleExceptionAsync(entityId,
                                                              (client, uri) => client.DeleteDocumentAsync(uri),
                                                              nameof(IDocumentClient.ReplaceDocumentAsync))
                            .ConfigureAwait(false);
                }
            }

            return null;
        }

        private void HandleDocumentClientException(DocumentClientException dcex, Uri documentUri, string actionName)
        {
            if (dcex.StatusCode != HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Error {StatusCode} while calling {Action} on {DocumentUri}. Retry after {RetryTime}.",
                                   dcex.StatusCode,
                                   actionName,
                                   documentUri,
                                   dcex.RetryAfter);
            }
        }

        private T ParseResponseDocument(IResourceResponse<AzureDocument> response)
        {
            var document = JsonConvert.DeserializeObject<T>(response.Resource.ToString());
            return document;
        }

        private async Task<T> WrapCallAndHandleExceptionAsync(Guid id,
                                                                        Func<IDocumentClient, Uri, Task<ResourceResponse<AzureDocument>>> action,
                                                                        string actionName)
        {
            var documentUri = UriFactory.CreateDocumentUri(
                                                           _settings.DatabaseName,
                                                           _settings.CollectionName,
                                                           id.ToString());

            try
            {
                var client = await _clientTask.ConfigureAwait(false);
                var response = await action(client, documentUri).ConfigureAwait(false);
                if (response?.Resource != null)
                {
                    return ParseResponseDocument(response);
                }
            }
            catch (DocumentClientException dcex)
            {
                HandleDocumentClientException(dcex, documentUri, actionName);
            }
            return null;
        }
    }

    public class ItemRepository : DocumentRepository<Item> {
        public ItemRepository(DocumentDbSettings settings, IDocumentClientFactory clientFactory, ILogger<ItemRepository> logger) : base(settings, clientFactory, logger) {}
    }
}