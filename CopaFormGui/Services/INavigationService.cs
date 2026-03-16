using CopaFormGui.Models;

namespace CopaFormGui.Services;

public interface INavigationService
{
    void NavigateTo(string viewName, object? parameter = null);
    void GoBack();
}
