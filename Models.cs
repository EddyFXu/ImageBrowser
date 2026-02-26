using System;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace ImageBrowser
{
    public enum ItemType { Drive, Folder, Image, Unknown }

    public class FileSystemItem : ViewModelBase
    {
        public string FullPath { get; set; }
        public string Name { get; set; }
        public ItemType Type { get; set; }
        public long Size { get; set; }
        public DateTime CreationTime { get; set; }
        public ImageSource Icon { get; set; }
        
        private string _resolution;
        public string Resolution
        {
            get { return _resolution; }
            set { _resolution = value; OnPropertyChanged("Resolution"); }
        }

        // For sorting: pixels count (Width * Height)
        public long TotalPixels { get; set; }

        private bool _isExpanded;
        public bool IsExpanded 
        { 
            get { return _isExpanded; }
            set 
            { 
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                    if (_isExpanded && LoadChildrenAction != null)
                        LoadChildrenAction(this);
                }
            }
        }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set 
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        public Action<FileSystemItem> LoadChildrenAction { get; set; }

        private ObservableCollection<FileSystemItem> _children;
        public ObservableCollection<FileSystemItem> Children
        {
            get { return _children; }
            set { _children = value; OnPropertyChanged("Children"); }
        }

        public FileSystemItem()
        {
            Children = new ObservableCollection<FileSystemItem>();
            // Dummy item for expander
            Children.Add(new FileSystemItem(true) { Name = "加载中..." });
        }
        
        public FileSystemItem(bool noDummy)
        {
            Children = new ObservableCollection<FileSystemItem>();
        }
    }
    
    public class SlideSettings : ViewModelBase
    {
        private int _intervalSeconds = 3;
        public int IntervalSeconds
        {
            get { return _intervalSeconds; }
            set { _intervalSeconds = value; OnPropertyChanged("IntervalSeconds"); }
        }

        private bool _isLoop = true;
        public bool IsLoop
        {
            get { return _isLoop; }
            set { _isLoop = value; OnPropertyChanged("IsLoop"); }
        }

        private bool _isRandom = false;
        public bool IsRandom
        {
            get { return _isRandom; }
            set 
            { 
                _isRandom = value; 
                OnPropertyChanged("IsRandom"); 
                OnPropertyChanged("IsSequential");
            }
        }
        
        public bool IsSequential
        {
            get { return !IsRandom; }
            set 
            { 
                if (value) IsRandom = false;
                else IsRandom = true;
                OnPropertyChanged("IsSequential");
            }
        }
    }

    
    public class ImageInfo : ViewModelBase
    {
        private string _filePath;
        public string FilePath { get { return _filePath; } set { _filePath = value; OnPropertyChanged("FilePath"); } }
        
        private string _resolution;
        public string Resolution { get { return _resolution; } set { _resolution = value; OnPropertyChanged("Resolution"); } }
        
        private string _fileSize;
        public string FileSize { get { return _fileSize; } set { _fileSize = value; OnPropertyChanged("FileSize"); } }
        
        private string _format;
        public string Format { get { return _format; } set { _format = value; OnPropertyChanged("Format"); } }
        
        private string _cameraModel;
        public string CameraModel { get { return _cameraModel; } set { _cameraModel = value; OnPropertyChanged("CameraModel"); } }
        
        private string _exposure;
        public string Exposure { get { return _exposure; } set { _exposure = value; OnPropertyChanged("Exposure"); } }
        
        private DateTime _shootTime;
        public DateTime ShootTime { get { return _shootTime; } set { _shootTime = value; OnPropertyChanged("ShootTime"); } }
    }
}
