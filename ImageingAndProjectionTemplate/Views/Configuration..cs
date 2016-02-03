using System;
using System.Collections.Generic;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.Storage.FileProperties;
using ImageingAndProjectionTemplate.Common;

namespace ImageingAndProjectionTemplate.Views
{
    //public partial class Home : Page
    //{
    //    public const string FEATURE_NAME = "ImageingAndProjection";      
    //    // Keep track of the view that's being projected
    //    public ViewLifetimeControl ProjectionViewPageControl;
    //}

    internal class ProjectionViewPageInitializationData
    {
        public CoreDispatcher MainDispatcher;
        public ViewLifetimeControl ProjectionViewPageControl;
        public int MainViewId;
        public Type page;
        public PageNavigateParam naviParam;
    }

    public enum GLOABOALSTAGE { MAINPAGE_PICLIST = 1, MAINPAGE_PICFLIP, EDITPAGE_CROP, EDITPAGE_ROTATE, EDITPAGE_TEXT, EDITPAGE_INK }
    public class PageNavigateParam
    {
        public int MainViewId { get; set; }
        public GLOABOALSTAGE stage { get; set; }
        public object stageParam { get; set; }
    }
    public class CropStateParam
    {
        //public double CropGridWidth { get; set; }
        //public double CropGridHeight { get; set; }
        public PictureListInfo PicInfo { get; set; }
        //public double Row0Height { get; set; }
        //public double Column0Width { get; set; }
        public double CropPanelWidth { get; set; }
        public double CropPanelHeight { get; set; }

        public double ImageWidth { get; set; }
        public double offsetX { get; set; }
        public double offsetY { get; set; }
    }
    public class RotateStateParam
    {
        public PictureListInfo PicInfo { get; set; }
        public PhotoOrientation UserRotation { get; set; }
    }

    public class InkStateParam
    {
        public PictureListInfo PicInfo { get; set; }
    }

    public class TextStateParam
    {
        public PictureListInfo PicInfo { get; set; }
    }
}
