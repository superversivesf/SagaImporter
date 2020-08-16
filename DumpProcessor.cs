using SagaDb.Database;
using System;

namespace SagaImporter
{
    internal class DumpProcessor
    {
        private BookCommands _bookCommands;
        private bool _authors;
        private bool _books;
        private bool _failedLookups;
        private bool _genres;
        private bool _series;
        private bool _stats;
        private bool _duplicates;

        public DumpProcessor()
        {
        }

        internal bool Initialize(DumpOptions d)
        {
            this._bookCommands = new BookCommands(d.DatabaseFile);
            this._authors = d.Authors;
            this._books = d.Books;
            this._failedLookups = d.FailedLookups;
            this._genres = d.Genres;  
            this._series = d.Series;
            this._stats = d.Stats;
            this._duplicates = d.Duplicates;
            return true;
        }

        internal void Execute()
        {
            if (this._authors)
            {
                var _authors = this._bookCommands.GetAuthors();
                Console.WriteLine("\n=== Authors ===");
                foreach (var a in _authors)
                {
                    var _msg = a.GoodReadsAuthor ? " - GR" : "";
                    _msg = $"- {a.AuthorType.ToString()}";
                    Console.WriteLine($"{a.AuthorName}{_msg}");
                }
            }
            if (this._books)
            {
                var _books = this._bookCommands.GetBooks();
                Console.WriteLine("\n=== Books ===");
                foreach (var b in _books)
                {
                    var _gr = String.IsNullOrEmpty(b.GoodReadsDescription) ? " - GR" : "";
                    Console.WriteLine($"{b.BookTitle}{_gr}");
                }
            }
            if (this._failedLookups)
            {
                var _books = this._bookCommands.GetBooksFailedGoodReads();
                Console.WriteLine("\n=== Failed Lookups ===");
                foreach (var b in _books)
                {
                    Console.WriteLine($"{b.BookTitle}");
                }
            }
            if (this._genres)
            {
                var _genres = this._bookCommands.GetGenres();
                Console.WriteLine("\n=== Genres ===");
                foreach (var g in _genres)
                {
                    Console.WriteLine($"{g.GenreName}");
                }
            }
            if (this._series)
            {
                var _series = this._bookCommands.GetAllSeries();
                Console.WriteLine("\n=== Series ===");
                foreach (var s in _series)
                {
                    Console.WriteLine($"{s.SeriesName}");
                }
            }

            if (this._duplicates)
            {
                var _books = this._bookCommands.GetBooks();
                var _books2 = this._bookCommands.GetBooks();

                foreach (var book in _books)
                {
                    int matchCount = 0;
                    foreach (var book2 in _books2)
                    {
                        if (book.BookTitle == book2.BookTitle)
                            matchCount++;
                    }

                    if (matchCount > 1)
                    {
                        Console.WriteLine($"Duplicate Title: {book.BookTitle} -> {book.BookLocation}");
                    }
                }

            }

            if (this._stats)
            {                
                var _series = this._bookCommands.GetAllSeries();
                var _genres = this._bookCommands.GetGenres();
                var _missed = this._bookCommands.GetBooksFailedGoodReads();
                var _books = this._bookCommands.GetBooks();
                var _missingGoodReads = this._bookCommands.GetBooksMissingGoodReads();
                Console.WriteLine("\n=== Stats ===");
                Console.WriteLine($"Books: {_books.Count}");
                Console.WriteLine($"Series: {_series.Count}");
                Console.WriteLine($"Genres: {_genres.Count}");
                Console.WriteLine($"Failed Lookup: {_missed.Count}");
                Console.WriteLine($"Miss Percentage: {(((double)_missed.Count / (double)_books.Count) * 100).ToString("F0")}%");
                Console.WriteLine($"Goodreads Lookup not done: {_missingGoodReads.Count}");
            }

        }
    }
}