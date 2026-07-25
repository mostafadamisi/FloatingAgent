using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using FloatingAgent.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace FloatingAgent;

public partial class MainWindow : Window
{
    private double _normalWidth = 440;
    private double _normalHeight = 640;
    private bool _isMinimized;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        var services = new ServiceCollection();
            services.AddWpfBlazorWebView();
            services.AddSingleton(App.Bridge);
            WebView.HostPage = "wwwroot/index.html";
            WebView.Services = services.BuildServiceProvider();
            WebView.RootComponents.Add(new RootComponent
            {
                Selector = "#app",
                ComponentType = typeof(Blazor.ChatPage)
            });

        App.Bridge.RequestEvent += OnBridgeRequest;
        App.Bridge.ScreenshotCaptured += OnScreenshotCaptured;
        App.ScreenAutomation.OwnerWindow = this;
        LoadConfig();
    }

    private void OnBridgeRequest(string action)
    {
        Dispatcher.Invoke(() =>
        {
            switch (action)
            {
                case "minimize": MinimizeToBubble(); break;
                case "close": Application.Current.Shutdown(); break;
                case "config": LoadConfig(); break;
            }
        });
    }

    private void LoadConfig()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null)
                {
                    Topmost = cfg.Topmost;
                    Width = cfg.WindowWidth;
                    Height = cfg.WindowHeight;
                }
            }
            catch { }
        }
        PositionWindow();
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
    }

    public void MinimizeToBubble()
    {
        _normalWidth = Width;
        _normalHeight = Height;
        Width = 80; Height = 80;
        Left = 14; Top = 14;
        NormalView.Visibility = Visibility.Collapsed;
        BubbleView.Visibility = Visibility.Visible;
        _isMinimized = true;
    }

    public void RestoreFromBubble()
    {
        Width = _normalWidth;
        Height = _normalHeight;
        PositionWindow();
        NormalView.Visibility = Visibility.Visible;
        BubbleView.Visibility = Visibility.Collapsed;
        _isMinimized = false;
    }

    private void ToggleMinimize()
    {
        if (_isMinimized)
        {
            Width = _normalWidth; Height = _normalHeight;
            PositionWindow();
            NormalView.Visibility = Visibility.Visible;
            BubbleView.Visibility = Visibility.Collapsed;
            _isMinimized = false;
        }
        else
        {
            _normalWidth = Width; _normalHeight = Height;
            Width = 80; Height = 80; Left = 14; Top = 14;
            NormalView.Visibility = Visibility.Collapsed;
            BubbleView.Visibility = Visibility.Visible;
            _isMinimized = true;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!_isMinimized) try { DragMove(); } catch { }
        else RestoreFromBubble();
    }

    private void OnScreenshotCaptured()
    {
        if (!_isMinimized) return;
        Dispatcher.Invoke(async () =>
        {
            var pulse = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0, To = 1.15,
                Duration = TimeSpan.FromMilliseconds(150),
                AutoReverse = true
            };
            var rt = new System.Windows.Media.ScaleTransform(1, 1);
            BubbleView.RenderTransform = rt;
            BubbleView.RenderTransformOrigin = new Point(0.5, 0.5);
            rt.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, pulse);
            rt.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, pulse);
            await Task.Delay(300);
        });
    }
}
