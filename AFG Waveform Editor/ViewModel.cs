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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AFG_Waveform_Editor
{
    public partial class ViewModel : ObservableObject
    {
        public ViewModel()
        {

        }

        public async void Init()
        {
            if (window is null) { return; }
            window.Title += " Ver:" + GetWpfFileVersion();
            await WriteConsole($"File Version: {GetWpfFileVersion()}\nBuild Date: {GetWpfBuildDate()}\n");

            //var menuItem = new MenuItem();
            //menuItem.Name = "TestMenu";
            //menuItem.Header = "TestHearer";
            //window.WpfPlot1.ContextMenu.Items.Add(menuItem);
        }

        public MainWindow? window;

        #region GUI Binding
        [ObservableProperty]
        string? inputFilePath = null;
        [ObservableProperty]
        decimal timeUnit = 0;
        [ObservableProperty]
        decimal outputFrequency = 0;
        [ObservableProperty]
        int progressValue = 0;
        [ObservableProperty]
        int progressMax = 0;
        #endregion

        List<decimal>? PlotXData { get; set; } = null;
        List<decimal>? PlotYData { get; set; } = null;

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
                decimal voltage = decimal.Parse(param[1]);
                waveformLists.Add(new(duration, voltage));
                await WriteConsole($"=> duration, voltage = {duration},{voltage}\n");
            }
            return waveformLists;
        }
        async Task<(List<decimal>?, List<decimal>?)> ParseWaveformList(ObservableCollection<WaveformListData>? waveformListDataCollection)
        {
            if (waveformListDataCollection == null)
            {
                return (null, null);
            }
            await WriteConsole("Parse  Waveform List\n", Colors.LightBlue);

            List<decimal> dataX = new();
            List<decimal> dataY = new();


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
            TimeUnit = (decimal)timeResolution;
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
                decimal voltage = row.Voltage;
                await WriteConsole($"line,count, duration, voltage = {++index}, {count}, {duration}, {voltage}\n");
                GC.Collect();

                ProgressMax = count - 1;
                await Task.Run(async () =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        decimal time = (order + 1) * TimeUnit;
                        dataX.Add(time);
                        dataY.Add(voltage);
                        if ((i % 1000) == 0)
                        {
                            ProgressValue = i;
                            await Task.Delay(1);
                        }
                        order++;
                    }
                });
            }
            ProgressValue = order;
            OutputFrequency = Math.Round(1.0m / (TimeUnit * (decimal)order), 9);
            await WriteConsole($"Total Time = {dataX.Last()} Sec.\nTotal Data points = {dataX.Count}\n", Colors.Orange);
            await WriteConsole($"Set AFG31000 Frequency to {OutputFrequency}\n", Colors.Orange);
            return (dataX, dataY);
        }
        async Task PlotData(List<decimal>? dataX, List<decimal>? dataY)
        {
            GC.Collect();
            if (window is null)
            {
                return;
            }

            if ((dataX is null) || (dataY is null))
            {
                await WriteConsole("Plot Data Error\n", Colors.Red);
                return;
            }

            await WriteConsole("Convert Plot Data to Double\n", Colors.LightBlue);

            double[] X = dataX.Select(x => (double)x).ToArray();
            double[] Y = dataY.Select(x => (double)x).ToArray();

            await WriteConsole("Update Plot Data\n", Colors.LightBlue);
            WpfPlot wpfPlot = window.WpfPlot1;
            wpfPlot.Plot.XLabel("Time(s)");
            wpfPlot.Plot.YLabel("Voltage(V)");
            wpfPlot.Plot.Title("Voltage vs Time");
            wpfPlot.Plot.Clear();

            await Task.Run(async () =>
            {
                SignalPlotXY signalPlotXY = wpfPlot.Plot.AddSignalXY(X, Y);
                signalPlotXY.LineStyle = LineStyle.DashDot;
                await Task.Delay(1);
            });

            wpfPlot.Plot.AxisAuto();
            wpfPlot.Refresh();
        }
        async Task SavWaveformListToAwgFormat(List<decimal>? X, List<decimal>? Y)
        {
            await WriteConsole("Convert Waveform Data to AFG31000 Format\n", Colors.LightBlue);


            if (X == null || Y == null)
            {
                await WriteConsole("Data is null", Colors.Red);
                return;
            }

            string csvString = "";
            int count = X.Count;
            ProgressMax = count - 1;
            ProgressValue = 0;
            await WriteConsole($"Line Count = {count}\n");
            for (int i = 0; i < count; i++)
            {
                csvString += $"{X[i]},{Y[i]}\n";

                if ((i % 1000 == 0))
                {
                    ProgressValue = i;
                    await WriteConsole($"Process {i} lines, {(i * 100 / count):N1}%\n");
                    await Task.Delay(1);
                    GC.Collect();
                }
            }
            await WriteConsole($"Process {count} lines, {(count * 100 / count):N1}%\n");
            GC.Collect();

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
            (PlotXData, PlotYData) = await ParseWaveformList(WaveformListDataCollection);
            await PlotData(PlotXData, PlotYData);
        }

        [RelayCommand]
        public async Task SaveAsAfg3xxxx(object? param)
        {
            await SavWaveformListToAwgFormat(PlotXData, PlotYData);
        }

        #region WaveformList
        [ObservableProperty]
        public ObservableCollection<WaveformListData>? waveformListDataCollection = new();
        [ObservableProperty]
        int waveformListSelectedIndex = 0;
        [ObservableProperty]
        WaveformListData waveformEditItem = new(0.001m, 1.0m);

        [RelayCommand]
        public async Task UpdateWaveformPlot(object? param)
        {
            if ((WaveformListDataCollection == null) || (WaveformListDataCollection.Count == 0))
            {
                window.WpfPlot1.Plot.Clear();
                window.WpfPlot1.Refresh();

                await WriteConsole("No Data\n", Colors.Red);
                return;
            }

            await UpdateWaveformPlotAsync();
        }
        [RelayCommand]
        public async Task LoadWaveformList(object? param)
        {
            GC.Collect();

            InputFilePath = await SelectInputFile();
            if (InputFilePath == null)
            {
                await WriteConsole("File Error\n", Colors.Red);
                return;
            }

            WaveformListDataCollection = await ParseFileToWaveformList(InputFilePath);

            if (WaveformListDataCollection == null)
            {
                await WriteConsole("Parse File Error\n", Colors.Red);
                return;
            }

            await UpdateWaveformPlotAsync();


            //WaveformListDataCollection = waveforList;

            //(List<decimal>? X, List<decimal>? Y) = await ParseWaveformList(waveforList);

            ////(List<decimal>? dataX, List<decimal>? dataY) = await ParseFile(InputFilePath);

            //if ((X is null) || (Y is null))
            //{
            //    await WriteConsole("Parse Data Error\n", Colors.Red);
            //    return;
            //}

            //await PlotData(X, Y);

            ////await SavWaveformListToAwgFormat(X, Y);
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
        public async Task ClearWaveformList(object? param)
        {
            //window.Dispatcher.Invoke(() =>
            //{
            //    window.WpfPlot1.Plot.Clear();
            //    window.WpfPlot1.Refresh();
            //});

            window.WpfPlot1.Plot.Clear();
            window.WpfPlot1.Refresh();

            if (WaveformListDataCollection == null)
            {
                return;
            }
            WaveformListDataCollection.Clear();
            await Task.Delay(1);
        }
        [RelayCommand]
        public void AddWaveformList(object? param)
        {
            if (param is not DataGrid) { return; }
            if (WaveformListDataCollection is null) { return; }
            WaveformEditItem = new WaveformListData(WaveformEditItem.Duration, WaveformEditItem.Voltage);

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
            WaveformEditItem = new WaveformListData(WaveformEditItem.Duration, WaveformEditItem.Voltage);

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

        #region System Information
        // 获取WPF应用程序的版本号
        static string? GetWpfFileVersion()
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            if (assembly != null)
            {
                FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                return fileVersionInfo.FileVersion;
            }
            return null;
        }

        // 获取WPF应用程序的构建日期
        static DateTime? GetWpfBuildDate()
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            if (assembly != null)
            {
                string filePath = assembly.Location;
                if (System.IO.File.Exists(filePath))
                {
                    return System.IO.File.GetLastWriteTime(filePath);
                }
            }
            return DateTime.MinValue;
        }
        #endregion

        #region Help
        [RelayCommand]
        public void GotoUserGuide(object? param)
        {
            InternetHelper.OpenUrl(@"https://github.com/billwanggithub/AFG31000-WaveForm-Converter#readme");
        }
        [RelayCommand]
        public void GotoTektronic(object? param)
        {
            InternetHelper.OpenUrl(@"https://www.tek.com/en/products/signal-generators/arbitrary-function-generator/afg31000");
        }
        [RelayCommand]
        public void About(object? param)
        {
            MessageBox.Show($"File Version: {GetWpfFileVersion()}\nBuild Date: {GetWpfBuildDate()}\n", "About");
        }
        #endregion
    }
}
