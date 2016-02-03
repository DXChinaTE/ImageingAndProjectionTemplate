using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ImageingAndProjectionTemplate.Views;
using ImageingAndProjectionTemplate.Common;

namespace ImageingAndProjectionTemplate
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public App _MainWindowHandle = null;
        public ViewLifetimeControl ProjectionViewPageControl;
        public LaunchActivatedEventArgs LaunchArgs;
        public GLOABOALSTAGE gloaboalState = GLOABOALSTAGE.MAINPAGE_PICLIST;
        public int selectedPictureIndex = 0;
        public bool save = false;
        public List<InkStroke> Strokes = new List<InkStroke>();
        private Dictionary<InkStroke, double> StrokeMapping = new Dictionary<InkStroke, double>();
        public bool projectedFlag = false;
        public int projectedViewId = 0;
        public double originalCanvasWidth = 0;
        private enum OPTIONS { GET = 1, INCREASE, DECREASE };
        private object locker = new object();

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            _MainWindowHandle = this;       
        }

        public int GetOrSetIndex(int option)
        {
            lock (locker)
            {
                int index = 0;         
                switch (option)
                {
                    case (int)OPTIONS.GET:
                        index = selectedPictureIndex;
                        selectedPictureIndex = 0;
                        break;
                    case (int)OPTIONS.INCREASE:
                        selectedPictureIndex = 1;
                        break;
                    case (int)OPTIONS.DECREASE:
                        selectedPictureIndex = -1;
                        break;
                    default:
                        index = 0;
                        break;

                }
                return index;
            }
        }

        public void SyncStrokeEx(Dictionary<InkStroke,double> strokes, InkStrokeContainer containner, double currentCanvasWidth,bool forcrRefresh = false)
        {
            lock(locker)
            {
                if(strokes.Count > containner.GetStrokes().Count)
                {
                    foreach (var item in strokes)
                    {
                        bool exist = false;
                        foreach (var itemInner in containner.GetStrokes())
                        {
                            InkStroke strokeCopy = itemInner.Clone();
                            strokeCopy.PointTransform = System.Numerics.Matrix3x2.CreateScale(1f);
                            if (InkHelper.SameInkStroke(item.Key, strokeCopy))
                            {
                                exist = true;
                                break;
                            }
                        }
                        if (!exist)
                        {
                            foreach(var itemMp in StrokeMapping)
                            {
                                if(InkHelper.SameInkStroke(item.Key,itemMp.Key) && item.Value == itemMp.Value)
                                {
                                    StrokeMapping.Remove(itemMp.Key);
                                    break;
                                }
                            }                           
                        }
                    }
                }
                else if(strokes.Count < containner.GetStrokes().Count)
                {
                    InkStroke stroke = containner.GetStrokes()[containner.GetStrokes().Count - 1].Clone();
                    StrokeMapping.Add(stroke.Clone(),currentCanvasWidth);
                }
                else
                {
                    if(strokes.Count == StrokeMapping.Count && !forcrRefresh)
                    {
                        return;
                    }
                }
                strokes.Clear();
                containner.Clear();
                foreach(var item in StrokeMapping)
                {
                    InkStroke stroke = item.Key.Clone();
                    strokes.Add(stroke.Clone(), item.Value);
                    stroke.PointTransform = System.Numerics.Matrix3x2.CreateScale((float)(currentCanvasWidth / item.Value));
                    containner.AddStroke(stroke.Clone());
                }
                //if(read)
                //{
                //    strokes.Clear();
                //    foreach(var item in StrokeMapping)
                //    {
                //        strokes.Add(item.Key.Clone(),item.Value);
                //    }
                //}
                //else
                //{
                //    StrokeMapping.Clear();
                //    foreach(var item in strokes)
                //    {
                //        StrokeMapping.Add(item.Key.Clone(),item.Value);
                //    }
                //}
            }
        }
        public void SyncStroke(InkStrokeContainer containner,double currentCanvasWidth,bool syncAll = true, InkStroke stroke = null)
        {
            lock(locker)
            {
                try
                {
                    if (syncAll)
                    {
                        Strokes.Clear();
                        foreach (var item in containner.GetStrokes())
                        {
                            InkStroke strokeClone = item.Clone();
                            strokeClone.PointTransform = System.Numerics.Matrix3x2.CreateScale((float)(currentCanvasWidth / originalCanvasWidth));
                            Strokes.Add(strokeClone);
                        }
                    }
                    else
                    {
                        if (null != stroke)
                        {
                            InkStroke strokeClone = stroke.Clone();
                            strokeClone.PointTransform = System.Numerics.Matrix3x2.CreateScale((float)(currentCanvasWidth / originalCanvasWidth));
                            Strokes.Add(strokeClone);
                        }
                        else
                        {
                            containner.Clear();
                            foreach (var item in Strokes)
                            {
                                InkStroke strokeClone = item.Clone();
                                strokeClone.PointTransform = System.Numerics.Matrix3x2.CreateScale((float)(currentCanvasWidth / originalCanvasWidth));
                                containner.AddStroke(strokeClone);
                            }
                        }
                    }
                }
                catch(Exception e)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(e.Message);

#endif
                }                           
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter              
                rootFrame.Navigate(typeof(Home), e.Arguments);             
            }
            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
