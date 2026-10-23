using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace KodiakCleaner.App.Views;

/// <summary>
/// The "1:05 elapsed · about 3 min left" line under a page's own progress bar (Ted, 2026-10-23). It follows the newest task
/// the page (its DataContext, or <see cref="Owner"/>) is running and hides itself when there is none.
/// </summary>
public sealed class TaskClock : TextBlock
{
    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(nameof(Owner), typeof(object), typeof(TaskClock),
        new PropertyMetadata(null, (d, _) => ((TaskClock)d).Follow()));

    /// <summary>The page view model whose task to show; the DataContext when not set.</summary>
    public object? Owner { get => GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }

    public TaskClock()
    {
        SetResourceReference(StyleProperty, "Caption");
        Visibility = Visibility.Collapsed;
        DataContextChanged += (_, _) => Follow();
        Loaded += (_, _) => { BackgroundTasks.Changed += Follow; Follow(); };
        Unloaded += (_, _) => BackgroundTasks.Changed -= Follow;
    }

    private RunningTask? _task;

    private void Follow()
    {
        var task = BackgroundTasks.For(Owner ?? DataContext);
        if (task == _task) return;
        _task = task;
        if (task is null)
        {
            BindingOperations.ClearBinding(this, TextProperty);
            Visibility = Visibility.Collapsed;
            return;
        }
        SetBinding(TextProperty, new Binding(nameof(RunningTask.TimeText)) { Source = task, Mode = BindingMode.OneWay });
        Visibility = Visibility.Visible;
    }
}
