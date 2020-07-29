
using SagaDb;
using SagaDb.Database;
using SagaDb.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SagaServer
{
    class InputProcessor
    {
        private string rootDir;
        private BookCommands _bookCommands;
        public  InputProcessor()
        { 
            
        }

        public bool Initialize(InputOptions i)
        {
            this.rootDir = i.InputFolder;
            this._bookCommands = new BookCommands(i.DatabaseFile);
            return true;
        }

        public bool Execute()
        {
            var _root = this.rootDir;

            Console.WriteLine("Getting folder: " + _root);

            var folderWithoutSubfolder = Directory.EnumerateDirectories(_root, "*.*", SearchOption.AllDirectories)
                .Where(f => !Directory.EnumerateDirectories(f, "*.*", SearchOption.TopDirectoryOnly).Any());



            int i = 0;
            foreach (var folder in folderWithoutSubfolder)
            {
                try
                {
                    Console.Write("\rFolder -> " + ++i + "/" + folderWithoutSubfolder.Count() + "            ");

                    KeyMaker _km = new KeyMaker();
                    string[] files = Directory.GetFiles(folder, "*.mp3", SearchOption.TopDirectoryOnly);

                    foreach (var f in files)
                    {
                        var _tagFile = TagLib.File.Create(f);
                        var _tag = _tagFile.GetTag(TagLib.TagTypes.Id3v2);
                        var _authorEntries = new List<Author>();

                        var title = _tag.Title;

                        TimeSpan duration = _tagFile.Properties.Duration;

                        // Clean authors
                        var authors = CleanAuthors(_tag.AlbumArtists);

                        #pragma warning disable CS0618 // For a lot of files the Artists is set but this is obselete. Still need to check. 
                        if (authors.Count == 0)
                            authors = CleanAuthors(_tag.Artists); // Try the older version if the newer one isn't filled in
                        #pragma warning restore CS0618 

                            // Add authors if not already added
                        foreach (var a in authors)
                        {
                            var _authorKey = _km.AuthorKey(a);

                            Author _author = this._bookCommands.GetAuthor(_authorKey);

                            if (_author == null)
                            {
                                _author = new Author
                                {
                                    AuthorId = _authorKey,
                                    AuthorName = a,
                                    AuthorDescription = String.Empty,
                                    GoodReadsAuthor = false,
                                    AuthorType = AuthorType.Unknown,
                                    GoodReadsAuthorLink = String.Empty,
                                    AuthorImageLink = String.Empty,
                                    AuthorWebsite = String.Empty,
                                    Born = String.Empty,
                                    Died = String.Empty,
                                    Genre = String.Empty,
                                    Influences = String.Empty
                                };
                                this._bookCommands.InsertAuthor(_author);
                            }
                            _authorEntries.Add(_author);
                        }

                        // Clean Title
                        var _title = CleanTitle(_tag.Album);

                        // Add authors if not already added
                        var _bookKey = _km.BookKey(_title, _authorEntries.Select(a => a.AuthorId));
                        Book _book = this._bookCommands.GetBook(_bookKey);

                        if (_book == null)
                        {
                            _book = new Book
                            {
                                BookId = _bookKey,
                                BookTitle = _title,
                                GoodReadsDescription = String.Empty,
                                GoodReadsLink = String.Empty,
                                GoodReadsTitle = String.Empty,
                                BookLocation = Path.GetDirectoryName(f),
                                GoodReadsFetchTried = false
                            };
                            this._bookCommands.InsertBook(_book);
                            _bookCommands.LinkAuthorToBook(_authorEntries, _book, AuthorType.Unknown);
                        }

                        // Add the audioFile

                        var _audioFileKey = _km.FileKey(f);
                        AudioFile _audioFile = this._bookCommands.GetAudioFile(_audioFileKey);

                        if (_audioFile == null)
                        {
                            _audioFile = new AudioFile
                            {
                                AudioFileFolder = Path.GetDirectoryName(f),
                                AudioFileName = Path.GetFileName(f),
                                AudioFileId = _audioFileKey,
                                Duration = _tagFile.Properties.Duration.TotalSeconds
                            };
                            this._bookCommands.InsertAudioFile(_audioFile);
                        }

                        _bookCommands.LinkBookToFile(_book, _audioFile);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Problem importing {folder}");
                    Console.WriteLine($"Exception -> {e.Message}");
                    //Console.WriteLine($"Stack Trace \n=============\n{e.StackTrace}\n===============\n");
                }
            }
            Console.WriteLine("\n === Authors ===");
            _bookCommands.DumpAuthors();
            Console.WriteLine(" === Books ===");
            _bookCommands.DumpBooks();
            //Console.WriteLine(" === Audio Files ===");
            //_bookCommands.DumpAudiofiles();

            return true;
        }

        private string CleanTitle(string title)
        {
            var _title = Regex.Replace(title, @"^\d+\.*\d*\s-", String.Empty, RegexOptions.IgnoreCase);

            return _title.Trim();
        }

        public List<string> CleanAuthors(string[] authorsArray)
        {
            var _result = new List<string>();

            var _authors =  string.Join(',', authorsArray);

            _authors = Regex.Replace(_authors, @"\s*,\s*jr", " Jr", RegexOptions.IgnoreCase);
            _authors = Regex.Replace(_authors, @"\(.*\)", String.Empty);

            var _toReplace =    "àèìòùÀÈÌÒÙ äëïöüÄËÏÖÜ âêîôûÂÊÎÔÛ áéíóúÁÉÍÓÚðÐýÝ ãñõÃÑÕšŠžŽçÇåÅøØ `\"".ToCharArray();
            var _replaceChars = "aeiouAEIOU aeiouAEIOU aeiouAEIOU aeiouAEIOUdDyY anoANOsSzZcCaAoO .'".ToCharArray();

            for (int i = 0; i < _toReplace.Length; i++)
            {
                _authors = _authors.Replace(_toReplace[i], _replaceChars[i]);
            }

            var _authorList = _authors.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            var _authorQueue = new Queue<string>(_authorList);

            while(_authorQueue.Count != 0)
            {
                var _processed = _authorQueue.Dequeue();

                if (_processed.ToLower().Contains(" and ") || _processed.Contains(" & "))
                {
                    // Split on and/And/& and then requeue and continue

                    string[] _newAuthors;
                    if (_processed.Contains("&"))
                    {
                        _newAuthors = _processed.Split('&');
                    }
                    else
                    {
                        _newAuthors = Regex.Split(_processed, " and ", RegexOptions.IgnoreCase);
                    }
                    foreach (var n in _newAuthors)
                    {
                        if (!String.IsNullOrEmpty(n))
                            _authorQueue.Enqueue(n.Trim());
                    }
                    continue;
                }

                if (_processed.ToLower().Contains("phd"))
                {
                    continue;                
                }

                if (_processed.ToLower().Contains("- adaptation"))
                {
                    _processed = Regex.Replace(_processed, "- adaptation", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("(translator)"))
                {
                    _processed = Regex.Replace(_processed, ".translator.", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("- translator"))
                {
                    _processed = Regex.Replace(_processed, "- translator", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("(editor)"))
                {
                    _processed = Regex.Replace(_processed, ".editor.", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("dr. "))
                {
                    _processed = Regex.Replace(_processed, @"dr\.", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("dr "))
                {
                    _processed = Regex.Replace(_processed, "dr ", String.Empty, RegexOptions.IgnoreCase);
                }


                if (_processed.ToLower().Contains("edited by"))
                {
                    _processed = Regex.Replace(_processed, "edited by", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("- editor"))
                {
                    _processed = Regex.Replace(_processed, "- editor", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("professor"))
                {
                    _processed = Regex.Replace(_processed, "professor", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("prof."))
                {
                    _processed = Regex.Replace(_processed, @"prof\.", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("sir"))
                {
                    _processed = Regex.Replace(_processed, "sir", String.Empty, RegexOptions.IgnoreCase);
                }

                if (_processed.ToLower().Contains("foreword"))
                {
                    //_processed = Regex.Replace(_processed, "foreword by", String.Empty, RegexOptions.IgnoreCase);
                    continue;
                }


                if (_processed.ToLower().Contains("introduction"))
                {
                    //_processed = Regex.Replace(_processed, "foreword by", String.Empty, RegexOptions.IgnoreCase);
                    continue;
                }

                if (_processed.Contains('.'))
                {
                    var _parts = _processed.Split('.');

                    StringBuilder _tmp = new StringBuilder();

                    int i = 0;
                    for (; i < _parts.Count() - 1; i++)
                    {
                        if (_parts[i + 1].StartsWith(' '))
                            _tmp.Append(_parts[i] + ".");
                        else
                            _tmp.Append(_parts[i] + ". ");
                    }
                    _tmp.Append(_parts[i]);
                    _processed = _tmp.ToString();
                }
                                
                _result.Add(_processed.Trim());
            }
            return _result;
        }
    }
}
