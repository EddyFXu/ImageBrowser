using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Markup;
using System.Windows.Shell;
using System.Windows.Controls.Primitives; // Added for ScrollBar Track

namespace ImageBrowser
{
    public class MainWindow : Window
    {
        private MainViewModel _viewModel;
        
        private ListView _fileList;
        private ListBox _thumbList;
        private TreeView _treeView;
        private ScrollViewer _viewScrollViewer;
        private Image _viewImage;
        private bool _isCenteringThumb;
        private bool _isAutoFitMode = true;

        public MainWindow()
        {
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;
            
            // Load Custom ScrollBar Style
            try
            {
                string scrollStyle = @"
                    <Style TargetType='ScrollBar' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
                        <Setter Property='Background' Value='Transparent'/>
                        <Setter Property='Stylus.IsPressAndHoldEnabled' Value='false'/>
                        <Setter Property='Stylus.IsFlicksEnabled' Value='false'/>
                        <Setter Property='Foreground' Value='{DynamicResource Foreground}'/>
                        <Setter Property='Width' Value='10'/>
                        <Setter Property='MinWidth' Value='10'/>
                        <Setter Property='Template'>
                            <Setter.Value>
                                <ControlTemplate TargetType='ScrollBar'>
                                    <Grid x:Name='Bg' SnapsToDevicePixels='true'>
                                        <Track x:Name='PART_Track' IsDirectionReversed='true'>
                                            <Track.Thumb>
                                                <Thumb>
                                                    <Thumb.Template>
                                                        <ControlTemplate TargetType='Thumb'>
                                                            <Border x:Name='ThumbBorder' Background='{TemplateBinding Foreground}' Opacity='0.3' CornerRadius='4' Margin='2'/>
                                                            <ControlTemplate.Triggers>
                                                                <Trigger Property='IsMouseOver' Value='true'>
                                                                    <Setter TargetName='ThumbBorder' Property='Opacity' Value='0.6'/>
                                                                </Trigger>
                                                                <Trigger Property='IsDragging' Value='true'>
                                                                    <Setter TargetName='ThumbBorder' Property='Opacity' Value='0.8'/>
                                                                </Trigger>
                                                            </ControlTemplate.Triggers>
                                                        </ControlTemplate>
                                                    </Thumb.Template>
                                                </Thumb>
                                            </Track.Thumb>
                                        </Track>
                                    </Grid>
                                    <ControlTemplate.Triggers>
                                        <Trigger Property='Orientation' Value='Horizontal'>
                                            <Setter Property='Width' Value='Auto'/>
                                            <Setter Property='MinWidth' Value='0'/>
                                            <Setter Property='Height' Value='10'/>
                                            <Setter Property='MinHeight' Value='10'/>
                                            <Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/>
                                        </Trigger>
                                    </ControlTemplate.Triggers>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>";
                this.Resources.Add(typeof(ScrollBar), XamlReader.Parse(scrollStyle));
            }
            catch { }
            
            // Set up WindowChrome
            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 40;
            chrome.ResizeBorderThickness = new Thickness(5);
            chrome.GlassFrameThickness = new Thickness(1);
            chrome.CornerRadius = new CornerRadius(0);
            WindowChrome.SetWindowChrome(this, chrome);
            
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.CanResize;
            this.BorderThickness = new Thickness(1);
            this.SetResourceReference(Window.BorderBrushProperty, "BorderBrush");

            // Drag and Drop
            this.AllowDrop = true;
            this.Drop += MainWindow_Drop;

            // Hook up Scroll Commands (Must be done before InitializeComponent so buttons can bind to them)
            _viewModel.ScrollLeftCommand = new RelayCommand(o => {
                if (_thumbList != null && _thumbList.Items.Count > 0) 
                {
                    var scrollViewer = GetVisualChild<ScrollViewer>(_thumbList);
                    // Scroll 3 items
                    if (scrollViewer != null) scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - 3);
                }
            });
            
            _viewModel.ScrollRightCommand = new RelayCommand(o => {
                if (_thumbList != null && _thumbList.Items.Count > 0) 
                {
                    var scrollViewer = GetVisualChild<ScrollViewer>(_thumbList);
                    if (scrollViewer != null) scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + 3);
                }
            });

            // Global Key Bindings
            this.InputBindings.Add(new KeyBinding(_viewModel.DeleteCommand, Key.Delete, ModifierKeys.None));

            InitializeComponent();
            
            // Apply Settings
            if (_viewModel.Settings.WindowWidth > 0) this.Width = _viewModel.Settings.WindowWidth;
            if (_viewModel.Settings.WindowHeight > 0) this.Height = _viewModel.Settings.WindowHeight;
            this.WindowState = _viewModel.Settings.WindowState;
            
            this.SizeChanged += (s, e) => {
                if (_viewModel.IsViewMode || _viewModel.IsSlideMode)
                {
                    AutoFitImage();
                }
            };

            this.Closing += (s, e) => {
                if (this.WindowState == WindowState.Normal)
                {
                    _viewModel.Settings.WindowWidth = this.Width;
                    _viewModel.Settings.WindowHeight = this.Height;
                }
                _viewModel.Settings.WindowState = this.WindowState;
                _viewModel.SaveSettings();
            };
            
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            
            // Full Screen Binding
            Binding fsBinding = new Binding("IsFullScreen");
            fsBinding.Source = _viewModel;
            this.SetBinding(MainWindow.IsFullScreenProperty, fsBinding);
            
            // Global Mouse Wheel (for Slide Mode)
            this.PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            
            // Full Screen Logic
            _viewModel.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == "SelectedFile")
                {
                    _viewModel.ImageRotation = 0; // Reset rotation on new image
                    _isAutoFitMode = true; // Reset to auto-fit mode on new image
                    this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => {
                        AutoFitImage();
                        BringTreeSelectionIntoView();
                        if (_viewModel.IsViewMode || _viewModel.IsSlideMode) CenterThumbOnSelected();
                    }));
                }
                else if (e.PropertyName == "IsViewMode" || e.PropertyName == "IsSlideMode" || e.PropertyName == "IsFullScreen" || e.PropertyName == "ImageRotation")
                {
                    if (_viewModel.IsViewMode || _viewModel.IsSlideMode)
                    {
                        if (e.PropertyName == "ImageRotation") _isAutoFitMode = true; // 旋转时也重置为自适应
                        _isAutoFitMode = true; // 切换模式或全屏时，重置为自适应模式
                        this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => {
                            AutoFitImage();
                        }));
                    }
                }
                if (e.PropertyName == "IsFullScreen")
                {
                    var winChrome = WindowChrome.GetWindowChrome(this);
                    var rootGrid = this.Content as Grid;

                    if (_viewModel.IsFullScreen)
                    {
                        // Avoid Taskbar: Maximize within WorkArea
                        // Requires ResizeMode=CanResize and GlassFrameThickness > 0
                        if (winChrome != null) 
                        {
                            winChrome.GlassFrameThickness = new Thickness(1);
                            winChrome.CaptionHeight = 0;
                        }
                        
                        if (rootGrid != null && rootGrid.RowDefinitions.Count > 0)
                            rootGrid.RowDefinitions[0].Height = new GridLength(0);

                        this.BorderThickness = new Thickness(0);
                        this.ResizeMode = ResizeMode.CanResize;
                        this.WindowState = WindowState.Maximized;
                        this.Topmost = false; 
                        this.Focus(); 
                    }
                    else
                    {
                        // Restore
                        if (winChrome != null) 
                        {
                            winChrome.GlassFrameThickness = new Thickness(1);
                            winChrome.CaptionHeight = 40;
                        }

                        if (rootGrid != null && rootGrid.RowDefinitions.Count > 0)
                            rootGrid.RowDefinitions[0].Height = GridLength.Auto;

                        this.BorderThickness = new Thickness(1);
                        this.Topmost = false;
                        this.WindowState = WindowState.Normal;
                        this.ResizeMode = ResizeMode.CanResize;
                        this.Activate();
                    }
                }
                else if (e.PropertyName == "IsThumbnailView")
                {
                    UpdateFileListView();
                }
            };
        }
        
        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_viewModel.IsSlideMode)
            {
                if (e.Delta > 0) _viewModel.PrevImage();
                else _viewModel.NextImage();
                e.Handled = true;
            }
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string path = files[0];
                    if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))
                    {
                        OpenInitialImage(path);
                    }
                }
            }
        }

        public void OpenInitialImage(string path)
        {
            if (_viewModel != null)
            {
                _viewModel.OpenPath(path);
                this.WindowState = WindowState.Normal;
                this.Activate();
            }
        }
        
        // Helper to find ScrollViewer
        private static T GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null) child = GetVisualChild<T>(v);
                if (child != null) break;
            }
            return child;
        }

        // Ensure the selected folder is visible in the TreeView
        private void BringTreeSelectionIntoView()
        {
            if (_treeView == null) return;
            // Find selected item
            FileSystemItem target = FindSelectedItemInTree();
            if (target == null) return;

            // Ensure containers are generated by delaying to Loaded priority
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                var tvi = FindTreeViewItem(_treeView, target);
                if (tvi != null)
                {
                    tvi.BringIntoView();
                }
            }));
        }

        private FileSystemItem FindSelectedItemInTree()
        {
            foreach (var drive in _viewModel.Drives)
            {
                var found = FindSelectedRecursive(drive);
                if (found != null) return found;
            }
            return null;
        }

        private FileSystemItem FindSelectedRecursive(FileSystemItem item)
        {
            if (item.IsSelected) return item;
            foreach (var child in item.Children)
            {
                var found = FindSelectedRecursive(child);
                if (found != null) return found;
            }
            return null;
        }

        private TreeViewItem FindTreeViewItem(ItemsControl container, object item)
        {
            if (container == null) return null;
            var tvi = container.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (tvi != null) return tvi;

            foreach (var subItem in container.Items)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromItem(subItem) as TreeViewItem;
                if (childContainer != null)
                {
                    // Do NOT auto-expand here to avoid expanding entire tree.
                    // Only search within already expanded containers.

                    var result = FindTreeViewItem(childContainer, item);
                    if (result != null) return result;
                }
            }
            return null;
        }

        public static readonly DependencyProperty IsFullScreenProperty =
            DependencyProperty.Register("IsFullScreen", typeof(bool), typeof(MainWindow));

        private void UpdateFileListView()
        {
            if (_fileList == null) return;
            
            if (_viewModel.IsThumbnailView)
            {
                _fileList.View = null; // Clear GridView
                
                // Set ItemsPanel to WrapPanel
                FrameworkElementFactory factory = new FrameworkElementFactory(typeof(WrapPanel));
                _fileList.ItemsPanel = new ItemsPanelTemplate { VisualTree = factory };
                
                // Disable Horizontal ScrollBar to force WrapPanel to wrap
                ScrollViewer.SetHorizontalScrollBarVisibility(_fileList, ScrollBarVisibility.Disabled);
                
                // Set ItemTemplate to Image + Text
                DataTemplate template = new DataTemplate();
                FrameworkElementFactory stack = new FrameworkElementFactory(typeof(StackPanel));
                stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
                stack.SetValue(StackPanel.WidthProperty, new Binding("ThumbnailSize") { Source = _viewModel });
                stack.SetValue(StackPanel.MarginProperty, new Thickness(5));
                
                FrameworkElementFactory img = new FrameworkElementFactory(typeof(Image));
                img.SetBinding(Image.SourceProperty, new Binding("FullPath") { Converter = new ImagePathConverter() });
                img.SetValue(Image.HeightProperty, new Binding("ThumbnailSize") { Source = _viewModel });
                img.SetValue(Image.StretchProperty, Stretch.Uniform);
                
                FrameworkElementFactory txt = new FrameworkElementFactory(typeof(TextBlock));
                txt.SetBinding(TextBlock.TextProperty, new Binding("Name"));
                txt.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                txt.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                txt.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("Foreground"));
                
                stack.AppendChild(img);
                stack.AppendChild(txt);
                
                template.VisualTree = stack;
                _fileList.ItemTemplate = template;
            }
            else
            {
                _fileList.ItemTemplate = null;
                _fileList.ItemsPanel = new ItemsPanelTemplate { VisualTree = new FrameworkElementFactory(typeof(VirtualizingStackPanel)) };
                _fileList.View = CreateGridView();
                ScrollViewer.SetHorizontalScrollBarVisibility(_fileList, ScrollBarVisibility.Auto);
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel.IsSlideMode)
            {
                if (e.Key == Key.Escape)
                {
                    if (_viewModel.IsSettingsOpen)
                    {
                        _viewModel.IsSettingsOpen = false;
                        e.Handled = true;
                        return;
                    }

                    _viewModel.CurrentMode = DisplayMode.Browse;
                    _viewModel.StopSlideShow();
                    e.Handled = true;
                }
                else if (e.Key == Key.Space)
                {
                    _viewModel.ToggleSlideShow();
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    _viewModel.NextImage();
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    _viewModel.PrevImage();
                    e.Handled = true;
                }
            }
            else if (_viewModel.IsViewMode)
            {
                if (e.Key == Key.Right) 
                {
                    _viewModel.NextImage();
                    e.Handled = true;
                }
                if (e.Key == Key.Left) 
                {
                    _viewModel.PrevImage();
                    e.Handled = true;
                }
            }
        }

        private Point _scrollMousePoint;
        private double _hOff = 1;
        private double _vOff = 1;

        private void OnViewScrollMouseDown(object sender, MouseButtonEventArgs e)
        {
            _scrollMousePoint = e.GetPosition(_viewScrollViewer);
            _hOff = _viewScrollViewer.HorizontalOffset;
            _vOff = _viewScrollViewer.VerticalOffset;
            _viewScrollViewer.CaptureMouse();
            _viewScrollViewer.Cursor = Cursors.SizeAll;
        }

        private void OnViewScrollMouseMove(object sender, MouseEventArgs e)
        {
            if (_viewScrollViewer.IsMouseCaptured)
            {
                Point newPoint = e.GetPosition(_viewScrollViewer);
                double deltaX = _scrollMousePoint.X - newPoint.X;
                double deltaY = _scrollMousePoint.Y - newPoint.Y;
                
                _viewScrollViewer.ScrollToHorizontalOffset(_hOff + deltaX);
                _viewScrollViewer.ScrollToVerticalOffset(_vOff + deltaY);
            }
        }

        private void OnViewScrollMouseUp(object sender, MouseButtonEventArgs e)
        {
            _viewScrollViewer.ReleaseMouseCapture();
            _viewScrollViewer.Cursor = Cursors.Arrow;
        }

        private void AutoFitImage()
        {
            if (!_isAutoFitMode) return;
            if (_viewImage == null || _viewImage.Source == null || _viewScrollViewer == null) return;
            
            var src = _viewImage.Source as BitmapSource;
            if (src == null) return;

            // 1. 暂时设置图片不可见，以获取 ScrollViewer 纯净的可视区域 (Viewport)
            _viewImage.Visibility = Visibility.Collapsed;
            
            // 必须使用 Auto 或 Hidden，不能用 Disabled，否则无法获取 Viewport 尺寸
            _viewScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            _viewScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            _viewScrollViewer.UpdateLayout();

            // 2. 获取真实的物理可用尺寸
            double viewW = _viewScrollViewer.ActualWidth;
            double viewH = _viewScrollViewer.ActualHeight;

            if (viewW <= 0 || viewH <= 0) 
            {
                _viewImage.Visibility = Visibility.Visible;
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => {
                    AutoFitImage();
                }));
                return;
            }

            // 3. 引入 8 像素的安全留白（左右各 4，上下各 4）
            viewW -= 8;
            viewH -= 8;

            // 获取图片原始尺寸
            double w = src.PixelWidth;
            double h = src.PixelHeight;

            // 考虑旋转角度
            double rotation = _viewModel.ImageRotation % 180;
            bool isVertical = Math.Abs(rotation - 90) < 0.1;

            double targetW = isVertical ? h : w;
            double targetH = isVertical ? w : h;

            // 4. 计算缩放比例 (使用向下取整，确保 100% 不触发滚动条)
            double scaleX = viewW / targetW;
            double scaleY = viewH / targetH;
            double scale = Math.Min(scaleX, scaleY);
            
            _viewModel.ImageScale = Math.Floor(scale * 1000) / 10.0;

            // 5. 关键：恢复图片显示，并根据当前模式决定滚动条状态
            _viewImage.Visibility = Visibility.Visible;
            
            // 即使是自适应模式，也把滚动条设为 Auto，但因为比例已经缩小了，它们自然不会出现
            // 这样就解决了“无法直接拖动”的问题，因为 ScrollViewer 现在是处于“可滚动”状态的
            _viewScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            _viewScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            
            _viewScrollViewer.UpdateLayout();
        }

        private Grid _backgroundGrid;

        private void InitializeComponent()
        {
            this.Title = "小美图片查看器";
            try {
                this.Icon = new BitmapImage(new Uri("pack://siteoforigin:,,,/app.ico"));
            } catch { /* Icon load fail */ }
            
            this.Width = 1024;
            this.Height = 768;
            this.SetResourceReference(Window.BackgroundProperty, "WindowBackground");
            
            Grid rootGrid = new Grid();
            
            // Background Layer (RowSpan 3)
            _backgroundGrid = new Grid();
            Grid.SetRowSpan(_backgroundGrid, 3);
            Panel.SetZIndex(_backgroundGrid, -1);
            rootGrid.Children.Add(_backgroundGrid);

            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Custom Title Bar (Unified Header)
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            this.Content = rootGrid;

            // Subscribe to Theme Changes
            ThemeManager.ThemeChanged += UpdateBackgroundVisuals;

            // --- Custom Title Bar (Unified) ---
            Grid titleBar = new Grid();
            titleBar.Height = 40;
            titleBar.SetResourceReference(Grid.BackgroundProperty, "HeaderBackground");
            
            // 为标题栏增加底部边界线
            Border titleBottomBorder = new Border { BorderThickness = new Thickness(0, 0, 0, 1), VerticalAlignment = VerticalAlignment.Bottom };
            titleBottomBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            titleBar.Children.Add(titleBottomBorder);
            
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            
            // Layout: [Icon+Title] [Spacer] [SkinSwitcher] [ModeButtons] [Sep] [Zoom] [Spacer] [WinBtns]
            // We use ColumnDefinitions for better control
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0: Icon+Title
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1: Spacer
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 2: Tools Center
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 3: Spacer
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 4: Win Buttons

            // Window Support (Drag and Maximize fix)
            titleBar.MouseLeftButtonDown += (s, e) => {
                if (e.ClickCount == 2) {
                    this.WindowState = (this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
                } else {
                    this.DragMove();
                }
            };

            // 0: App Icon and Title
            StackPanel titleInfo = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) };
            Image iconImg = new Image { Source = this.Icon, Width = 20, Height = 20, Margin = new Thickness(0,0,10,0) };
            TextBlock titleText = new TextBlock { Text = this.Title + " v1.2.2", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "Foreground");
            titleInfo.Children.Add(iconImg);
            titleInfo.Children.Add(titleText);
            Grid.SetColumn(titleInfo, 0);
            titleBar.Children.Add(titleInfo);

            // 2: Tools (Skin, Modes, Zoom)
            StackPanel toolsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            
            // Skin Switcher (Palette Icon)
            toolsPanel.Children.Add(CreateGlassToolButton("M17.5,12A1.5,1.5 0 0,1 16,10.5A1.5,1.5 0 0,1 17.5,9A1.5,1.5 0 0,1 19,10.5A1.5,1.5 0 0,1 17.5,12M14.5,8A1.5,1.5 0 0,1 13,6.5A1.5,1.5 0 0,1 14.5,5A1.5,1.5 0 0,1 16,6.5A1.5,1.5 0 0,1 14.5,8M9.5,8A1.5,1.5 0 0,1 8,6.5A1.5,1.5 0 0,1 9.5,5A1.5,1.5 0 0,1 11,6.5A1.5,1.5 0 0,1 9.5,8M6.5,12A1.5,1.5 0 0,1 5,10.5A1.5,1.5 0 0,1 6.5,9A1.5,1.5 0 0,1 8,10.5A1.5,1.5 0 0,1 6.5,12M12,3A9,9 0 0,0 3,12A9,9 0 0,0 12,21C12.75,21 13.5,20.33 13.5,19.5C13.5,19.11 13.35,18.76 13.11,18.5C12.88,18.23 12.73,17.88 12.73,17.5A1.5,1.5 0 0,1 14.23,16H16A5,5 0 0,0 21,11C21,6.58 16.97,3 12,3Z", _viewModel.ToggleSkinCommand, "切换皮肤"));

            // Mode Buttons (Outline Style)
            // Browse (Folder Outline)
            toolsPanel.Children.Add(CreateGlassToolButton("M20,6H12L10,4H4A2,2 0 0,0 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8A2,2 0 0,0 20,6M20,18H4V6H8.83L10.83,8H20V18Z", _viewModel.SwitchToBrowseCommand, "浏览模式"));
            // View (Image Outline)
            toolsPanel.Children.Add(CreateGlassToolButton("M19,19H5V5H19M19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M13.96,12.29L11.21,15.83L9.25,13.47L6.5,17H17.5L13.96,12.29Z", _viewModel.SwitchToViewCommand, "查看模式"));
            // Slide (Screen Play Outline)
            toolsPanel.Children.Add(CreateGlassToolButton("M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M19,19H5V5H19V19M10,17L15,12L10,7V17Z", _viewModel.SwitchToSlideCommand, "幻灯片"));
            
            // Separator
            Rectangle sep1 = new Rectangle { Width = 1, Height = 16, Fill = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)), Margin = new Thickness(15, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center };
            toolsPanel.Children.Add(sep1);
            
            // Zoom Slider
            StackPanel zoomPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            // Zoom Icon (Vector Path instead of Text)
            Path zoomIcon = new Path();
            zoomIcon.Data = Geometry.Parse("M15.5,14L14.71,13.21L14.43,12.93C15.41,11.79 16,10.32 16,8.71C16,4.71 12.79,1.5 8.79,1.5C4.79,1.5 1.5,4.71 1.5,8.71C1.5,12.71 4.71,15.93 8.71,15.93C10.32,15.93 11.79,15.41 12.93,14.43L13.21,14.71L14,15.5L19,20.5L20.5,19L15.5,14M8.79,14C5.86,14 3.5,11.64 3.5,8.71C3.5,5.79 5.86,3.43 8.79,3.43C11.72,3.43 14.07,5.79 14.07,8.71C14.07,11.64 11.72,14 8.79,14Z");
            zoomIcon.SetResourceReference(Path.FillProperty, "Foreground");
            zoomIcon.Stretch = Stretch.Uniform;
            zoomIcon.Width = 18;
            zoomIcon.Height = 18;
            zoomIcon.Margin = new Thickness(0,0,8,0);
            zoomIcon.VerticalAlignment = VerticalAlignment.Center;
            zoomPanel.Children.Add(zoomIcon);

            Slider zoomSlider = new Slider { Minimum = 20, Maximum = 400, Width = 120, VerticalAlignment = VerticalAlignment.Center, IsSnapToTickEnabled = false };
            zoomSlider.SetBinding(Slider.ValueProperty, new Binding("CurrentZoom") { Source = _viewModel, Delay = 10 }); // Reduced delay for smoother feel
            zoomSlider.ToolTip = "缩放比例";
            WindowChrome.SetIsHitTestVisibleInChrome(zoomSlider, true);
            zoomPanel.Children.Add(zoomSlider);
            toolsPanel.Children.Add(zoomPanel);
            
            Grid.SetColumn(toolsPanel, 2);
            titleBar.Children.Add(toolsPanel);
            
            // 4: Window Buttons (Min, Max, Close)
            StackPanel winBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            
            Button minBtn = CreateSystemButton(CreateSystemPath("M19,13H5V11H19V13Z"), () => this.WindowState = WindowState.Minimized);
            
            Path maxPath = CreateSystemPath("M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M19,19H5V5H19V19Z");
            Button maxBtn = CreateSystemButton(maxPath, () => this.WindowState = (this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));
            
            this.StateChanged += (s, e) => {
                if (this.WindowState == WindowState.Maximized)
                {
                    // Exit Full Screen (Four inward corners)
                    maxPath.Data = Geometry.Parse("M5,16H8V19H10V14H5V16M8,8H5V10H10V5H8V8M14,19H16V16H19V14H14V19M16,8V5H14V10H19V8H16Z");
                    this.BorderThickness = new Thickness(0);
                    this.Padding = new Thickness(8); // Standard offset to prevent content bleeding in WindowStyle.None
                    if (rootGrid.Margin.Left != 0) rootGrid.Margin = new Thickness(0);
                }
                else
                {
                    // Maximize (Square)
                    maxPath.Data = Geometry.Parse("M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M19,19H5V5H19V19Z");
                    this.BorderThickness = new Thickness(1);
                    this.Padding = new Thickness(0);
                    rootGrid.Margin = new Thickness(0);
                }
            };
            
            // Initial State Check
            if (this.WindowState == WindowState.Maximized)
            {
                 maxPath.Data = Geometry.Parse("M5,16H8V19H10V14H5V16M8,8H5V10H10V5H8V8M14,19H16V16H19V14H14V19M16,8V5H14V10H19V8H16Z");
                 this.BorderThickness = new Thickness(0);
            }

            Button closeBtn = CreateSystemButton(CreateSystemPath("M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z"), () => this.Close(), true);
            
            winBtns.Children.Add(minBtn);
            winBtns.Children.Add(maxBtn);
            winBtns.Children.Add(closeBtn);
            
            Grid.SetColumn(winBtns, 4);
            titleBar.Children.Add(winBtns);
            
            Grid.SetRow(titleBar, 0);
            rootGrid.Children.Add(titleBar);

            // --- Main Content ---
            Grid mainContent = new Grid();
            Grid.SetRow(mainContent, 1); // Row 1 (was 2)
            mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250), MinWidth = 100 }); 
            mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); 
            
            GridSplitter splitter = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch };
            Grid.SetColumn(splitter, 1);
            mainContent.Children.Add(splitter);
            splitter.SetBinding(UIElement.VisibilityProperty, new Binding("IsSlideMode") { Converter = new BooleanToVisibilityConverter(true) });

            // Sidebar
            Border treeBorder = new Border { BorderThickness = new Thickness(0,0,1,0), Margin = new Thickness(0,0,0,0) };
            // 使用更明显的边框色
            treeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
            
            _treeView = new TreeView();
            _treeView.SetResourceReference(TreeView.BackgroundProperty, "PanelBackground"); // Use PanelBackground for contrast
            _treeView.SetResourceReference(TreeView.ForegroundProperty, "Foreground");
            _treeView.BorderThickness = new Thickness(0);
            _treeView.Padding = new Thickness(5);
            
            Style treeItemStyle = new Style(typeof(TreeViewItem));
            treeItemStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }));
            treeItemStyle.Setters.Add(new Setter(TreeViewItem.IsSelectedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay }));
            treeItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Foreground")));
            _treeView.ItemContainerStyle = treeItemStyle;
            
            _treeView.ItemsSource = _viewModel.Drives;
            _treeView.ItemTemplate = CreateTreeTemplate();
            _treeView.SelectedItemChanged += (s, e) => _viewModel.SelectedTreeItem = e.NewValue as FileSystemItem;
            _treeView.SetBinding(UIElement.VisibilityProperty, new Binding("IsSlideMode") { Converter = new BooleanToVisibilityConverter(true) });
            
            treeBorder.Child = _treeView;
            Grid.SetColumn(treeBorder, 0);
            mainContent.Children.Add(treeBorder);

            // Display Area
            Grid displayArea = new Grid();
            Grid.SetColumn(displayArea, 2);
            
            // Browse View
            Grid browseGrid = new Grid();
            browseGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            browseGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            browseGrid.SetBinding(UIElement.VisibilityProperty, new Binding("IsBrowseMode") { Converter = new BooleanToVisibilityConverter() });

            _fileList = new ListView();
            _fileList.ItemsSource = _viewModel.CurrentFiles;
            _fileList.View = CreateGridView();
            _fileList.MouseDoubleClick += (s, e) => { 
                // 防止在DoubleClick事件中直接切换ItemsSource引发重入卡顿，延迟到消息队列执行
                var item = _fileList.SelectedItem as FileSystemItem;
                if (item == null) return;
                e.Handled = true;
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    if (item.Type == ItemType.Folder)
                    {
                        _viewModel.OpenPath(item.FullPath); // 进入该文件夹（保持浏览模式）
                    }
                    else if (item.Type == ItemType.Image)
                    {
                        _viewModel.OpenPath(item.FullPath); // 进入查看模式并选中该图片
                    }
                    // 其他类型不响应
                }));
            };
            _fileList.SetBinding(ListView.SelectedItemProperty, new Binding("SelectedFile"));
            _fileList.SetResourceReference(ListView.BackgroundProperty, "WindowBackground"); // Use WindowBackground
            _fileList.SetResourceReference(ListView.ForegroundProperty, "Foreground");
            _fileList.BorderThickness = new Thickness(0);
            
            // Zoom with Ctrl+Wheel
            _fileList.PreviewMouseWheel += (s, e) => {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    double delta = e.Delta > 0 ? 10 : -10;
                    double newSize = _viewModel.ThumbnailSize + delta;
                    if (newSize < 20) newSize = 20;
                    if (newSize > 200) newSize = 200;
                    _viewModel.ThumbnailSize = newSize;
                    e.Handled = true;
                }
            };

            browseGrid.Children.Add(_fileList);

            // File Count Footer
            Border footerBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)), Padding = new Thickness(5) };
            Grid.SetRow(footerBorder, 1);
            TextBlock countText = new TextBlock();
            countText.SetBinding(TextBlock.TextProperty, new Binding("CurrentFiles.Count") { StringFormat = "共 {0} 个项目" });
            countText.SetResourceReference(TextBlock.ForegroundProperty, "Foreground");
            countText.HorizontalAlignment = HorizontalAlignment.Right;
            footerBorder.Child = countText;
            browseGrid.Children.Add(footerBorder);

            displayArea.Children.Add(browseGrid);
            
            // View Mode
            Grid viewModeGrid = new Grid();
            viewModeGrid.SetResourceReference(Grid.BackgroundProperty, "GlassBackground");
            viewModeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            viewModeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            viewModeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            viewModeGrid.SetBinding(UIElement.VisibilityProperty, new Binding("IsViewMode") { Converter = new BooleanToVisibilityConverter() });
            
            // Image Area Container
            Grid imageArea = new Grid();
            Grid.SetRow(imageArea, 0);

            _viewScrollViewer = new ScrollViewer();
            _viewScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            _viewScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _viewScrollViewer.Padding = new Thickness(0);
            _viewScrollViewer.BorderThickness = new Thickness(0);
            _viewScrollViewer.PanningMode = PanningMode.Both;
            _viewScrollViewer.PreviewMouseLeftButtonDown += OnViewScrollMouseDown;
            _viewScrollViewer.PreviewMouseMove += OnViewScrollMouseMove;
            _viewScrollViewer.PreviewMouseLeftButtonUp += OnViewScrollMouseUp;
            _viewScrollViewer.MouseDoubleClick += (s, e) => _viewModel.CurrentMode = DisplayMode.Browse;

            // View Mode ToolBar (Top Floating inside imageArea)
            StackPanel viewToolBar = new StackPanel { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Center, 
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 0, 0),
                Background = Brushes.Transparent 
            };
            Panel.SetZIndex(viewToolBar, 100);
            viewToolBar.Visibility = Visibility.Collapsed;

            Button rotateCCWBtn = CreateRotationButton("M7.11,8.53L5.7,7.11C4.8,8.27 4.24,9.61 4.07,11H6.09C6.24,10.13 6.57,9.33 7.11,8.53M7.11,15.47C6.57,14.67 6.24,13.87 6.09,13H4.07C4.24,14.39 4.8,15.73 5.7,16.89L7.11,15.47M13,4.07V1L8.45,5.55L13,10.1V7.07C15.8,7.58 18,10.03 18,13C18,15.22 16.75,17.15 14.9,18.12L16.03,19.95C18.44,18.57 20,15.97 20,13C20,9.13 16.87,6 13,4.07Z", _viewModel.RotateCounterClockwiseCommand);
            viewToolBar.Children.Add(rotateCCWBtn);

            Button rotateCWBtn = CreateRotationButton("M16.89,15.47L18.31,16.89C19.2,15.73 19.76,14.39 19.93,13H17.91C17.76,13.87 17.43,14.67 16.89,15.47M16.89,8.53C17.43,9.33 17.76,10.13 17.91,11H19.93C19.76,9.61 19.2,8.27 18.31,7.11L16.89,8.53M11,4.07V1L15.55,5.55L11,10.1V7.07C8.2,7.58 6,10.03 6,13C6,15.22 7.25,17.15 9.1,18.12L7.97,19.95C5.56,18.57 4,15.97 4,13C4,9.13 7.13,6 11,4.07Z", _viewModel.RotateClockwiseCommand);
            viewToolBar.Children.Add(rotateCWBtn);

            // Floating Navigation Buttons
            Button prevFloatBtn = CreateNavButton("M15.41,16.59L10.83,12L15.41,7.41L14,6L8,12L14,18L15.41,16.59Z", _viewModel.PrevImageCommand);
            prevFloatBtn.HorizontalAlignment = HorizontalAlignment.Left;
            prevFloatBtn.Margin = new Thickness(30, 0, 0, 0);
            prevFloatBtn.Visibility = Visibility.Collapsed;

            Button nextFloatBtn = CreateNavButton("M8.59,16.59L13.17,12L8.59,7.41L10,6L16,12L10,18L8.59,16.59Z", _viewModel.NextImageCommand);
            nextFloatBtn.HorizontalAlignment = HorizontalAlignment.Right;
            nextFloatBtn.Margin = new Thickness(0, 0, 30, 0);
            nextFloatBtn.Visibility = Visibility.Collapsed;

            imageArea.Children.Add(_viewScrollViewer);
            imageArea.Children.Add(viewToolBar);
            imageArea.Children.Add(prevFloatBtn);
            imageArea.Children.Add(nextFloatBtn);

            imageArea.MouseEnter += (s, e) => { 
                if (_viewModel.IsViewMode) {
                    viewToolBar.Visibility = Visibility.Visible;
                    prevFloatBtn.Visibility = Visibility.Visible;
                    nextFloatBtn.Visibility = Visibility.Visible;
                }
            };
            imageArea.MouseLeave += (s, e) => {
                viewToolBar.Visibility = Visibility.Collapsed;
                prevFloatBtn.Visibility = Visibility.Collapsed;
                nextFloatBtn.Visibility = Visibility.Collapsed;
            };

            viewModeGrid.Children.Add(imageArea);
            
            _viewImage = new Image();
            _viewImage.SetBinding(Image.SourceProperty, new Binding("SelectedFile.FullPath") { Converter = new ImagePathConverter() });
            _viewImage.Stretch = Stretch.None;
            // 启用高质量缩放和像素对齐，确保图片边缘严丝合缝
            RenderOptions.SetBitmapScalingMode(_viewImage, BitmapScalingMode.HighQuality);
            _viewImage.SnapsToDevicePixels = true;
            _viewImage.UseLayoutRounding = true;
            
            // Image Zoom and Rotate Transform
            TransformGroup transformGroup = new TransformGroup();
            
            // Rotate
            RotateTransform rotateTransform = new RotateTransform();
            Binding rotateBinding = new Binding("ImageRotation") { Source = _viewModel };
            BindingOperations.SetBinding(rotateTransform, RotateTransform.AngleProperty, rotateBinding);
            transformGroup.Children.Add(rotateTransform);

            // Scale
            ScaleTransform scaleTransform = new ScaleTransform();
            Binding scaleBinding = new Binding("ImageScale") { Source = _viewModel, Converter = new ZoomConverter() };
            BindingOperations.SetBinding(scaleTransform, ScaleTransform.ScaleXProperty, scaleBinding);
            BindingOperations.SetBinding(scaleTransform, ScaleTransform.ScaleYProperty, scaleBinding);
            transformGroup.Children.Add(scaleTransform);

            _viewImage.LayoutTransform = transformGroup;
            
            _viewScrollViewer.Content = _viewImage;
            
            // Mouse Wheel Zoom
            _viewScrollViewer.PreviewMouseWheel += (s, e) => {
                // 移除 Ctrl 键限制，直接滚动滚轮即可缩放
                double oldSize = _viewModel.ImageScale;
                double delta = e.Delta > 0 ? 10 : -10;
                double newSize = oldSize + delta;
                if (newSize < 20) newSize = 20;
                if (newSize > 400) newSize = 400;
                
                if (Math.Abs(newSize - oldSize) > 0.01)
                {
                    _isAutoFitMode = false; // 进入手动缩放模式，停止自适应跟随窗口
                    
                    // 手动缩放模式下开启滚动条
                    _viewScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                    _viewScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                    // Calculate Mouse Position Ratio
                    Point mousePos = e.GetPosition(_viewScrollViewer);
                    double ratio = newSize / oldSize;
                    
                    // Calculate Target Offset
                    double targetH = (_viewScrollViewer.HorizontalOffset + mousePos.X) * ratio - mousePos.X;
                    double targetV = (_viewScrollViewer.VerticalOffset + mousePos.Y) * ratio - mousePos.Y;
                    
                    _viewModel.ImageScale = newSize;
                    
                    // Apply scroll after layout update
                    _viewScrollViewer.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => {
                        _viewScrollViewer.ScrollToHorizontalOffset(targetH);
                        _viewScrollViewer.ScrollToVerticalOffset(targetV);
                    }));
                }
                e.Handled = true;
            };
            
            // viewModeGrid.Children.Add(imageArea); // 移除这行重复添加
            
            // --- 彻底清理冗余的导航按钮添加逻辑 ---
            // 上面已经把它们移入 imageArea 的初始化块中了
            
            // Thumbnail Strip with Scroll Buttons
            Grid thumbStripGrid = new Grid();
            Grid.SetRow(thumbStripGrid, 1);
            thumbStripGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            thumbStripGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            thumbStripGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            thumbStripGrid.Children.Add(CreateStripScrollButton("M15.41,7.41L14,6L8,12L14,18L15.41,16.59L10.83,12L15.41,7.41Z", _viewModel.PrevImageCommand, "上一张"));
            
            _thumbList = new ListBox();
            _thumbList.Height = 100;
            _thumbList.ItemsSource = _viewModel.CurrentFiles;
            _thumbList.ItemsPanel = CreateHorizontalPanelTemplate();
            _thumbList.ItemTemplate = CreateThumbnailTemplate();
            _thumbList.SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true);
            _thumbList.SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
            _thumbList.SetValue(VirtualizingStackPanel.ScrollUnitProperty, ScrollUnit.Pixel);
            // Selected item magnify effect
            Style thumbItemStyle = new Style(typeof(ListBoxItem));
            ScaleTransform st = new ScaleTransform(1.0, 1.0);
            thumbItemStyle.Setters.Add(new Setter(ListBoxItem.LayoutTransformProperty, st));
            Trigger selTrig = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selTrig.Setters.Add(new Setter(ListBoxItem.LayoutTransformProperty, new ScaleTransform(1.15, 1.15)));
            thumbItemStyle.Triggers.Add(selTrig);
            _thumbList.ItemContainerStyle = thumbItemStyle;
            _thumbList.SetBinding(ListBox.SelectedItemProperty, new Binding("SelectedFile"));
            _thumbList.SetResourceReference(ListBox.BackgroundProperty, "GlassBackground");
            ScrollViewer.SetVerticalScrollBarVisibility(_thumbList, ScrollBarVisibility.Disabled);
            ScrollViewer.SetHorizontalScrollBarVisibility(_thumbList, ScrollBarVisibility.Hidden);
            _thumbList.PreviewMouseWheel += (s, e) => {
                if (_viewModel.CurrentFiles == null || _viewModel.CurrentFiles.Count == 0) { e.Handled = true; return; }
                int currentIndex = _viewModel.CurrentFiles.IndexOf(_viewModel.SelectedFile);
                int step = e.Delta > 0 ? -1 : 1; // Up -> previous, Down -> next
                int newIndex = currentIndex == -1 ? 0 : currentIndex + step;
                if (newIndex < 0) newIndex = 0;
                if (newIndex >= _viewModel.CurrentFiles.Count) newIndex = _viewModel.CurrentFiles.Count - 1;
                if (newIndex != currentIndex) _viewModel.SelectedFile = _viewModel.CurrentFiles[newIndex];
                CenterThumbOnSelected();
                e.Handled = true;
            };

            Grid.SetColumn(_thumbList, 1);
            thumbStripGrid.Children.Add(_thumbList);
            
            Button rightBtn = CreateStripScrollButton("M10,6L8.59,7.41L13.17,12L8.59,16.59L10,18L16,12L10,6Z", _viewModel.NextImageCommand, "下一张");
            Grid.SetColumn(rightBtn, 2);
            thumbStripGrid.Children.Add(rightBtn);
            
            viewModeGrid.Children.Add(thumbStripGrid);
            
            StackPanel propsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            propsPanel.Children.Add(CreatePropText("分辨率", "Resolution"));
            propsPanel.Children.Add(CreatePropText("文件大小", "FileSize"));
            propsPanel.Children.Add(CreatePropText("图片格式", "Format"));
            propsPanel.Children.Add(CreatePropText("拍摄时间", "ShootTime"));
            Grid.SetRow(propsPanel, 2);
            viewModeGrid.Children.Add(propsPanel);
            
            displayArea.Children.Add(viewModeGrid);
            mainContent.Children.Add(displayArea);
            
            rootGrid.Children.Add(mainContent);
            
            // --- Slide Overlay ---
            Grid slideOverlay = new Grid();
            slideOverlay.Background = Brushes.Black;
            slideOverlay.SetBinding(UIElement.VisibilityProperty, new Binding("IsSlideMode") { Converter = new BooleanToVisibilityConverter() });
            Grid.SetRowSpan(slideOverlay, 2); 
            
            Image slideImage = new Image();
            slideImage.SetBinding(Image.SourceProperty, new Binding("SelectedFile.FullPath") { Converter = new ImagePathConverter() });
            slideImage.Stretch = Stretch.Uniform;
            slideOverlay.Children.Add(slideImage);
            
            // Slide Controls Layer
            Grid slideControls = new Grid();
            
            // Settings Button (Top Left)
            Path settingsPath = CreateSystemPath("M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,13L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.04 4.95,18.95L7.44,17.95C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.68 16.04,18.34 16.56,17.95L19.05,18.95C19.27,19.04 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z");
            settingsPath.Fill = Brushes.Gray;
            Button settingsBtn = CreateGlassToolButton("", _viewModel.ToggleSettingsCommand, "幻灯片设置");
            settingsBtn.Content = settingsPath;
            settingsBtn.HorizontalAlignment = HorizontalAlignment.Left;
            settingsBtn.VerticalAlignment = VerticalAlignment.Top;
            settingsBtn.Margin = new Thickness(10);
            slideControls.Children.Add(settingsBtn);

            // Full Screen Button (Top Right)
            Path slideMaxPath = CreateSystemPath("M5,5H10V7H7V10H5V5M14,5H19V10H17V7H14V5M17,14H19V19H14V17H17V14M10,17V19H5V14H7V17H10Z");
            slideMaxPath.Fill = Brushes.Gray;
            Button fullScreenBtn = CreateGlassToolButton("", _viewModel.ToggleFullScreenCommand, "全屏/退出全屏");
            fullScreenBtn.Content = slideMaxPath;
            
            // Re-template for slide buttons to have better visibility
            ControlTemplate slideBtnTemplate = new ControlTemplate(typeof(Button));
            FrameworkElementFactory slideBtnGrid = new FrameworkElementFactory(typeof(Grid));
            slideBtnGrid.SetValue(Grid.BackgroundProperty, Brushes.Transparent); // Ensure hit test
            
            FrameworkElementFactory slideBtnBack = new FrameworkElementFactory(typeof(Border));
            slideBtnBack.Name = "backBorder";
            slideBtnBack.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            slideBtnBack.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(15, 128, 128, 128)));
            
            FrameworkElementFactory slideBtnCP = new FrameworkElementFactory(typeof(ContentPresenter));
            slideBtnCP.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            slideBtnCP.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            slideBtnGrid.AppendChild(slideBtnBack);
            slideBtnGrid.AppendChild(slideBtnCP);
            slideBtnTemplate.VisualTree = slideBtnGrid;
            
            // Hover Trigger
            Trigger slideHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            slideHover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 200, 200, 200)), "backBorder"));
            slideHover.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), "backBorder"));
            slideHover.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "backBorder"));
            slideBtnTemplate.Triggers.Add(slideHover);
            
            settingsBtn.Template = slideBtnTemplate;
            fullScreenBtn.Template = slideBtnTemplate;

            _viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == "IsFullScreen") {
                    if (_viewModel.IsFullScreen)
                        slideMaxPath.Data = Geometry.Parse("M5,16H8V19H10V14H5V16M8,8H5V10H10V5H8V8M14,19H16V16H19V14H14V19M16,8V5H14V10H19V8H16Z");
                    else
                        slideMaxPath.Data = Geometry.Parse("M5,5H10V7H7V10H5V5M14,5H19V10H17V7H14V5M17,14H19V19H14V17H17V14M10,17V19H5V14H7V17H10Z");
                }
            };

            fullScreenBtn.HorizontalAlignment = HorizontalAlignment.Right;
            fullScreenBtn.VerticalAlignment = VerticalAlignment.Top;
            fullScreenBtn.Margin = new Thickness(10);
            slideControls.Children.Add(fullScreenBtn);

            // Settings Panel
            Border settingsPanel = new Border { 
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), 
                BorderBrush = Brushes.Gray, 
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Left, 
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 50, 0, 0)
            };
            settingsPanel.SetBinding(UIElement.VisibilityProperty, new Binding("IsSettingsOpen") { Converter = new BooleanToVisibilityConverter() });
            
            StackPanel settingsStack = new StackPanel();
            
            // Speed
            StackPanel speedPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,5) };
            speedPanel.Children.Add(new TextBlock { Text = "间隔(秒): ", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            
            // Numeric Input Control
            Border speedBorder = new Border { BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, CornerRadius = new CornerRadius(2), Background = Brushes.White, Margin = new Thickness(5,0,0,0) };
            StackPanel boxContainer = new StackPanel { Orientation = Orientation.Horizontal };
            
            TextBox speedBox = new TextBox { Width = 30, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center };
            speedBox.SetBinding(TextBox.TextProperty, new Binding("SlideSettings.IntervalSeconds"));
            boxContainer.Children.Add(speedBox);
            
            StackPanel btnStack = new StackPanel { Orientation = Orientation.Vertical };
            
            // Up Button
            Button upBtn = new Button { Content = "▴", FontSize = 8, Width = 15, Height = 10, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            upBtn.Click += (s,e) => _viewModel.SlideSettings.IntervalSeconds++;
            
            // Down Button
            Button downBtn = new Button { Content = "▾", FontSize = 8, Width = 15, Height = 10, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            downBtn.Click += (s,e) => { if(_viewModel.SlideSettings.IntervalSeconds > 1) _viewModel.SlideSettings.IntervalSeconds--; };
            
            btnStack.Children.Add(upBtn);
            btnStack.Children.Add(downBtn);
            boxContainer.Children.Add(btnStack);
            
            speedBorder.Child = boxContainer;
            speedPanel.Children.Add(speedBorder);
            settingsStack.Children.Add(speedPanel);
            
            // Loop/Random (Radio Buttons)
            RadioButton seqRadio = new RadioButton { Content = "顺序播放", Foreground = Brushes.White, Margin = new Thickness(0,0,0,5), GroupName = "PlayOrder" };
            seqRadio.SetBinding(RadioButton.IsCheckedProperty, new Binding("SlideSettings.IsSequential"));
            settingsStack.Children.Add(seqRadio);
            
            RadioButton rndRadio = new RadioButton { Content = "随机播放", Foreground = Brushes.White, GroupName = "PlayOrder" };
            rndRadio.SetBinding(RadioButton.IsCheckedProperty, new Binding("SlideSettings.IsRandom"));
            settingsStack.Children.Add(rndRadio);
            
            settingsPanel.Child = settingsStack;
            slideControls.Children.Add(settingsPanel);
            
            TextBlock hint = new TextBlock { Text = "空格: 暂停/播放 | Esc: 退出 | 方向键: 切换图片", Foreground = Brushes.Gray, Margin = new Thickness(10), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center };
            slideControls.Children.Add(hint);
            
            slideOverlay.Children.Add(slideControls);
            
            rootGrid.Children.Add(slideOverlay);
            
            // Initial Visuals
            UpdateBackgroundVisuals(_viewModel.Settings.CurrentTheme);
        }

        private Button CreateRotationButton(string pathData, ICommand command)
        {
            Button btn = new Button 
            { 
                Command = command, 
                Width = 36,  // 进一步调小
                Height = 36,
                Margin = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand,
                Focusable = false
            };
            
            btn.Background = Brushes.Transparent;
            btn.BorderThickness = new Thickness(0);
            
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(Grid.BackgroundProperty, Brushes.Transparent);
            
            // Background Border (Extremely Light Frosted Gray)
            FrameworkElementFactory back = new FrameworkElementFactory(typeof(Border));
            back.Name = "backBorder";
            back.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(10, 150, 150, 150))); // 更浅更透明
            back.SetValue(Border.CornerRadiusProperty, new CornerRadius(18));
            
            // Icon
            FrameworkElementFactory viewbox = new FrameworkElementFactory(typeof(Viewbox));
            viewbox.SetValue(Viewbox.WidthProperty, 20.0); // 图标也变小
            viewbox.SetValue(Viewbox.HeightProperty, 20.0);
            
            FrameworkElementFactory path = new FrameworkElementFactory(typeof(Path));
            path.SetValue(Path.DataProperty, Geometry.Parse(pathData));
            path.SetValue(Path.FillProperty, new SolidColorBrush(Color.FromRgb(180, 180, 180))); // 极浅灰色图标
            path.SetValue(Path.StretchProperty, Stretch.Uniform);
            
            viewbox.AppendChild(path);
            grid.AppendChild(back);
            grid.AppendChild(viewbox);
            
            template.VisualTree = grid;
            
            // Triggers
            Trigger mouseOver = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            mouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(30, 150, 150, 150)), "backBorder"));
            template.Triggers.Add(mouseOver);
            
            btn.Template = template;
            return btn;
        }

        private Button CreateGlassToolButton(string pathData, ICommand command, string tooltip)
        {
            Button btn = new Button 
            { 
                Command = command, 
                ToolTip = tooltip,
                Width = 40, // Slightly larger touch area
                Height = 32,
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                Focusable = false // Prevent focus stealing for key bindings
            };
            
            btn.Background = Brushes.Transparent;
            btn.BorderThickness = new Thickness(0);
            WindowChrome.SetIsHitTestVisibleInChrome(btn, true);
            
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(Grid.BackgroundProperty, Brushes.Transparent);
            
            // Hover/Active Background (Frosted Gray)
            FrameworkElementFactory back = new FrameworkElementFactory(typeof(Border));
            back.Name = "backBorder";
            back.SetValue(Border.BackgroundProperty, Brushes.Transparent); // Default invisible
            back.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            
            // Icon
            FrameworkElementFactory path = new FrameworkElementFactory(typeof(Path));
            path.SetValue(Path.DataProperty, Geometry.Parse(pathData));
            path.SetValue(Path.FillProperty, new DynamicResourceExtension("Foreground"));
            path.SetValue(Path.StretchProperty, Stretch.Uniform);
            path.SetValue(Path.WidthProperty, 18.0); // Slightly larger icon
            path.SetValue(Path.HeightProperty, 18.0);
            path.SetValue(Path.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            path.SetValue(Path.VerticalAlignmentProperty, VerticalAlignment.Center);
            // No shadow for clean flat look
            
            grid.AppendChild(back);
            grid.AppendChild(path);
            
            template.VisualTree = grid;
            
            // Triggers
            Trigger mouseOver = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            // Frosted Gray Transparent (Approx 20% Gray)
            mouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), "backBorder"));
            template.Triggers.Add(mouseOver);
            
            Trigger pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            // Darker Gray on Press
            pressed.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)), "backBorder"));
            template.Triggers.Add(pressed);

            btn.Template = template;
            return btn;
        }

        private Button CreateStripScrollButton(string pathData, ICommand command, string tooltip)
        {
            Button btn = new Button
            {
                Command = command,
                // ToolTip = tooltip, // 移除 ToolTip
                Width = 28,
                VerticalAlignment = VerticalAlignment.Stretch,
                Cursor = Cursors.Hand
            };
            btn.Background = Brushes.Transparent;
            btn.BorderThickness = new Thickness(0);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(Grid.BackgroundProperty, Brushes.Transparent);

            FrameworkElementFactory glass = new FrameworkElementFactory(typeof(Border));
            glass.Name = "glassBorder";
            glass.SetValue(Border.OpacityProperty, 1.0); // 改为常驻显示（从 0.0 改为 1.0）
            glass.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(30, 80, 80, 80)));
            glass.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)));
            glass.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            glass.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            glass.SetValue(Border.MarginProperty, new Thickness(2));

            FrameworkElementFactory path = new FrameworkElementFactory(typeof(Path));
            path.SetValue(Path.DataProperty, Geometry.Parse(pathData));
            path.SetValue(Path.FillProperty, Brushes.White);
            path.SetValue(Path.StretchProperty, Stretch.Uniform);
            path.SetValue(Path.WidthProperty, 18.0);
            path.SetValue(Path.HeightProperty, 18.0);
            path.SetValue(Path.VerticalAlignmentProperty, VerticalAlignment.Center);
            path.SetValue(Path.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            glass.AppendChild(path);
            grid.AppendChild(glass);
            template.VisualTree = grid;

            Trigger mouseOver = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            mouseOver.Setters.Add(new Setter(UIElement.OpacityProperty, 1.0, "glassBorder"));
            mouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 100, 100, 100)), "glassBorder"));
            template.Triggers.Add(mouseOver);

            btn.Template = template;
            return btn;
        }
        
        private TextBlock CreatePropText(string label, string bindingPath)
        {
            TextBlock tb = new TextBlock { Margin = new Thickness(10,5,10,5) };
            tb.SetBinding(TextBlock.TextProperty, new Binding("CurrentImageInfo." + bindingPath) { StringFormat = label + ": {0}" });
            tb.SetResourceReference(TextBlock.ForegroundProperty, "Foreground");
            return tb;
        }

        private Button CreateNavButton(string pathData, ICommand command)
        {
            Button btn = new Button 
            { 
                Command = command, 
                Width = 36, // 稍微加宽
                Height = 180,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            Panel.SetZIndex(btn, 100); 
            
            btn.Background = Brushes.Transparent; 
            btn.BorderThickness = new Thickness(0);
            
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(Grid.BackgroundProperty, Brushes.Transparent);
            
            // Glass Pane Border
            FrameworkElementFactory glass = new FrameworkElementFactory(typeof(Border));
            glass.Name = "glassBorder";
            glass.SetValue(Border.OpacityProperty, 0.0); // 默认隐藏
            
            // Alpha 15 (Very light), Gray Color (120,120,120)
            glass.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(15, 120, 120, 120)));
            glass.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)));
            glass.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            glass.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            glass.SetValue(Border.MarginProperty, new Thickness(2));
            
            // Icon
            FrameworkElementFactory viewbox = new FrameworkElementFactory(typeof(Viewbox));
            viewbox.SetValue(Viewbox.WidthProperty, 24.0);
            viewbox.SetValue(Viewbox.HeightProperty, 24.0);
            viewbox.SetValue(Viewbox.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            FrameworkElementFactory path = new FrameworkElementFactory(typeof(Path));
            path.SetValue(Path.DataProperty, Geometry.Parse(pathData));
            path.SetValue(Path.FillProperty, new SolidColorBrush(Color.FromRgb(180, 180, 180))); // 浅灰色图标
            path.SetValue(Path.StretchProperty, Stretch.Uniform);
            
            viewbox.AppendChild(path);
            glass.AppendChild(viewbox);
            grid.AppendChild(glass);
            
            template.VisualTree = grid;
            
            // Triggers
            Trigger mouseOver = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            mouseOver.Setters.Add(new Setter(UIElement.OpacityProperty, 1.0, "glassBorder"));
            mouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 120, 120, 120)), "glassBorder"));
            template.Triggers.Add(mouseOver);
            
            btn.Template = template;
            return btn;
        }

        private void UpdateBackgroundVisuals(Theme theme)
        {
            if (_backgroundGrid == null) return;
            _backgroundGrid.Children.Clear();
            
            // Create Visuals
            FrameworkElement visuals = ThemeVisuals.CreateVisuals(theme);
            _backgroundGrid.Children.Add(visuals);
        }

        private Path CreateSystemPath(string data)
        {
            Path path = new Path();
            path.Data = Geometry.Parse(data);
            path.Stretch = Stretch.Uniform;
            path.Width = 10;
            path.Height = 10;
            path.SetBinding(Path.FillProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1) });
            return path;
        }

        private Button CreateSystemButton(object content, Action action, bool isClose = false)
        {
            Button btn = new Button();
            btn.Content = content;
            btn.Width = 46;
            btn.Height = 30;
            btn.Foreground = Brushes.Gray;
            btn.Background = Brushes.Transparent;
            btn.BorderThickness = new Thickness(0);
            btn.Click += (s, e) => action();
            WindowChrome.SetIsHitTestVisibleInChrome(btn, true);

            // Template for hover effect
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(cp);
            template.VisualTree = border;
            
            Trigger mouseOver = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            mouseOver.Setters.Add(new Setter(Border.BackgroundProperty, isClose ? Brushes.Red : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), "border"));
            mouseOver.Setters.Add(new Setter(Control.ForegroundProperty, isClose ? Brushes.White : Brushes.Black));
            template.Triggers.Add(mouseOver);
            
            btn.Template = template;
            return btn;
        }

        private ControlTemplate CreateHaloButtonTemplate()
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            
            // Halo Circle
            FrameworkElementFactory ellipse = new FrameworkElementFactory(typeof(Ellipse));
            ellipse.SetValue(Ellipse.StrokeThicknessProperty, 3.0);
            
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);
            brush.GradientStops.Add(new GradientStop(Colors.Red, 0.0));
            brush.GradientStops.Add(new GradientStop(Colors.Orange, 0.2));
            brush.GradientStops.Add(new GradientStop(Colors.Yellow, 0.4));
            brush.GradientStops.Add(new GradientStop(Colors.Green, 0.6));
            brush.GradientStops.Add(new GradientStop(Colors.Blue, 0.8));
            brush.GradientStops.Add(new GradientStop(Colors.Violet, 1.0));
            
            ellipse.SetValue(Ellipse.StrokeProperty, brush);
            ellipse.SetValue(Ellipse.FillProperty, Brushes.Transparent);
            
            grid.AppendChild(ellipse);
            
            // Icon
            FrameworkElementFactory path = new FrameworkElementFactory(typeof(Path));
            path.SetValue(Path.DataProperty, Geometry.Parse("M12,3A9,9 0 0,0 3,12C3,17 7,21 12,21C17,21 21,17 21,12A9,9 0 0,0 12,3M12,19A7,7 0 0,1 5,12A7,7 0 0,1 12,5A7,7 0 0,1 19,12A7,7 0 0,1 12,19Z"));
            path.SetValue(Path.FillProperty, new DynamicResourceExtension("Foreground"));
            path.SetValue(Path.StretchProperty, Stretch.Uniform);
            path.SetValue(Path.MarginProperty, new Thickness(6));
            
            grid.AppendChild(path);
            
            // Trigger: MouseOver -> Opacity
            Trigger mouseOver = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            mouseOver.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8));
            template.Triggers.Add(mouseOver);
            
            template.VisualTree = grid;
            return template;
        }

        private HierarchicalDataTemplate CreateTreeTemplate()
        {
            FrameworkElementFactory text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            text.SetValue(TextBlock.MarginProperty, new Thickness(2));
            
            HierarchicalDataTemplate template = new HierarchicalDataTemplate();
            template.ItemsSource = new Binding("Children");
            template.VisualTree = text;
            return template;
        }
        
        private ViewBase CreateGridView()
        {
            GridView grid = new GridView();
            
            // Name Column
            GridViewColumn colName = new GridViewColumn { Header = CreateSortHeader("名称", "Name"), Width = 250 };
            FrameworkElementFactory textName = new FrameworkElementFactory(typeof(TextBlock));
            textName.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            textName.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("Foreground"));
            colName.CellTemplate = new DataTemplate { VisualTree = textName };

            // Resolution Column (New)
            GridViewColumn colRes = new GridViewColumn { Header = CreateSortHeader("分辨率", "Resolution"), Width = 120 };
            FrameworkElementFactory textRes = new FrameworkElementFactory(typeof(TextBlock));
            textRes.SetBinding(TextBlock.TextProperty, new Binding("Resolution"));
            textRes.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("Foreground"));
            colRes.CellTemplate = new DataTemplate { VisualTree = textRes };
            
            // Size Column
            GridViewColumn colSize = new GridViewColumn { Header = CreateSortHeader("大小", "Size"), Width = 100 };
            FrameworkElementFactory textSize = new FrameworkElementFactory(typeof(TextBlock));
            textSize.SetBinding(TextBlock.TextProperty, new Binding("Size") { Converter = new FileSizeConverter() });
            textSize.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("Foreground"));
            colSize.CellTemplate = new DataTemplate { VisualTree = textSize };
            
            // Type Column
            GridViewColumn colType = new GridViewColumn { Header = CreateSortHeader("类型", "Type"), Width = 80 };
            FrameworkElementFactory textType = new FrameworkElementFactory(typeof(TextBlock));
            textType.SetBinding(TextBlock.TextProperty, new Binding("Type") { Converter = new ItemTypeConverter() });
            textType.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("Foreground"));
            colType.CellTemplate = new DataTemplate { VisualTree = textType };

            // Date Column
            GridViewColumn colDate = new GridViewColumn { Header = CreateSortHeader("修改日期", "Date"), Width = 150 };
            FrameworkElementFactory textDate = new FrameworkElementFactory(typeof(TextBlock));
            textDate.SetBinding(TextBlock.TextProperty, new Binding("CreationTime") { StringFormat = "yyyy-MM-dd HH:mm" });
            textDate.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("Foreground"));
            colDate.CellTemplate = new DataTemplate { VisualTree = textDate };

            grid.Columns.Add(colName);
            grid.Columns.Add(colRes);
            grid.Columns.Add(colSize);
            grid.Columns.Add(colType);
            grid.Columns.Add(colDate);
            return grid;
        }

        private object CreateSortHeader(string text, string commandParam)
        {
            Button btn = new Button
            {
                Command = _viewModel.SortCommand,
                CommandParameter = commandParam,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5, 0, 5, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Focusable = false
            };
            
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(Grid.BackgroundProperty, Brushes.Transparent); // 整个 Grid 可点击
            
            FrameworkElementFactory sp = new FrameworkElementFactory(typeof(StackPanel));
            sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            sp.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            sp.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            FrameworkElementFactory tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetValue(TextBlock.TextProperty, text);
            tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            sp.AppendChild(tb);
            
            FrameworkElementFactory arrow = new FrameworkElementFactory(typeof(TextBlock));
            arrow.SetValue(TextBlock.MarginProperty, new Thickness(5, 0, 0, 0));
            arrow.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(TextBlock.FontSizeProperty, 10.0);
            
            // Binding for Arrow Visibility and Direction
            Binding visibilityBinding = new Binding("SortColumn") { 
                Source = _viewModel, 
                Converter = new SortArrowVisibilityConverter(), 
                ConverterParameter = commandParam 
            };
            arrow.SetBinding(UIElement.VisibilityProperty, visibilityBinding);
            
            Binding textBinding = new Binding("IsSortDescending") { 
                Source = _viewModel, 
                Converter = new SortArrowDirectionConverter() 
            };
            arrow.SetBinding(TextBlock.TextProperty, textBinding);
            
            sp.AppendChild(arrow);
            grid.AppendChild(sp);
            template.VisualTree = grid;
            btn.Template = template;
            
            return btn;
        }

        private ItemsPanelTemplate CreateHorizontalPanelTemplate()
        {
            FrameworkElementFactory factory = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
            factory.SetValue(VirtualizingStackPanel.OrientationProperty, Orientation.Horizontal);
            return new ItemsPanelTemplate { VisualTree = factory };
        }
        
        private DataTemplate CreateThumbnailTemplate()
        {
            FrameworkElementFactory image = new FrameworkElementFactory(typeof(Image));
            image.SetBinding(Image.SourceProperty, new Binding("FullPath") { Converter = new ImagePathConverter() });
            image.SetValue(Image.HeightProperty, 80.0);
            image.SetValue(Image.MarginProperty, new Thickness(5));
            
            return new DataTemplate { VisualTree = image };
        }

        // Center the selected thumbnail in the thumbnail strip
        private void CenterThumbOnSelected()
        {
            if (_thumbList == null || _thumbList.SelectedItem == null) return;
            if (!_thumbList.IsVisible || _thumbList.ActualWidth <= 0) return;
            if (_isCenteringThumb) return;
            _isCenteringThumb = true;
            var item = _thumbList.SelectedItem;
            var container = _thumbList.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (container == null)
            {
                _thumbList.ScrollIntoView(item);
                _isCenteringThumb = false;
                _thumbList.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => {
                    if (_viewModel.IsViewMode || _viewModel.IsSlideMode) CenterThumbOnSelected();
                }));
                return;
            }

            var scrollViewer = GetVisualChild<ScrollViewer>(_thumbList);
            if (scrollViewer == null) return;
            var presenter = GetVisualChild<ScrollContentPresenter>(_thumbList);

            Point itemPos = (presenter != null ? container.TransformToAncestor(presenter) : container.TransformToAncestor(scrollViewer)).Transform(new Point(0, 0));
            double itemCenter = itemPos.X + container.ActualWidth / 2.0;
            double viewportCenter = (presenter != null ? presenter.ActualWidth : scrollViewer.ViewportWidth) / 2.0;
            double targetOffset = scrollViewer.HorizontalOffset + (itemCenter - viewportCenter);

            if (targetOffset < 0) targetOffset = 0;
            double maxOffset = scrollViewer.ExtentWidth - scrollViewer.ViewportWidth;
            if (targetOffset > maxOffset) targetOffset = maxOffset;

            scrollViewer.ScrollToHorizontalOffset(targetOffset);
            _isCenteringThumb = false;
        }
    }
}
