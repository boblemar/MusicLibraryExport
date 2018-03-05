using MusicLibraryExport.Model;
using NAudio.Flac;
using NAudio.Lame;
using NAudio.Wave;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicLibraryExport
{
    public class MusicFoldersExporter
    {
        #region Énumérations
        public enum ExportProgressType
        {
            CopyBegin,
            CopyEnd,
            ConvertBegin,
            ConvertEnd,
            End,
            DeleteBegin,
            DeleteEnd,
            Error
        }
        #endregion

        #region Classes
        public class ExportProgressEventArgs : EventArgs
        {
            public ExportProgressType Type { get; set; }
            public string Message { get; set; }
            public int TotalCopyTaskCount { get; set; }
            public TimeSpan ElapsedCopyDuration { get; set; }
            public int RemainingCopyTaskCount { get; set; }
            public int TotalConversionTaskCount { get; set; }
            public TimeSpan ElapsedConversionDuration { get; set; }
            public int RemainingConversionTaskCount { get; set; }

            public ExportProgressEventArgs(ExportProgressType type,
                                           string message,
                                           int totalCopyTaskCount,
                                           TimeSpan elapsedCopyDuration,
                                           int remainingCopyTaskCount,
                                           int totalConversionTaskCount,
                                           TimeSpan elapsedConversionDuration,
                                           int remainingConversionTaskCount)
            {
                this.Type = type;
                this.Message = message;
                this.TotalCopyTaskCount = totalCopyTaskCount;
                this.ElapsedCopyDuration = elapsedCopyDuration;
                this.RemainingCopyTaskCount = remainingCopyTaskCount;
                this.TotalConversionTaskCount = totalConversionTaskCount;
                this.ElapsedConversionDuration = elapsedConversionDuration;
                this.RemainingConversionTaskCount = remainingConversionTaskCount;
            }
        }
        #endregion

        #region Délégués
        public delegate void ExportProgressHandler(ExportProgressEventArgs e);
        #endregion

        #region Événements
        public event ExportProgressHandler ExportProgress;
        #endregion

        #region Constantes
        private readonly IEnumerable<string> _musicFileExtension = new string[] { ".flac", ".mp3" };
        private const string DESTINATION_EXTENSION = ".mp3";
        #endregion

        #region Propriétés
        public string DestinationPath { get; set; }

        public string TempPath { get; set; }
        #endregion

        #region Membres
        private volatile bool _isConvertInProgress;

        private ConcurrentQueue<FileExportTask> _copyTasks = new ConcurrentQueue<FileExportTask>();

        private int _totalConversionTaskCount;

        private int _remainingConversionTaskCount;

        private volatile int _totalCopyTaskCount;

        private volatile int _remainingCopyTaskCount;

        private TimeSpan _elapsedCopyDuration;

        private TimeSpan _elapsedConvertionDuration;

        private static Logger _logger = LogManager.GetCurrentClassLogger();
        #endregion

        public MusicFoldersExporter(string destinationPath, string tempPath)
        {
            this.DestinationPath = destinationPath;
            this.TempPath = tempPath;
        }

        private void RaiseExportProgress(ExportProgressType type, string message)
        {
            this.ExportProgress(new ExportProgressEventArgs(type,
                                                            message,
                                                            this._totalCopyTaskCount,
                                                            this._elapsedCopyDuration,
                                                            this._remainingCopyTaskCount,
                                                            this._totalConversionTaskCount,
                                                            this._elapsedConvertionDuration,
                                                            this._remainingConversionTaskCount));
        }

        private void ProcessCopyTasks()
        {
            Task.Run(() =>
            {
                var stopWatchCopy = new Stopwatch();
                FileExportTask task = null;

                while (
                        (this._copyTasks.TryDequeue(out task)) ||
                        (this._isConvertInProgress)
                      )
                {
                    if (task != null)
                    {
                        stopWatchCopy.Start();
                        string message = $"Copie de {task.DestinationPath}...";
                        this.RaiseExportProgress(ExportProgressType.CopyBegin, message);
                        _logger.Info(message);

                        if (string.IsNullOrEmpty(task.ConvertedPath))
                        {
                            File.Copy(task.SourcePath, task.DestinationPath);
                        }
                        else
                        {
                            File.Copy(task.ConvertedPath, task.DestinationPath, true);

                            try
                            {
                                File.Delete(task.ConvertedPath);
                            }
                            catch (Exception e)
                            {
                                _logger.Error(e);
                            }
                        }

                        this._remainingCopyTaskCount--;
                        message = $"Copie de {task.DestinationPath} terminée";

                        //stopWatchCopy.Stop();
                        this._elapsedCopyDuration = stopWatchCopy.Elapsed;

                        this.RaiseExportProgress(ExportProgressType.CopyEnd, message);
                        _logger.Info(message);
                    }

                    Task.Delay(1000).Wait();
                }

                this.RaiseExportProgress(ExportProgressType.End, "FIN !");
            });
        }

        public void Reset()
        {
            this._totalConversionTaskCount = 0;
            this._totalCopyTaskCount = 0;
            this._elapsedConvertionDuration = TimeSpan.Zero;
            this._elapsedCopyDuration = TimeSpan.Zero;
        }

        public void Export(IEnumerable<MusicFolder> musicFolders)
        {
            Task.Run(() =>
            {
                var exportTasks = new List<FileExportTask>();
                var convertTasks = new List<FileExportTask>();

                this.Reset();

                // On commence par supprimer de la sortie des dossiers ne correspondant pas à la sélection.
                var diDestination = new DirectoryInfo(this.DestinationPath);
                var dossiersAExporter = musicFolders.Select(mf => mf.DestinationFolderName);

                foreach (var folder in diDestination.GetDirectories())
                {
                    if (!dossiersAExporter.Contains(folder.Name))
                    {
                        try
                        {
                            string message = $"Suppression de {folder.Name}";
                            this.RaiseExportProgress(ExportProgressType.DeleteBegin, message);

                            folder.Delete(true);

                            message = $"Fin de suppression de {folder.Name}";
                            this.RaiseExportProgress(ExportProgressType.DeleteEnd, message);
                        }
                        catch (IOException ioe)
                        {
                            _logger.Error(ioe);
                        }
                    }
                }

                foreach (var musicFolder in musicFolders)
                {
                    // Si le dossier existe déjà, on saute le traitement
                    if (diDestination.GetDirectories(musicFolder.DestinationFolderName, SearchOption.TopDirectoryOnly).Any())
                    {
                        continue;
                    }

                    foreach (var filePath in Directory.GetFiles(musicFolder.Path))
                    {
                        var fi = new FileInfo(filePath);
                        if (_musicFileExtension.Contains(fi.Extension.ToLower()))
                        {
                            var outFolderName = Path.Combine(this.DestinationPath, musicFolder.DestinationFolderName);

                            if (Directory.Exists(outFolderName))
                            {
                                try
                                {
                                    Directory.Delete(outFolderName, true);
                                }
                                catch (Exception e)
                                {
                                    _logger.Error(e);
                                }
                            }

                            try
                            {
                                Directory.CreateDirectory(outFolderName);
                            }
                            catch (Exception e)
                            {
                                _logger.Error(e);
                            }

                            var fileName = fi.Extension.ToLower() == DESTINATION_EXTENSION.ToLower() ? fi.Name
                                                                                                     : fi.Name.Substring(0, fi.Name.Length - fi.Extension.Length) + DESTINATION_EXTENSION;

                            var destination = Path.Combine(outFolderName, fileName);

                            var exportTask = new FileExportTask(fi.FullName, destination);

                            this._totalCopyTaskCount++;
                            this._remainingCopyTaskCount++;

                            if (exportTask.IsConvertionNeeded)
                            {
                                this._totalConversionTaskCount++;
                                this._remainingConversionTaskCount++;
                                convertTasks.Add(exportTask);
                            }
                            else
                            {
                                this._copyTasks.Enqueue(exportTask);
                            }
                        }
                    }
                }

                this._isConvertInProgress = true;

                this.ProcessCopyTasks();

                //                this._exportTasks = exportTasks;
                ProcessConvertTasks(convertTasks);
            });
        }

        /// <summary>
        /// Démarre le processus de conversion.
        /// </summary>
        /// <param name="convertTasks">Les conversions à réaliser.</param>
        private void ProcessConvertTasks(List<FileExportTask> convertTasks)
        {
            string message;

            var stopWatchConvert = new Stopwatch();
            Parallel.ForEach(convertTasks, fileToConvert =>
            {
                stopWatchConvert.Start();

                var fi = new FileInfo(fileToConvert.SourcePath);
                var tempPath = Path.Combine(this.TempPath, Path.GetTempFileName());

                try
                {
                    message = $"Conversion de {fileToConvert.SourcePath}";
                    this.RaiseExportProgress(ExportProgressType.ConvertBegin, message);

                    using (var reader = new FlacReader(fi.FullName))
                    {
                        using (WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(reader))
                        {
                            using (var mp3Writer = new LameMP3FileWriter(tempPath, pcmStream.WaveFormat, LAMEPreset.VBR_90))
                            {
                                reader.CopyTo(mp3Writer);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    message = $"Une erreur s'est produite lors de la conversion de {fileToConvert.SourcePath}";
                    this.RaiseExportProgress(ExportProgressType.Error, message);
                    this.RaiseExportProgress(ExportProgressType.Error, e.Message);
                    _logger.Error(message);
                }

                fileToConvert.ConvertedPath = tempPath;

                this._remainingConversionTaskCount--;

                message = $"Conversion de {fileToConvert.SourcePath} terminée.";
                this._copyTasks.Enqueue(fileToConvert);

                stopWatchConvert.Stop();
                _elapsedConvertionDuration = stopWatchConvert.Elapsed;

                this.RaiseExportProgress(ExportProgressType.ConvertEnd, message);
                _logger.Info(message);
            });

            this._isConvertInProgress = false;
        }
    }
}