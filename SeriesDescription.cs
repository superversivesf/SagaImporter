using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SagaImporter
{
    public class Description
    {
        public string truncatedHtml { get; set; }
        public string html { get; set; }

    }

    class SeriesDescription
    {
        public string title { get; set; }
        public string subtitle { get; set; }
        public Description description { get; set; }

    }

    public class GRAuthor
    {
        public int id { get; set; }
        public string name { get; set; }
        public bool isGoodreadsAuthor { get; set; }
        public string profileUrl { get; set; }
        public string worksListUrl { get; set; }

    }

    public class GRBook
    {
        public string imageUrl { get; set; }
        //public string bookId { get; set; }
        //public string workId { get; set; }
        public string bookUrl { get; set; }
        //public bool from_search { get; set; }
        //public bool from_srp { get; set; }
        //public object qid { get; set; }
        //public object rank { get; set; }
        public string title { get; set; }
        public string volume { get; set; }
        //public string bookTitleBare { get; set; }
        //public int numPages { get; set; }
        //public double avgRating { get; set; }
        //public int ratingsCount { get; set; }
        [JsonProperty("author")]
        public GRAuthor author { get; set; }
        //public string kcrPreviewUrl { get; set; }
        public Description description { get; set; }
        //public int textReviewsCount { get; set; }
        //public string publicationDate { get; set; }
        //public bool toBePublished { get; set; }
        //public string editions { get; set; }
        //public string editionsUrl { get; set; }

    }

    public class GRSeries
    {
        public bool isLibrarianView { get; set; }
        [JsonProperty("book")]
        public GRBook book { get; set; }

    }

    public class SeriesChunk
    {
        public List<GRSeries> series { get; set; }
        public List<string> seriesHeaders { get; set; }

    }

}
