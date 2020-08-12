using SagaDb.Models;

namespace SagaImporter
{
    public class AuthorDetails
    { 
        public string AuthorName { get; set; }
        public string AuthorLink { get; set; }
        public AuthorType AuthorType { get; set; }
    }
}