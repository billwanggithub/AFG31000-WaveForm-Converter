using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AFG_Waveform_Editor
{
    partial class ViewModel : ObservableObject
    {
        [ObservableProperty]
        string? inputFilePath = string.Empty;
        [ObservableProperty]
        double timeUnit = 0.001;
        [ObservableProperty]
        double outputFrequency = 0;

        WpfPlot WpfPlot1 { get; set; }
        string? outputFilePath = string.Empty;

        public ViewModel(WpfPlot plot)
        {
            WpfPlot1 = plot;
        }


        [RelayCommand]
        public async Task LoadFile(object? param)
        {
            InputFilePath = await SelectFile();
            string? outString = await ParseFile(InputFilePath);
            SaveFileDialog saveFileDialog = new()
            {
                RestoreDirectory = true,
                Title = "Save Waveform",
                DefaultExt = "csv",
                Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            saveFileDialog.ShowDialog();
            if (saveFileDialog.FileName == "")
                return;
            if (outString != null)
            {
                File.WriteAllText(saveFileDialog.FileName, outString);
            }
        }

        static async Task<string?> SelectFile()
        {
            OpenFileDialog openFileDialog = new()
            {
                RestoreDirectory = true,
                Title = "Load Rolling Plot File",
                DefaultExt = "csv",
                Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = true
            };
            //openFileDialog.Reset();
            openFileDialog.ShowDialog();
            if (openFileDialog.FileName == "")
                return null;
            await Task.Delay(0);
            return openFileDialog.FileName;
        }

        async Task<string?> ParseFile(string? filePath)
        {
            if (filePath is null)
            {
                return null;
            }


            List<double> dataX = new();
            List<double> dataY = new();

            int order = 0;
            string outLines = "";
            string[] lines = await File.ReadAllLinesAsync(filePath);
            foreach (string line in lines)
            {
                string[] param = line.Split(new char[] { ',' });
                int count = int.Parse(param[1]);
                for (int i = 0; i < count; i++)
                {
                    double time = order * TimeUnit;
                    double voltage = double.Parse(param[0]);
                    dataX.Add(time);
                    dataY.Add(voltage);
                    outLines += $"{time:E},{voltage:N3}\n";// order.ToString() + "," + param[0] + "\n";
                    order++;
                }
            }
            OutputFrequency = Math.Round(1.0 / (TimeUnit * (double)order), 6);

            List<(decimal, decimal)> data = new();

            WpfPlot1.Plot.Clear();
            SignalPlotXY signalPlotXY = WpfPlot1.Plot.AddSignalXY(dataX.ToArray(), dataY.ToArray());
            WpfPlot1.Plot.XLabel("Time(s)");
            WpfPlot1.Plot.YLabel("Voltage(V)");
            WpfPlot1.Plot.Title("Voltage vs Time");

            WpfPlot1.Plot.AxisAuto();
            WpfPlot1.Refresh();

            return outLines;
        }
    }
}
