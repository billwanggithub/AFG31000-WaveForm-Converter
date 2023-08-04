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
        public decimal voltage = 0;

        public WaveformListData(decimal duration, decimal voltage)
        {
            this.Duration = duration;
            this.Voltage = voltage;
        }
    }
}
