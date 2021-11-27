using CsvHelper;
using HtmlAgilityPack;
using Newtonsoft.Json;
using SagaDb;
using SagaDb.Database;
using SagaDb.Models;
using SagaUtil;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;

namespace SagaImporter
{
    internal class LookupProcessor
    {
        private BookCommands _bookCommands;
        private bool _retry;
        private string _hintfile;
        private bool _authors;
        private bool _images;
        private bool _books;
        private bool _series;
        private bool _purge;
        private const string GOODREADS = "https://www.goodreads.com";
        private readonly HtmlWeb _web;
        private readonly Random _random;
        private readonly KeyMaker _keyMaker;

        public LookupProcessor()
        {
            _web = new HtmlWeb();
            _random = new Random();
            _keyMaker = new KeyMaker();
        }

        internal bool Initialize(LookupOptions l)
        {
            _bookCommands = new BookCommands(l.DatabaseFile);
            _retry = l.Retry;
            _hintfile = l.HintFile;
            _authors = l.Authors;
            _books = l.Books;
            _series = l.Series;
            _purge = l.PurgeAndRebuild;
            _images = l.Images;
            return true;
        }

        internal void Execute()
        {
            if (_books)
            {
                if (!String.IsNullOrEmpty(_hintfile))
                {
                    // Failed Book CSV File
                    using (var reader = new StreamReader(_hintfile))
                    {
                        using (var csv = new CsvReader(reader, CultureInfo.CurrentCulture))
                        {
                            var records = csv.GetRecords<FailBookHint>();

                            foreach (var r in records)
                            {
                                Console.WriteLine($"Updating {r.Title}");
                                var _book = _bookCommands.GetBook(r.BookId);
                                var _bookLookup = new QueryResult(null, null, r.GoodreadsLink);
                                var _entry = ProcessGoodreadsBookEntry(_bookLookup);
                                _book.GoodReadsFetchTried = true;
                                if (_entry != null)
                                {
                                    UpdateGenres(_entry, _book);
                                    UpdateSeries(_entry, _book);
                                    UpdateAuthors(_entry, _book);
                                    UpdateBook(_entry, _book);
                                }
                            }
                        }
                    }
                }
                else
                {
                    List<Book> _books = null;
                    if (_retry)
                    {
                        _books = _bookCommands.GetBooksFailedGoodReads();
                    }
                    else
                    {
                        _books = _bookCommands.GetBooksMissingGoodReads();
                    }
                    Console.WriteLine($"Processing {_books.Count} books");

                    _books.Sort((x, y) => x.BookId.CompareTo(y.BookId)); // Not in alphabetical order
                                                                         // var shuffled = myList.OrderBy(x => Guid.NewGuid()).ToList();

                    foreach (var _book in _books)
                    {
                        var _authors = _bookCommands.GetAuthorsByBookId(_book.BookId);

                        var _title = _book.BookTitle.ToLower();

                        Console.Write($"Good reads lookup of {_title} ...");

                        QueryResult _searchResult = SearchGoodreads(_book, _authors);
                        _book.GoodReadsFetchTried = true;
                        if (_searchResult != null)
                        {
                            var _entry = ProcessGoodreadsBookEntry(_searchResult);

                            if (_entry != null)
                            {
                                UpdateGenres(_entry, _book);
                                UpdateSeries(_entry, _book);
                                UpdateAuthors(_entry, _book);
                                UpdateBook(_entry, _book);
                            }
                            Console.WriteLine($" done");
                        }
                        else
                        {
                            _bookCommands.UpdateBook(_book);
                            Console.WriteLine($" failed");
                        }
                    }
                }
            }

            int i = 1;
            int count = 0;

            if (_purge)
            {
                Console.WriteLine("== Purging and Rebuilding Series and Author information ==");

                Console.WriteLine("- Purging Authors");
                _bookCommands.PurgeAuthors();
                _bookCommands.PurgeAuthorLinks();
                Console.WriteLine("- Purging Series");
                _bookCommands.PurgeSeries();
                _bookCommands.PurgeSeriesLinks();
                Console.WriteLine("- Purging Genres");
                _bookCommands.PurgeGenres();
                _bookCommands.PurgeGenreLinks();
                Console.WriteLine("- Purging Images");
                _bookCommands.PurgeImages();
                var _books = _bookCommands.GetBooks();
                count = _books.Count;
                _books.Shuffle();

                Console.WriteLine("== Processing Authors ==");
                foreach (var b in _books)
                {
                    Console.Write($"\r({i++}/{count}) {FormatOutputLine(b.GoodReadsTitle)}");
                    var _bookLookup = new QueryResult(null, null, b.GoodReadsLink);
                    var _entry = ProcessGoodreadsBookEntry(_bookLookup);

                    if (_entry != null)
                    {
                        UpdateGenres(_entry, b);
                        UpdateSeries(_entry, b);
                        UpdateAuthors(_entry, b);
                    }
                }
            }

            if (_series)
            {
                // Doesnt try to do anything smart. Looks up each book series and gets all books in that series.
                // Then sees if any of those books exist in the DB and adds them to the series if it does
                // Requires the books in question have existing good reads links.
                // Doesnt try to do anything clever. That is a recipe for trouble.
                var _seriesList = _bookCommands.GetAllSeries();
                _seriesList = _seriesList.Where(s => s.SeriesDescription == null).ToList();

                i = 1;
                count = _seriesList.Count;
                Console.Write("\n== Processing Series ==");
                foreach (var s in _seriesList)
                {
                    Console.WriteLine($"\r({i++}/{count}) {FormatOutputLine(s.SeriesName)}");
                    var _link = s.SeriesLink;
                    var _seriesBooks = GetSeriesFromGoodReads(_link);

                    if (_seriesBooks.Count > 0)
                    {
                        // Update the series description from first book
                        s.SeriesDescription = _seriesBooks[0].SeriesDescription;
                        _bookCommands.UpdateSeries(s);
                    }

                    foreach (var b in _seriesBooks)
                    {
                        var _seriesBook = _bookCommands.GetBookByGoodReadsLink(b.BookLink);
                        if (_seriesBook.Count == 1)
                        {
                            // Ok we found the right book and only one
                            var _book = _seriesBook[0];

                            // Add a series link for the book if one doesn't exist.
                            _bookCommands.LinkBookToSeries(_book, s, b.BookVolume);
                        }
                    }
                }
            }

            if (_authors)
            {
                var _authorList = _bookCommands.GetAuthorsWithGoodReads();
                i = 1;
                count = _authorList.Count;

                Console.WriteLine("\n== Processing Authors ==");
                foreach (var a in _authorList)
                {
                    Console.Write($"\r({i++}/{count} {FormatOutputLine(a.AuthorName)}");
                    var _authDetails = GetAuthorFromGoodReads(a.GoodReadsAuthorLink);

                    a.AuthorDescription = _authDetails.AuthorDesc;
                    a.AuthorImageLink = _authDetails.AuthorImageLink;
                    a.AuthorWebsite = _authDetails.AuthorWebsite;
                    a.Born = _authDetails.BirthDate;
                    a.Died = _authDetails.DeathDate;
                    a.Influences = _authDetails.AuthorInfluences;
                    a.Genre = _authDetails.AuthorGenres;
                    a.Twitter = _authDetails.AuthorTwitter;

                    _bookCommands.UpdateAuthor(a);
                }
            }

            if (_images)
            {
                var _authorList = _bookCommands.GetAuthorsWithGoodReads();
                var _bookList = _bookCommands.GetBooks();

                Console.WriteLine("\n== Downloading Cover Images ==");

                i = 1;
                count = _bookList.Count;
                foreach (var b in _bookList)
                {
                    if (!String.IsNullOrEmpty(b.GoodReadsCoverImage))
                    {
                        Console.Write($"\r({i++}/{count} {FormatOutputLine(b.GoodReadsTitle)}");
                        var _dbImage = _bookCommands.GetImage(b.BookId);

                        if (_dbImage != null)
                        {
                            continue;
                        }

                        var _image = ImageHelper.DownloadImage(b.GoodReadsCoverImage);

                        if (_dbImage == null)
                        {
                            _dbImage = new DbImage();
                            _dbImage.ImageId = b.BookId;
                            _dbImage.ImageData = _image;

                            _bookCommands.InsertImage(_dbImage);
                        }
                        else
                        {
                            _dbImage.ImageData = _image;
                            _bookCommands.UpdateImage(_dbImage);
                        }
                    }
                }
                Console.WriteLine("== Downloading Author Images ==");

                i = 1;
                count = _authorList.Count;
                foreach (var a in _authorList)
                {
                    if (!String.IsNullOrEmpty(a.AuthorImageLink))
                    {
                        Console.Write($"\r({i++}/{count} {FormatOutputLine(a.AuthorName)}");
                        var _dbImage = _bookCommands.GetImage(a.AuthorId);

                        if (_dbImage != null)
                        {
                            continue;
                        }

                        var _image = ImageHelper.DownloadImage(a.AuthorImageLink);


                        if (_dbImage == null)
                        {
                            _dbImage = new DbImage();
                            _dbImage.ImageId = a.AuthorId;
                            _dbImage.ImageData = _image;

                            _bookCommands.InsertImage(_dbImage);
                        }
                        else
                        {
                            _dbImage.ImageData = _image;
                            _bookCommands.UpdateImage(_dbImage);
                        }
                    }
                }
            }
        }

        private AuthorDescription GetAuthorFromGoodReads(string authorLink)
        {
            var _authorDescription = new AuthorDescription();

            var _authorData = DoWebQuery(authorLink);

            HtmlNodeCollection _dataNodes = null;

            if (_authorData != null)
            {
                _authorDescription.AuthorImageLink = _authorData.DocumentNode.SelectSingleNode("//div[contains(@class, \"leftContainer\")]//img")?.GetAttributeValue("src", String.Empty).Trim();
                _dataNodes = _authorData.DocumentNode.SelectNodes("//div[@class=\"dataTitle\"]");
            }

            if (_dataNodes != null)
            {
                foreach (var n in _dataNodes)
                {
                    var _dataTitle = n.InnerText.ToLower();
                    var _dataValue = n.SelectSingleNode("./following-sibling::div").InnerText.Trim();
                    switch (_dataTitle)
                    {
                        case "born":
                            _authorDescription.BirthDate = _dataValue;
                            break;

                        case "genre":
                            _authorDescription.AuthorGenres = _dataValue;
                            break;

                        case "died":
                            _authorDescription.DeathDate = _dataValue;
                            break;

                        case "website":
                            _authorDescription.AuthorWebsite = _dataValue;
                            break;

                        case "influences":
                            _authorDescription.AuthorInfluences = _dataValue;
                            break;

                        case "twitter":
                            _authorDescription.AuthorTwitter = _dataValue;
                            break;

                        case "url":
                        case "member since":
                            break;

                        default:
                            Console.WriteLine("Unknown DataTitle -> " + _dataTitle);
                            break;
                    }
                }
            }

            var _descNodes = _authorData.DocumentNode.SelectNodes("//div[@class=\"rightContainer\"]//div[@class=\"aboutAuthorInfo\"]//span[contains(@id, \"freeText\")]");

            if (_descNodes != null)
            {
                if (_descNodes.Count > 1)
                {
                    _authorDescription.AuthorDesc = _descNodes[1].InnerHtml;
                }
                else
                {
                    _authorDescription.AuthorDesc = _descNodes[0].InnerHtml;
                }
            }

            return _authorDescription;
        }

        private void UpdateAuthors(BookDetails entry, Book _book)
        {
            foreach (var a in entry.Authors)
            {
                var _authorKey = _keyMaker.AuthorKey(a.AuthorName);

                var _author = _bookCommands.GetAuthor(_authorKey);

                if (_author == null)
                {
                    _author = new Author();
                    _author.AuthorId = _authorKey;
                    _author.AuthorName = a.AuthorName;
                    _author.GoodReadsAuthor = true;
                    _author.GoodReadsAuthorLink = a.AuthorLink;
                    _author.AuthorType = a.AuthorType;
                    _bookCommands.InsertAuthor(_author);
                }
                else
                {
                    _author.GoodReadsAuthor = true;
                    _author.GoodReadsAuthorLink = a.AuthorLink;
                    _author.AuthorType = a.AuthorType;
                    _bookCommands.UpdateAuthor(_author);
                }

                _bookCommands.LinkAuthorToBook(_author, _book, a.AuthorType);
            }
        }

        private void UpdateBook(BookDetails entry, Book book)
        {
            book.GoodReadsDescription = entry.BookDescription;
            book.GoodReadsTitle = entry.BookTitle;
            book.GoodReadsLink = entry.BookLink;
            book.GoodReadsCoverImage = entry.GoodReadsCoverImageLink;
            _bookCommands.UpdateBook(book);
        }

        private void UpdateSeries(BookDetails entry, Book book)
        {
            if (String.IsNullOrEmpty(entry.SeriesTitle))
            {
                return;
            }

            var _seriesId = _keyMaker.SeriesKey(entry.SeriesTitle);
            var _series = _bookCommands.GetSeries(_seriesId);

            if (_series == null)
            {
                _series = new Series();
                _series.SeriesId = _seriesId;
                _series.SeriesName = entry.SeriesTitle;
                _series.SeriesLink = entry.SeriesLink;
                _bookCommands.InsertSeries(_series);
            }

            _bookCommands.LinkBookToSeries(book, _series, entry.SeriesVolume);
        }

        private void UpdateGenres(BookDetails entry, Book book)
        {
            foreach (var g in entry.Genres)
            {
                var _genreId = _keyMaker.GenreKey(g);
                var _genre = _bookCommands.GetGenre(_genreId);

                if (_genre == null)
                {
                    _genre = new Genre();
                    _genre.GenreId = _genreId;
                    _genre.GenreName = g;
                    _bookCommands.InsertGenre(_genre);
                }

                _bookCommands.LinkBookToGenre(book, _genre);
            }
        }

        private BookDetails ProcessGoodreadsBookEntry(QueryResult goodreadsEntry)
        {
            HtmlNode _leftNode = null;
            HtmlNode _rightNode = null;
            HtmlDocument _bookResult = null;

            for (int i = 0; i < 20; i++)
            {
                _bookResult = DoWebQuery(goodreadsEntry.link);

                if (_bookResult != null)
                {
                    _leftNode = _bookResult.DocumentNode.SelectSingleNode("//div[@class=\"leftContainer\"]");
                    _rightNode = _bookResult.DocumentNode.SelectSingleNode("//div[@class=\"rightContainer\"]");
                }
                if (_leftNode != null && _rightNode != null)
                {
                    break;
                }

                Console.WriteLine("Failed Query");

            }

            if (_leftNode == null || _rightNode == null)
            {
                return null;
            }

            var _bookDetails = new BookDetails();
            _bookDetails.BookLink = goodreadsEntry.link.Split('?')[0].Trim();

            GetBookDetails(_leftNode, _bookDetails);
            GetGenreList(_rightNode, _bookDetails);

            return _bookDetails;
        }

        private void GetGenreList(HtmlNode rightNode, BookDetails bookDetails)
        {
            var _result = new List<string>();
            var _genreList = new List<string>();
            var _elementList = rightNode.SelectNodes("//div[@class=\"stacked\"]//div[contains(@class, \"elementList\")]//div[@class=\"left\"]//a");

            if (_elementList != null)
            {
                foreach (var e in _elementList)
                {
                    _genreList.Add(e.InnerText);
                }
            }
            bookDetails.Genres = _genreList;
        }

        private void GetBookDetails(HtmlNode leftNode, BookDetails bookDetails)
        {
            var _bookData = leftNode.SelectSingleNode("//div[@id=\"metacol\"]");
            var _coverImageLink = _bookData.SelectSingleNode("//img[@id=\"coverImage\"]")?.GetAttributeValue("src", String.Empty);

            var _title = HttpUtility.HtmlDecode(_bookData.SelectSingleNode("//h1[@id=\"bookTitle\"]")?.InnerText.Trim());
            //var _authorNodes = _bookData.SelectNodes("//a[@class=\"authorName\"]");
            var _authorNodes = _bookData.SelectNodes("//div[contains(@class, 'authorName')]");
            var _moreAuthorNodes = _bookData.SelectNodes("//span[@class='toggleContent']/a[@class='authorName']");
            var _authors = ProcessAuthorDetails(_authorNodes, _moreAuthorNodes);
            var _seriesLabel = _bookData.SelectSingleNode("//h2[@id=\"bookSeries\"]/a")?.InnerText.Trim();
            var _seriesLinkElement = _bookData.SelectSingleNode("//h2[@id=\"bookSeries\"]/a");
            if (_seriesLinkElement != null)
            {
                var _seriesLink = _seriesLinkElement.GetAttributeValue("href", String.Empty);
                var _seriesDto = ProcessSeriesLabel(_seriesLabel);
                var _seriesName = _seriesDto.SeriesTitle;
                var _seriesVolume = _seriesDto.SeriesVolume;
                bookDetails.SeriesTitle = _seriesName;
                bookDetails.SeriesVolume = _seriesVolume;
                bookDetails.SeriesLink = $"{GOODREADS}{_seriesLink}";
            }
            var _description = _bookData.SelectSingleNode("//div[@id=\"descriptionContainer\"]/div[@id=\"description\"]/span[2]")?.InnerHtml.Trim();

            if (String.IsNullOrEmpty(_description))
            {
                _description = _bookData.SelectSingleNode("//div[@id=\"descriptionContainer\"]/div[@id=\"description\"]/span[1]")?.InnerHtml.Trim();
            }

            bookDetails.BookTitle = _title;
            bookDetails.BookDescription = _description;
            bookDetails.Authors = _authors;
            bookDetails.GoodReadsCoverImageLink = _coverImageLink;
        }

        public string GetAuthorName(string author)
        {
            if (author.IndexOf('(') != -1)
            {
                return author.Substring(0, author.IndexOf('(') - 1);
            }

            return author;
        }

        public string GetAuthorTypes(string author)
        {
            if (author.IndexOf('(') != -1)
            {
                return author.Substring(author.IndexOf('('));
            }

            return String.Empty;
        }

        public List<AuthorDetails> ProcessAuthorDetails(HtmlNodeCollection authors, HtmlNodeCollection moreAuthors)
        {
            var _authorDetailsList = new List<AuthorDetails>();
            foreach (var a in authors)
            {
                var _authorInnerText = HttpUtility.HtmlDecode(a.InnerText.Replace(",", " ").Trim());
                var _name = GetAuthorName(_authorInnerText);
                var _description = GetAuthorTypes(_authorInnerText);
                var _link = a.SelectSingleNode("./a")?.GetAttributeValue("href", "");

                if (String.IsNullOrEmpty(_description))
                {
                    var _authorDetails = new AuthorDetails();
                    _authorDetails.AuthorLink = _link;
                    _authorDetails.AuthorName = _name;
                    _authorDetails.AuthorType = AuthorType.Author;
                    _authorDetailsList.Add(_authorDetails);
                }
                if (_description.ToLower().Contains("author"))
                {
                    var _authorDetails = new AuthorDetails();
                    _authorDetails.AuthorLink = _link;
                    _authorDetails.AuthorName = _name;
                    _authorDetails.AuthorType = AuthorType.Author;
                    _authorDetailsList.Add(_authorDetails);
                }
                if (_description.ToLower().Contains("editor"))
                {
                    var _authorDetails = new AuthorDetails();
                    _authorDetails.AuthorLink = _link;
                    _authorDetails.AuthorName = _name;
                    _authorDetails.AuthorType = AuthorType.Editor;
                    _authorDetailsList.Add(_authorDetails);
                }
                if (_description.ToLower().Contains("translator"))
                {
                    var _authorDetails = new AuthorDetails();
                    _authorDetails.AuthorLink = _link;
                    _authorDetails.AuthorName = _name;
                    _authorDetails.AuthorType = AuthorType.Translator;
                    _authorDetailsList.Add(_authorDetails);
                }
                if (_description.ToLower().Contains("foreword") || _description.ToLower().Contains("introduction"))
                {
                    var _authorDetails = new AuthorDetails();
                    _authorDetails.AuthorLink = _link;
                    _authorDetails.AuthorName = _name;
                    _authorDetails.AuthorType = AuthorType.Foreword;
                    _authorDetailsList.Add(_authorDetails);
                }
                if (_description.ToLower().Contains("contributor"))
                {
                    var _authorDetails = new AuthorDetails();
                    _authorDetails.AuthorLink = _link;
                    _authorDetails.AuthorName = _name;
                    _authorDetails.AuthorType = AuthorType.Contributor;
                    _authorDetailsList.Add(_authorDetails);
                }
                if (_description.ToLower().Contains("illustrator"))
                {
                    var _authorDetails = new AuthorDetails();
                    _authorDetails.AuthorLink = _link;
                    _authorDetails.AuthorName = _name;
                    _authorDetails.AuthorType = AuthorType.Illustrator;
                    _authorDetailsList.Add(_authorDetails);
                }
                if (_description.ToLower().Contains("narrator"))
                {
                    var _authorDetails = new AuthorDetails();
                    _authorDetails.AuthorLink = _link;
                    _authorDetails.AuthorName = _name;
                    _authorDetails.AuthorType = AuthorType.Narrator;
                    _authorDetailsList.Add(_authorDetails);
                }
            }

            //var _authorList = moreAuthors.First().ParentNode?.InnerText.Trim().Split(',').ToList();
            if (moreAuthors != null)
            {
                foreach (var a in moreAuthors)
                {
                    var _authorInnerText = a.InnerText.Trim();
                    var _name = a.InnerText.Trim();
                    var _description = a.SelectSingleNode("//a/following-sibling::span")?.InnerText.Trim();
                    var _link = a.GetAttributeValue("href", "");

                    if (String.IsNullOrEmpty(_description))
                    {
                        var _authorDetails = new AuthorDetails();
                        _authorDetails.AuthorLink = _link;
                        _authorDetails.AuthorName = _name;
                        _authorDetails.AuthorType = AuthorType.Author;
                        _authorDetailsList.Add(_authorDetails);
                    }
                    if (_description.ToLower().Contains("author"))
                    {
                        var _authorDetails = new AuthorDetails();
                        _authorDetails.AuthorLink = _link;
                        _authorDetails.AuthorName = _name;
                        _authorDetails.AuthorType = AuthorType.Author;
                        _authorDetailsList.Add(_authorDetails);
                    }
                    if (_description.ToLower().Contains("editor"))
                    {
                        var _authorDetails = new AuthorDetails();
                        _authorDetails.AuthorLink = _link;
                        _authorDetails.AuthorName = _name;
                        _authorDetails.AuthorType = AuthorType.Editor;
                        _authorDetailsList.Add(_authorDetails);
                    }
                    if (_description.ToLower().Contains("translator"))
                    {
                        var _authorDetails = new AuthorDetails();
                        _authorDetails.AuthorLink = _link;
                        _authorDetails.AuthorName = _name;
                        _authorDetails.AuthorType = AuthorType.Translator;
                        _authorDetailsList.Add(_authorDetails);
                    }
                    if (_description.ToLower().Contains("foreword") || _description.ToLower().Contains("introduction"))
                    {
                        var _authorDetails = new AuthorDetails();
                        _authorDetails.AuthorLink = _link;
                        _authorDetails.AuthorName = _name;
                        _authorDetails.AuthorType = AuthorType.Foreword;
                        _authorDetailsList.Add(_authorDetails);
                    }
                    if (_description.ToLower().Contains("contributor"))
                    {
                        var _authorDetails = new AuthorDetails();
                        _authorDetails.AuthorLink = _link;
                        _authorDetails.AuthorName = _name;
                        _authorDetails.AuthorType = AuthorType.Contributor;
                        _authorDetailsList.Add(_authorDetails);
                    }
                    if (_description.ToLower().Contains("illustrator"))
                    {
                        var _authorDetails = new AuthorDetails();
                        _authorDetails.AuthorLink = _link;
                        _authorDetails.AuthorName = _name;
                        _authorDetails.AuthorType = AuthorType.Illustrator;
                        _authorDetailsList.Add(_authorDetails);
                    }
                    if (_description.ToLower().Contains("narrator"))
                    {
                        var _authorDetails = new AuthorDetails();
                        _authorDetails.AuthorLink = _link;
                        _authorDetails.AuthorName = _name;
                        _authorDetails.AuthorType = AuthorType.Narrator;
                        _authorDetailsList.Add(_authorDetails);
                    }
                }
            }
            return _authorDetailsList;
        }

        private SeriesDto ProcessSeriesLabel(string seriesLabel)
        {
            var _seriesDto = new SeriesDto();
            seriesLabel = HttpUtility.HtmlDecode(seriesLabel);
            if (!String.IsNullOrEmpty(seriesLabel))
            {
                var _seriesLabel = seriesLabel.Replace('(', ' ').Replace(')', ' ').Replace(',', ' ');
                var _seriesParts = _seriesLabel.Split('#', StringSplitOptions.RemoveEmptyEntries);

                _seriesDto.SeriesTitle = _seriesParts.ElementAtOrDefault(0)?.Trim();
                _seriesDto.SeriesVolume = _seriesParts.ElementAtOrDefault(1);

                if (!String.IsNullOrEmpty(_seriesDto.SeriesVolume))
                {
                    _seriesDto.SeriesVolume = _seriesDto.SeriesVolume.Trim();
                }
            }
            return _seriesDto;
        }

        private string MakeGoodReadsQueryFromTitle(string title)
        {
            var _baseQuery = $"{GOODREADS}/search?utf8=%E2%9C%93&query=";
            var _titleQuery = Regex.Replace(title, @"\s+", " ").Trim().Replace(" ", "+");
            return _baseQuery + _titleQuery;
        }

        private string MakeGoodReadsQueryFromTitleAuthor(string title, List<Author> authors)
        {
            var _baseQuery = $"{GOODREADS}/search?utf8=%E2%9C%93&query=";
            var _titleQuery = Regex.Replace(title, @"\s+", " ").Trim().Replace(" ", "+");
            var _authorQueryList = new List<String>();

            foreach (var a in authors)
            {
                _authorQueryList.Add(Regex.Replace(a.AuthorName, @"\s+", " ").Trim().Replace(" ", "+"));
            }

            return _baseQuery + _titleQuery + "+" + String.Join("+", _authorQueryList);
        }

        private string MakeGoodReadsQueryFromAuthors(List<Author> authors)
        {
            var _baseQuery = $"{GOODREADS}/search?utf8=%E2%9C%93&query=";

            var _authorQueryList = new List<String>();

            foreach (var a in authors)
            {
                _authorQueryList.Add(Regex.Replace(a.AuthorName, @"\s+", " ").Trim().Replace(" ", "+"));
            }

            return _baseQuery + String.Join("+", _authorQueryList);
        }

        private HtmlDocument DoWebQuery(string request)
        {
            Thread.Sleep(_random.Next(100, 250)); // 2 - 4 sec delay so as not to upset good reads
            HtmlDocument result = null;

            for (int i = 0; i < 5; i++)
            {
                // Try it a few times, throws exceptions on connection problems
                try
                {
                    result = _web.Load(request);
                    return result;
                }
                catch
                {
                }
            }
            return null;
        }

        private List<SeriesResult> GetSeriesFromGoodReads(string link)
        {
            var _queryResult = DoWebQuery(link);

            if (_queryResult == null)
                return null;

            var _seriesBookList = ProcessSeriesQueryToList(_queryResult);

            return _seriesBookList;
        }

        private List<SeriesResult> ProcessSeriesQueryToList(HtmlDocument _queryResult)
        {
            var _result = new List<SeriesResult>();

            var _seriesDescJson = _queryResult.DocumentNode.SelectSingleNode("//div[@data-react-class=\"ReactComponents.SeriesHeader\"]")?.GetAttributeValue("data-react-props", String.Empty);
            var _seriesDesc = JsonConvert.DeserializeObject<SeriesDescription>(HttpUtility.HtmlDecode(_seriesDescJson));

            var _seriesListElements = _queryResult.DocumentNode.SelectNodes(".//div[@data-react-class=\"ReactComponents.SeriesList\"]");

            List<GRSeries> _grSeriesItems = new List<GRSeries>();

            foreach (var e in _seriesListElements)
            {
                var _jsonNode = HttpUtility.HtmlDecode(e.GetAttributeValue("data-react-props", String.Empty));
                var _seriesBooks = JsonConvert.DeserializeObject<SeriesChunk>(_jsonNode);

                if (_seriesBooks.seriesHeaders != null)
                {
                    for (int i = 0; i < _seriesBooks.series.Count; i++)
                    {
                        _seriesBooks.series[i].book.volume = _seriesBooks.seriesHeaders[i] != null ? _seriesBooks.seriesHeaders[i].Replace("Book", " ").Trim() : String.Empty;
                    }
                }
                _grSeriesItems.AddRange(_seriesBooks.series);
            }

            foreach (var si in _grSeriesItems)
            {
                var _bookTitle = si.book.title.Split('(')[0].Trim();
                var _bookVolumeSplit = si.book.title.Replace(")", String.Empty).Split('#');
                var _bookVolume = _bookVolumeSplit.Count() > 1 ? _bookVolumeSplit[1] : String.Empty;

                _result.Add(new SeriesResult()
                {
                    BookTitle = _bookTitle,
                    BookVolume = si.book.volume,
                    BookLink = GOODREADS + si.book.bookUrl,
                    SeriesTitle = _seriesDesc.title,
                    SeriesDescription = _seriesDesc.description.html,
                    CoverLink = si.book.imageUrl
                });
            }

            return _result;
        }

        private QueryResult SearchGoodreads(Book book, List<Author> authors)
        {
            var _searchRequest = MakeGoodReadsQueryFromAuthors(authors);

            List<QueryResult> _bookResults = new List<QueryResult>();

            for (int i = 0; i < 5; i++) // Get up to the first 100 hits
            {
                var _searchResult = DoWebQuery(_searchRequest);
                if (_searchRequest == null)
                    return null;

                _bookResults.AddRange(ProcessSearchQueryToList(_searchResult));
                _searchRequest = GetNextLink(_searchResult);
                if (_searchRequest == null)
                {
                    break;
                }
            }

            var _bookMatch = MatchBookResults(_bookResults, book, authors);

            if (_bookMatch == null)
            {
                // Lets add the title in with the author name, this is kinda messy if there is
                // series names and things, but is for the case of Limitless by Alan Glynn
                var _book = NormalizeTitle(book.BookTitle);
                _searchRequest = MakeGoodReadsQueryFromTitleAuthor(_book, authors);
                var _searchResult = DoWebQuery(_searchRequest);

                if (_searchResult == null)
                    return null;

                _bookResults = ProcessSearchQueryToList(_searchResult);
                // Trying to match the normalized title exactly but ignoring author
                _bookMatch = MatchBookResults(_bookResults, book, authors);
            }
            return _bookMatch;
        }

        private QueryResult MatchBookResults(List<QueryResult> bookResults, Book book)
        {
            var _bookToMatch = NormalizeTitle(HttpUtility.HtmlDecode(book.BookTitle));

            foreach (var br in bookResults)
            {
                var _resultTitle = NormalizeTitle(HttpUtility.HtmlDecode(br.Title));

                if (_bookToMatch.Contains(_resultTitle))
                {
                    return br;
                }
            }
            return null;
        }

        private QueryResult MatchBookResults(List<QueryResult> bookResults, Book book, List<Author> authors)
        {
            var _bookToMatch = NormalizeTitle(HttpUtility.HtmlDecode(book.BookTitle));

            QueryResult _bestMatch = null;
            int _bestMatchScore = -1;

            foreach (var br in bookResults)
            {
                int _matchScore = -1;
                // Now try to find the matching book

                // Match at least some of the authors. No author match def. wrong book.
                // Drop all spaces from author
                // Multi author anthos will only list a couple of authors. So match at least one author then stop

                int authorMatch = 0;

                foreach (var a in authors)
                {
                    foreach (var b in br.authors)
                    {
                        var a1 = NormalizeAuthor(a.AuthorName);
                        var a2 = NormalizeAuthor(HttpUtility.HtmlDecode(b));

                        var match = CalculateStringSimilarity(a1, a2);

                        if (match > 0.70)
                        {
                            authorMatch++;
                        }
                    }
                }

                if (authorMatch > 0)
                {
                    _matchScore = authorMatch;
                }

                if (authorMatch == 0)
                {
                    _matchScore = -2;
                }

                var _resultTitle = NormalizeTitle(HttpUtility.HtmlDecode(br.Title));
                var _resultSeries = String.IsNullOrEmpty(br.seriesTitle) ? null : NormalizeTitle(HttpUtility.HtmlDecode(br.seriesTitle));

                if (_bookToMatch.Contains(_resultTitle))
                {
                    _matchScore += 2;
                }

                if (!String.IsNullOrEmpty(br.seriesCount) && _bookToMatch.Contains($"book {br.seriesCount}"))
                {
                    _matchScore++;
                }

                if (!String.IsNullOrEmpty(_resultSeries) && _bookToMatch.Contains(_resultSeries))
                {
                    _matchScore++;
                }

                if (!String.IsNullOrEmpty(_resultSeries) && CalculateStringSimilarity(_resultTitle, _resultSeries) > 0.95)
                {
                    _matchScore -= 2;
                }

                //_matchScore += LongestCommonSubstringLength(_resultTitle, _bookToMatch);

                // Count the total number of common substrings and add that
                _matchScore += _resultTitle.Split().Intersect(_bookToMatch.Split(), new LevenshteinComparer()).Count();
                if (!String.IsNullOrEmpty(_resultSeries))
                {
                    _matchScore += _resultSeries.Split().Intersect(_bookToMatch.Split(), new LevenshteinComparer()).Count();
                }

                // Record all the best matches and return them. Search with each different methodoly and see what is best
                // at the end.
                if (_matchScore > _bestMatchScore)
                {
                    _bestMatchScore = _matchScore;
                    _bestMatch = br;
                }
            }

            // Check the best match does make sense as a match
            if (_bestMatch != null && (_bookToMatch.Contains(NormalizeTitle(_bestMatch.Title)) || NormalizeTitle(_bestMatch.Title).Contains(_bookToMatch)))
            {
                return _bestMatch;
            }

            return null;
        }

        private string NormalizeAuthor(string s)
        {
            //Regex.Replace(a.AuthorName, @"\s+", " ").Tr

            s = Regex.Replace(s, @"\s*,?\s*Jr\.?\s*$", String.Empty, RegexOptions.IgnoreCase).Trim(); // Remove Jr and cominations there of at end of string)
            s = s.Replace('"', ' ').Replace("'", " "); // E.E. "Doc" Smith case

            return Regex.Replace(s, @"\s+", String.Empty).Replace(".", String.Empty).ToLower().Trim();
        }

        private string NormalizeTitle(string s)
        {
            s = s.ToLower().Replace(":", " ").Replace("_", " ").Replace("?", " ").Replace("!", " ").Replace("'", "").Replace("'", "").Replace("-", " ");

            if (s.StartsWith("the"))
            {
                s = s.Substring(3).Trim();
            }

            s = Regex.Replace(s, @"^the\s+|-\s*the\s+", String.Empty);
            s = Regex.Replace(s, @"^a\s+|-\s*a\s+", String.Empty);
            s = s.Replace("-", " ").Replace(",", " ");

            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private string GetNextLink(HtmlDocument searchResult)
        {
            var _links = searchResult.DocumentNode.Descendants("a");

            foreach (var l in _links)
            {
                var _class = l.GetAttributeValue("class", null);

                if (_class == "next_page")
                {
                    var _href = l.GetAttributeValue("href", null);

                    return GOODREADS + _href;
                }
            }
            return null;
        }

        private List<QueryResult> ProcessSearchQueryToList(HtmlDocument searchResult)
        {
            var _result = new List<QueryResult>();

            var _tableRows = searchResult.DocumentNode.Descendants("tr").Where(node => node.Attributes.Contains("itemscope"));

            foreach (var _tr in _tableRows)
            {
                var _authorNodes = _tr.Descendants("a").Where(div => div.GetAttributeValue("class", null) == "authorName");
                var _titleNode = _tr.Descendants("a").Where(a => a.GetAttributeValue("class", null) == "bookTitle");

                var _authors = new List<string>();
                foreach (var a in _authorNodes)
                {
                    _authors.Add(a.InnerText);
                }

                var _href = GOODREADS + _titleNode.First().GetAttributeValue("href", null);

                var _title = _titleNode.First().InnerText.Trim();

                _result.Add(new QueryResult(_title, _authors, _href));
            }

            return _result;
        }

        private int ComputeLevenshteinDistance(string s1, string s2)
        {
            if ((s1 == null) || (s2 == null))
            {
                return 0;
            }

            if ((s1.Length == 0) || (s2.Length == 0))
            {
                return 0;
            }

            if (s1 == s2)
            {
                return s1.Length;
            }

            int sourceWordCount = s1.Length;
            int targetWordCount = s2.Length;

            // Step 1
            if (sourceWordCount == 0)
            {
                return targetWordCount;
            }

            if (targetWordCount == 0)
            {
                return sourceWordCount;
            }

            int[,] distance = new int[sourceWordCount + 1, targetWordCount + 1];

            // Step 2
            for (int i = 0; i <= sourceWordCount; distance[i, 0] = i++)
            {
                ;
            }

            for (int j = 0; j <= targetWordCount; distance[0, j] = j++)
            {
                ;
            }

            for (int i = 1; i <= sourceWordCount; i++)
            {
                for (int j = 1; j <= targetWordCount; j++)
                {
                    // Step 3
                    int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;

                    // Step 4
                    distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
                }
            }

            return distance[sourceWordCount, targetWordCount];
        }

        private double CalculateStringSimilarity(string s1, string s2)
        {
            if ((s1 == null) || (s2 == null))
            {
                return 0.0;
            }

            if ((s1.Length == 0) || (s2.Length == 0))
            {
                return 0.0;
            }

            if (s1 == s2)
            {
                return 1.0;
            }

            int stepsToSame = ComputeLevenshteinDistance(s1, s2);
            return (1.0 - (stepsToSame / (double)Math.Max(s1.Length, s2.Length)));
        }

        public int LongestCommonSubstringLength(string a, string b)
        {
            var lengths = new int[a.Length, b.Length];
            int greatestLength = 0;
            string output = "";
            for (int i = 0; i < a.Length; i++)
            {
                for (int j = 0; j < b.Length; j++)
                {
                    if (a[i] == b[j])
                    {
                        lengths[i, j] = i == 0 || j == 0 ? 1 : lengths[i - 1, j - 1] + 1;
                        if (lengths[i, j] > greatestLength)
                        {
                            greatestLength = lengths[i, j];
                            output = a.Substring(i - greatestLength + 1, greatestLength);
                        }
                    }
                    else
                    {
                        lengths[i, j] = 0;
                    }
                }
            }
            return output.Length;
        }

        public string FormatOutputLine(string s)
        {
            int len = s.Length;

            if (len > 30)
            {
                return s.Substring(0, 30);
            }

            return s.PadRight(30);
        }
    }
}