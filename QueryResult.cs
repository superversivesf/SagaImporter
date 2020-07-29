using System;
using System.Collections.Generic;

namespace AudioBookOrganizer
{
    public class QueryResult
    {
        public QueryResult(string title, List<string> authors, string link)
        {
            if (title != null)
            {
                if (title.StartsWith("("))
                {
                    var _index = title.IndexOf(')');
                    var _titleArray = title.ToCharArray();
                    _titleArray[0] = ' ';
                    if (_index > 0)
                        _titleArray[_index] = ' ';
                    title = _titleArray.ToString();
                }

                if (title.Contains('('))
                {
                    var _titleParts = title.Split('(', StringSplitOptions.RemoveEmptyEntries);

                    this.Title = _titleParts[0].Trim();

                    var _seriesParts = _titleParts[1].Replace(")", string.Empty).Trim().Split("#", StringSplitOptions.RemoveEmptyEntries);

                    this.seriesTitle = _seriesParts[0].Replace(",", String.Empty).Trim();
                    if (_seriesParts.Length > 1)
                    {
                        this.seriesCount = _seriesParts[1].Trim();
                    }
                }
                else
                {
                    this.Title = title.Trim();
                }
            }
            else
            {
                this.Title = String.Empty;
            }
            
            this.link = link;

            if (authors == null)
            {
                this.authors = new List<string>();
            }
            else
            {
                this.authors = authors;
            }

        }

        public List<string> authors { get;}
        public string Title { get;}
        public string seriesTitle { get;}
        public string seriesCount { get;}
        public string link { get;}

    }
}