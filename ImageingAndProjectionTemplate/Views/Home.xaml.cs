using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.ApplicationModel.Activation;
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
    public sealed partial class Home : Page
    {
        public static Home _MainWindowHandle = null;        
        public Home()
        {
            this.InitializeComponent();
            if (!Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Mobile"))
            {
                SetTitleBar();               
            } 
            else
            {
                Windows.Phone.UI.Input.HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            }         
            _MainWindowHandle = this;
            mainFrame.Navigate(typeof(MainPage));
        }

        private void HardwareButtons_BackPressed(object sender, Windows.Phone.UI.Input.BackPressedEventArgs e)
        {
            e.Handled = true;
            ProcessBackRequest();
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
            if(mainFrame.Content.ToString().Equals((typeof(PictureEditor)).ToString()))
            {
                mainFrame.Navigate(typeof(MainPage));
            }
            else if(mainFrame.Content.ToString().Equals((typeof(MainPage)).ToString()))
            {
                MainPage main = mainFrame.Content as MainPage;
                main.NaviBack();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            //if (e != null && (e.Parameter as ProjectionViewPageInitializationData) != null)
            //{
            //    ProjectionViewPageInitializationData data = e.Parameter as ProjectionViewPageInitializationData;
            //    mainFrame.Navigate(data.page,data);
            //    return;
            //}
            //else
            //{
            //    mainFrame.Navigate(typeof(MainPage));
            //}
            mainFrame.Navigate(typeof(MainPage));

        }
        public void Navigate(Type t, object param)
        {
            try
            {
                mainFrame.Navigate(t, param);
            }
            catch(Exception e)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(e.Message);
#endif
            }            
        }
    }
}
