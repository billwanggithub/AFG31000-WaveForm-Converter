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
        public ViewModel()
        {

        }

        public MainWindow window;

        [ObservableProperty]
        public ObservableCollection<WaveformListData>? waveformListDataCollection = new();
        [ObservableProperty]
        int waveformListSelectedIndex = 0;
        [ObservableProperty]
        WaveformListData waveformEditItem = new(0.001m, 1.0);

        [ObservableProperty]
        string? inputFilePath = string.Empty;
        [ObservableProperty]
        double timeUnit = 0;
        [ObservableProperty]
        double outputFrequency = 0;
        [ObservableProperty]
        int progressValue = 0;
        [ObservableProperty]
        int progressMax = 0;

        ConsoleControl.WPF.ConsoleControl consoleControl { get; set; }
        string? outputFilePath = string.Empty;

        SemaphoreSlim semaphoreConsole = new(1, 1);


        string outCsvString = "";



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

        [RelayCommand]
        public async Task LoadFile(object? param)
        {
            InputFilePath = await SelectInputFile();

            (List<double>? dataX, List<double>? dataY) = await ParseFile(InputFilePath);

            if ((dataX is null) || (dataY is null))
            {
                await WriteConsole("Data Error\n", Colors.Red);
                return;
            }

            await PlotData(dataX, dataY);

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

            if (outCsvString != null)
            {
                File.WriteAllText(saveFileDialog.FileName, outCsvString);
                await WriteConsole($"Write to {saveFileDialog.FileName}\n", Colors.LightGreen);
            }
        }

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

        async Task<(List<double>?, List<double>?)> ParseFile(string? filePath)
        {
            await WriteConsole("Parse File\n", Colors.LightBlue);
            if (filePath is null)
            {
                return (null, null);
            }

            List<double> dataX = new();
            List<double> dataY = new();


            string outLines = "";
            string[] lines = await File.ReadAllLinesAsync(filePath);

            // Calculate precision
            await WriteConsole("Calculation Time Unit\n", Colors.LightBlue);
            decimal timeResolution = 1m;
            int maxDigit = 0;
            int index = 0;
            ProgressMax = lines.Length - 1;
            foreach (string line in lines)
            {
                ProgressValue = index;
                string[] param = line.Split(new char[] { ',' });

                // calculate the decimal digital 
                string[] timeStringArray = decimal.Parse(param[0], System.Globalization.NumberStyles.Float).ToString().Split(new char[] { '.' });
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
            await WriteConsole("Add Data points\n", Colors.LightBlue);
            index = 0;
            int order = 0;
            //WaveformListDataCollection = new();
            foreach (string line in lines)
            {
                string[] param = line.Split(new char[] { ',' });
                decimal duration = decimal.Parse(param[0], System.Globalization.NumberStyles.Float);
                int count = (int)(duration * (decimal)Math.Pow(10.0, (double)maxDigit));
                double voltage = double.Parse(param[1]);
                // Add data to list
                WaveformListDataCollection.Add(new(duration, voltage));

                await WriteConsole($"line,count, duration, voltage = {++index}, {count}, {duration}, {decimal.Parse(param[1])}\n");


                ProgressMax = count - 1;
                await Task.Run(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        double time = (order + 1) * TimeUnit;
                        dataX.Add(time);
                        dataY.Add(voltage);
                        outLines += $"{time:E},{voltage:N3}\n";// order.ToString() + "," + param[0] + "\n";
                        order++;

                        if ((count % 1000) == 0)
                        {
                            ProgressValue = i;
                        }
                    }
                });
            }
            ProgressValue = order;
            OutputFrequency = Math.Round(1.0 / (TimeUnit * (double)order), 9);
            await WriteConsole($"Total Time = {dataX.Last()} Sec.\nTotal Data points = {dataX.Count}\n", Colors.Orange);

            outCsvString = outLines;
            return (dataX, dataY);
        }

        async Task PlotData(List<double>? dataX, List<double>? dataY)
        {
            WpfPlot WpfPlot1 = window.WpfPlot1;
            if ((dataX is null) || (dataY is null))
            {
                await WriteConsole("Data Error\n", Colors.Red);
                return;
            }

            WpfPlot1.Plot.Clear();
            await Task.Run(() =>
            {

                SignalPlotXY signalPlotXY = WpfPlot1.Plot.AddSignalXY(dataX.ToArray(), dataY.ToArray());
                signalPlotXY.LineStyle = LineStyle.DashDot;
            });


            WpfPlot1.Plot.XLabel("Time(s)");
            WpfPlot1.Plot.YLabel("Voltage(V)");
            WpfPlot1.Plot.Title("Voltage vs Time");
            WpfPlot1.Plot.AxisAuto();
            WpfPlot1.Refresh();
        }

        #region WaveformList
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
        #endregion

        #region Help
        [RelayCommand]
        public void AGotoUserGuide(object? param)
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
