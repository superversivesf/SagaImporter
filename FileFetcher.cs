using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TagLib;

namespace SagaImporter
{
    class FileFetcher
    {
        public FileFetcher()
        { }

        public TagLib.File GetMetaData(string path)
        {
            return TagLib.File.Create(path);
        }

        public List<string> GetFiles(string path, string filter = "*.*")
        {
            var _result =  Directory.EnumerateFiles(path, filter, SearchOption.AllDirectories).ToList();
            return _result;
        }
    }
}
