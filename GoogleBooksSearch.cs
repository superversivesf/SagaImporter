using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace SagaImporter
{
    class GoogleBooksSearch
    {
        // https://www.googleapis.com/books/v1/volumes?q=quilting
         public GoogleBooksSearch()
        {
        }

        // https://googleapis.dev/dotnet/Google.Apis.Books.v1/latest/api/Google.Apis.Books.v1.VolumesResource.html#constructors

        public void SearchForSeriesByTitle(string title)
        {
            //var _seriesService = this._service.Series;
            //var _membershipService = _seriesService.Membership;
            

            
            //_listRequest.Q = title;
            //var _result = _listRequest.Execute();

        }

        public void SearchForBookByTitle(string title)
        {
        
        }
    }
}
