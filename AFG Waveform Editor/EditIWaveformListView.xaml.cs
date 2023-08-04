using AFG_Waveform_Editor;
using System.Windows;

namespace Views
{
    /// <summary>
    /// Interaction logic for EditIRollingListView.xaml
    /// </summary>
    public partial class EditWaveformListView : Window
    {
        public EditWaveformListView()
        {
            InitializeComponent();
            DataContext = App.Current.viewModel;
        }

        private void OnOkClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

    }
}
