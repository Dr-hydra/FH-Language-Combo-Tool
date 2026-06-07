using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FH.LanguageComboTool.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        QING.Core.Main.Init();

        ToolTipService.InitialShowDelayProperty.OverrideMetadata(
            typeof(DependencyObject),
            new FrameworkPropertyMetadata(300));
        ToolTipService.BetweenShowDelayProperty.OverrideMetadata(
            typeof(DependencyObject),
            new FrameworkPropertyMetadata(400));
        ToolTipService.ShowDurationProperty.OverrideMetadata(
            typeof(DependencyObject),
            new FrameworkPropertyMetadata(9999999));
        ToolTipService.PlacementProperty.OverrideMetadata(
            typeof(DependencyObject),
            new FrameworkPropertyMetadata(PlacementMode.Bottom));

        base.OnStartup(e);
    }
}
