using SagaDb.Database;
using SagaImporter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SagaImporter
{
    class CleanProcessor
    {
        private BookCommands _bookCommands;

        internal bool Initialize(CleanOptions c)
        {
            this._bookCommands = new BookCommands(c.DatabaseFile);
            return true;
        }
        internal void Execute()
        {
            var _books = this._bookCommands.GetBooks();
            int _removeBookCount = 0;
            int _removeAuthorCount = 0;
            int _removeSeriesCount = 0;

            foreach (var book in _books)
            {
                if (!Directory.Exists(book.BookLocation))
                {
                    var title = book.BookTitle;
                    this._bookCommands.RemoveBookToSeriesLinksByBook(book);
                    this._bookCommands.RemoveBookToAuthorLinksByBook(book);
                    this._bookCommands.RemoveBookToGenreLinksByBook(book);
                    this._bookCommands.RemoveBookToAudioLinksAndAudioFilesByBook(book);
                    this._bookCommands.RemoveBook(book);
                    _removeBookCount++;
                    Console.WriteLine($"Removed {title}: Directory Missing");
                }
            }

            var _authors = this._bookCommands.GetAuthors();

            foreach (var author in _authors)
            {
                var _bookCount = this._bookCommands.GetBooksByAuthorId(author.AuthorId).ToList().Count();

                if (_bookCount == 0)
                {
                    Console.WriteLine($"Orphaned Author: {author.AuthorName}");
                    this._bookCommands.RemoveAuthor(author);
                    _removeAuthorCount++;
                }
            }

            var _series = this._bookCommands.GetAllSeries();

            foreach (var series in _series)
            {
                var _bookCount = this._bookCommands.GetSeriesBooks(series.SeriesId).ToList().Count();

                if (_bookCount == 0)
                {
                    Console.WriteLine($"Orphaned Series: {series.SeriesName}");
                    this._bookCommands.RemoveSeries(series);
                    _removeSeriesCount++;
                }
            }

            Console.WriteLine($"Removed {_removeBookCount} books from library");
            Console.WriteLine($"Removed {_removeAuthorCount} authors from library");
            Console.WriteLine($"Removed {_removeSeriesCount} series from library");

        }
    }
}
