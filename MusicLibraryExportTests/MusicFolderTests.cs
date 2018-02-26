using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MusicLibraryExport.Model;
using NFluent;

namespace MusicLibraryExportTests
{
    [TestClass]
    public class MusicFolderTests
    {
        private void BuiltTests_Check(string pathRoot, string path, string artist, string record, string volume, string format)
        {
            var musicFolder = MusicFolder.BuildFromPath(pathRoot, path);

            Check.That(musicFolder.Artist)
                 .IsEqualTo(artist);

            Check.That(musicFolder.Record)
                 .IsEqualTo(record);

            Check.That(musicFolder.Volume)
                 .IsEqualTo(volume);

            Check.That(musicFolder.Format)
                 .IsEqualTo(format);
        }

        [TestMethod]
        public void BuiltTests_1()
        {
            this.BuiltTests_Check(@"c:\", @"c:\Artist\Record", "Artist", "Record", string.Empty, string.Empty);
        }

        [TestMethod]
        public void BuiltTests_2()
        {
            this.BuiltTests_Check(@"c:\", @"c:\Artist\Record\FLAC", "Artist", "Record", string.Empty, "FLAC");
        }

        [TestMethod]
        public void BuiltTests_3()
        {
            this.BuiltTests_Check(@"c:\", @"c:\Artist\Record\MP3", "Artist", "Record", string.Empty, "MP3");
        }

        [TestMethod]
        public void BuiltTests_4()
        {
            this.BuiltTests_Check(@"c:\", @"c:\Artist\Record\Volume1\", "Artist", "Record", "Volume1", string.Empty);
        }

        [TestMethod]
        public void BuiltTests_5()
        {
            this.BuiltTests_Check(@"c:\", @"c:\Artist\Record\FLAC\Volume1", "Artist", "Record", "Volume1", "FLAC");
        }
    }
}
