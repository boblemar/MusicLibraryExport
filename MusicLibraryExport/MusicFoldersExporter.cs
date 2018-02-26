using MusicLibraryExport.Model;
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
                this.RemainingConversionTaskCount = remainingCopyTaskCount;
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

        private int _conversionTimeout = 3;

        private int _conversionMaxTry = 3;

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
                        (this._isConvertInProgress) ||
                        (this._copyTasks.TryDequeue(out task))
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
                string message;

                var exportTasks = new List<FileExportTask>();
                var convertTasks = new List<FileExportTask>();

                this.Reset();

                foreach (var musicFolder in musicFolders)
                {
                    foreach (var filePath in Directory.GetFiles(musicFolder.Path))
                    {
                        var fi = new FileInfo(filePath);
                        if (_musicFileExtension.Contains(fi.Extension.ToLower()))
                        {
                            var outFolderName = $"{musicFolder.Artist}-{musicFolder.Record}";
                            if (!string.IsNullOrEmpty(musicFolder.Volume))
                            {
                                outFolderName += $"-{musicFolder.Volume}";
                            }

                            outFolderName = Path.Combine(this.DestinationPath, outFolderName);

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

                var stopWatchConvert = new Stopwatch();
                foreach (var fileToConvert in convertTasks)
                {
                    stopWatchConvert.Start();

                    var fi = new FileInfo(fileToConvert.SourcePath);
                    var tempPath = Path.Combine(this.TempPath, Path.GetTempFileName());

                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.FileName = @"c:\Outils\ffmpeg-3.2.2-win64-static\bin\ffmpeg.exe";
                    processStartInfo.RedirectStandardError = false;
                    processStartInfo.RedirectStandardOutput = false;
                    processStartInfo.UseShellExecute = true;
                    processStartInfo.CreateNoWindow = false;
                    processStartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                    processStartInfo.Arguments = $"-i \"{fi.FullName}\" -ab 192k -id3v2_version 3 -y -f mp3 \"{tempPath}\"";

                    var process = new Process();

                    int nCount = 1;
                    bool isSuccess = false;
                    string erreur = string.Empty;

                    for (nCount = 1; nCount <= this._conversionMaxTry; nCount++)
                    {
                        message = $"Conversion de {fileToConvert.SourcePath}";
                        if (nCount > 1)
                        {
                            message += $"[Essai {nCount}]";
                        }
                        message += "..";

                        this.RaiseExportProgress(ExportProgressType.ConvertBegin, message);
                        _logger.Info(message);

                        process.StartInfo = processStartInfo;
                        process.Start();
                        

                        if (process.WaitForExit(this._conversionTimeout * 60000))
                        {
                            isSuccess = true;
                            break;
                        }

                        process.Kill();

                    }

                    if (!isSuccess)
                    {
                        message = $"Une erreur s'est produite lors de la conversion de {fileToConvert.SourcePath}";
                        this.RaiseExportProgress(ExportProgressType.Error, message);
                        this.RaiseExportProgress(ExportProgressType.Error, erreur);
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
                }

                this._isConvertInProgress = false;
            });
        }
    }
}