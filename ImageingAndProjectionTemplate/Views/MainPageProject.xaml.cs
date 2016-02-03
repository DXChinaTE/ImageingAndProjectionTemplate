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

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上提供

namespace ImageingAndProjectionTemplate.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPageProject : Page
    {
        private double pictureWidth = 180;
        private int thisViewId;
        private GLOABOALSTAGE stage = GLOABOALSTAGE.MAINPAGE_PICLIST;
        public static int selectedIndex = 0;
        private DispatcherTimer flipTimer;
        private enum OPTIONS { GET = 1, INCREASE, DECREASE };
        private object locker = new object();
        private CoreDispatcher mainDispatcher;
        private int mainViewId;
        //private ViewLifetimeControl thisViewControl;

        public MainPageProject()
        {
            this.InitializeComponent();
            if (!Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Mobile"))
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
            StorageFolder folder = KnownFolders.PicturesLibrary;
            ObservableCollection<PictureListInfo> pictureList = pictureGrid.ItemsSource as ObservableCollection<PictureListInfo>;
            ObservableCollection<PictureListInfo> pictureListFlip = pictureFlipview.ItemsSource as ObservableCollection<PictureListInfo>;
            IReadOnlyCollection<StorageFile> files = await folder.GetFilesAsync();
            foreach (var item in files)
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
            pictureFlipview.SelectedIndex = -1;
            if (e.Parameter != null && (e.Parameter as PageNavigateParam) != null)
            {
                //ProjectionViewPageInitializationData param = e.Parameter as PageNavigateParam;
                PageNavigateParam naviParam = e.Parameter as PageNavigateParam;
                if (naviParam.stage == GLOABOALSTAGE.MAINPAGE_PICFLIP)
                {
                    pictureGrid.Visibility = Visibility.Collapsed;
                    pictureFlipview.SelectedIndex = (int)naviParam.stageParam;
                    pictureFlipview.Visibility = Visibility.Visible;
                }
                flipTimer = new DispatcherTimer();
                flipTimer.Tick += FlipTimer_Tick;
                flipTimer.Interval = new TimeSpan(0,0,1);
                flipTimer.Start();
                mainViewId = naviParam.MainViewId;
                //thisViewControl = param.ProjectionViewPageControl;
                //mainDispatcher = param.MainDispatcher;
                //thisViewControl.Released += ThisViewControl_Released;
                ((App)App.Current).projectedViewId = thisViewId;
            }
        }

        //private async void ThisViewControl_Released(object sender, EventArgs e)
        //{
        //    thisViewControl.Released -= ThisViewControl_Released;
        //    await mainDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        //    {
        //        ((App)App.Current)._MainWindowHandle.ProjectionViewPageControl = null;
        //    });
        //    ((App)App.Current).projectedFlag = false;
        //    Window.Current.Close();
        //}

        private void FlipTimer_Tick(object sender, object e)
        {
            flipTimer.Stop();
            //flipTimer.Interval = new TimeSpan(5 * 1000);
             int index  = ((App)App.Current)._MainWindowHandle.GetOrSetIndex((int)OPTIONS.GET);
            switch(index)
            {
                case 0:
                    break;
                case -1:
                    if(pictureFlipview.SelectedIndex > 0)
                    {
                        pictureFlipview.SelectedIndex = pictureFlipview.SelectedIndex - 1;
                    }
                    break;
                case 1:
                    if (pictureFlipview.SelectedIndex < pictureFlipview.Items.Count - 1)
                    {
                        pictureFlipview.SelectedIndex = pictureFlipview.SelectedIndex + 1;
                    }
                    break;
                default:
                    break;
            }
            if (pictureFlipview.SelectedIndex >= 0)
            {
                pictureGrid.Visibility = Visibility.Collapsed;
                pictureFlipview.Visibility = Visibility.Visible;
            }
            flipTimer.Start();
        }

        //private int GetOrSetIndex(OPTIONS option)
        //{
        //    lock (locker)
        //    {
        //        int index = -1;
        //        switch (option)
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
            HomeProject._MainWindowHandle.Navigate(typeof(PictureEditorProject), item);
            return;
        }

        private async void projectButton_Click(object sender, RoutedEventArgs e)
        {          
            //thisViewControl.StartViewInUse();
            await ProjectionManager.StopProjectingAsync(
                ApplicationView.GetForCurrentView().Id,
                mainViewId);
            //thisViewControl.StopViewInUse();           
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

        //private void NextPageBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    GetOrSetIndex(OPTIONS.INCREASE);
        //}

        //private async void StopProjectBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    await ProjectionManager.StopProjectingAsync(
        //                        ((App)App.Current).projectedViewId,
        //                        ApplicationView.GetForCurrentView().Id
        //                        );
        //    ((App)App.Current).projectedViewId = 0;
        //}

        //private void PrePageBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    GetOrSetIndex(OPTIONS.DECREASE);
        //}
        public void NaviBack()
        {
            if (pictureFlipview.Visibility == Visibility.Visible)
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
