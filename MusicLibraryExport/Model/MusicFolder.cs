using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace MusicLibraryExport.Model
{
    /// <summary>
    /// Classe décomposant un dossier de musique.
    /// </summary>
    public class MusicFolder : ObservableObject
    {
        #region Expressions rationnelles
        private static readonly Regex _regexBase = new Regex(@"^\\?(?<Artist>[^\\]+)\\(?<Record>[^\\]+)(\\(?<VolumeFormat>.*))?$", RegexOptions.Compiled);
        #endregion

        #region Constantes
        private static readonly HashSet<string> FORMATS = new HashSet<string>(new string [] { "MP3", "FLAC" });

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Membres
        private string _path;
        private string _artist;
        private string _record;
        private string _volume = string.Empty;
        private string _format = string.Empty;
        private bool _estSelectionne;
        #endregion

        #region Propriétés
        public string Path
        {
            get => this._path;
            set => Set<string>(() => this.Path, ref this._path, value);
        }

        public string Artist
        {
            get => this._artist;
            set => Set<string>(() => this.Artist, ref this._artist, value);
        }

        public string Record
        {
            get => this._record;
            set => Set<string>(() => this.Record, ref this._record, value);
        }

        public string Volume
        {
            get => this._volume;
            set => Set<string>(() => this.Volume, ref this._volume, value);
        }

        public string Format
        {
            get => this._format;
            set => Set<string>(() => this.Format, ref this._format, value);
        }
        
        [XmlIgnore]
        public bool EstSelectionne
        {
            get => this._estSelectionne;
            set => Set<bool>(() => this.EstSelectionne, ref this._estSelectionne, value);
        }

        [XmlIgnore]
        public string DestinationFolderName
        {
            get
            {
                var outFolderName = $"{this.Artist}-{this.Record}";
                if (!string.IsNullOrEmpty(this.Volume))
                {
                    outFolderName += $"-{this.Volume}";
                }

                return outFolderName;
            }
        }
        #endregion

        private MusicFolder()
        { }

        private MusicFolder(string path, string artist, string record)
        {
            this.Path = path;
            this.Artist = artist;
            this.Record = record;
        }

        private MusicFolder(string path, string artist, string record, string volume, string format)
            :this(path, artist, record)
        {
            this.Volume = volume;
            this.Format = format;
        }

        public static MusicFolder BuildFromPath(string pathRoot, string path)
        {
            if (!path.StartsWith(pathRoot))
            {
                throw new ArgumentException($"Le chemin \"{path}\" n'a pas pour base \"pathRoot\".");
            }

            var relativePath = path.Substring(pathRoot.Length);

            var match = _regexBase.Match(relativePath);

            if (!match.Success)
            {
                throw new ArgumentException($"Le chemin \"{path}\" n'est pas un chemin de musique reconnu.");
            }

            string artist = match.Groups["Artist"].Value;
            string record = match.Groups["Record"].Value;
            string volumeFormat = match.Groups["VolumeFormat"].Value;

            if (string.IsNullOrEmpty(volumeFormat))
            {
                return new MusicFolder(path, artist, record);
            }

            int lastBackslashIndex = volumeFormat.LastIndexOf(@"\");
            string format = string.Empty;
            string volume = string.Empty;
            string partie1;
            string partie2;
            if (lastBackslashIndex > -1)
            {
                partie1 = volumeFormat.Substring(0, lastBackslashIndex);
                partie2 = volumeFormat.Substring(lastBackslashIndex + 1);
            }
            else
            {
                partie1 = volumeFormat;
                partie2 = string.Empty;
            }

            if (FORMATS.Contains(partie1))
            {
                format = partie1;
                volume = partie2;
            }
            else
            {
                volume = partie1;

                if (!string.IsNullOrEmpty(partie2))
                {
                    throw new ArgumentException($"Le chemin \"{path}\" n'est pas un chemin de musique reconnu : le volume doit se situer après le format.");
                }
            }

            return new MusicFolder(path, artist, record, volume, format);
        }
    }
}