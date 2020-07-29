using System.Collections.Generic;

namespace SagaServer
{
    public class AuthorDescription
    {
        public string AuthorName { get; set; }
        public string AuthorDesc { get; set; }
        public string AuthorImageLink { get; set; }
        public string AuthorInfluences { get; set; }
        public string AuthorGenres { get; set; }
        public string BirthDate { get; set; }
        public string DeathDate { get; set; }
        public string AuthorWebsite { get; set; }
        public string AuthorTwitter { get; internal set; }
    }
}