using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MusicLibraryExport.Model
{
    /// <summary>
    /// Classe décomposant un dossier de musique.
    /// </summary>
    public class MusicFolder
    {
        #region Expressions rationnelles
        private static readonly Regex _regexBase = new Regex(@"^\\?(?<Artist>[^\\]+)\\(?<Record>[^\\]+)(\\(?<VolumeFormat>.*))?$", RegexOptions.Compiled);
        #endregion

        #region Constantes
        private static readonly HashSet<string> FORMATS = new HashSet<string>(new string [] { "MP3", "FLAC" });
        #endregion

        #region Propriétés
        public string Path { get; set; }

        public string Artist { get; set; }

        public string Record { get; set; }

        public string Volume { get; set; } = string.Empty;

        public string Format { get; set; } = string.Empty;

        [XmlIgnore]
        public bool EstSelectionne { get; set; }
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
