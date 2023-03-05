using System;
using System.Collections.Generic;
using System.Text;
 

namespace MiScaleExporter.MAUI.ViewModels
{
    public interface IScaleHistoryViewModel
    {
        Task CheckPreferencesAsync();
        Task LoadPreferencesAsync();
    }
}
