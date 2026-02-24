using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace ImageBrowser
{
    public class AppSettings
    {
        public string LastPath { get; set; }
        public Theme CurrentTheme { get; set; }
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public WindowState WindowState { get; set; }
        
        // Slide Settings Persistence
        public int SlideInterval { get; set; }
        public bool SlideIsRandom { get; set; }
        public bool SlideIsLoop { get; set; }
        
        public AppSettings()
        {
            CurrentTheme = Theme.Mei;
            WindowWidth = 1024;
            WindowHeight = 768;
            WindowState = WindowState.Normal;
            
            SlideInterval = 3;
            SlideIsRandom = false;
            SlideIsLoop = true;
        }
    }

    public enum DisplayMode { Browse, View, Slide }

    public class MainViewModel : ViewModelBase
    {
        private DisplayMode _currentMode;
        public DisplayMode CurrentMode
        {
            get { return _currentMode; }
            set 
            { 
                if (_currentMode != value)
                {
                    _currentMode = value; 
                    OnPropertyChanged("CurrentMode");
                    OnPropertyChanged("IsBrowseMode");
                    OnPropertyChanged("IsViewMode");
                    OnPropertyChanged("IsSlideMode");
                    OnPropertyChanged("CurrentZoom");
                }
            }
        }
        
        public bool IsBrowseMode { get { return CurrentMode == DisplayMode.Browse; } }
        public bool IsViewMode { get { return CurrentMode == DisplayMode.View; } }
        public bool IsSlideMode { get { return CurrentMode == DisplayMode.Slide; } }

        public ObservableCollection<FileSystemItem> Drives { get; set; }
        
        private ObservableCollection<FileSystemItem> _currentFiles;
        public ObservableCollection<FileSystemItem> CurrentFiles
        {
            get { return _currentFiles; }
            set { _currentFiles = value; OnPropertyChanged("CurrentFiles"); }
        }

        private FileSystemItem _selectedTreeItem;
        public FileSystemItem SelectedTreeItem
        {
            get { return _selectedTreeItem; }
            set 
            { 
                _selectedTreeItem = value; 
                OnPropertyChanged("SelectedTreeItem");
                if (value != null) LoadFiles(value.FullPath);
            }
        }

        private FileSystemItem _selectedFile;
        public FileSystemItem SelectedFile
        {
            get { return _selectedFile; }
            set 
            { 
                _selectedFile = value; 
                OnPropertyChanged("SelectedFile");
                if (value != null && value.Type == ItemType.Image)
                {
                    ImageScale = 0.0; // Signal View to Auto-Fit
                    LoadImageInfo(value);
                    SyncTree(value.FullPath);
                }
            }
        }

        public void SyncTree(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir)) return;

            // Find Drive
            foreach (var drive in Drives)
            {
                if (dir.StartsWith(drive.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    ExpandAndSelect(drive, dir);
                    break;
                }
            }
        }

        private void ExpandAndSelect(FileSystemItem item, string targetPath)
        {
            item.IsExpanded = true; // Triggers LoadChildren
            
            if (string.Equals(item.FullPath.TrimEnd('\\'), targetPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                item.IsSelected = true;
                // Ensure Visible? View handles this usually.
                return;
            }

            // Find next child
            foreach (var child in item.Children)
            {
                if (child.Type == ItemType.Folder && 
                    (targetPath.Equals(child.FullPath, StringComparison.OrdinalIgnoreCase) || 
                     targetPath.StartsWith(child.FullPath + "\\", StringComparison.OrdinalIgnoreCase)))
                {
                    ExpandAndSelect(child, targetPath);
                    return;
                }
            }
        }

        private ImageInfo _currentImageInfo;
        public ImageInfo CurrentImageInfo
        {
            get { return _currentImageInfo; }
            set { _currentImageInfo = value; OnPropertyChanged("CurrentImageInfo"); }
        }

        public RelayCommand SwitchToBrowseCommand { get; set; }
        public RelayCommand SwitchToViewCommand { get; set; }
        public RelayCommand SwitchToSlideCommand { get; set; }
        public RelayCommand ToggleSkinCommand { get; set; }
        public RelayCommand ToggleSettingsCommand { get; set; }
        
        private DispatcherTimer _slideTimer;
        public bool IsPlaying { get; private set; }
        public SlideSettings SlideSettings { get; set; }
        
        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get { return _isSettingsOpen; }
            set { _isSettingsOpen = value; OnPropertyChanged("IsSettingsOpen"); }
        }

        private bool _isFullScreen;
        public bool IsFullScreen
        {
            get { return _isFullScreen; }
            set { _isFullScreen = value; OnPropertyChanged("IsFullScreen"); }
        }

        private double _thumbnailSize = 20.0;
        public double ThumbnailSize
        {
            get { return _thumbnailSize; }
            set
            {
                if (_thumbnailSize != value)
                {
                    bool wasThumbnailView = IsThumbnailView;
                    _thumbnailSize = value;
                    OnPropertyChanged("ThumbnailSize");
                    
                    if (wasThumbnailView != IsThumbnailView)
                    {
                        OnPropertyChanged("IsThumbnailView");
                    }
                    if (IsBrowseMode) OnPropertyChanged("CurrentZoom");
                }
            }
        }
        
        private double _imageScale = 100.0;
        public double ImageScale
        {
            get { return _imageScale; }
            set
            {
                if (_imageScale != value)
                {
                    _imageScale = value;
                    OnPropertyChanged("ImageScale");
                    if (IsViewMode || IsSlideMode) OnPropertyChanged("CurrentZoom");
                }
            }
        }

        public double CurrentZoom
        {
            get 
            { 
                if (IsBrowseMode) return ThumbnailSize;
                return ImageScale;
            }
            set
            {
                if (IsBrowseMode) ThumbnailSize = value;
                else ImageScale = value;
                OnPropertyChanged("CurrentZoom");
            }
        }
        
        public bool IsThumbnailView { get { return ThumbnailSize > 50; } }

        public RelayCommand ToggleFullScreenCommand { get; set; }
        public RelayCommand ScrollLeftCommand { get; set; }
        public RelayCommand ScrollRightCommand { get; set; }
        public RelayCommand NextImageCommand { get; set; }
        public RelayCommand PrevImageCommand { get; set; }

        private string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImageBrowser", "settings.xml");
        public AppSettings Settings { get; set; }

        public MainViewModel()
        {
            CurrentMode = DisplayMode.Browse;
            Drives = new ObservableCollection<FileSystemItem>();
            CurrentFiles = new ObservableCollection<FileSystemItem>();
            SlideSettings = new SlideSettings();
            
            LoadDrives();
            
            SwitchToBrowseCommand = new RelayCommand(o => { CurrentMode = DisplayMode.Browse; StopSlideShow(); });
            SwitchToViewCommand = new RelayCommand(o => { CurrentMode = DisplayMode.View; StopSlideShow(); });
            SwitchToSlideCommand = new RelayCommand(o => StartSlideShow());
            ToggleSkinCommand = new RelayCommand(o => SwitchSkin());
            ToggleSettingsCommand = new RelayCommand(o => IsSettingsOpen = !IsSettingsOpen);
            ToggleFullScreenCommand = new RelayCommand(o => IsFullScreen = !IsFullScreen);
            NextImageCommand = new RelayCommand(o => NextImage());
            PrevImageCommand = new RelayCommand(o => PrevImage());
            
            _slideTimer = new DispatcherTimer();
            
            // Load Settings
            LoadSettings();
            
            // Apply Slide Settings from Persistence
            SlideSettings.IntervalSeconds = Settings.SlideInterval;
            SlideSettings.IsRandom = Settings.SlideIsRandom;
            SlideSettings.IsLoop = Settings.SlideIsLoop;
            
            _slideTimer.Interval = TimeSpan.FromSeconds(SlideSettings.IntervalSeconds);
            _slideTimer.Tick += SlideTimer_Tick;

            // Listen for changes to persist
            SlideSettings.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == "IntervalSeconds")
                {
                    _slideTimer.Interval = TimeSpan.FromSeconds(SlideSettings.IntervalSeconds);
                    Settings.SlideInterval = SlideSettings.IntervalSeconds;
                    SaveSettings();
                }
                else if (e.PropertyName == "IsRandom")
                {
                    Settings.SlideIsRandom = SlideSettings.IsRandom;
                    SaveSettings();
                }
                else if (e.PropertyName == "IsLoop")
                {
                    Settings.SlideIsLoop = SlideSettings.IsLoop;
                    SaveSettings();
                }
            };

            // Apply Saved Theme
            _currentTheme = Settings.CurrentTheme;
            ThemeManager.ApplyTheme(_currentTheme);

            // Load last directory
            string lastPath = Settings.LastPath;
            if (!string.IsNullOrEmpty(lastPath) && Directory.Exists(lastPath))
            {
                LoadFiles(lastPath);
            }
            else
            {
                string myPics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (Directory.Exists(myPics))
                {
                    LoadFiles(myPics);
                }
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                    using (StreamReader reader = new StreamReader(_settingsPath))
                    {
                        Settings = (AppSettings)serializer.Deserialize(reader);
                    }
                }
            }
            catch { }
            
            if (Settings == null) Settings = new AppSettings();
        }

        public void SaveSettings()
        {
            try
            {
                string dir = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                
                // Update Theme in Settings before saving (Window size is updated by MainWindow)
                Settings.CurrentTheme = _currentTheme;
                
                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                using (StreamWriter writer = new StreamWriter(_settingsPath))
                {
                    serializer.Serialize(writer, Settings);
                }
            }
            catch { }
        }

        // Removed old LoadLastPath/SaveLastPath methods



        private void LoadDrives()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    var item = new FileSystemItem 
                    { 
                        Name = drive.Name, 
                        FullPath = drive.Name, 
                        Type = ItemType.Drive,
                        LoadChildrenAction = this.LoadFolderChildren
                    };
                    Drives.Add(item);
                }
            }
        }

        public void LoadFolderChildren(FileSystemItem item)
        {
            try
            {
                item.Children.Clear();
                var dirInfo = new DirectoryInfo(item.FullPath);
                foreach (var dir in dirInfo.GetDirectories())
                {
                    if ((dir.Attributes & FileAttributes.Hidden) == 0)
                    {
                        item.Children.Add(new FileSystemItem
                        {
                            Name = dir.Name,
                            FullPath = dir.FullName,
                            Type = ItemType.Folder,
                            LoadChildrenAction = this.LoadFolderChildren
                        });
                    }
                }
            }
            catch { }
        }

        private void LoadFiles(string path)
        {
            CurrentFiles.Clear();
            Settings.LastPath = path; // Update Settings
            SaveSettings(); // Save Settings
            try
            {
                var dirInfo = new DirectoryInfo(path);
                var allFiles = dirInfo.GetFiles();
                var allDirs = dirInfo.GetDirectories();
                
                var images = new List<FileSystemItem>();
                var others = new List<FileSystemItem>();
                
                // Collect images
                foreach (var file in allFiles)
                {
                    string ext = file.Extension.ToLower();
                    if (ext == ".jpg" || ext == ".png" || ext == ".bmp" || ext == ".jpeg")
                    {
                        images.Add(new FileSystemItem(true)
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            Type = ItemType.Image,
                            Size = file.Length,
                            CreationTime = file.CreationTime
                        });
                    }
                    else
                    {
                        others.Add(new FileSystemItem(true)
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            Type = ItemType.Unknown, // Or Generic File
                            Size = file.Length,
                            CreationTime = file.CreationTime
                        });
                    }
                }
                
                // If images exist, show only images (as per usual image browser behavior)
                // If NO images, show folders and other files
                if (images.Count > 0)
                {
                    foreach (var img in images) CurrentFiles.Add(img);
                }
                else
                {
                    // Add directories first
                    foreach (var dir in allDirs)
                    {
                        if ((dir.Attributes & FileAttributes.Hidden) == 0)
                        {
                            CurrentFiles.Add(new FileSystemItem
                            {
                                Name = dir.Name,
                                FullPath = dir.FullName,
                                Type = ItemType.Folder,
                                CreationTime = dir.CreationTime
                            });
                        }
                    }
                    // Add other files
                    foreach (var file in others) CurrentFiles.Add(file);
                }
            }
            catch { }
        }

        public void OpenPath(string path)
        {
            if (File.Exists(path))
            {
                // It's a file
                string dir = Path.GetDirectoryName(path);
                LoadFiles(dir);
                
                // Find the file in the list and select it
                var fileItem = CurrentFiles.FirstOrDefault(f => f.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (fileItem != null)
                {
                    SelectedFile = fileItem;
                    CurrentMode = DisplayMode.View;
                }
            }
            else if (Directory.Exists(path))
            {
                // It's a directory
                LoadFiles(path);
                CurrentMode = DisplayMode.Browse;
            }
        }

        private void LoadImageInfo(FileSystemItem item)
        {
            CurrentImageInfo = new ImageInfo
            {
                FilePath = item.FullPath,
                FileSize = (item.Size / 1024.0).ToString("F2") + " KB",
                Format = Path.GetExtension(item.FullPath),
                ShootTime = item.CreationTime
            };
            
            try
            {
                using (var stream = new FileStream(item.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Use IgnoreColorProfile for speed, None for immediate load (header)
                    var frame = BitmapFrame.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                    CurrentImageInfo.Resolution = frame.PixelWidth + " x " + frame.PixelHeight;
                }
            }
            catch { }
        }

        private void SwitchSkin()
        {
            // Cycle: Mei -> Lan -> Zhu -> Ju -> Mei
            Theme current = _currentTheme;
            Theme next = Theme.Mei;
            
            if (current == Theme.Mei) next = Theme.Lan;
            else if (current == Theme.Lan) next = Theme.Zhu;
            else if (current == Theme.Zhu) next = Theme.Ju;
            else if (current == Theme.Ju) next = Theme.Mei;
            
            _currentTheme = next;
            ThemeManager.ApplyTheme(next);
        }

        private Theme _currentTheme = Theme.Mei;
        
        public void StartSlideShow()
        {
            CurrentMode = DisplayMode.Slide;
            IsFullScreen = true;
            IsPlaying = true;
            _slideTimer.Start();
            // Start from first if none selected
            if (SelectedFile == null && CurrentFiles.Count > 0)
                SelectedFile = CurrentFiles[0];
        }
        
        public void StopSlideShow()
        {
            IsPlaying = false;
            _slideTimer.Stop();
            IsFullScreen = false;
        }

        public void ToggleSlideShow()
        {
            if (IsPlaying) StopSlideShow();
            else { IsPlaying = true; _slideTimer.Start(); }
        }
        
        private void SlideTimer_Tick(object sender, EventArgs e)
        {
            NextImage();
        }

        public void NextImage()
        {
            if (CurrentFiles.Count == 0) return;
            int index = CurrentFiles.IndexOf(SelectedFile);

            bool useRandom = IsSlideMode && SlideSettings.IsRandom;
            if (useRandom)
            {
                Random rnd = new Random();
                index = rnd.Next(0, CurrentFiles.Count);
            }
            else
            {
                if (index < 0) index = -1;
                index = index + 1;
                if (index >= CurrentFiles.Count)
                {
                    if (IsSlideMode && !SlideSettings.IsLoop) { StopSlideShow(); return; }
                    index = 0;
                }
            }

            SelectedFile = CurrentFiles[index];
        }

        public void PrevImage()
        {
            if (CurrentFiles.Count == 0) return;
            int index = CurrentFiles.IndexOf(SelectedFile);

            bool useRandom = IsSlideMode && SlideSettings.IsRandom;
            if (useRandom)
            {
                Random rnd = new Random();
                index = rnd.Next(0, CurrentFiles.Count);
            }
            else
            {
                if (index < 0) index = 0;
                else index = index - 1;
                if (index < 0)
                {
                    if (IsSlideMode && !SlideSettings.IsLoop) { StopSlideShow(); return; }
                    index = CurrentFiles.Count - 1;
                }
            }

            SelectedFile = CurrentFiles[index];
        }
    }
}
