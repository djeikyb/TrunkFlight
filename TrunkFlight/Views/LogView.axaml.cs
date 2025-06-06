using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Microsoft.Extensions.Logging;
using TrunkFlight.Vm;

namespace TrunkFlight.Views;

public partial class LogView : UserControl
{
    private readonly ILogger<LogView> _logger = Log.GetLogger<LogView>();
    public LogView()
    {
        InitializeComponent();

        DataContextChanged += (sender, _) =>
        {
            if (sender is not LogView lv)
            {
                _logger.LogError("🐍 ohno");
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
                    new TextColumn<LogEvent, string>("message", x => x.Message),
                },
            };
            ((ITreeDataGridSource)source).SortBy(source.Columns[1],
                ListSortDirection.Descending);
            source.RowSelection!.SingleSelect = false;
            LogTable.Source = source;
        };
    }

    private static string ToString(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "trc",
            LogLevel.Debug => "dbg",
            LogLevel.Information => "inf",
            LogLevel.Warning => "wrn",
            LogLevel.Error => "err",
            LogLevel.Critical => "crt",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        };
    }
}
