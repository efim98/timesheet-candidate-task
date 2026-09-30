using System.Collections.ObjectModel;
using TimesheetCandidateTask.Api.Domain;
using TimesheetCandidateTask.Wpf.Application;

namespace TimesheetCandidateTask.Wpf.ViewModels;

public sealed class TimesheetViewModel : ObservableObject
{
    private readonly TimesheetWorkspace _workspace;
    private string _message = "Нажмите «Загрузить», чтобы открыть демонстрационный табель.";
    private TimesheetStatus _status;

    public TimesheetViewModel(TimesheetWorkspace workspace)
    {
        _workspace = workspace;
        LoadCommand = new RelayCommand(Load);
        AddEmployeeCommand = new RelayCommand(AddEmployee, IsDraft);
        SaveCommand = new RelayCommand(Save, IsDraft);
        ApproveCommand = new RelayCommand(Approve, IsDraft);
        Load();
    }

    public ObservableCollection<TimesheetLineViewModel> Lines { get; } = new();
    public RelayCommand LoadCommand { get; }
    public RelayCommand AddEmployeeCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ApproveCommand { get; }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string StatusText => _status == TimesheetStatus.Draft ? "Черновик" : "Утверждён";
    public decimal OverallTotal => Lines.Sum(line => line.TotalHours);

    private void Load()
    {
        var timesheet = _workspace.Load();
        ShowTimesheet(timesheet);
        Message = "Демонстрационный табель загружен.";
    }

    private void AddEmployee()
    {
        ShowTimesheet(_workspace.AddEmployee());
        Message = "Сотрудник добавлен.";
    }

    private void Save()
    {
        try
        {
            foreach (var line in Lines)
            {
                _workspace.UpdateComment(line.Line.Id, new DateTime(2026, 1, 1), line.FirstDayComment);
                _workspace.UpdateComment(line.Line.Id, new DateTime(2026, 1, 2), line.SecondDayComment);
            }

            foreach (var line in Lines)
            {
                line.Refresh();
            }

            Message = "Изменения сохранены.";
        }
        catch (KeyNotFoundException)
        {
            foreach (var line in Lines)
            {
                line.Refresh();
            }

            Message = "Не удалось обновить комментарий для выбранного дня.";
        }
    }

    private void Approve()
    {
        _status = _workspace.Approve().Status;
        Message = "Табель утверждён.";
        NotifyStateChanged();
    }

    private bool IsDraft() => _status == TimesheetStatus.Draft;

    private void ShowTimesheet(Timesheet timesheet)
    {
        _status = timesheet.Status;
        Lines.Clear();
        foreach (var line in timesheet.Lines)
        {
            Lines.Add(new TimesheetLineViewModel(line, () => Message = "Есть несохранённые изменения комментариев."));
        }

        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(OverallTotal));
        AddEmployeeCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        ApproveCommand.RaiseCanExecuteChanged();
    }
}
