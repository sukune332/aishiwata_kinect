using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using System.ComponentModel;

namespace KinectApp
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        ColorImageFormat rgbFormat = ColorImageFormat.RgbResolution640x480Fps30;    // RGBカメラの解像度・フレームレート
        byte[] pixelBuffer = null;  // Kinectからの画像情報を受け取るバッファ
        RenderTargetBitmap bmpBuffer = null;   // 画面用のビットマップ
        Skeleton[] skeletonBuffer = null;   // kinectからの骨格情報を受け取るバッファ
        BitmapImage maskImage = null;   // 顔マスク用画像（今回未使用）
        DrawingVisual drawVisual = new DrawingVisual(); // ビットマップへの描画用DrawingVisual
        KinectSensorChooser kinectChooser = new KinectSensorChooser();  // 起動・終了処理用のSensorChooser

        bool checkedFlag = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            kinectChooser.KinectChanged += kinectChooser_KinectChanged;
            kinectChooser.PropertyChanged += kinectChooser_PropertyChanged;
            kinectChooser.Start();
        }

        void kinectChooser_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if("Status".Equals(e.PropertyName))
                Console.WriteLine("Status: "+kinectChooser.Status);
        }

        void kinectChooser_KinectChanged(object sender, KinectChangedEventArgs e)
        {
            if (e.OldSensor != null)
                UnInitKinectSensor(e.OldSensor);

            if (e.NewSensor != null)
                InitKinectSensor(e.NewSensor);
        }

        private void UnInitKinectSensor(KinectSensor kinectSensor)
        {
            kinectSensor.AllFramesReady -= AllFrameReady;
            
        }

        private void InitKinectSensor(KinectSensor kinect)
        {
            //KinectSensor kinect = KinectSensor.KinectSensors[0];    // センサーの取得
            kinect.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;

            maskImage = new BitmapImage();

            ColorImageStream clrStream = kinect.ColorStream;    // RGBカメラストリームの取得
            clrStream.Enable(rgbFormat);  // 取得するフォーマットを指定して有効化
            SkeletonStream skelStream = kinect.SkeletonStream;  // 骨格ストリームの取得
            skelStream.Enable();

            pixelBuffer = new byte[kinect.ColorStream.FramePixelDataLength];    // ピクセルデータの量だけ配列作成
            skeletonBuffer = new Skeleton[skelStream.FrameSkeletonArrayLength]; // 骨格データの量だけ配列作成
            bmpBuffer = new RenderTargetBitmap(clrStream.FrameWidth, clrStream.FrameHeight, 96, 96, PixelFormats.Default);
            rgbImage.Source = bmpBuffer;    // 画像をImageに入れる

            //kinect.ColorFrameReady += ColorImageReady;  // イベントハンドラ登録
            kinect.AllFramesReady += AllFrameReady;

            kinect.Start(); // センサーからのストリーム取得を開始
        }

        private void AllFrameReady(object sender, AllFramesReadyEventArgs e)
        {
            KinectSensor kinect = sender as KinectSensor;
            List<SkeletonPoint> headList = null;

            // 骨格情報から、頭の座標リストを作成
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    //headList = getHeadPoints(skeletonFrame);
                    headList = checkPosture(skeletonFrame);
                }
            }

            using (ColorImageFrame imageFrame = e.OpenColorImageFrame())
            {
                if (imageFrame != null)
                    fillBitmap(kinect, imageFrame, headList);
            }
        }

        private void fillBitmap(KinectSensor kinect, ColorImageFrame imgFrame, List<SkeletonPoint> headList)
        {
            var drawContext = drawVisual.RenderOpen();
            int frmWidth = imgFrame.Width;
            int frmHeight = imgFrame.Height;

            imgFrame.CopyPixelDataTo(pixelBuffer);

            var bgImg = new WriteableBitmap(frmWidth, frmHeight, 96, 96, PixelFormats.Bgr32, null);
            bgImg.WritePixels(new Int32Rect(0, 0, frmWidth, frmHeight),pixelBuffer,frmWidth*4,0);
            drawContext.DrawImage(bgImg, new Rect(0, 0, frmWidth, frmHeight));

            for (int idx = 0; headList != null && idx < headList.Count; ++idx)
            {
                ColorImagePoint headPt = kinect.MapSkeletonPointToColor(headList[idx], rgbFormat);
                Rect rect = new Rect(headPt.X - 64, headPt.Y - 64, 128, 128);
                //drawContext.DrawImage(maskImage, rect);
                //Console.WriteLine("headPt.X:"+headPt.X+" headPt.Y:"+headPt.Y);
            }

            drawContext.Close();
            bmpBuffer.Render(drawVisual);
        }

        private List<SkeletonPoint> getHeadPoints(SkeletonFrame skelFrame)
        {
            List<SkeletonPoint> results = new List<SkeletonPoint>();
            skelFrame.CopySkeletonDataTo(skeletonBuffer);
            foreach (Skeleton skeleton in skeletonBuffer)
            {
                if (skeleton.TrackingState != SkeletonTrackingState.Tracked)
                    continue;

                Joint head = skeleton.Joints[JointType.Head];

                if (head.TrackingState != JointTrackingState.Tracked &&
                    head.TrackingState != JointTrackingState.Inferred)
                    continue;

                results.Add(head.Position);
            }
            return results;
        }

        List<SkeletonPoint> checkPosture(SkeletonFrame sf)
        {
            List<SkeletonPoint> results = new List<SkeletonPoint>();
            sf.CopySkeletonDataTo(skeletonBuffer);
            
            // 人毎のループかな（最大6人）
            foreach (Skeleton skeleton in skeletonBuffer)
            {
                if (skeleton.TrackingState != SkeletonTrackingState.Tracked)
                    continue;

                Joint head = skeleton.Joints[JointType.Head];
                Joint rWrist = skeleton.Joints[JointType.WristRight];
                Joint rElbow = skeleton.Joints[JointType.ElbowRight];
                Joint lElbow = skeleton.Joints[JointType.ElbowLeft];
                Joint lShoulder = skeleton.Joints[JointType.ShoulderLeft];

                if ((head.TrackingState != JointTrackingState.Tracked &&
                    head.TrackingState != JointTrackingState.Inferred)
                    || (rWrist.TrackingState != JointTrackingState.Tracked &&
                    rWrist.TrackingState != JointTrackingState.Inferred)
                    || (rElbow.TrackingState != JointTrackingState.Tracked &&
                    rElbow.TrackingState != JointTrackingState.Inferred)
                    || (lElbow.TrackingState != JointTrackingState.Tracked &&
                    lElbow.TrackingState != JointTrackingState.Inferred)
                    || (lShoulder.TrackingState != JointTrackingState.Tracked &&
                    lShoulder.TrackingState != JointTrackingState.Inferred))
                    continue;


                printText(skeleton);


                if (head.Position.X > rWrist.Position.X)
                {
                    if (!checkedFlag)
                    {
                        checkedFlag = true;
                        flagrect.Fill = Brushes.Red;
                    }
                }
                else
                {
                    if (checkedFlag)
                    {
                        checkedFlag = false;
                        flagrect.Fill = Brushes.Blue;
                    }
                }

                results.Add(head.Position);
            }

            return results;
        }

        public void printText(Skeleton sk)
        {
            Joint head = sk.Joints[JointType.Head];
            Joint rWrist = sk.Joints[JointType.WristRight];

            textHeadx.Text = "X:" + head.Position.X;
            textHeady.Text = "Y:" + head.Position.Y;
            textHeadz.Text = "Z:" + head.Position.Z;
            textrwristx.Text = "X:" + rWrist.Position.X;
            textrwristy.Text = "Y:" + rWrist.Position.Y;
            textrwristz.Text = "Z:" + rWrist.Position.Z;

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            kinectChooser.Stop();
        }


        /*
        private void ColorImageReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame imageFrame = e.OpenColorImageFrame())
            {
                if (imageFrame != null)
                {
                    // 画像の大きさを取得
                    int frmWidth = imageFrame.Width;
                    int frmHeight = imageFrame.Height;

                    imageFrame.CopyPixelDataTo(pixelBuffer);    // もらえた画像をバッファにコピーする

                    Int32Rect src = new Int32Rect(0, 0, frmWidth, frmHeight);   // 大きさの画像枠を作って
                    bmpBuffer.WritePixels(src, pixelBuffer, frmWidth * 4, 0);   // ビットマップに描画
                }
            }
        }
         */
    }
}
