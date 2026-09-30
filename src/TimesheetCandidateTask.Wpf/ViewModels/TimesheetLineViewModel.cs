using TimesheetCandidateTask.Api.Application;
using TimesheetCandidateTask.Api.Domain;

namespace TimesheetCandidateTask.Wpf.ViewModels;

public sealed class TimesheetLineViewModel : ObservableObject
{
    private readonly TimesheetLine _line;
    private readonly Action _changed;
    private string? _firstDayComment;
    private string? _secondDayComment;

    public TimesheetLineViewModel(TimesheetLine line, Action changed)
    {
        _line = line;
        _changed = changed;
        _firstDayComment = FirstDay.Comment;
        _secondDayComment = SecondDay.Comment;
    }

    public string EmployeeName => "Иванов И.И.";
    public decimal FirstDayHours => FirstDay.Hours;
    public decimal SecondDayHours => SecondDay.Hours;
    public decimal WorkedHours => TimesheetCalculator.Calculate(_line).WorkedHours;
    public decimal HolidayHours => TimesheetCalculator.Calculate(_line).HolidayHours;
    public decimal TotalHours => TimesheetCalculator.Calculate(_line).TotalHours;

    public string? FirstDayComment
    {
        get => _firstDayComment;
        set
        {
            if (SetProperty(ref _firstDayComment, value))
            {
                _changed();
            }
        }
    }

    public string? SecondDayComment
    {
        get => _secondDayComment;
        set
        {
            if (SetProperty(ref _secondDayComment, value))
            {
                _changed();
            }
        }
    }

    public TimesheetLine Line => _line;

    public void Refresh()
    {
        _firstDayComment = FirstDay.Comment;
        _secondDayComment = SecondDay.Comment;
        OnPropertyChanged(nameof(FirstDayComment));
        OnPropertyChanged(nameof(SecondDayComment));
        OnPropertyChanged(nameof(WorkedHours));
        OnPropertyChanged(nameof(HolidayHours));
        OnPropertyChanged(nameof(TotalHours));
    }

    private TimesheetDay FirstDay => _line.Days.Single(day => day.Date.Date == new DateTime(2026, 1, 1));
    private TimesheetDay SecondDay => _line.Days.Single(day => day.Date.Date == new DateTime(2026, 1, 2));
}
