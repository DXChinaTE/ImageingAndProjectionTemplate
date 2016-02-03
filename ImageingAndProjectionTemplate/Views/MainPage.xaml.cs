using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using ImageingAndProjectionTemplate.Common;
using ImageingAndProjectionTemplate;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ImageingAndProjectionTemplate.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private double pictureWidth = 180;
        private App rootPage;
        private int thisViewId;
        private GLOABOALSTAGE stage = GLOABOALSTAGE.MAINPAGE_PICLIST;
        public static int selectedIndex = 0;
        private bool isProjected = false;
        private DispatcherTimer flipTimer;
        private DispatcherTimer monitorTimer = null;
        private enum OPTIONS { GET=1,INCREASE,DECREASE};
        //private object locker = new object();


        public MainPage()
        {
            this.InitializeComponent();
            if (Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Mobile"))
            {
                pictureWidth = (Window.Current.Bounds.Width - 30) / 2;
            }
            pictureGrid.ItemsSource = new ObservableCollection<PictureListInfo>();
            pictureFlipview.ItemsSource = new ObservableCollection<PictureListInfo>();
            thisViewId = ApplicationView.GetForCurrentView().Id;           
        }
       
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            StorageFolder folder = KnownFolders.PicturesLibrary;//Camera Roll
            if (Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Mobile"))
            {
                IReadOnlyList<StorageFolder> folders = await folder.GetFoldersAsync();
                foreach(var item in folders)
                {
                    if(item.Name.Equals("Camera Roll"))
                    {
                        folder = item;
                        break;
                    }
                }
            }           
            ObservableCollection<PictureListInfo> pictureList = pictureGrid.ItemsSource as ObservableCollection<PictureListInfo>;
            ObservableCollection<PictureListInfo> pictureListFlip = pictureFlipview.ItemsSource as ObservableCollection<PictureListInfo>;
            IReadOnlyCollection<StorageFile> files = await folder.GetFilesAsync();
            foreach(var item in files)
            {
                PictureListInfo info = new PictureListInfo();
                info.picturePath = item.Path;
                using (IRandomAccessStream fileStream = await item.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    BitmapImage image = new BitmapImage();
                    image.SetSource(fileStream);
                    info.picture = image;
                }
                pictureList.Add(info);
                pictureListFlip.Add(info);
            }           
        }
      
        //private int GetOrSetIndex(OPTIONS option)
        //{
        //    lock(locker)
        //    {
        //        int index = -1;
        //        switch(option)
        //        {
        //            case OPTIONS.GET:
        //                index = ((App)App.Current).selectedPictureIndex;
        //                break;
        //            case OPTIONS.INCREASE:
        //                if (((App)App.Current).selectedPictureIndex < pictureFlipview.Items.Count - 1)
        //                {
        //                    ((App)App.Current).selectedPictureIndex++;
        //                }                       
        //                break;
        //            case OPTIONS.DECREASE:
        //                if (((App)App.Current).selectedPictureIndex > 0)
        //                {
        //                    ((App)App.Current).selectedPictureIndex--;
        //                }
        //                break;
        //            default:
        //                index = 0;
        //                break;

        //        }
        //        return index;
        //    }            
        //} 

        private void editButton_Click(object sender, RoutedEventArgs e)
        {
            var item = pictureFlipview.SelectedItem;
            if (Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Mobile"))
            {
                //pictureWidth = (Window.Current.Bounds.Width - 30) / 2;
                Home._MainWindowHandle.Navigate(typeof(PictureEditorWP), item);
            }
            else
            {
                Home._MainWindowHandle.Navigate(typeof(PictureEditor), item);  
                //Home._MainWindowHandle.Navigate(typeof(PictureEditorWP), item);
            }            
            return;
        }

        private async void projectButton_Click(object sender, RoutedEventArgs e)
        {            
            // If projection is already in progress, then it could be shown on the monitor again
            // Otherwise, we need to create a new view to show the presentation
            rootPage = ((App)App.Current)._MainWindowHandle;
            if (rootPage.ProjectionViewPageControl == null)
            {
                // First, create a new, blank view
                var thisDispatcher = Window.Current.Dispatcher;
                await CoreApplication.CreateNewView().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // ViewLifetimeControl is a wrapper to make sure the view is closed only
                    // when the app is done with it
                    rootPage.ProjectionViewPageControl = ViewLifetimeControl.CreateForCurrentView();

                    // Assemble some data necessary for the new page
                    var initData = new ProjectionViewPageInitializationData();
                    initData.MainDispatcher = thisDispatcher;
                    initData.ProjectionViewPageControl = rootPage.ProjectionViewPageControl;
                    initData.MainViewId = thisViewId;
                    initData.page = typeof(MainPageProject);
                    PageNavigateParam param = new PageNavigateParam();
                    param.stage = stage;
                    param.MainViewId = thisViewId;
                    if (stage == GLOABOALSTAGE.MAINPAGE_PICFLIP)
                    {
                        param.stageParam = selectedIndex;
                    }
                    else
                    {
                        param.stageParam = null;
                    }
                    initData.naviParam = param;
                    // Display the page in the view. Note that the view will not become visible
                    // until "StartProjectingAsync" is called
                    var rootFrame = new Frame();
                    rootFrame.Navigate(typeof(HomeProject),initData);
                    Window.Current.Content = rootFrame;
                    Window.Current.Activate();
                });

                try
                {
                    // Start/StopViewInUse are used to signal that the app is interacting with the
                    // view, so it shouldn't be closed yet, even if the user loses access to it
                    rootPage.ProjectionViewPageControl.StartViewInUse();

                    // Show the view on a second display (if available) or on the primary display
                    Rect pickerLocation = new Rect(470.0, 0.0, 200.0, 300.0);
                    bool projected = await ProjectionManager.RequestStartProjectingAsync(rootPage.ProjectionViewPageControl.Id, thisViewId, pickerLocation, Windows.UI.Popups.Placement.Above);
                    if (projected)
                    {
                        ((App)App.Current).projectedFlag = true;
                        rootPage.ProjectionViewPageControl.StopViewInUse();
                        projectControlPanel.Visibility = Visibility.Visible;
                        mainGrid.Visibility = Visibility.Collapsed;
                        if(monitorTimer == null)
                        {
                            monitorTimer = new DispatcherTimer();
                            monitorTimer.Interval = new TimeSpan(0,0,2);
                            monitorTimer.Tick += MonitorTimer_Tick;
                            monitorTimer.Start();
                        }
                        else
                        {
                            monitorTimer.Start();
                        }
                    }
                }
                catch (InvalidOperationException)
                {

                }
            }           
        }

        private void MonitorTimer_Tick(object sender, object e)
        {
            if(!((App)App.Current).projectedFlag)
            {
                monitorTimer.Stop();
                projectControlPanel.Visibility = Visibility.Collapsed;
                mainGrid.Visibility = Visibility.Visible;
            }
        }

        private void pictureGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            pictureGrid.Visibility = Visibility.Collapsed;
            selectedIndex = pictureFlipview.SelectedIndex = pictureGrid.SelectedIndex;
            pictureFlipview.Visibility = Visibility.Visible;
            stage = GLOABOALSTAGE.MAINPAGE_PICFLIP;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void NextPageBtn_Click(object sender, RoutedEventArgs e)
        {
            ((App)App.Current)._MainWindowHandle.GetOrSetIndex((int)OPTIONS.INCREASE);
        }

        private async void StopProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            await ProjectionManager.StopProjectingAsync(
                                ((App)App.Current).projectedViewId,
                                ApplicationView.GetForCurrentView().Id
                                );
            ((App)App.Current).projectedViewId = 0;
        }

        private void PrePageBtn_Click(object sender, RoutedEventArgs e)
        {         
            ((App)App.Current)._MainWindowHandle.GetOrSetIndex((int)OPTIONS.DECREASE);
        }
        public void NaviBack()
        {
            if(pictureFlipview.Visibility == Visibility.Visible)
            {
                pictureFlipview.Visibility = Visibility.Collapsed;
                pictureGrid.Visibility = Visibility.Visible;
                pictureGrid.SelectedIndex = -1;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            return;
        }
    }
}
