using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicLibraryExport.Model
{
    public class FileExportTask
    {
        public string SourcePath { get; set; }

        public string ConvertedPath { get; set; }

        public string DestinationPath { get; set; }

        public bool IsConvertionNeeded
        {
            get
            {
                return this.SourcePath.Substring(this.SourcePath.LastIndexOf(".")).ToLower() != this.DestinationPath.Substring(this.DestinationPath.LastIndexOf(".")).ToLower();
            }
        }

        public bool IsReadyToCopyToDestination
        {
            get
            {
                return !this.IsDone &&
                       (
                            !this.IsConvertionNeeded ||
                            !string.IsNullOrEmpty(ConvertedPath)
                       );
            }
        }

        public bool IsDone { get; set; }

        public FileExportTask(string sourcePath, string destinationPath)
        {
            this.SourcePath = sourcePath;
            this.DestinationPath = destinationPath;
        }
    }
}
