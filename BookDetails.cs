using System.Collections.Generic;

namespace AudioBookOrganizer
{
    public class BookDetails
    {
        public string BookTitle { get; set; }
        public string SeriesTitle { get; set; }
        public string SeriesVolume { get; set; }
        public string BookDescription { get; set; }
        //public string ASIN { get; set; }
        //public string ISBN { get; set; }
        //public List<string> Characters { get; set; }
        //public List<string> Settings { get; set; }
        public List<string> Genres { get; set; }
        public List<AuthorDetails> Authors { get; set; }

        public string BookLink { get; set; }
        public string SeriesLink { get; set; }
        public string GoodReadsCoverImageLink { get; set; }
    }

    public class SeriesDto
    { 
        public string SeriesTitle { get; set; }
        public string SeriesVolume { get; set; }
    }
}