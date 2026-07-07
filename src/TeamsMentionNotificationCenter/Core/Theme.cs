using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Core;

/// <summary>
/// Zentrales Farb-/Stilsystem im Stil der Windows-11-Einstellungs-App: Paletten für Hell/Dunkel
/// (Modus „System" folgt Windows live), Fluent-artige Styles für die Standard-Controls (Buttons mit
/// dezentem Hover, abgerundete Eingabefelder, Seitenleisten-Navigation für TabControl, Kacheln) und
/// dunkle Titelleisten. Farben liegen als DynamicResource in den Application-Ressourcen – ein
/// Moduswechsel färbt alle offenen Fenster live um.
/// </summary>
public static class Theme
{
    public static event EventHandler? Changed;

    private static AppThemeMode _mode = AppThemeMode.System;
    private static bool _stylesLoaded;

    /// <summary>Effektiv aktiver Modus (System aufgelöst).</summary>
    public static bool IsDark { get; private set; }

    public static void Initialize(AppThemeMode mode)
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _mode == AppThemeMode.System)
                Application.Current?.Dispatcher.BeginInvoke(() => Apply(_mode));
        };
        Apply(mode);
    }

    public static void Apply(AppThemeMode mode)
    {
        _mode = mode;
        IsDark = mode == AppThemeMode.Dark || (mode == AppThemeMode.System && SystemPrefersDark());
        var res = Application.Current.Resources;

        void Set(string key, string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            res[key] = brush;
        }

        if (IsDark)
        {
            Set("ThBg", "#202020");
            Set("ThCard", "#2B2B2B");
            Set("ThCardBorder", "#1A1A1A");
            Set("ThText", "#FFFFFF");
            Set("ThTextDim", "#C9C9C9");
            Set("ThCtrlBg", "#333333");
            Set("ThCtrlBorder", "#454545");
            Set("ThHover", "#3A3A3A");
            Set("ThPressed", "#2E2E2E");
            Set("ThNavHover", "#2D2D2D");
            Set("ThNavSelected", "#333333");
            Set("ThAccent", "#4FA3E3");
            Set("ThAccentText", "#FFFFFF");
            Set("ThScroll", "#5F5F5F");
            Set("ThScrollHover", "#7A7A7A");
        }
        else
        {
            Set("ThBg", "#F3F3F3");
            Set("ThCard", "#FFFFFF");
            Set("ThCardBorder", "#E5E5E5");
            Set("ThText", "#1B1B1B");
            Set("ThTextDim", "#5D5D5D");
            Set("ThCtrlBg", "#FBFBFB");
            Set("ThCtrlBorder", "#D5D5D5");
            Set("ThHover", "#F0F0F0");
            Set("ThPressed", "#E8E8E8");
            Set("ThNavHover", "#EAEAEA");
            Set("ThNavSelected", "#E8E8E8");
            Set("ThAccent", "#0F6CBD");
            Set("ThAccentText", "#FFFFFF");
            Set("ThScroll", "#A0A0A0");
            Set("ThScrollHover", "#8A8A8A");
        }

        if (!_stylesLoaded)
        {
            res.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(StylesXaml));
            _stylesLoaded = true;
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    /// <summary>Fenster ins Theme einhängen: Hintergrund-/Textfarbe dynamisch, dunkle Titelleiste,
    /// und gegen das weiße Erstbild: unsichtbar starten und erst nach dem ersten fertigen Rendern
    /// einblenden (Windows zeigt Fenster sonst weiß, bis WPF den ersten Frame präsentiert).</summary>
    public static void Prepare(Window w)
    {
        w.SetResourceReference(Control.BackgroundProperty, "ThBg");
        w.SetResourceReference(Control.ForegroundProperty, "ThText");
        w.SourceInitialized += (_, _) => ApplyTitleBar(w);
        w.Opacity = 0;
        w.ContentRendered += (_, _) => w.Opacity = 1;
        EventHandler onChanged = (_, _) => ApplyTitleBar(w);
        Changed += onChanged;
        w.Closed += (_, _) => Changed -= onChanged;
    }

    private static void ApplyTitleBar(Window w)
    {
        try
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            int dark = IsDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        }
        catch { /* ältere Windows-Builds */ }
    }

    /// <summary>Setzt die dünne Win11-Scroll-Optik auf einen ScrollViewer (explizit statt implizit).</summary>
    public static void ThinScroll(ScrollViewer sv) =>
        sv.SetResourceReference(FrameworkElement.StyleProperty, "ThScrollViewer");

    /// <summary>Win11-„Kachel": Karte mit Rundung, Rahmen und Innenabstand.</summary>
    public static Border Card(UIElement child)
    {
        var b = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 11, 14, 11),
            Margin = new Thickness(0, 3, 0, 3),
            Child = child
        };
        b.SetResourceReference(Border.BackgroundProperty, "ThCard");
        b.SetResourceReference(Border.BorderBrushProperty, "ThCardBorder");
        return b;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // Implizite Styles für die Standard-Controls (Fluent-/Win11-Optik). Alle Farben per
    // DynamicResource, damit der Moduswechsel live wirkt.
    private const string StylesXaml = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Dünne Win11-Scrollbar. Als benannter Style, damit die ScrollViewer-Vorlage sie EXPLIZIT
       setzen kann – implizite Styles erreichen Template-Innereien nicht zuverlässig. -->
  <ControlTemplate x:Key="ThinRepeatButton" TargetType="RepeatButton">
    <Border Background="Transparent"/>
  </ControlTemplate>

  <!-- Win11-Settings-Verhalten: im Ruhezustand nur ein 3-px-Strich am Rand, beim Überfahren
       der (12 px breiten, unsichtbaren) Trefferfläche verbreitert sich der Daumen auf 8 px. -->
  <ControlTemplate x:Key="ThinScrollBarVertical" TargetType="ScrollBar">
    <Grid Background="Transparent">
      <Track x:Name="PART_Track" IsDirectionReversed="True">
        <Track.DecreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageUpCommand" Focusable="False" Template="{StaticResource ThinRepeatButton}"/>
        </Track.DecreaseRepeatButton>
        <Track.IncreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageDownCommand" Focusable="False" Template="{StaticResource ThinRepeatButton}"/>
        </Track.IncreaseRepeatButton>
        <Track.Thumb>
          <Thumb>
            <Thumb.Template>
              <ControlTemplate TargetType="Thumb">
                <Grid Background="Transparent">
                  <Border x:Name="tb" Width="3" HorizontalAlignment="Right" Margin="0,0,2,0"
                          CornerRadius="4" Background="{DynamicResource ThScroll}"/>
                </Grid>
                <ControlTemplate.Triggers>
                  <DataTrigger Binding="{Binding IsMouseOver, RelativeSource={RelativeSource AncestorType=ScrollBar}}" Value="True">
                    <DataTrigger.EnterActions>
                      <BeginStoryboard>
                        <Storyboard>
                          <DoubleAnimation Storyboard.TargetName="tb" Storyboard.TargetProperty="Width"
                                           To="8" Duration="0:0:0.12">
                            <DoubleAnimation.EasingFunction>
                              <CubicEase EasingMode="EaseOut"/>
                            </DoubleAnimation.EasingFunction>
                          </DoubleAnimation>
                        </Storyboard>
                      </BeginStoryboard>
                    </DataTrigger.EnterActions>
                    <DataTrigger.ExitActions>
                      <BeginStoryboard>
                        <Storyboard>
                          <DoubleAnimation Storyboard.TargetName="tb" Storyboard.TargetProperty="Width"
                                           To="3" Duration="0:0:0.18">
                            <DoubleAnimation.EasingFunction>
                              <CubicEase EasingMode="EaseOut"/>
                            </DoubleAnimation.EasingFunction>
                          </DoubleAnimation>
                        </Storyboard>
                      </BeginStoryboard>
                    </DataTrigger.ExitActions>
                    <Setter TargetName="tb" Property="Background" Value="{DynamicResource ThScrollHover}"/>
                  </DataTrigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Thumb.Template>
          </Thumb>
        </Track.Thumb>
      </Track>
    </Grid>
  </ControlTemplate>

  <ControlTemplate x:Key="ThinScrollBarHorizontal" TargetType="ScrollBar">
    <Grid Background="Transparent">
      <Track x:Name="PART_Track">
        <Track.DecreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageLeftCommand" Focusable="False" Template="{StaticResource ThinRepeatButton}"/>
        </Track.DecreaseRepeatButton>
        <Track.IncreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageRightCommand" Focusable="False" Template="{StaticResource ThinRepeatButton}"/>
        </Track.IncreaseRepeatButton>
        <Track.Thumb>
          <Thumb>
            <Thumb.Template>
              <ControlTemplate TargetType="Thumb">
                <Grid Background="Transparent">
                  <Border x:Name="tb" Height="3" VerticalAlignment="Bottom" Margin="0,0,0,2"
                          CornerRadius="4" Background="{DynamicResource ThScroll}"/>
                </Grid>
                <ControlTemplate.Triggers>
                  <DataTrigger Binding="{Binding IsMouseOver, RelativeSource={RelativeSource AncestorType=ScrollBar}}" Value="True">
                    <DataTrigger.EnterActions>
                      <BeginStoryboard>
                        <Storyboard>
                          <DoubleAnimation Storyboard.TargetName="tb" Storyboard.TargetProperty="Height"
                                           To="8" Duration="0:0:0.12">
                            <DoubleAnimation.EasingFunction>
                              <CubicEase EasingMode="EaseOut"/>
                            </DoubleAnimation.EasingFunction>
                          </DoubleAnimation>
                        </Storyboard>
                      </BeginStoryboard>
                    </DataTrigger.EnterActions>
                    <DataTrigger.ExitActions>
                      <BeginStoryboard>
                        <Storyboard>
                          <DoubleAnimation Storyboard.TargetName="tb" Storyboard.TargetProperty="Height"
                                           To="3" Duration="0:0:0.18">
                            <DoubleAnimation.EasingFunction>
                              <CubicEase EasingMode="EaseOut"/>
                            </DoubleAnimation.EasingFunction>
                          </DoubleAnimation>
                        </Storyboard>
                      </BeginStoryboard>
                    </DataTrigger.ExitActions>
                    <Setter TargetName="tb" Property="Background" Value="{DynamicResource ThScrollHover}"/>
                  </DataTrigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Thumb.Template>
          </Thumb>
        </Track.Thumb>
      </Track>
    </Grid>
  </ControlTemplate>

  <Style x:Key="ThinScrollBar" TargetType="ScrollBar">
    <Setter Property="Width" Value="12"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Template" Value="{StaticResource ThinScrollBarVertical}"/>
    <Style.Triggers>
      <Trigger Property="Orientation" Value="Horizontal">
        <Setter Property="Width" Value="Auto"/>
        <Setter Property="Height" Value="12"/>
        <Setter Property="Template" Value="{StaticResource ThinScrollBarHorizontal}"/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style TargetType="ScrollBar" BasedOn="{StaticResource ThinScrollBar}"/>

  <!-- ScrollViewer-Vorlage, die die dünne Leiste garantiert verwendet. Wird gezielt per
       Style-Referenz auf unsere Scroll-Bereiche gesetzt (nicht implizit, um z. B. das
       TextBox-Innenleben nicht anzufassen). -->
  <Style x:Key="ThScrollViewer" TargetType="ScrollViewer">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ScrollViewer">
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
              <RowDefinition Height="*"/>
              <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <ScrollContentPresenter x:Name="PART_ScrollContentPresenter"
                                    Grid.Column="0" Grid.Row="0"
                                    Margin="{TemplateBinding Padding}"
                                    CanContentScroll="{TemplateBinding CanContentScroll}"/>
            <ScrollBar x:Name="PART_VerticalScrollBar" Grid.Column="1" Grid.Row="0"
                       Style="{StaticResource ThinScrollBar}"
                       Value="{TemplateBinding VerticalOffset}"
                       Maximum="{TemplateBinding ScrollableHeight}"
                       ViewportSize="{TemplateBinding ViewportHeight}"
                       Visibility="{TemplateBinding ComputedVerticalScrollBarVisibility}"/>
            <ScrollBar x:Name="PART_HorizontalScrollBar" Orientation="Horizontal"
                       Grid.Column="0" Grid.Row="1"
                       Style="{StaticResource ThinScrollBar}"
                       Value="{TemplateBinding HorizontalOffset}"
                       Maximum="{TemplateBinding ScrollableWidth}"
                       ViewportSize="{TemplateBinding ViewportWidth}"
                       Visibility="{TemplateBinding ComputedHorizontalScrollBarVisibility}"/>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="Button">
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="Padding" Value="12,5"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="bd" CornerRadius="4" Background="{DynamicResource ThCtrlBg}"
                  BorderBrush="{DynamicResource ThCtrlBorder}" BorderThickness="1"
                  Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="bd" Property="Background" Value="{DynamicResource ThHover}"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="bd" Property="Background" Value="{DynamicResource ThPressed}"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="TextBox">
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="CaretBrush" Value="{DynamicResource ThText}"/>
    <Setter Property="SelectionBrush" Value="{DynamicResource ThAccent}"/>
    <Setter Property="Padding" Value="7,4"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TextBox">
          <Border x:Name="bd" CornerRadius="4" Background="{DynamicResource ThCtrlBg}"
                  BorderBrush="{DynamicResource ThCtrlBorder}" BorderThickness="1"
                  Padding="{TemplateBinding Padding}">
            <ScrollViewer x:Name="PART_ContentHost" Margin="0" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocusWithin" Value="True">
              <Setter TargetName="bd" Property="BorderBrush" Value="{DynamicResource ThAccent}"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="CheckBox">
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="CheckBox">
          <StackPanel Orientation="Horizontal" Background="Transparent">
            <Border x:Name="box" Width="18" Height="18" CornerRadius="4"
                    Background="{DynamicResource ThCtrlBg}"
                    BorderBrush="{DynamicResource ThCtrlBorder}" BorderThickness="1"
                    VerticalAlignment="Center">
              <Path x:Name="check" Data="M 3.5 9 L 7.5 13 L 14.5 4.5" Stroke="{DynamicResource ThAccentText}"
                    StrokeThickness="2" Visibility="Collapsed" Stretch="None"/>
            </Border>
            <ContentPresenter Margin="9,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="box" Property="Background" Value="{DynamicResource ThAccent}"/>
              <Setter TargetName="box" Property="BorderBrush" Value="{DynamicResource ThAccent}"/>
              <Setter TargetName="check" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="box" Property="BorderBrush" Value="{DynamicResource ThAccent}"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ComboBoxItem">
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBoxItem">
          <Border x:Name="bd" CornerRadius="4" Padding="9,6" Margin="2,1" Background="Transparent">
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="bd" Property="Background" Value="{DynamicResource ThHover}"/>
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="bd" Property="Background" Value="{DynamicResource ThNavSelected}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ComboBox">
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBox">
          <Grid>
            <ToggleButton x:Name="toggle" Focusable="False" ClickMode="Press"
                          IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
              <ToggleButton.Template>
                <ControlTemplate TargetType="ToggleButton">
                  <Border x:Name="bd" CornerRadius="4" Background="{DynamicResource ThCtrlBg}"
                          BorderBrush="{DynamicResource ThCtrlBorder}" BorderThickness="1">
                    <Grid>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                      </Grid.ColumnDefinitions>
                      <ContentPresenter Grid.Column="0" Margin="9,5,4,5" VerticalAlignment="Center"/>
                      <Path Grid.Column="1" Data="M 0 0 L 4.5 4.5 L 9 0" Stroke="{DynamicResource ThTextDim}"
                            StrokeThickness="1.6" Margin="6,0,10,0" VerticalAlignment="Center"/>
                    </Grid>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                      <Setter TargetName="bd" Property="Background" Value="{DynamicResource ThHover}"/>
                    </Trigger>
                    <Trigger Property="IsChecked" Value="True">
                      <Setter TargetName="bd" Property="BorderBrush" Value="{DynamicResource ThAccent}"/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
              <ContentPresenter Content="{TemplateBinding SelectionBoxItem}"
                                ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                TextElement.Foreground="{DynamicResource ThText}"/>
            </ToggleButton>
            <Popup x:Name="popup" Placement="Bottom" AllowsTransparency="True" PopupAnimation="Fade"
                   IsOpen="{TemplateBinding IsDropDownOpen}" StaysOpen="False"
                   MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}">
              <Border CornerRadius="8" Background="{DynamicResource ThCard}"
                      BorderBrush="{DynamicResource ThCardBorder}" BorderThickness="1"
                      Padding="2" Margin="0,4,12,12">
                <Border.Effect>
                  <DropShadowEffect BlurRadius="14" ShadowDepth="2" Opacity="0.35"/>
                </Border.Effect>
                <ScrollViewer MaxHeight="320" Style="{StaticResource ThScrollViewer}">
                  <ItemsPresenter/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="TabControl">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TabControl">
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="Auto"/>
              <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <ScrollViewer Grid.Column="0" VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Disabled" Margin="0,0,10,0"
                          Style="{StaticResource ThScrollViewer}">
              <StackPanel IsItemsHost="True" Width="196"/>
            </ScrollViewer>
            <ContentPresenter Grid.Column="1" ContentSource="SelectedContent"/>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="TabItem">
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TabItem">
          <Border x:Name="bd" CornerRadius="6" Margin="0,2" Padding="10,8" Background="Transparent">
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
              </Grid.ColumnDefinitions>
              <Rectangle x:Name="pill" Grid.Column="0" Width="3" Height="16" RadiusX="1.5" RadiusY="1.5"
                         Fill="{DynamicResource ThAccent}" Visibility="Hidden" VerticalAlignment="Center"/>
              <ContentPresenter Grid.Column="1" ContentSource="Header" Margin="9,0,0,0"
                                VerticalAlignment="Center"/>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="bd" Property="Background" Value="{DynamicResource ThNavHover}"/>
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="bd" Property="Background" Value="{DynamicResource ThNavSelected}"/>
              <Setter TargetName="pill" Property="Visibility" Value="Visible"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="Expander">
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Expander">
          <StackPanel>
            <ToggleButton x:Name="header" IsChecked="{Binding IsExpanded, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"
                          Focusable="False">
              <ToggleButton.Template>
                <ControlTemplate TargetType="ToggleButton">
                  <Border x:Name="hbd" CornerRadius="4" Padding="8,7" Background="Transparent">
                    <Grid>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                      </Grid.ColumnDefinitions>
                      <Path x:Name="chev" Grid.Column="0" Data="M 0 0 L 4.5 4.5 L 9 0"
                            Stroke="{DynamicResource ThTextDim}" StrokeThickness="1.6"
                            VerticalAlignment="Center" Margin="2,1,10,0" RenderTransformOrigin="0.5,0.5">
                        <Path.RenderTransform>
                          <RotateTransform Angle="-90"/>
                        </Path.RenderTransform>
                      </Path>
                      <ContentPresenter Grid.Column="1" VerticalAlignment="Center"/>
                    </Grid>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                      <Setter TargetName="hbd" Property="Background" Value="{DynamicResource ThHover}"/>
                    </Trigger>
                    <Trigger Property="IsChecked" Value="True">
                      <Setter TargetName="chev" Property="RenderTransform">
                        <Setter.Value>
                          <RotateTransform Angle="0"/>
                        </Setter.Value>
                      </Setter>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
              <ContentPresenter Content="{TemplateBinding Header}" TextElement.Foreground="{DynamicResource ThText}"/>
            </ToggleButton>
            <ContentPresenter x:Name="content" Visibility="Collapsed" Margin="24,2,0,2"/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property="IsExpanded" Value="True">
              <Setter TargetName="content" Property="Visibility" Value="Visible"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="Separator">
    <Setter Property="Background" Value="{DynamicResource ThCardBorder}"/>
    <Setter Property="Height" Value="1"/>
    <Setter Property="Margin" Value="6,3"/>
  </Style>

  <Style TargetType="ContextMenu">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="HasDropShadow" Value="False"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ContextMenu">
          <!-- Kein Margin/Effekt-Schatten: der transparente Randbereich würde beim Auf-/Zuklappen
               des (nicht layered) Popups für einen Frame WEISS aufblitzen. -->
          <Border CornerRadius="8" Background="{DynamicResource ThCard}"
                  BorderBrush="{DynamicResource ThCardBorder}" BorderThickness="1" Padding="4">
            <ItemsPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="MenuItem">
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="MenuItem">
          <Border x:Name="bd" CornerRadius="4" Padding="9,7" Margin="2,1" Background="Transparent">
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="18"/>
                <ColumnDefinition Width="*"/>
              </Grid.ColumnDefinitions>
              <Path x:Name="check" Grid.Column="0" Data="M 1 5 L 4.5 8.5 L 11.5 0.5"
                    Stroke="{DynamicResource ThAccent}" StrokeThickness="2"
                    Visibility="Hidden" VerticalAlignment="Center"/>
              <ContentPresenter Grid.Column="1" ContentSource="Header"
                                VerticalAlignment="Center" RecognizesAccessKey="True"/>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsHighlighted" Value="True">
              <Setter TargetName="bd" Property="Background" Value="{DynamicResource ThHover}"/>
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="check" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ToolTip">
    <Setter Property="Foreground" Value="{DynamicResource ThText}"/>
    <Setter Property="Background" Value="{DynamicResource ThCard}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource ThCardBorder}"/>
  </Style>

</ResourceDictionary>
""";
}
