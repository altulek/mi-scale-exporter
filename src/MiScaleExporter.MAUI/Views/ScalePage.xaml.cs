﻿using Autofac;
using MiScaleExporter.MAUI.ViewModels;

namespace MiScaleExporter.MAUI.Views
{
    public partial class ScalePage : ContentPage
    {
        private IScaleViewModel vm;
        public ScalePage()
        {
            InitializeComponent();
            using (var scope = App.Container.BeginLifetimeScope())
            {
                this.BindingContext = vm = scope.Resolve<IScaleViewModel>();
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await vm.CheckPreferencesAsync();
        }

    }
}