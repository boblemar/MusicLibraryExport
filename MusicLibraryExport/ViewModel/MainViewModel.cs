using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MusicLibraryExport.Model;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace MusicLibraryExport.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        #region Constantes
        private const string FOLDER_LIST_FILE_NAME = "folderList.xml";
        #endregion

        #region Membres
        private bool _isListBuildingInProgress = false;
        private bool _isExportInProgress = false;

        private readonly IEnumerable<string> _formatFolderName      = new string[] { "FLAC", "MP3" };
        private readonly IEnumerable<string> _musicFileExtension    = new string[] { ".flac", ".mp3" };

        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private MusicFoldersExporter _exporter;

        private string _progressLog;
        private string _progressCopyMessage;
        private string _progressConvertMessage;
        private TimeSpan _elapsedCopyDuration;
        private TimeSpan _elapsedConvertDuration;

        private int _conversionTasksTotalCount;
        private int _conversionTasksDoneCount;
        private volatile int _copyTasksTotalCount;
        private volatile int _copyTasksDoneCount;

        #endregion

        #region Commandes
        public RelayCommand CommandListUpdate
        {
            get => new RelayCommand(UpdateList, () => this.ListUpdateButtonVisibility == Visibility.Visible);
        }

        public RelayCommand CommandExport
        {
            get => new RelayCommand(() => {
                                            this._exporter.Reset();
                                            this.IsExportInProgress = true;
                                            this._exporter.Export(this.MusicFolders.Where(mf => mf.EstSelectionne));
                                          });
        }
        #endregion

        #region Propriétés
        public ObservableCollection<MusicFolder> MusicFolders { get; set; }

        public int CopyTasksTotalCount
        {
            get { return _copyTasksTotalCount; }
            set
            {
                _copyTasksTotalCount = value;
                RaisePropertyChanged();
            }
        }

        public int CopyTasksDoneCount
        {
            get { return _copyTasksDoneCount; }
            set
            {
                _copyTasksDoneCount = value;
                RaisePropertyChanged();
            }
        }

        public TimeSpan CopyElapsedDuration
        {
            get { return _elapsedCopyDuration; }
            set
            {
                _elapsedCopyDuration = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(this.CopyRemainingDuration));
            }
        }

        public TimeSpan CopyRemainingDuration
        {
            get
            {
                return this.CopyTasksDoneCount == 0 ? TimeSpan.Zero
                                                    : TimeSpan.FromTicks(this.CopyElapsedDuration.Ticks * this.CopyTasksTotalCount / this.CopyTasksDoneCount - this.CopyElapsedDuration.Ticks);
            }
        }

        public int ConversionTasksTotalCount
        {
            get { return _conversionTasksTotalCount; }
            set
            {
                _conversionTasksTotalCount = value;
                RaisePropertyChanged();
            }
        }

        public int ConversionTasksDoneCount
        {
            get { return _conversionTasksDoneCount; }
            set
            {
                _conversionTasksDoneCount = value;
                RaisePropertyChanged();
            }
        }

        public TimeSpan ConvertElapsedDuration
        {
            get { return _elapsedConvertDuration; }
            set
            {
                _elapsedConvertDuration = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(this.ConvertRemainingDuration));
            }
        }

        public TimeSpan ConvertRemainingDuration
        {
            get
            {
                return this.ConversionTasksDoneCount == 0 ? TimeSpan.Zero
                                                          : TimeSpan.FromTicks(this.ConvertElapsedDuration.Ticks * this.ConversionTasksTotalCount / this.ConversionTasksDoneCount - this.ConvertElapsedDuration.Ticks);
            }
        }

        public string ProgressLog
        {
            get { return _progressLog; }
            set
            {
                _progressLog = value;
                RaisePropertyChanged();
            }
        }

        public string ProgressCopyMessage
        {
            get { return _progressCopyMessage; }
            set
            {
                _progressCopyMessage = value;
                RaisePropertyChanged();
            }
        }

        public string ProgressConvertMessage
        {
            get { return _progressConvertMessage; }
            set
            {
                _progressConvertMessage = value;
                RaisePropertyChanged();
            }
        }

        public bool IsListBuildingInProgress
        {
            get { return this._isListBuildingInProgress; }
            set
            {
                this._isListBuildingInProgress = value;
                RaisePropertyChanged();
            }
        }
        
        public bool IsExportInProgress
        {
            get { return this._isExportInProgress; }
            set
            {
                this._isExportInProgress = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(MusicFolderVisibility));
                RaisePropertyChanged(nameof(this.ExportButtonVisibility));
                RaisePropertyChanged(nameof(this.ListUpdateButtonVisibility));
                RaisePropertyChanged(nameof(this.ProgressPanelVisibility));
            }
        }

        public Visibility ProgressPanelVisibility
        {
            get => this.IsExportInProgress ? Visibility.Visible
                                           : Visibility.Hidden;
        }
        
        public Visibility MusicFolderVisibility
        {
            get => this.IsExportInProgress ? Visibility.Hidden
                                           : Visibility.Visible;
        }

        public Visibility ExportButtonVisibility
        {
            get
            {
                return this._isExportInProgress ||
                       this._isListBuildingInProgress ||
                       this.MusicFolders == null ||
                       !this.MusicFolders.Any() ? Visibility.Hidden
                                                : Visibility.Visible;
            }
        }

        public Visibility ListUpdateButtonVisibility
        {
            get
            {
                return this._isExportInProgress ||
                       this._isListBuildingInProgress ? Visibility.Hidden
                                                      : Visibility.Visible;
            }
        }

        public string FolderListFilePath
        {
            get
            {
                string directory = Path.Combine(Path.GetTempPath(), Assembly.GetExecutingAssembly().GetName().Name);
                
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                return Path.Combine(directory, FOLDER_LIST_FILE_NAME);
            }
        }
        #endregion

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            ////if (IsInDesignMode)
            ////{
            ////    // Code runs in Blend --> create design time data.
            ////}
            ////else
            ////{
            ////    // Code runs "for real"
            ////}

            this.LoadFolderList();

            this._exporter = new MusicFoldersExporter(Properties.Settings.Default.PublicationPath, @"c:\Temp\MusicLibraryExport");
            this._exporter.ExportProgress += e =>
                                                {
                                                    this.ProgressLog += $"{e.Message}\r\n";
                                                    this.CopyTasksTotalCount = e.TotalCopyTaskCount;
                                                    this.CopyTasksDoneCount = e.TotalCopyTaskCount - e.RemainingCopyTaskCount;
                                                    this.ConversionTasksTotalCount = e.TotalConversionTaskCount;
                                                    this.ConversionTasksDoneCount = e.TotalConversionTaskCount - e.RemainingConversionTaskCount;
                                                    this.CopyElapsedDuration = e.ElapsedCopyDuration;
                                                    this.ConvertElapsedDuration = e.ElapsedConversionDuration;

                                                    switch (e.Type)
                                                    {
                                                        case MusicFoldersExporter.ExportProgressType.End:
                                                            this.IsExportInProgress = false;
                                                            RaisePropertyChanged(nameof(this.ExportButtonVisibility));
                                                            RaisePropertyChanged(nameof(this.ListUpdateButtonVisibility));
                                                            break;
                                                        case MusicFoldersExporter.ExportProgressType.ConvertBegin:
                                                        case MusicFoldersExporter.ExportProgressType.ConvertEnd:
                                                            this.ProgressConvertMessage = e.Message;
                                                            break;
                                                        case MusicFoldersExporter.ExportProgressType.CopyBegin:
                                                        case MusicFoldersExporter.ExportProgressType.CopyEnd:
                                                            this.ProgressCopyMessage = e.Message;
                                                            break;
                                                    }
                                                };
        }

        public void UpdateList()
        {
            this.IsListBuildingInProgress = true;
            
            if (!Directory.Exists(Properties.Settings.Default.MusicLibraryPath))
            {
                MessageBox.Show(string.Format(Properties.Resources.ERROR_INCORRECT_FOLDER, Properties.Settings.Default.MusicLibraryPath));
            }
            else
            {
                var folderList = this.ContruireListe(Properties.Settings.Default.MusicLibraryPath, Properties.Settings.Default.MusicLibraryPath);

                var existingList = this.MusicFolders.Select(f => f.Path).ToList();
                var updatedList = folderList.Select(f => f.Path).ToList();

                // éléments à supprimer
                var foldersToRemove = existingList.Except(updatedList);
                for (var i = this.MusicFolders.Count - 1; i >= 0 ; i--)
                {
                    if (foldersToRemove.Contains(this.MusicFolders[i].Path))
                    {
                        this.MusicFolders.RemoveAt(i);
                    }
                }

                // éléments à ajouter
                var foldersToAdd = updatedList.Except(existingList);
                foreach (var folderToAdd in foldersToAdd)
                {
                    this.MusicFolders.Add(MusicFolder.BuildFromPath(Properties.Settings.Default.MusicLibraryPath, folderToAdd));
                }
            }

            RaisePropertyChanged(nameof(MusicFolders));

            this.IsListBuildingInProgress = false;
            RaisePropertyChanged(nameof(ExportButtonVisibility));

            this.SaveFolderList();
        }

        private IEnumerable<MusicFolder> ContruireListe(string pathRoot, string path)
        {
            var di = new DirectoryInfo(path);

            foreach (var musicFolder in this.BuildFolderList(pathRoot, di))
            {
                yield return musicFolder;
            }
        }

        private IEnumerable<MusicFolder> BuildFolderList(string pathRoot, DirectoryInfo di)
        {
            if (di.GetFiles().Any(fi => this._musicFileExtension.Contains(fi.Extension)))
            {
                MusicFolder musicFolder = null;
                try
                {
                    musicFolder = MusicFolder.BuildFromPath(pathRoot, di.FullName);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }

                if (musicFolder != null)
                {
                    yield return musicFolder;
                }
            }

            foreach (var subFolder in di.GetDirectories())
            {
                foreach (var musicFolder in this.BuildFolderList(pathRoot, subFolder))
                {
                    yield return musicFolder;
                }
            }
        }

        /// <summary>
        /// Enregistre la liste des dossiers dans le répertoire temporaire de l'application.
        /// </summary>
        private void SaveFolderList()
        {
            var serializer = new XmlSerializer(typeof(List<MusicFolder>));

            using (var sw = new StreamWriter(this.FolderListFilePath))
            {
                using (var xmlWriter = XmlWriter.Create(sw))
                {
                    serializer.Serialize(xmlWriter, this.MusicFolders.ToList());
                }
            }
        }

        /// <summary>
        /// Charge la liste des dossiers depuis le répertoire temporaire de l'application.
        /// </summary>
        private void LoadFolderList()
        {
            var serializer = new XmlSerializer(typeof(List<MusicFolder>));

            if (File.Exists(this.FolderListFilePath))
            {
                using (var sr = new StreamReader(this.FolderListFilePath))
                {
                    using (var xmlReader = XmlReader.Create(sr))
                    {
                        this.MusicFolders = new ObservableCollection<MusicFolder>(((IEnumerable<MusicFolder>)serializer.Deserialize(xmlReader)).OrderBy(f => f.Artist)
                                                                                                                                               .ThenBy(f => f.Record));
                    }
                }
            }
            else
            {
                this.MusicFolders = new ObservableCollection<MusicFolder>();
            }
        }
    }
}