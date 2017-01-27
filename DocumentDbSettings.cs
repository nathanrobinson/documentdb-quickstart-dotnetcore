namespace todo
{
    public class DocumentDbSettings
    {
        public string Endpoint { get; } = "https://localhost:8081/";
        public string PrimaryKey { get; } = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        public string DatabaseName { get; } = "queuetms_db";
        public string CollectionName { get; } = "queuetms_single_collection";
    }    
}