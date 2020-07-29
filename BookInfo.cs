using SagaDb.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SagaServer
{
    class BookInfo
    {
        public BookInfo()
        {
            this.Authors = new List<string>();
            this.Series = new List<SeriesInfo>();
        }

        public string GoodreadsLink { get; set; }
        public string Title { get; set; }
        public List<string> Authors { get; set; }
        public List<SeriesInfo> Series { get; set; }
        public bool GoodreadsFetchTried { get; set; }

        public string GoodreadsDescription { get; set; }
    }
}
