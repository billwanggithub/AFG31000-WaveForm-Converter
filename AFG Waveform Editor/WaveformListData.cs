using CommunityToolkit.Mvvm.ComponentModel;

namespace Models
{
    public partial class WaveformListData : ObservableObject
    {
        [ObservableProperty]
        public int order = 0;
        [ObservableProperty]
        public decimal duration = 0;
        [ObservableProperty]
        public double voltage = 0;

        public WaveformListData(decimal duration, double voltage)
        {
            this.Duration = duration;
            this.Voltage = voltage;
        }
    }
}
