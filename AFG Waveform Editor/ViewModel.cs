using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Internet;
using Microsoft.Win32;
using Models;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace AFG_Waveform_Editor
{
    public partial class ViewModel : ObservableObject
    {
        public MainWindow? window;

        [ObservableProperty]
        string? inputFilePath = null;
        [ObservableProperty]
        double timeUnit = 0;
        [ObservableProperty]
        double outputFrequency = 0;
        [ObservableProperty]
        int progressValue = 0;
        [ObservableProperty]
        int progressMax = 0;

        //ConsoleControl.WPF.ConsoleControl consoleControl { get; set; }
        //string? outputFilePath = string.Empty;

        #region Console
        SemaphoreSlim semaphoreConsole = new(1, 1);
        public async Task WriteConsole(string? text, System.Windows.Media.Color? forecolor = null, System.Windows.Media.Color? backcolor = null, bool? isBold = false, string[]? target = null)
        {
            System.Windows.Media.Color color = forecolor ?? Colors.White;

            await semaphoreConsole.WaitAsync();
            window.consoleControl.WriteOutput(text, color);
            if (window is not null)
            {
                await window.Dispatcher.InvokeAsync(() =>
                {
                    window.consoleScrollViewer.ScrollToEnd();
                });
            }
            semaphoreConsole.Release();
        }
        #endregion

        static async Task<string?> SelectInputFile()
        {
            OpenFileDialog openFileDialog = new()
            {
                RestoreDirectory = true,
                Title = "Load Data File",
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
        async Task<ObservableCollection<WaveformListData>?> ParseFileToWaveformList(string filePath)
        {
            await WriteConsole("Parse File\n", Colors.LightBlue);
            if (filePath is null)
            {
                return null;
            }
            string[] lines = await File.ReadAllLinesAsync(filePath);

            ObservableCollection<WaveformListData> waveformLists = new ObservableCollection<WaveformListData>();
            foreach (string line in lines)
            {
                string[] param = line.Split(new char[] { ',' });
                decimal duration = decimal.Parse(param[0], System.Globalization.NumberStyles.Float);
                double voltage = double.Parse(param[1]);
                waveformLists.Add(new(duration, voltage));
                await WriteConsole($"=> duration, voltage = {duration},{voltage}\n");
            }
            return waveformLists;
        }
        async Task<(List<double>?, List<double>?)> ParseWaveformList(ObservableCollection<WaveformListData>? waveformListDataCollection)
        {
            await WriteConsole("Parse  Waveform List\n", Colors.LightBlue);

            List<double> dataX = new();
            List<double> dataY = new();


            // Calculate precision
            await WriteConsole("Calculation Time Unit\n", Colors.LightBlue);
            decimal timeResolution = 1m;
            int maxDigit = 0;
            int index = 0;
            ProgressMax = waveformListDataCollection.Count - 1;
            foreach (WaveformListData row in waveformListDataCollection)
            {
                ProgressValue = index;
                // calculate the decimal digital 
                string[] timeStringArray = decimal.Parse(row.Duration.ToString(), System.Globalization.NumberStyles.Float).ToString().Split(new char[] { '.' });
                if (timeStringArray.Length > 1)
                {
                    if (timeStringArray[1].Length > maxDigit)
                    {
                        maxDigit = timeStringArray[1].Length;
                    }
                }
                await WriteConsole($"line, maxdigit = {++index}, {maxDigit}\n");
            }
            timeResolution = 1m / (decimal)Math.Pow(10.0, (double)maxDigit);
            TimeUnit = (double)timeResolution;
            await WriteConsole($"Time Resolution = {timeResolution} Sec.\n", Colors.Orange);

            // Add data
            await WriteConsole("Add Data points for AWG\n", Colors.LightBlue);
            index = 0;
            int order = 0;
            //WaveformListDataCollection = new();
            foreach (WaveformListData row in waveformListDataCollection)
            {
                decimal duration = row.Duration;
                int count = (int)(duration * (decimal)Math.Pow(10.0, (double)maxDigit));
                double voltage = row.Voltage;
                await WriteConsole($"line,count, duration, voltage = {++index}, {count}, {duration}, {voltage}\n");

                ProgressMax = count - 1;
                await Task.Run(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        double time = (order + 1) * TimeUnit;
                        dataX.Add(time);
                        dataY.Add(voltage);
                        if ((count % 1000) == 0)
                        {
                            ProgressValue = i;
                        }
                        order++;
                    }
                });
            }
            ProgressValue = order;
            OutputFrequency = Math.Round(1.0 / (TimeUnit * (double)order), 9);
            await WriteConsole($"Total Time = {dataX.Last()} Sec.\nTotal Data points = {dataX.Count}\n", Colors.Orange);
            await WriteConsole($"Set AFG31000 Frequency to {OutputFrequency}\n", Colors.Orange);
            return (dataX, dataY);
        }
        async Task PlotData(List<double>? dataX, List<double>? dataY)
        {
            if (window is null)
            {
                return;
            }

            if ((dataX is null) || (dataY is null))
            {
                await WriteConsole("Plot Data Error\n", Colors.Red);
                return;
            }

            WpfPlot wpfPlot = window.WpfPlot1;
            wpfPlot.Plot.XLabel("Time(s)");
            wpfPlot.Plot.YLabel("Voltage(V)");
            wpfPlot.Plot.Title("Voltage vs Time");
            wpfPlot.Plot.Clear();

            await Task.Run(() =>
            {
                SignalPlotXY signalPlotXY = wpfPlot.Plot.AddSignalXY(dataX.ToArray(), dataY.ToArray());
                signalPlotXY.LineStyle = LineStyle.DashDot;
            });

            wpfPlot.Plot.AxisAuto();
            wpfPlot.Refresh();
        }
        async Task SavWaveformListToAwgFormat(List<double>? X, List<double>? Y)
        {
            if (X == null || Y == null)
            {
                await WriteConsole("Data is null", Colors.Red);
                return;
            }

            string csvString = "";
            int count = X.Count;
            ProgressMax = count - 1;
            ProgressValue = 0;
            for (int i = 0; i < count; i++)
            {
                csvString += $"{X[i]},{Y[i]}";
                ProgressValue = i;
            }

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

            File.WriteAllText(saveFileDialog.FileName, csvString);
            await WriteConsole($"Write to {saveFileDialog.FileName}\n", Colors.LightGreen);
        }
        async Task UpdateWaveformPlotAsync()
        {
            (List<double>? X, List<double>? Y) = await ParseWaveformList(WaveformListDataCollection);
            await PlotData(X, Y);
            await SavWaveformListToAwgFormat(X, Y);
        }

        #region WaveformList
        [ObservableProperty]
        public ObservableCollection<WaveformListData>? waveformListDataCollection = new();
        [ObservableProperty]
        int waveformListSelectedIndex = 0;
        [ObservableProperty]
        WaveformListData waveformEditItem = new(0.001m, 1.0);

        [RelayCommand]
        public async Task UpdateWaveformPlot(object? param)
        {
            if ((WaveformListDataCollection == null) || (WaveformListDataCollection.Count == 0))
            {
                await WriteConsole("No Data\n", Colors.Red);
                return;
            }

            await UpdateWaveformPlotAsync();
        }
        [RelayCommand]
        public async Task LoadWaveformList(object? param)
        {
            InputFilePath = await SelectInputFile();
            if (InputFilePath == null)
            {
                await WriteConsole("File Error\n", Colors.Red);
                return;
            }

            var waveforList = await ParseFileToWaveformList(InputFilePath);

            if (waveforList == null)
            {
                await WriteConsole("Parse File Error\n", Colors.Red);
                return;
            }

            WaveformListDataCollection = waveforList;

            (List<double>? X, List<double>? Y) = await ParseWaveformList(waveforList);

            //(List<double>? dataX, List<double>? dataY) = await ParseFile(InputFilePath);

            if ((X is null) || (Y is null))
            {
                await WriteConsole("Parse Data Error\n", Colors.Red);
                return;
            }

            await PlotData(X, Y);

            await SavWaveformListToAwgFormat(X, Y);
        }
        [RelayCommand]
        public async Task SaveWaveformList(object? param)
        {
            if ((WaveformListDataCollection == null) || (WaveformListDataCollection.Count == 0))
            {
                await WriteConsole("No Data\n", Colors.Red);
                return;
            }

            string csvString = "";
            foreach (var item in WaveformListDataCollection)
            {
                csvString += $"{item.Duration}, {item.Voltage}\n";
            }

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

            File.WriteAllText(saveFileDialog.FileName, csvString);
            await WriteConsole($"Write to {saveFileDialog.FileName}\n", Colors.LightGreen);
        }

        [RelayCommand]
        public void AddWaveformList(object? param)
        {
            if (param is not DataGrid) { return; }
            if (WaveformListDataCollection is null) { return; }
            WaveformEditItem = new(0, 0);

            Views.EditWaveformListView dialog = new();
            bool? result = dialog.ShowDialog();

            // If the user clicked the OK button, add the new item to the collection.
            if (result == true)
            {
                WaveformListDataCollection.Add(WaveformEditItem);
            }
        }
        [RelayCommand]
        public void InsertWaveformList(object? param)
        {
            if (param is not DataGrid dataGrid) { return; }
            if (WaveformListDataCollection is null) { return; }
            WaveformEditItem = new(0, 0);

            Views.EditWaveformListView dialog = new();
            bool? result = dialog.ShowDialog();

            // If the user clicked the OK button, add the new item to the collection.
            if (result == true)
            {
                int selectedIndex = dataGrid.Items.IndexOf(dataGrid.SelectedItem);
                WaveformListDataCollection.Insert(selectedIndex + 1, WaveformEditItem);
            }
        }
        #endregion

        #region Help
        [RelayCommand]
        public void GotoUserGuide(object? param)
        {
            InternetHelper.OpenUrl(@"https://github.com/billwanggithub/AFG31000-WaveForm-Converter");
        }
        [RelayCommand]
        public void GotoTektronic(object? param)
        {
            InternetHelper.OpenUrl(@"https://www.tek.com/en/products/signal-generators/arbitrary-function-generator/afg31000");
        }
        #endregion
    }
}
