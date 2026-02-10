using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
// using System.IO; // Removed to avoid conflict with Path

namespace ImageBrowser
{
    public static class ThemeVisuals
    {
        public static FrameworkElement CreateVisuals(Theme theme)
        {
            Grid root = new Grid();
            root.IsHitTestVisible = false; // Background should not interfere with clicks
            root.ClipToBounds = true;

            // 1. Watermark Layer (Static Background Image)
            AddWatermark(root, theme.ToString());

            // 2. Dynamic Layer (Vector Animations)
            Canvas canvas = new Canvas();
            canvas.IsHitTestVisible = false;
            canvas.ClipToBounds = true;
            root.Children.Add(canvas);

            switch (theme)
            {
                case Theme.Mei:
                    CreateMeiVisuals(canvas);
                    break;
                case Theme.Lan:
                    CreateLanVisuals(canvas);
                    break;
                case Theme.Zhu:
                    CreateZhuVisuals(canvas);
                    break;
                case Theme.Ju:
                    CreateJuVisuals(canvas);
                    break;
            }

            return root;
        }

        private static void AddWatermark(Grid root, string themeName)
        {
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Themes", themeName + ".png");
                if (System.IO.File.Exists(path))
                {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(path);
                    bi.EndInit();

                    Image img = new Image();
                    img.Source = bi;
                    img.Opacity = 0.15; // Subtle watermark effect
                    img.Stretch = Stretch.Uniform;
                    img.HorizontalAlignment = HorizontalAlignment.Right;
                    img.VerticalAlignment = VerticalAlignment.Bottom;
                    
                    // Allow image to be large but bounded by container
                    // Setting MaxHeight/MaxWidth to ensure it doesn't overflow weirdly if stretch is Uniform
                    // But usually Uniform inside a Grid cell works fine.
                    
                    root.Children.Add(img);
                }
            }
            catch { /* Ignore image load errors */ }
        }

        private static void CreateMeiVisuals(Canvas canvas)
        {
            // 1. Branches (Tree Shadow)
            Path branch = new Path();
            branch.Data = Geometry.Parse("M 0,800 C 100,700 50,600 150,500 S 200,300 100,200 M 150,500 C 200,450 300,450 350,400");
            branch.Stroke = new SolidColorBrush(Color.FromArgb(40, 60, 40, 40)); // Faint shadow
            branch.StrokeThickness = 8;
            canvas.Children.Add(branch);
            Canvas.SetLeft(branch, -50);
            Canvas.SetBottom(branch, 0);

            // Animate Branch (Slight Sway)
            RotateTransform rotate = new RotateTransform(0, 0, 800);
            branch.RenderTransform = rotate;
            DoubleAnimation sway = new DoubleAnimation(-2, 2, TimeSpan.FromSeconds(5));
            sway.AutoReverse = true;
            sway.RepeatBehavior = RepeatBehavior.Forever;
            rotate.BeginAnimation(RotateTransform.AngleProperty, sway);

            // 2. Blossoms (Static)
            AddBlossom(canvas, 100, 200, Brushes.Crimson);
            AddBlossom(canvas, 160, 480, Brushes.Pink);
            AddBlossom(canvas, 350, 400, Brushes.DeepPink);

            // 3. Falling Petals (Animation)
            for (int i = 0; i < 15; i++)
            {
                Ellipse petal = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromArgb(150, 255, 240, 245)) };
                canvas.Children.Add(petal);
                
                // Random start position
                double startLeft = new Random(i * 123).Next(0, 1000);
                Canvas.SetLeft(petal, startLeft);
                Canvas.SetTop(petal, -20);

                // Fall Animation
                DoubleAnimation fall = new DoubleAnimation(1000, TimeSpan.FromSeconds(10 + i));
                fall.RepeatBehavior = RepeatBehavior.Forever;
                fall.BeginTime = TimeSpan.FromSeconds(i * 0.5);
                petal.BeginAnimation(Canvas.TopProperty, fall);

                // Drift Animation
                DoubleAnimation drift = new DoubleAnimation(startLeft - 100, startLeft + 100, TimeSpan.FromSeconds(3));
                drift.AutoReverse = true;
                drift.RepeatBehavior = RepeatBehavior.Forever;
                petal.BeginAnimation(Canvas.LeftProperty, drift);
            }
        }

        private static void AddBlossom(Canvas canvas, double x, double y, Brush color)
        {
            Ellipse flower = new Ellipse { Width = 15, Height = 15, Fill = color, Opacity = 0.6 };
            canvas.Children.Add(flower);
            Canvas.SetLeft(flower, x);
            Canvas.SetTop(flower, y);
        }

        private static void CreateLanVisuals(Canvas canvas)
        {
            // Orchid Leaves (Long curved paths)
            for (int i = 0; i < 3; i++)
            {
                Path leaf = new Path();
                leaf.Data = Geometry.Parse("M 0,0 Q 50,-200 150,-300");
                leaf.Stroke = new SolidColorBrush(Color.FromArgb(50, 34, 139, 34)); // ForestGreen transparent
                leaf.StrokeThickness = 15 - i * 2;
                leaf.StrokeEndLineCap = PenLineCap.Round;
                
                canvas.Children.Add(leaf);
                Canvas.SetLeft(leaf, 100 + i * 30);
                Canvas.SetBottom(leaf, -50);

                // Sway Animation
                RotateTransform rotate = new RotateTransform(i * 10, 0, 0);
                leaf.RenderTransform = rotate;
                
                DoubleAnimation sway = new DoubleAnimation(i * 10 - 5, i * 10 + 5, TimeSpan.FromSeconds(4 + i));
                sway.AutoReverse = true;
                sway.RepeatBehavior = RepeatBehavior.Forever;
                rotate.BeginAnimation(RotateTransform.AngleProperty, sway);
            }
            
            // Floating Pollen/Scent
            for (int i = 0; i < 10; i++)
            {
                Ellipse pollen = new Ellipse { Width = 4, Height = 4, Fill = new SolidColorBrush(Color.FromArgb(100, 200, 160, 255)) };
                canvas.Children.Add(pollen);
                Canvas.SetLeft(pollen, 200);
                Canvas.SetBottom(pollen, 200);

                DoubleAnimation rise = new DoubleAnimation(200, 600, TimeSpan.FromSeconds(5 + i));
                rise.RepeatBehavior = RepeatBehavior.Forever;
                pollen.BeginAnimation(Canvas.BottomProperty, rise);
                
                DoubleAnimation drift = new DoubleAnimation(200, 400, TimeSpan.FromSeconds(7));
                drift.AutoReverse = true;
                drift.RepeatBehavior = RepeatBehavior.Forever;
                pollen.BeginAnimation(Canvas.LeftProperty, drift);
            }
        }

        private static void CreateZhuVisuals(Canvas canvas)
        {
            // Bamboo Stalks
            for (int i = 0; i < 5; i++)
            {
                Rectangle stalk = new Rectangle { Width = 15, Height = 1000, Fill = new SolidColorBrush(Color.FromArgb(40, 85, 107, 47)) }; // OliveDrab
                canvas.Children.Add(stalk);
                Canvas.SetLeft(stalk, 50 + i * 150);
                Canvas.SetTop(stalk, 0);
                
                // Bamboo Leaves
                for (int j = 0; j < 8; j++)
                {
                    Path leaf = new Path();
                    leaf.Data = Geometry.Parse("M 0,0 Q 30,10 60,0");
                    leaf.Stroke = new SolidColorBrush(Color.FromArgb(60, 85, 107, 47));
                    leaf.StrokeThickness = 4;
                    canvas.Children.Add(leaf);
                    Canvas.SetLeft(leaf, 50 + i * 150);
                    Canvas.SetTop(leaf, 100 + j * 120);
                    
                    RotateTransform rotate = new RotateTransform(20);
                    leaf.RenderTransform = rotate;
                    
                    DoubleAnimation flutter = new DoubleAnimation(15, 25, TimeSpan.FromSeconds(1 + i * 0.2));
                    flutter.AutoReverse = true;
                    flutter.RepeatBehavior = RepeatBehavior.Forever;
                    rotate.BeginAnimation(RotateTransform.AngleProperty, flutter);
                }
            }
        }

        private static void CreateJuVisuals(Canvas canvas)
        {
            // Chrysanthemum Petals (Radial Pattern)
            Canvas flowerGroup = new Canvas { Width = 400, Height = 400 };
            Canvas.SetRight(flowerGroup, -100);
            Canvas.SetBottom(flowerGroup, -100);
            canvas.Children.Add(flowerGroup);

            for (int i = 0; i < 24; i++)
            {
                Path petal = new Path();
                petal.Data = Geometry.Parse("M 0,0 Q 50,-20 150,0 Q 50,20 0,0");
                petal.Fill = new SolidColorBrush(Color.FromArgb(30, 218, 165, 32)); // GoldenRod
                petal.RenderTransformOrigin = new Point(0, 0);
                
                TransformGroup tg = new TransformGroup();
                tg.Children.Add(new RotateTransform(i * 15));
                tg.Children.Add(new TranslateTransform(200, 200));
                petal.RenderTransform = tg;
                
                flowerGroup.Children.Add(petal);
            }

            // Slow Rotation of the whole flower
            RotateTransform flowerRotate = new RotateTransform(0, 200, 200);
            flowerGroup.RenderTransform = flowerRotate;
            DoubleAnimation rotateAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(60));
            rotateAnim.RepeatBehavior = RepeatBehavior.Forever;
            flowerRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);

            // Floating Gold Dust
            for (int i = 0; i < 20; i++)
            {
                Ellipse dust = new Ellipse { Width = 3, Height = 3, Fill = Brushes.Gold, Opacity = 0.5 };
                canvas.Children.Add(dust);
                Canvas.SetLeft(dust, new Random(i).Next(0, 800));
                Canvas.SetTop(dust, new Random(i*2).Next(0, 600));

                DoubleAnimation fade = new DoubleAnimation(0.2, 0.8, TimeSpan.FromSeconds(2 + i % 3));
                fade.AutoReverse = true;
                fade.RepeatBehavior = RepeatBehavior.Forever;
                dust.BeginAnimation(UIElement.OpacityProperty, fade);
            }
        }
    }
}
