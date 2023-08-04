using System.Collections;
using System.Windows.Controls;

namespace ControlExtentions
{
    //https://stackoverflow.com/questions/18019425/scrollintoview-for-wpf-datagrid-mvvm
    public class ScrollingDataGrid : DataGrid
    {
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            var grid = e.Source as DataGrid;

            if (grid!.SelectedItem != null)
            {
                grid.UpdateLayout();
                grid.ScrollIntoView(grid.SelectedItem);
            }

            base.OnSelectionChanged(e);
        }

        // https://stackoverflow.com/questions/4663771/wpf-4-datagrid-getting-the-row-number-into-the-rowheader/4663799#4663799
        //protected override void OnLoadingRow(DataGridRowEventArgs e)
        //{
        //    e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        //    Trace.WriteLine(e.Row.GetIndex().ToString() + Environment.NewLine);
        //}

        protected override void OnItemsSourceChanged(
                                IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);
        }

        //protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        //{
        //    base.OnItemsChanged(e);
        //    //this.UpdateLayout();
        //}
        //private void OnDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
        //{
        //    DataGrid dataGrid = (DataGrid)sender;

        //    if (dataGrid.SelectedItem == null || dataGrid.Items.IndexOf(dataGrid.SelectedItem) == dataGrid.Items.Count - 1)
        //    {
        //        // If the user double-clicks an empty area or the last row of the DataGrid, add a new item to the collection.
        //        Items.Add(new MyDataObject());
        //    }
        //    else
        //    {
        //        // If the user double-clicks an existing row, insert a new item after the selected row.
        //        int selectedIndex = dataGrid.Items.IndexOf(dataGrid.SelectedItem);
        //        Items.Insert(selectedIndex + 1, new MyDataObject());
        //    }
        //}
    }

}
