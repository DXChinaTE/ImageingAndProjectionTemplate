﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Input.Inking;
using Windows.Graphics.Imaging;
using Windows.Graphics.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.Core;
using Microsoft.Graphics.Canvas;
using ImageingAndProjectionTemplate.Common;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace ImageingAndProjectionTemplate.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 
    public sealed partial class PictureEditorWP : Page
    {
        private List<AppBarButton> barList = new List<AppBarButton>();
        private Point prePositon;
        private enum POSITIONS { LEFTTOP = 1, RIGHTTOP, LEFTBUTTOM, RIGHTBUTTOM, UNKNOWN };
        private POSITIONS positon = POSITIONS.UNKNOWN;
        private GLOABOALSTAGE stage = GLOABOALSTAGE.EDITPAGE_CROP;
        WriteableBitmap WB_CapturedImage = null;//for original image
        WriteableBitmap WB_CroppedImage;//for cropped image

        private double colOriginalWidth = 0;
        private double rowOriginalHeight = 0;
        private string filePath = string.Empty;

        private PhotoOrientation m_userRotation = PhotoOrientation.Normal;
        private RotateTransform m_transform = new RotateTransform();
        private int m_displayWidthNonScaled;
        private int m_displayHeightNonScaled;

        private Dictionary<ListBoxItem, UIElement> ListBoxItemMapping = new Dictionary<ListBoxItem, UIElement>();
        private Dictionary<ListBoxItem, GLOABOALSTAGE> stageMapping = new Dictionary<ListBoxItem, GLOABOALSTAGE>();

        private App rootPage;
        private int thisViewId;
        private PictureListInfo info;
        private CropStateParam cropParam;
        private double paddingX;
        private double paddingY;
        private DispatcherTimer inkTimer = null;
        private DispatcherTimer monitorTimer = null;
      
        private object locker = new object();       
        private CoreDispatcher coreDispatcher;
        private List<InkStroke> strokes = new List<InkStroke>();
        private Dictionary<InkStroke, double> strokeMapping = new Dictionary<InkStroke, double>();

        public PictureEditorWP()
        {
            this.InitializeComponent();
            prePositon.X = -1;
            prePositon.Y = -1;
            barList.Add(bar1);
            barList.Add(bar2);
            barList.Add(bar3);
            barList.Add(bar4);
            ListBoxItemMapping.Add(listBoxItemCrop, clippingBarGrid);
            ListBoxItemMapping.Add(listBoxItemRotate, rotateBarGrid);
            ListBoxItemMapping.Add(listBoxItemInk, inkGrid);
            ListBoxItemMapping.Add(listBoxItemText,textGrid);
            stageMapping.Add(listBoxItemCrop, GLOABOALSTAGE.EDITPAGE_CROP);
            stageMapping.Add(listBoxItemRotate, GLOABOALSTAGE.EDITPAGE_ROTATE);
            stageMapping.Add(listBoxItemText, GLOABOALSTAGE.EDITPAGE_TEXT);
            stageMapping.Add(listBoxItemInk, GLOABOALSTAGE.EDITPAGE_INK);
            menuList.SelectedIndex = 0;
            thisViewId = ApplicationView.GetForCurrentView().Id;

            InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.Color = Color.FromArgb(255, 243, 156, 17);
            drawingAttributes.Size = new Size(2, 2);
            drawingAttributes.IgnorePressure = false;
            drawingAttributes.FitToCurve = true;

            ink.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
            ink.InkPresenter.InputDeviceTypes = Windows.UI.Core.CoreInputDeviceTypes.Mouse | Windows.UI.Core.CoreInputDeviceTypes.Pen | Windows.UI.Core.CoreInputDeviceTypes.Touch;
            ink.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
            ink.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
            coreDispatcher = Windows.UI.Core.CoreWindow.GetForCurrentThread().Dispatcher;
            listBoxItemCrop.Width = listBoxItemInk.Width = listBoxItemRotate.Width = listBoxItemText.Width = Window.Current.Bounds.Width / 4;
        }

        private void menuList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            editMenuGrid.Visibility = Visibility.Visible;
            menuList.Visibility = Visibility.Collapsed;
            foreach (var item in ListBoxItemMapping)
            {
                if (item.Key.Equals((sender as ListBox).SelectedItem))
                {
                    item.Value.Visibility = Visibility.Visible;
                }
                else
                {
                    item.Value.Visibility = Visibility.Collapsed;
                }
            }
            if (stageMapping.ContainsKey((sender as ListBox).SelectedItem as ListBoxItem))
            {
                stage = stageMapping[(sender as ListBox).SelectedItem as ListBoxItem];
                switch (stage)
                {
                    case GLOABOALSTAGE.EDITPAGE_ROTATE:
                        ResetImage();
                        break;
                    case GLOABOALSTAGE.EDITPAGE_CROP:
                        ResetCrop();
                        break;
                    case GLOABOALSTAGE.EDITPAGE_INK:
                        ResetInk();
                        break;
                    case GLOABOALSTAGE.EDITPAGE_TEXT:
                        ResetText();
                        break;
                    default:
                        break;

                }
            }
        }


        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            try
            {
                ((App)App.Current).SyncStrokeEx(strokeMapping, ink.InkPresenter.StrokeContainer, ink.Width);
            }
            catch (Exception e)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(e.Message);
#endif
            }
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            try
            {
                ((App)App.Current).SyncStrokeEx(strokeMapping, ink.InkPresenter.StrokeContainer, ink.Width);
            }
            catch (Exception e)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(e.Message);
#endif
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null)
            {
                var param = e.Parameter;
                if (param.ToString().Equals((typeof(PictureListInfo)).ToString()))
                {
                    PictureListInfo info = param as PictureListInfo;
                    image.Source = info.picture;
                    filePath = info.picturePath;
                    this.info = info;
                }
                stage = GLOABOALSTAGE.EDITPAGE_CROP;
            }
        }

        private async void InkTimer_Tick(object sender, object e)
        {
            inkTimer.Stop();
            await coreDispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (ink.Width != 0)
                {
                    ((App)App.Current).SyncStrokeEx(strokeMapping, ink.InkPresenter.StrokeContainer, ink.Width);
                }
            });
            inkTimer.Start();

        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            switch (stage)
            {
                case GLOABOALSTAGE.EDITPAGE_ROTATE:
                    ResetImage();
                    break;
                case GLOABOALSTAGE.EDITPAGE_CROP:
                    ResetCrop();
                    break;
                case GLOABOALSTAGE.EDITPAGE_INK:
                    ResetInk();
                    break;
                default:
                    break;
            }
        }
        private void ResetImage()
        {
            image.Visibility = Visibility.Visible;
            double width = imageFrame.ActualWidth > imageFrame.ActualHeight ? imageFrame.ActualHeight : imageFrame.ActualWidth;
            imagePanel.Width = imagePanel.Height = width;
        }

        private async void ResetInk()
        {
            m_transform.Angle = 0;
            imagePanel.ClearValue(WidthProperty);
            imagePanel.ClearValue(HeightProperty);
            image.Visibility = Visibility.Visible;
            ink.Width = image.ActualWidth;
            ink.Height = image.ActualHeight;
            if (ink.Width == 0)
            {
                return;
            }
            await coreDispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                ((App)App.Current).SyncStrokeEx(strokeMapping, ink.InkPresenter.StrokeContainer, ink.Width, true);
                //ResetInk();
                //((App)App.Current).SyncStroke(inkProject.InkPresenter.StrokeContainer,inkProject.ActualWidth, false);                
            });
            //lock (locker)
            //{
            //    ink.Width = image.ActualWidth;
            //    ink.Height = image.ActualHeight;               
            //    foreach (var item in strokeMapping)
            //    {
            //        InkStroke stroke = item.Key.Clone();
            //        stroke.PointTransform = System.Numerics.Matrix3x2.CreateScale((float)(ink.Width / item.Value));
            //        ink.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
            //    }
            //}            
        }

        private void ResetText()
        {
            m_transform.Angle = 0;
            imagePanel.ClearValue(WidthProperty);
            imagePanel.ClearValue(HeightProperty);
            textCanvas.Width = image.ActualWidth;
            textCanvas.Height = image.ActualHeight;
            Thickness margin = new Thickness();
            margin.Left = (textCanvas.Width - addText.ActualWidth) / 2;
            margin.Top = (textCanvas.Height - addText.ActualHeight) / 2;
            addText.Margin = margin;
            ImageBrush brus = new ImageBrush();
            brus.ImageSource = WB_CapturedImage;
            textCanvas.Background = brus;
            image.Visibility = Visibility.Collapsed;
        }
        private void ResetCrop()
        {
            m_transform.Angle = 0;
            imagePanel.ClearValue(WidthProperty);
            imagePanel.ClearValue(HeightProperty);
            image.Visibility = Visibility.Visible;
            paddingX = (clippingGrid.ActualWidth - image.ActualWidth) / 2;
            paddingY = (clippingGrid.ActualHeight - image.ActualHeight) / 2;
            if(paddingX <=0 || paddingY<= 0)
            {
                this.Width -= 1;
                return;
            }
            colOriginalWidth = paddingX;
            rowOriginalHeight = paddingY;
            clippingGrid.RowDefinitions[0].Height = clippingGrid.RowDefinitions[2].Height = new GridLength(paddingY);
            clippingGrid.ColumnDefinitions[0].Width = clippingGrid.ColumnDefinitions[2].Width = new GridLength(paddingX);
            clippingPanel.Width = image.ActualWidth;
            clippingPanel.Height = image.ActualHeight;
        }
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                BitmapImage image1 = new BitmapImage();
                using (IRandomAccessStream fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    image1.SetSource(fileStream);
                    WB_CapturedImage = new WriteableBitmap((int)image1.PixelWidth, (int)image1.PixelHeight);
                }
                using (IRandomAccessStream fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {

                    await WB_CapturedImage.SetSourceAsync(fileStream);
                    m_displayHeightNonScaled = WB_CapturedImage.PixelHeight;
                    m_displayWidthNonScaled = WB_CapturedImage.PixelWidth;
                }

            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
            }
        }

        private async void projectBtn_Click(object sender, RoutedEventArgs e)
        {
            rootPage = ((App)App.Current)._MainWindowHandle;

            if (rootPage.ProjectionViewPageControl == null)
            {
                double CropPanelHeight = clippingPanel.Height;
                double CropPanelWidth = clippingPanel.Width;
                double offsetX = clippingGrid.ColumnDefinitions[0].Width.Value - paddingX;
                double offsetY = clippingGrid.RowDefinitions[0].Height.Value - paddingY;
                double ImageWidth = image.ActualWidth;

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
                    initData.page = typeof(PictureEditorProject);
                    PageNavigateParam param = new PageNavigateParam();
                    param.MainViewId = thisViewId;
                    switch (stage)
                    {
                        case GLOABOALSTAGE.EDITPAGE_CROP:
                            param.stage = GLOABOALSTAGE.EDITPAGE_CROP;
                            CropStateParam cropParam = new CropStateParam();
                            cropParam.PicInfo = info;
                            cropParam.CropPanelHeight = CropPanelHeight;
                            cropParam.CropPanelWidth = CropPanelWidth;
                            cropParam.offsetX = offsetX;
                            cropParam.offsetY = offsetY;
                            cropParam.ImageWidth = ImageWidth;
                            param.stageParam = cropParam;
                            break;
                        case GLOABOALSTAGE.EDITPAGE_ROTATE:
                            param.stage = GLOABOALSTAGE.EDITPAGE_ROTATE;
                            RotateStateParam rotateParam = new RotateStateParam();
                            rotateParam.PicInfo = info;
                            rotateParam.UserRotation = m_userRotation;
                            param.stageParam = rotateParam;
                            break;
                        case GLOABOALSTAGE.EDITPAGE_INK:
                            param.stage = GLOABOALSTAGE.EDITPAGE_INK;
                            InkStateParam inkParam = new InkStateParam();
                            inkParam.PicInfo = info;
                            param.stageParam = inkParam;
                            //foreach (var item in strokes)
                            //{
                            //    ((App)App.Current).Strokes.Add(item.Clone());                                
                            //}
                            //((App)App.Current).SyncStrokeEx(strokeMapping, false);
                            inkTimer = new DispatcherTimer();
                            inkTimer.Tick += InkTimer_Tick;
                            inkTimer.Interval = new TimeSpan(0, 0, 2);
                            inkTimer.Start();
                            break;
                        case GLOABOALSTAGE.EDITPAGE_TEXT:
                            param.stage = GLOABOALSTAGE.EDITPAGE_TEXT;
                            TextStateParam textParam = new TextStateParam();
                            textParam.PicInfo = info;
                            param.stageParam = textParam;
                            break;
                    }
                    initData.naviParam = param;

                    // Display the page in the view. Note that the view will not become visible
                    // until "StartProjectingAsync" is called
                    var rootFrame = new Frame();
                    rootFrame.Navigate(typeof(HomeProject), initData);
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
                        if (stage != GLOABOALSTAGE.EDITPAGE_INK)
                        {
                            projectControlPanel.Visibility = Visibility.Visible;
                            editorGrid.Visibility = Visibility.Collapsed;
                            if (monitorTimer == null)
                            {
                                monitorTimer = new DispatcherTimer();
                                monitorTimer.Interval = new TimeSpan(0, 0, 2);
                                monitorTimer.Tick += MonitorTimer_Tick;
                                monitorTimer.Start();
                            }
                            else
                            {
                                monitorTimer.Start();
                            }
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
            if (!((App)App.Current).projectedFlag)
            {
                monitorTimer.Stop();
                projectControlPanel.Visibility = Visibility.Collapsed;
                editorGrid.Visibility = Visibility.Visible;
            }
        }

        private async void StopProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            await ProjectionManager.StopProjectingAsync(
                                            ((App)App.Current).projectedViewId,
                                            ApplicationView.GetForCurrentView().Id
                                            );
            ((App)App.Current).projectedViewId = 0;
        }

        #region crop
        private void clippingPanel_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {

        }
        private void clippingPanel_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            prePositon = e.Position;
            if (prePositon.X <= 5 && prePositon.Y <= 5)
            {
                positon = POSITIONS.LEFTTOP;
            }
            else if (prePositon.X >= ((sender as RelativePanel).ActualWidth - 5) && prePositon.Y <= 5)
            {
                positon = POSITIONS.RIGHTTOP;
            }
            else if (prePositon.X <= 5 && prePositon.Y >= ((sender as RelativePanel).ActualHeight - 5))
            {
                positon = POSITIONS.LEFTBUTTOM;
            }
            else if (prePositon.X >= ((sender as RelativePanel).ActualWidth - 5) && prePositon.Y >= ((sender as RelativePanel).ActualHeight - 5))
            {
                positon = POSITIONS.RIGHTBUTTOM;
            }
            else
            {
                positon = POSITIONS.UNKNOWN;
            }
        }

        private void clippingPanel_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            switch (positon)
            {
                case POSITIONS.LEFTTOP:
                    ManipulationDeltaLeftTop(sender, e);
                    break;
                case POSITIONS.RIGHTTOP:
                    ManipulationDeltaRightTop(sender, e);
                    break;
                case POSITIONS.LEFTBUTTOM:
                    ManipulationDeltaLeftButtom(sender, e);
                    break;
                case POSITIONS.RIGHTBUTTOM:
                    ManipulationDeltaRightButtom(sender, e);
                    break;
                case POSITIONS.UNKNOWN:
                    break;
            }
        }

        private void ManipulationDeltaLeftTop(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            double offsetX = e.Position.X - prePositon.X;
            double offsetY = e.Position.Y - prePositon.Y;
            if ((sender as RelativePanel).Width - offsetX < 100 || (sender as RelativePanel).Width - offsetX > image.ActualWidth || (sender as RelativePanel).Height - offsetY < 100 || (sender as RelativePanel).Height - offsetY > image.ActualHeight)
            {
                return;
            }
            if (clippingGrid.RowDefinitions[0].Height.Value + offsetY <= 0 || clippingGrid.ColumnDefinitions[0].Width.Value + offsetX <= 0)
            {
                return;
            }
            (sender as RelativePanel).Width -= offsetX;
            (sender as RelativePanel).Height -= offsetY;
            clippingGrid.RowDefinitions[0].Height = new GridLength(clippingGrid.RowDefinitions[0].Height.Value + offsetY);
            clippingGrid.ColumnDefinitions[0].Width = new GridLength(clippingGrid.ColumnDefinitions[0].Width.Value + offsetX);
            if (clippingGrid.RowDefinitions[0].Height.Value < (clippingGrid.ActualHeight - image.ActualHeight) / 2 || clippingGrid.ColumnDefinitions[0].Width.Value < (clippingGrid.ActualWidth - image.ActualWidth) / 2)
            {
                clippingGrid.RowDefinitions[0].Height = new GridLength(clippingGrid.RowDefinitions[0].Height.Value - offsetY);
                clippingGrid.ColumnDefinitions[0].Width = new GridLength(clippingGrid.ColumnDefinitions[0].Width.Value - offsetX);
                (sender as RelativePanel).Width += offsetX;
                (sender as RelativePanel).Height += offsetY;
            }
        }
        private void ManipulationDeltaRightTop(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            double offsetX = e.Position.X - prePositon.X;
            double offsetY = e.Position.Y - prePositon.Y;
            if ((sender as RelativePanel).Width + offsetX < 100 || (sender as RelativePanel).Width + offsetX > image.ActualWidth || (sender as RelativePanel).Height - offsetY < 100 || (sender as RelativePanel).Height - offsetY > image.ActualHeight)
            {
                return;
            }
            if (clippingGrid.RowDefinitions[0].Height.Value + offsetY <= 0 || clippingGrid.ColumnDefinitions[2].Width.Value - offsetX <= 0)
            {
                return;
            }
            (sender as RelativePanel).Width += offsetX;
            (sender as RelativePanel).Height -= offsetY;
            clippingGrid.RowDefinitions[0].Height = new GridLength(clippingGrid.RowDefinitions[0].Height.Value + offsetY);
            clippingGrid.ColumnDefinitions[2].Width = new GridLength(clippingGrid.ColumnDefinitions[2].Width.Value - offsetX);
            if (clippingGrid.RowDefinitions[0].Height.Value < (clippingGrid.ActualHeight - image.ActualHeight) / 2 || clippingGrid.ColumnDefinitions[2].Width.Value < (clippingGrid.ActualWidth - image.ActualWidth) / 2)
            {
                clippingGrid.RowDefinitions[0].Height = new GridLength(clippingGrid.RowDefinitions[0].Height.Value - offsetY);
                clippingGrid.ColumnDefinitions[2].Width = new GridLength(clippingGrid.ColumnDefinitions[2].Width.Value + offsetX);
                (sender as RelativePanel).Width -= offsetX;
                (sender as RelativePanel).Height += offsetY;
                return;
            }
            prePositon.X = e.Position.X;
        }

        private void ManipulationDeltaLeftButtom(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            double offsetX = e.Position.X - prePositon.X;
            double offsetY = e.Position.Y - prePositon.Y;
            if ((sender as RelativePanel).Width - offsetX < 100 || (sender as RelativePanel).Width - offsetX > image.ActualWidth || (sender as RelativePanel).Height + offsetY < 100 || (sender as RelativePanel).Height + offsetY > image.ActualHeight)
            {
                return;
            }
            if (clippingGrid.RowDefinitions[2].Height.Value - offsetY <= 0 || clippingGrid.ColumnDefinitions[0].Width.Value + offsetX <= 0)
            {
                return;
            }
            (sender as RelativePanel).Width -= offsetX;
            (sender as RelativePanel).Height += offsetY;
            clippingGrid.RowDefinitions[2].Height = new GridLength(clippingGrid.RowDefinitions[2].Height.Value - offsetY);
            clippingGrid.ColumnDefinitions[0].Width = new GridLength(clippingGrid.ColumnDefinitions[0].Width.Value + offsetX);
            if (clippingGrid.RowDefinitions[2].Height.Value < (clippingGrid.ActualHeight - image.ActualHeight) / 2 || clippingGrid.ColumnDefinitions[0].Width.Value < (clippingGrid.ActualWidth - image.ActualWidth) / 2)
            {
                clippingGrid.RowDefinitions[2].Height = new GridLength(clippingGrid.RowDefinitions[2].Height.Value + offsetY);
                clippingGrid.ColumnDefinitions[0].Width = new GridLength(clippingGrid.ColumnDefinitions[0].Width.Value - offsetX);
                (sender as RelativePanel).Width += offsetX;
                (sender as RelativePanel).Height -= offsetY;
                return;
            }
            prePositon.Y = e.Position.Y;
        }

        private void ManipulationDeltaRightButtom(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            double offsetX = e.Position.X - prePositon.X;
            double offsetY = e.Position.Y - prePositon.Y;
            if ((sender as RelativePanel).Width + offsetX < 100 || (sender as RelativePanel).Width + offsetX > image.ActualWidth || (sender as RelativePanel).Height + offsetY < 100 || (sender as RelativePanel).Height + offsetY > image.ActualHeight)
            {
                return;
            }
            if (clippingGrid.RowDefinitions[2].Height.Value - offsetY <= 0 || clippingGrid.ColumnDefinitions[2].Width.Value - offsetX <= 0)
            {
                return;
            }
            (sender as RelativePanel).Width += offsetX;
            (sender as RelativePanel).Height += offsetY;
            clippingGrid.RowDefinitions[2].Height = new GridLength(clippingGrid.RowDefinitions[2].Height.Value - offsetY);
            clippingGrid.ColumnDefinitions[2].Width = new GridLength(clippingGrid.ColumnDefinitions[2].Width.Value - offsetX);
            if (clippingGrid.RowDefinitions[2].Height.Value < (clippingGrid.ActualHeight - image.ActualHeight) / 2 || clippingGrid.ColumnDefinitions[2].Width.Value < (clippingGrid.ActualWidth - image.ActualWidth) / 2)
            {
                clippingGrid.RowDefinitions[2].Height = new GridLength(clippingGrid.RowDefinitions[2].Height.Value + offsetY);
                clippingGrid.ColumnDefinitions[2].Width = new GridLength(clippingGrid.ColumnDefinitions[2].Width.Value + offsetX);
                (sender as RelativePanel).Width -= offsetX;
                (sender as RelativePanel).Height -= offsetY;
                return;
            }
            prePositon = e.Position;
        }

        private async void SaveCropedPicture()
        {
            try
            {
                double originalImageWidth = WB_CapturedImage.PixelWidth;
                double originalImageHeight = WB_CapturedImage.PixelHeight;

                double displayedWidth = image.ActualWidth;
                double displayedHeight = image.ActualHeight;

                double widthRatio = originalImageWidth / displayedWidth;
                double heightRatio = originalImageHeight / displayedHeight;

                int with = (int)(widthRatio * clippingPanel.Width);
                int height = (int)(heightRatio * clippingPanel.Height);
                WB_CroppedImage = new WriteableBitmap(with, height);

                int xoffset = (int)((clippingGrid.ColumnDefinitions[0].Width.Value - colOriginalWidth) * widthRatio);
                xoffset *= 4;
                int yoffset = (int)((clippingGrid.RowDefinitions[0].Height.Value - rowOriginalHeight) * heightRatio);

                if (WB_CroppedImage.PixelBuffer.Length > 0)
                {
                    uint cout = WB_CapturedImage.PixelBuffer.Length;
                    cout = WB_CroppedImage.PixelBuffer.Length;
                    byte[] data = WB_CapturedImage.PixelBuffer.ToArray();
                    byte[] cropData = WB_CroppedImage.PixelBuffer.ToArray();
                    for (int i = 0; i < cropData.Length; i++)
                    {
                        int x = Convert.ToInt32((i % (WB_CroppedImage.PixelWidth * 4)) + xoffset);//(int)((i % WB_CroppedImage.PixelWidth) + xoffset);
                        int y = Convert.ToInt32((i / (WB_CroppedImage.PixelWidth * 4)) + yoffset);//(int)((i / WB_CroppedImage.PixelWidth) + yoffset);
                        cropData[i] = data[y * WB_CapturedImage.PixelWidth * 4 + x];
                        // cropData[i] = data[i];


                    }
                    cropData.CopyTo(WB_CroppedImage.PixelBuffer);
                    StorageFolder savedPics = KnownFolders.PicturesLibrary;
                    string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".png";
                    StorageFile newFile = await savedPics.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    IRandomAccessStream streamOut = await newFile.OpenAsync(FileAccessMode.ReadWrite);
                    BitmapEncoder pngEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, streamOut);
                    pngEncoder.SetPixelData(BitmapPixelFormat.Bgra8,
                                            BitmapAlphaMode.Premultiplied,
                                            (uint)WB_CroppedImage.PixelWidth,
                                            (uint)WB_CroppedImage.PixelHeight,
                                            96d, 96d, cropData);
                    await pngEncoder.FlushAsync();
                    streamOut.Dispose();
                    image.Source = WB_CroppedImage;
                    ResetClippingGrid();
                    filePath = newFile.Path;
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
            }
        }

        private void ResetClippingGrid()
        {
            double paddingX = (clippingGrid.ActualWidth - image.ActualWidth) / 2;
            double paddingY = (clippingGrid.ActualHeight - image.ActualHeight) / 2;
            colOriginalWidth = paddingX;
            rowOriginalHeight = paddingY;
            clippingGrid.RowDefinitions[0].Height = clippingGrid.RowDefinitions[2].Height = new GridLength(paddingY);
            clippingGrid.ColumnDefinitions[0].Width = clippingGrid.ColumnDefinitions[2].Width = new GridLength(paddingX);
            clippingPanel.Width = image.ActualWidth;
            clippingPanel.Height = image.ActualHeight;
            //clippingGrid.Visibility = Visibility.Visible;
        }
        #endregion

        private void acceptBtn_Click(object sender, RoutedEventArgs e)
        {
            editMenuGrid.Visibility = Visibility.Collapsed;
            menuList.Visibility = Visibility.Visible;
            switch (stage)
            {
                case GLOABOALSTAGE.EDITPAGE_CROP:
                    SaveCropedPicture();
                    break;
                case GLOABOALSTAGE.EDITPAGE_ROTATE:
                    SaveRotatePicture();
                    break;
                case GLOABOALSTAGE.EDITPAGE_INK:
                    SaveInkPicture();
                    break;
                case GLOABOALSTAGE.EDITPAGE_TEXT:
                    SaveTextPicture();
                    break;
            }
        }

        private async void SaveInkPicture()
        {
            if (ink.Width == 0)
            {
                return;
            }
             //ink.Width = WB_CapturedImage.PixelWidth;
             //ink.Height = WB_CapturedImage.PixelHeight;
             ((App)App.Current).SyncStrokeEx(strokeMapping, ink.InkPresenter.StrokeContainer, ink.Width, true);
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            //CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, WB_CapturedImage.PixelWidth, WB_CapturedImage.PixelHeight, 96);
            CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (float)ink.Width, (float)ink.Height, 96);
            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.Clear(Color.FromArgb(0, 255, 255, 255));
                ds.DrawInk(ink.InkPresenter.StrokeContainer.GetStrokes());
            }
            StorageFolder savedPics = KnownFolders.PicturesLibrary;
            string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".png";
            StorageFile file = await savedPics.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
            }
            using (IRandomAccessStream fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                //image.SetSource(fileStream);
                WriteableBitmap InkImage = new WriteableBitmap(WB_CapturedImage.PixelWidth, WB_CapturedImage.PixelHeight);
                await InkImage.SetSourceAsync(fileStream);
                imageInk.Source = InkImage;
                imageInk.Visibility = Visibility.Visible;
                ink.InkPresenter.StrokeContainer.Clear();
                ink.Visibility = Visibility.Collapsed;
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(this.imagePanel);
            // 提取像素数据
            IBuffer buffer = await rtb.GetPixelsAsync();

            // 获取文件流
            IRandomAccessStream streamOut = await file.OpenAsync(FileAccessMode.ReadWrite);
            // 实例化编码器
            BitmapEncoder pngEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, streamOut);
            //重写writeablebitmap
            WB_CapturedImage = new WriteableBitmap((int)rtb.PixelWidth, (int)rtb.PixelHeight);
            // 写入像素数据
            byte[] data = buffer.ToArray();
            pngEncoder.SetPixelData(BitmapPixelFormat.Bgra8,
                                    BitmapAlphaMode.Straight,
                                    (uint)rtb.PixelWidth,
                                    (uint)rtb.PixelHeight,
                                    96d, 96d, data);
            await pngEncoder.FlushAsync();
            streamOut.Dispose();

            ink.Visibility = Visibility.Visible;
            imageInk.Visibility = Visibility.Collapsed;

            using (IRandomAccessStream fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                await WB_CapturedImage.SetSourceAsync(fileStream);
                image.Source = WB_CapturedImage;
            }

            //var bmp = new RenderTargetBitmap();          
            //await bmp.RenderAsync(ink);
            //StorageFolder savedPics = KnownFolders.PicturesLibrary;
            //string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".png";
            //StorageFile file = await savedPics.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            //// Saving to file.  
            //using (var stream = await file.OpenStreamForWriteAsync())
            //{
            //    // Initialization.  
            //    var pixelBuffer = await bmp.GetPixelsAsync();
            //    var logicalDpi = DisplayInformation.GetForCurrentView().LogicalDpi;
            //    // convert stream to IRandomAccessStream  
            //    var randomAccessStream = stream.AsRandomAccessStream();
            //    // encoding to PNG  
            //    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, randomAccessStream);
            //    // Finish saving  
            //    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)bmp.PixelWidth,
            //               (uint)bmp.PixelHeight, logicalDpi, logicalDpi, pixelBuffer.ToArray());
            //    // Flush encoder.  
            //    await encoder.FlushAsync();
            //    filePath = file.Path;
            //}
        }

        private async void SaveTextPicture()
        {
            try
            {
                StorageFolder savedPics = KnownFolders.PicturesLibrary;
                IReadOnlyList<StorageFolder> folders = await savedPics.GetFoldersAsync();
                foreach (var item in folders)
                {
                    if (item.Name.Equals("Camera Roll"))
                    {
                        savedPics = item;
                        break;
                    }
                }
                string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".png";
                // 1、在位图中呈现UI元素
                RenderTargetBitmap rtb = new RenderTargetBitmap();
                await rtb.RenderAsync(this.textCanvas);
                // 提取像素数据
                IBuffer buffer = await rtb.GetPixelsAsync();

                // 创建新文件
                StorageFile newFile = await savedPics.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                using (IRandomAccessStream streamOut = await newFile.OpenAsync(FileAccessMode.ReadWrite))
                {

                    // 实例化编码器
                    BitmapEncoder pngEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, streamOut);
                    // 写入像素数据
                    byte[] data = buffer.ToArray();
                    pngEncoder.SetPixelData(BitmapPixelFormat.Bgra8,
                                            BitmapAlphaMode.Straight,
                                            (uint)rtb.PixelWidth,
                                            (uint)rtb.PixelHeight,
                                            96d, 96d, data);
                    await pngEncoder.FlushAsync();
                    streamOut.Dispose();
                }
                //StorageFile file = await StorageFile.GetFileFromPathAsync(newFile.Path);
                //StorageFile newFile1 = await savedPics.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists); 
                using (IRandomAccessStream streamOut = await newFile.OpenAsync(FileAccessMode.Read))
                {
                    BitmapImage image1 = new BitmapImage();
                    image1.SetSource(streamOut);
                    image.Source = image1;
                    WB_CapturedImage = new WriteableBitmap((int)image1.PixelWidth, (int)image1.PixelHeight);
                    streamOut.Dispose();
                }
                //WB_CapturedImage.SetSource(streamOut);
                using (IRandomAccessStream streamOut = await newFile.OpenAsync(FileAccessMode.Read))
                {
                    await WB_CapturedImage.SetSourceAsync(streamOut);
                    m_displayHeightNonScaled = WB_CapturedImage.PixelHeight;
                    m_displayWidthNonScaled = WB_CapturedImage.PixelWidth;
                    //image.Source = WB_CapturedImage;
                    streamOut.Dispose();
                }
                //textCanvas.ClearValue(BackgroundProperty);
                textCanvas.Width = image.ActualWidth;
                textCanvas.Height = image.ActualHeight;
            }
            catch (Exception e)
            {
                textCanvas.ClearValue(BackgroundProperty);
#if DEBUG
                System.Diagnostics.Debug.WriteLine(e.Message);
#endif
            }
        }
        private async void SaveRotatePicture()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.ReadWrite),
                                           memStream = new InMemoryRandomAccessStream())
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);
                    uint originalWidth = decoder.PixelWidth;
                    uint originalHeight = decoder.PixelHeight;

                    // Set the encoder's destination to the temporary, in-memory stream.
                    BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(memStream, decoder);
                    encoder.BitmapTransform.Rotation = Helpers.ConvertToBitmapRotation(m_userRotation);
                    encoder.IsThumbnailGenerated = false;//test
                    if (encoder.IsThumbnailGenerated == false)
                    {
                        await encoder.FlushAsync();
                    }

                    // Now that the file has been written to the temporary stream, copy the data to the file.
                    memStream.Seek(0);
                    fileStream.Seek(0);
                    fileStream.Size = 0;
                    await RandomAccessStream.CopyAsync(memStream, fileStream);
                }

                // Because the original file has been overwritten, reload it in the UI.
                ResetSessionState();
                await DisplayImageFileAsync(file);

            }
            catch (Exception e)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(e.Message);
#endif              
                ResetSessionState();
            }
        }

        private void ResetSessionState()
        {
            m_displayHeightNonScaled = 0;
            m_displayWidthNonScaled = 0;
            m_userRotation = PhotoOrientation.Normal;
            m_transform.Angle = 0;
        }

        private async Task DisplayImageFileAsync(StorageFile file)
        {
            // Request persisted access permissions to the file the user selected.

            BitmapImage src = new BitmapImage();
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            {
                await src.SetSourceAsync(stream);
            }

            image.Source = src;
        }

        private void discardBtn_Click(object sender, RoutedEventArgs e)
        {
            editMenuGrid.Visibility = Visibility.Collapsed;
            menuList.Visibility = Visibility.Visible;
            switch (stage)
            {
                case GLOABOALSTAGE.EDITPAGE_CROP:
                    ResetCrop();
                    break;
                case GLOABOALSTAGE.EDITPAGE_ROTATE:
                    m_transform.Angle = 0;
                    break;
                case GLOABOALSTAGE.EDITPAGE_INK:
                    ink.InkPresenter.StrokeContainer.Clear();
                    break;
                default:
                    break;
            }
        }

        private async void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            (sender as AppBarButton).Icon.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 169, 3));
            (sender as AppBarButton).Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 169, 3));
            AppBarButton bar = sender as AppBarButton;
            foreach (var item in barList)
            {
                if (item.Equals(bar))
                {
                    item.Icon.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 169, 3));
                    item.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 169, 3));
                }
                else
                {
                    item.Icon.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                    item.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                }
            }
            if (WB_CapturedImage == null)
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                BitmapImage image1 = new BitmapImage();
                using (IRandomAccessStream fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    image1.SetSource(fileStream);
                    WB_CapturedImage = new WriteableBitmap((int)image1.PixelWidth, (int)image1.PixelHeight);
                }
                using (IRandomAccessStream fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {

                    await WB_CapturedImage.SetSourceAsync(fileStream);
                    m_displayHeightNonScaled = WB_CapturedImage.PixelHeight;
                    m_displayWidthNonScaled = WB_CapturedImage.PixelWidth;
                }
            }
            if (clippingGrid.Visibility == Visibility.Collapsed)
            {
                ResetClippingGrid();
            }
        }

        private void rotateBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button).Equals(rotateBtn))
            {
                rotateBtnText.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 168, 1));
                rotateBtnImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/MenuIco/icon 07 2.png"));
                rotateBtnTextRevert.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                rotateBtnImageRevert.Source = new BitmapImage(new Uri("ms-appx:///Assets/MenuIco/icon 08.png"));
                m_transform.CenterX = imagePanel.ActualWidth / 2;
                m_transform.CenterY = imagePanel.ActualHeight / 2;
                imagePanel.RenderTransform = m_transform;
                m_userRotation = Helpers.Add90DegreesCW(m_userRotation);

                //Swap width and height.
                int temp = m_displayHeightNonScaled;
                m_displayHeightNonScaled = m_displayWidthNonScaled;
                m_displayWidthNonScaled = temp;
                UpdateImageRotation(m_userRotation);
            }
            else
            {
                rotateBtnText.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                rotateBtnImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/MenuIco/icon 07.png"));
                rotateBtnTextRevert.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 168, 1));
                rotateBtnImageRevert.Source = new BitmapImage(new Uri("ms-appx:///Assets/MenuIco/icon 08 2.png"));
                m_transform.CenterX = imagePanel.ActualWidth / 2;
                m_transform.CenterY = imagePanel.ActualHeight / 2;
                imagePanel.RenderTransform = m_transform;
                m_userRotation = Helpers.Add90DegreesCCW(m_userRotation);

                // Swap width and height.
                int temp = m_displayHeightNonScaled;
                m_displayHeightNonScaled = m_displayWidthNonScaled;
                m_displayWidthNonScaled = temp;
                UpdateImageRotation(m_userRotation);
            }
        }

        private void UpdateImageRotation(PhotoOrientation rotation)
        {
            switch (rotation)
            {
                case PhotoOrientation.Rotate270:
                    // Note that the PhotoOrientation enumeration uses a counterclockwise convention,
                    // while the RotationTransform uses a clockwise convention.
                    m_transform.Angle = 90;
                    break;
                case PhotoOrientation.Rotate180:
                    m_transform.Angle = 180;
                    break;
                case PhotoOrientation.Rotate90:
                    m_transform.Angle = 270;
                    break;
                case PhotoOrientation.Normal:
                default:
                    m_transform.Angle = 0;
                    break;
            }
        }

        private void inkMenuList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {           
            if ((sender as ListBox).SelectedItem == null)
            {
                return;
            }
            if ((sender as ListBox).SelectedItem.Equals(eraserItem))
            {
                ink.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Erasing;
                inkMenuPanel.Visibility = Visibility.Collapsed;
            }
            else if ((sender as ListBox).SelectedItem.Equals(penItem))
            {
                ink.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
                inkMenuPanel.Visibility = Visibility.Visible;
                penPanel.Visibility = Visibility.Visible;
                colorPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ink.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
                inkMenuPanel.Visibility = Visibility.Visible;
                penPanel.Visibility = Visibility.Collapsed;
                colorPanel.Visibility = Visibility.Visible;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            InkDrawingAttributes drawingAttributes = ink.InkPresenter.CopyDefaultDrawingAttributes();
            double penSize = (sender as Button).Width / 5;
            drawingAttributes.Size = new Size(penSize, penSize);
            ink.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
            inkMenuPanel.Visibility = Visibility.Collapsed;
            inkMenuList.SelectedIndex = -1;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            InkDrawingAttributes drawingAttributes = ink.InkPresenter.CopyDefaultDrawingAttributes();
            var btnSender = sender as Button;
            var brush = btnSender.Foreground as Windows.UI.Xaml.Media.SolidColorBrush;

            drawingAttributes.Color = brush.Color;
            ink.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
            inkMenuPanel.Visibility = Visibility.Collapsed;
        }

        private void saveBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveBtn_Click_1(object sender, RoutedEventArgs e)
        {
            ((App)App.Current).save = true;
        }
    }
}
