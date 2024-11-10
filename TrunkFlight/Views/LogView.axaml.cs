using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using TrunkFlight.Vm;
using Serilog;
using Serilog.Events;

namespace TrunkFlight.Views;

public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();

        DataContextChanged += (sender, _) =>
        {
            if (sender is not LogView lv)
            {
                Log.Error("🐍 ohno");
                return;
            }

            var vm = lv.DataContext as MainViewModel;
            if (vm is null) return;

            var source = new FlatTreeDataGridSource<LogEvent>(vm.View)
            {
                Columns =
                {
                    new TextColumn<LogEvent, string>("level", x => ToString(x.Level)),
                    new TextColumn<LogEvent, string>("time", x => x.Timestamp.LocalDateTime.ToString("hh:mm:ss:fff")),
                    new TextColumn<LogEvent, string>("message template", x => x.MessageTemplate.Text),
                },
            };
            ((ITreeDataGridSource)source).SortBy(source.Columns[1],
                ListSortDirection.Descending);
            source.RowSelection!.SingleSelect = false;
            LogTable.Source = source;
        };
    }

    private static string ToString(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "vrb",
            LogEventLevel.Debug => "dbg",
            LogEventLevel.Information => "inf",
            LogEventLevel.Warning => "wrn",
            LogEventLevel.Error => "err",
            LogEventLevel.Fatal => "ftl",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        };
    }
}
