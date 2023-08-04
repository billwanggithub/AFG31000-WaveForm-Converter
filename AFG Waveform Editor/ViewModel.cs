using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace AFG_Waveform_Editor
{
    partial class ViewModel : ObservableObject
    {
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

        WpfPlot WpfPlot1 { get; set; }
        ConsoleControl.WPF.ConsoleControl consoleControl { get; set; }
        string? outputFilePath = string.Empty;

        SemaphoreSlim semaphoreConsole = new(1, 1);
        MainWindow window;

        public ViewModel(MainWindow window)//, WpfPlot plot, ConsoleControl.WPF.ConsoleControl console)
        {
            this.window = window;
            WpfPlot1 = window.WpfPlot1; // plot;
            consoleControl = window.consoleControl;// console;
        }

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
            await WriteConsole("Parse File\n", Colors.LightBlue);
            if (filePath is null)
            {
                return null;
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
                string[] timeStringArray = param[0].Split(new char[] { '.' });
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
            foreach (string line in lines)
            {
                string[] param = line.Split(new char[] { ',' });
                int count = (int)(decimal.Parse(param[0]) * (decimal)Math.Pow(10.0, (double)maxDigit));
                await WriteConsole($"line,count, voltage = {++index}, {count}, {decimal.Parse(param[1])}\n");

                double voltage = double.Parse(param[1]);
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


            List<(decimal, decimal)> data = new();
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

            return outLines;
        }
    }
}
