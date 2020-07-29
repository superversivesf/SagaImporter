using AudiobookDb.Models;

namespace AudioBookOrganizer
{
    public class AuthorDetails
    { 
        public string AuthorName { get; set; }
        public string AuthorLink { get; set; }
        public AuthorType AuthorType { get; set; }
    }
}