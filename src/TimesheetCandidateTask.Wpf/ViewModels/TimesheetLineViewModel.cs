using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Shared;
using TimesheetCandidateTask.Shared.Contracts;
using TimesheetCandidateTask.Shared.Utils;
using TimesheetCandidateTask.Wpf.Configuration;

namespace TimesheetCandidateTask.Wpf.ViewModels;

public sealed class TimesheetLineViewModel : ObservableObject
{
    private static readonly DateTime FirstDisplayedDate = new(2026, 1, 1);
    private static readonly DateTime SecondDisplayedDate = new(2026, 1, 2);

    private readonly TimesheetLine _line;
    private readonly TimesheetDay _firstDay;
    private readonly TimesheetDay _secondDay;
    private readonly Action _changed;
    private string? _firstDayComment;
    private string? _secondDayComment;
    private TimesheetTotalsResponse _totals;
    private bool _isDirty;

    public TimesheetLineViewModel(TimesheetLine line, Action changed)
    {
        _line = line;
        _changed = changed;
        EmployeeName = EmployeesCollection.Employees.TryGetValue(line.EmployeeId, out var employee)
            ? employee.EmployeeName
            : "Неизвестный сотрудник";
        _firstDay = FindDay(FirstDisplayedDate);
        _secondDay = FindDay(SecondDisplayedDate);
        _firstDayComment = _firstDay.Comment;
        _secondDayComment = _secondDay.Comment;
        _totals = TimesheetCalculator.Calculate(_line);
    }

    public string EmployeeName { get; }
    public decimal FirstDayHours => _firstDay.Hours;
    public decimal SecondDayHours => _secondDay.Hours;
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }
    public decimal WorkedHours => _totals.WorkedHours;
    public decimal HolidayHours => _totals.HolidayHours;
    public decimal TotalHours => _totals.TotalHours;
    public bool IsNew { get;init; }

    public string FirstDayComment
    {
        get => _firstDayComment ?? string.Empty;
        set
        {
            if (SetProperty(ref _firstDayComment, value))
            {
                IsDirty = true;
                _changed();
            }
        }
    }

    public string SecondDayComment
    {
        get => _secondDayComment ?? string.Empty;
        set
        {
            if (SetProperty(ref _secondDayComment, value))
            {
                IsDirty = true;
                _changed();
            }
        }
    }

    public TimesheetLine Line => _line;

    public void MarkClean() => IsDirty = false;

    public void Refresh()
    {
        _firstDayComment = _firstDay.Comment;
        _secondDayComment = _secondDay.Comment;
        _totals = TimesheetCalculator.Calculate(_line);
        OnPropertyChanged(nameof(FirstDayComment));
        OnPropertyChanged(nameof(SecondDayComment));
        OnPropertyChanged(nameof(FirstDayHours));
        OnPropertyChanged(nameof(SecondDayHours));
        OnPropertyChanged(nameof(WorkedHours));
        OnPropertyChanged(nameof(HolidayHours));
        OnPropertyChanged(nameof(TotalHours));
    }

    private TimesheetDay FindDay(DateTime date) =>
        _line.Days.Single(day => day.Date.Date == date.Date);
}
