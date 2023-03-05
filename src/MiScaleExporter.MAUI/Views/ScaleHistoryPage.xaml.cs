using Autofac;
using MiScaleExporter.MAUI.ViewModels;
using MiScaleExporter.Models;

namespace MiScaleExporter.MAUI.Views
{
    public partial class ScaleHistoryPage : ContentPage
    {
        private IScaleHistoryViewModel vm;
        public ScaleHistoryPage()
        {
            InitializeComponent();
            using (var scope = App.Container.BeginLifetimeScope())
            {
                this.BindingContext = vm = scope.Resolve<IScaleHistoryViewModel>();
            }
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await vm.CheckPreferencesAsync();
        }
    }
}