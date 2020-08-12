using CommandLine;

namespace SagaImporter
{
    [Verb("input", HelpText = "Parse and try to add directory contents to db")]
    class InputOptions
    {

        [Option('i', "InputFolder", Required = true, HelpText = "The folder to read in from")]
        public string InputFolder { get; set; }

        [Option('d', "DatabaseFile", Required = true, HelpText = "The database file to write too    ")]
        public string DatabaseFile { get; set; }
    }

    [Verb("output", HelpText = "Write DB entries to output creating folders and links")]
    class OutputOptions
    {
        [Option('o', "OutputFolder", Required = true, HelpText = "The folder to write out to")]
        public string OutputFolder { get; set; }

        [Option('d', "DatabaseFile", Required = true, HelpText = "The database file to read from")]
        public string DatabaseFile { get; set; }

        [Option('a', "WriteAuthors", Required = false, HelpText = "Write out a set of directories for authors with their books underneath, defaults to Authors/{Author Name}/{Book Name}")]
        public bool WriteAuthors { get; set; }

        [Option('s', "WriteSeries", Required = false, HelpText = "Write out a set of directories for series with their books underneath, defaults to Series/{Series Name}/{Book Name}")]
        public bool WriteSeries { get; set; }

        [Option('g', "WriteGenre", Required = false, HelpText = "Write out a set of directories for genres with their books underneath, defaults to Genre/{Genre Name}/{Book Name}")]
        public bool WriteGenre { get; set; }

        [Option('r', "WriteReports", Required = false, HelpText = "Write out a reports directory that contains the hint csv for editing")]
        public bool WriteReports { get; set; }

        [Option('t', "TopLevelOverride", Required = false, HelpText = "Override the toplevel directory name when writing out from Authors/Series/Genres respectively. ")]
        public string TopLevelOverride { get; set; }
        [Option('e', "WriteDescriptions", Required = false, HelpText = "Write out desc.txt files for all looked up files. ")]
        public bool WriteDescriptions { get; set; }
        [Option('b', "WriteBooks", Required = false, HelpText = "Write out all the books to a books folder. Not really useful except for fidning for fixing things ")]
        public bool WriteBooks { get; set; }
        [Option('f', "WriteFailBooks", Required = false, HelpText = "Write out all the books to a books folder. Not really useful except for fidning for fixing things ")]
        public bool WriteFailBooks { get; set; }

    }

    [Verb("lookup", HelpText = "Try to gather description and other information from Goodreads")]
    class LookupOptions
    {
        [Option('d', "DatabaseFile", Required = true, HelpText = "The database file to write too or read from")]
        public string DatabaseFile { get; set; }

        [Option('f', "hintFile", Required = false, HelpText = "Search for missing goodreads lookups using hint file pointing to book pages")]
        public string HintFile { get; set; }

        [Option('r', "Retry", Required = false, HelpText = "Do good reads lookups, retrying the lookups that failed only")]
        public bool Retry { get; set; }

        [Option('b', "Books", Required = false, HelpText = "Do good reads lookups of book entries")]
        public bool Books { get; set; }

        [Option('s', "Series", Required = false, HelpText = "Do good reads lookups of series entries, backfill series entries as needed")]
        public bool Series { get; set; }

        [Option('a', "Authors", Required = false, HelpText = "Do good reads lookups of author entries")]
        public bool Authors { get; set; }

        [Option('p', "PurgeAndRebuild", Required = false, HelpText = "This will purge the series and author tables and rebuild them by using the good reads data")]
        public bool PurgeAndRebuild { get; set; }

        [Option('i', "Images", Required = false, HelpText = "Look up good reads author and cover images and add them to the database")]
        public bool Images { get; set; }
    }

    [Verb("dump", HelpText = "Dump database information")]
    class DumpOptions
    {
        [Option('d', "DatabaseFile", Required = true, HelpText = "The database file to write too or read from")]
        public string DatabaseFile { get; set; }

        [Option('a', "Authors", Required = false, HelpText = "Dump out all of the authors in the database")]
        public bool Authors { get; set; }

        [Option('b', "Books", Required = false, HelpText = "Dump out all of the books in the database")]
        public bool Books { get; set; }

        [Option('s', "Series", Required = false, HelpText = "Dump out all the series in the database")]
        public bool Series { get; set; }

        [Option('g', "Genres", Required = false, HelpText = "dump out all the genres in the database")]
        public bool Genres { get; set; }

        [Option('f', "FailedLookups", Required = false, HelpText = "Dump out all the books that failed a good reads lookup")]
        public bool FailedLookups { get; set; }

        [Option('t', "Stats", Required = false, HelpText = "Dump out librarry stats")]
        public bool Stats { get; set; }
    }

    [Verb("clean", HelpText = "clean database information")]
    class CleanOptions
    {
        [Option('d', "DatabaseFile", Required = true, HelpText = "The database file to write too or read from")]
        public string DatabaseFile { get; set; }
    }

}
