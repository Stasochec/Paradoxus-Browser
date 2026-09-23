using System.Windows;
using System.Windows.Controls;

namespace ParadoxusBrowser.Views.Controls
{
    public partial class ToggleSwitch : UserControl
    {
        public static readonly DependencyProperty IsToggledProperty =
            DependencyProperty.Register(
                nameof(IsToggled),
                typeof(bool),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool IsToggled
        {
            get => (bool)GetValue(IsToggledProperty);
            set => SetValue(IsToggledProperty, value);
        }

        public ToggleSwitch()
        {
            InitializeComponent();
        }
    }
}
