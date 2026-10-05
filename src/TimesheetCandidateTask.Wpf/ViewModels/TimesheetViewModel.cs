using System.Collections.ObjectModel;
using System.Net.Http;
using TimeSheetCandidateTask.Domain.Exceptions;
using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Shared;
using TimesheetCandidateTask.Wpf.Application;
using TimesheetCandidateTask.Wpf.Configuration;

namespace TimesheetCandidateTask.Wpf.ViewModels;

public sealed class TimesheetViewModel : ObservableObject
{
    private readonly TimesheetWorkspace _workspace;
    private string _message = "Нажмите «Загрузить», чтобы получить табель с сервера.";
    private TimesheetStatus _status;
    public bool IsLoaded { get; private set; }

    public TimesheetViewModel(TimesheetWorkspace workspace)
    {
        _workspace = workspace;
        LoadCommand = new RelayCommand(Load);
        AddEmployeeCommand = new RelayCommand(AddEmployee, () => IsDraft() && CanAddEmployee);
        SaveCommand = new RelayCommand(Save, () => IsDraft() && IsLoaded);
        ApproveCommand = new RelayCommand(Approve, () => IsDraft() && IsLoaded);
    }

    public ObservableCollection<TimesheetLineViewModel> Lines { get; } = new();
    public RelayCommand LoadCommand { get; }
    public RelayCommand AddEmployeeCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ApproveCommand { get; }
    public bool AreCommentsReadOnly => !IsLoaded || !IsDraft();

    private bool CanAddEmployee =>
        IsLoaded &&
        EmployeesCollection.Employees.Keys.Any(employeeId =>
            Lines.All(line => line.Line.EmployeeId != employeeId));

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string StatusText => _status == TimesheetStatus.Draft ? "Черновик" : "Утверждён";
    public decimal OverallTotal => Lines.Sum(line => line.TotalHours);

    private void Load()
    {
        try
        {
            var timesheet = _workspace.Load();
            IsLoaded = true;
            ShowTimesheet(timesheet);
            Message = IsDraft()
                ? "Табель загружен."
                : "Табель утверждён и доступен только для чтения.";
        }
        catch (Exception exception)
        {
            Message = "Ошибка при загрузке табеля";
        }
    }

    private void AddEmployee()
    {
        var line = _workspace.AddEmployee();
        var lineVm = new TimesheetLineViewModel(line, () => Message = "Есть несохранённые изменения комментариев.")
        {
            IsNew = true
        };
        Lines.Add(lineVm);

        NotifyStateChanged();
        Message = CanAddEmployee
            ? "Сотрудник добавлен."
            : "Все доступные сотрудники уже добавлены.";
    }

    private void Save()
    {
        SaveCore();
    }

    private bool SaveCore()
    {
        try
        {
            foreach (var line in Lines)
            {
                if (!line.IsDirty)
                {
                    continue;
                }

                _workspace.UpdateComment(line.Line.Id, new DateTime(2026, 1, 1), line.FirstDayComment);
                _workspace.UpdateComment(line.Line.Id, new DateTime(2026, 1, 2), line.SecondDayComment);
                line.MarkClean();
            }

            foreach (var line in Lines)
            {
                line.Refresh();
            }

            var savedTimesheet = _workspace.SaveAndGetResult();
            ShowTimesheet(savedTimesheet);
            Message = "Изменения сохранены.";
            return true;
        }
        catch (KeyNotFoundException)
        {
            Message = "Не удалось обновить комментарий для выбранного дня.";
            return false;
        }
        catch (ClientException exception)
        {
            Message = exception.Message;
            return false;
        }
        catch (HttpRequestException)
        {
            Message = "Не удалось связаться с API при сохранении.";
            return false;
        }
        catch (TaskCanceledException)
        {
            Message = "Запрос к API превысил время ожидания при сохранении.";
            return false;
        }
    }

    private void Approve()
    {
        if (!SaveCore())
        {
            return;
        }

        try
        {
            _workspace.Approve();
            var approvedTimesheet = _workspace.SaveAndGetResult();
            ShowTimesheet(approvedTimesheet);
            Message = "Табель утверждён.";
        }
        catch (ClientException exception)
        {
            Message = exception.Message;
        }
        catch (HttpRequestException)
        {
            Message = "Не удалось связаться с API при утверждении табеля.";
        }
        catch (TaskCanceledException)
        {
            Message = "Запрос к API превысил время ожидания при утверждении табеля.";
        }
    }

    private bool IsDraft() =>_status == TimesheetStatus.Draft;

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
        OnPropertyChanged(nameof(AreCommentsReadOnly));
        OnPropertyChanged(nameof(OverallTotal));
        AddEmployeeCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        ApproveCommand.RaiseCanExecuteChanged();
    }

}
