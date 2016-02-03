using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ImageingAndProjectionTemplate.Common;
using ImageingAndProjectionTemplate;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace ImageingAndProjectionTemplate.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomeProject : Page
    {
        public static HomeProject _MainWindowHandle = null;
        //public ViewLifetimeControl ProjectionViewPageControl;
        private ViewLifetimeControl thisViewControl;
        private CoreDispatcher mainDispatcher;
        private int mainViewId;
        private int thisViewId;
        public Windows.ApplicationModel.Activation.LaunchActivatedEventArgs LaunchArgs
        {
            get
            {
                return ((App)App.Current).LaunchArgs;
            }
        }
        public HomeProject()
        {
            this.InitializeComponent();
            //if (!Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Mobile"))
            //{
            //    SetTitleBar();
            //}
            SetTitleBar();
            _MainWindowHandle = this;
            //mainFrame.Navigate(typeof(MainPageProject));
            thisViewId = ((App)App.Current).projectedViewId = ApplicationView.GetForCurrentView().Id;
        }


        private void SetTitleBar()
        {
            var corTitleBar = Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar;
            var appTitleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
            corTitleBar.ExtendViewIntoTitleBar = true;
            appTitleBar.ButtonBackgroundColor = Windows.UI.Colors.Transparent;
            corTitleBar.LayoutMetricsChanged += CorTitleBar_LayoutMetricsChanged;
            Window.Current.SetTitleBar(myTitlebar);
            appName.Text = Windows.ApplicationModel.Package.Current.DisplayName;
        }

        private void CorTitleBar_LayoutMetricsChanged(Windows.ApplicationModel.Core.CoreApplicationViewTitleBar sender, object args)
        {
            backButton.Height = backButton.Width = tabletBackButtonGrid.Height = sender.Height;
        }

        private void backButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessBackRequest();
        }
        private void ProcessBackRequest()
        {
            if (mainFrame.Content.ToString().Equals((typeof(PictureEditorProject)).ToString()))
            {
                mainFrame.Navigate(typeof(MainPageProject));
            }
            else if (mainFrame.Content.ToString().Equals((typeof(MainPageProject)).ToString()))
            {
                MainPageProject main = mainFrame.Content as MainPageProject;
                main.NaviBack();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e != null && (e.Parameter as ProjectionViewPageInitializationData) != null)
            {                
                ProjectionViewPageInitializationData projectParam = e.Parameter as ProjectionViewPageInitializationData;
                thisViewControl = projectParam.ProjectionViewPageControl;
                mainViewId = projectParam.MainViewId;
                thisViewControl = projectParam.ProjectionViewPageControl;
                mainDispatcher = projectParam.MainDispatcher;
                thisViewControl.Released += ThisViewControl_Released;
                mainFrame.Navigate(projectParam.page, projectParam.naviParam);
            }
            else
            {
                mainFrame.Navigate(typeof(MainPage));
            }
        }

        private async void ThisViewControl_Released(object sender, EventArgs e)
        {
            thisViewControl.Released -= ThisViewControl_Released;
            await mainDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ((App)App.Current)._MainWindowHandle.ProjectionViewPageControl = null;
            });
            ((App)App.Current).projectedFlag = false;
            Window.Current.Close();
        }

        public void Navigate(Type t, object param)
        {
            try
            {
                mainFrame.Navigate(t, param);
            }
            catch (Exception e)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(e.Message);
#endif
            }
        }
    }
}
