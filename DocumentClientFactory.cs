using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace todo
{
    public interface IDocumentClientFactory
    {
        Task<IDocumentClient> GetClientAsync();
    }
    public class DocumentClientFactory : IDocumentClientFactory
    {
        private readonly DocumentDbSettings _settings;
        private IDocumentClient _documentClient;
        public DocumentClientFactory(DocumentDbSettings settings) { _settings = settings; }

        private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        public async Task<IDocumentClient> GetClientAsync() {

            if (_documentClient == null)
            {
                await _semaphoreSlim.WaitAsync();
                try
                {
                    if (_documentClient == null)
                    {
                        var documentClient = new DocumentClient(
                                                                new Uri(_settings.Endpoint),
                                                                _settings.PrimaryKey,
                                                                new ConnectionPolicy
                                                                {
                                                                    ConnectionMode = ConnectionMode.Direct,
                                                                    ConnectionProtocol = Protocol.Tcp
                                                                });

                        await documentClient.OpenAsync();
                        await CreateDatabaseIfNotExistsAsync(documentClient, _settings.DatabaseName);
                        await CreateCollectionIfNotExistsAsync(documentClient, _settings.DatabaseName,
                                                               _settings.CollectionName);
                        _documentClient = documentClient;
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }
            return _documentClient;
        }
        private static async Task CreateDatabaseIfNotExistsAsync(IDocumentClient client, string databaseName)
        {
            try
            {
                await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await client.CreateDatabaseAsync(new Database { Id = databaseName });
                }
                else
                {
                    throw;
                }
            }
        }

        private static async Task CreateCollectionIfNotExistsAsync(IDocumentClient client, string databaseName, string collectionName)
        {
            try
            {
                await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await client.CreateDocumentCollectionAsync(
                        UriFactory.CreateDatabaseUri(databaseName),
                        new DocumentCollection { Id = collectionName },
                        new RequestOptions { OfferThroughput = 1000 });
                }
                else
                {
                    throw;
                }
            }
        }
    }
}