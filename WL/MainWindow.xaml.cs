// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WL
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TransformedBitmap _mainImage = null;
        private ImageFile _originalImage = null;
        private CursorAdorner CursorSelector;
        private PreciseAdoner PreciseSelector;

        private WLParams _generalParams = new WLParams();

        private CollectionViewSource beamItemsView;

        public MainWindow()
        {
            InitializeComponent();
            beamItemsView = (CollectionViewSource)(this.Resources["beamItemsView"]);

            // Sorting
            beamItemsView.SortDescriptions.Add(new SortDescription("Ga", ListSortDirection.Ascending));
            beamItemsView.SortDescriptions.Add(new SortDescription("Ca", ListSortDirection.Ascending));
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(ImageLarge);
            CursorSelector = new CursorAdorner(ImageLarge);
            layer.Add(CursorSelector);
            CursorSelector.Cursorband.Visibility = Visibility.Hidden;

            AdornerLayer layerPrecise = AdornerLayer.GetAdornerLayer(ImageSmall);
            PreciseSelector = new PreciseAdoner(ImageSmall);
            PreciseSelector.Window = this;
            layerPrecise.Add(PreciseSelector);
            PreciseSelector.Cursorband.Visibility = Visibility.Hidden;

            CursorSelector.CursorChanged += new EventHandler<CursorEventArgs>(PreciseSelector.ChangeFrame);
            CursorSelector.CursorChanged += new EventHandler<CursorEventArgs>(this.ChangeCursorPosition);
            PreciseSelector.CursorChanged += new EventHandler<CursorEventArgs>(CursorSelector.ChangeCursorPosition);
        }

        /// <summary>
        /// Выбранное в данное время поле
        /// </summary>
        public BeamParams ActiveBeam
        {
            get
            {
                return (BeamParams)beamItemsView.View.CurrentItem;
            }
        }

        public void ChangeCursorPosition(object sender, CursorEventArgs e)
        {
            BeamParams bp = (BeamParams)beamItemsView.View.CurrentItem;
            bp.O = new Point(e.Position.X, e.Position.Y);
        }

        #region Menu
        private void New_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = "WL";
            dlg.DefaultExt = ".tif";
            dlg.Filter = "Image files (*.tif)|*.tif|All files (*.*)|*.*";

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                _originalImage = new ImageFile(dlg.FileName);

                _mainImage = new TransformedBitmap();
                _mainImage.BeginInit();
                _mainImage.Source = _originalImage.Image;
                _mainImage.EndInit();

                this.ImageLarge.Source = _mainImage;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }
        #endregion

        #region Mouse
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(ImageLarge);
            CursorSelector.CaptureMouse();
            CursorSelector.StartSelection(p);
            CursorSelector.IsEnabled = true;
        }

        private void OnPreciseMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(ImageSmall);
            PreciseSelector.CaptureMouse();
            PreciseSelector.StartSelection(p);
            PreciseSelector.IsEnabled = true;
        }

        private void ImageLarge_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CursorSelector.SetCursorPosition(ActiveBeam.O);
        }
        #endregion

        #region Commands
        private void checkBoxFlipHorisontal_Checked(object sender, RoutedEventArgs e)
        {
            _generalParams.FlipHorisontal = true;
            updateRotation();
        }

        private void checkBoxFlipHorisontal_Unchecked(object sender, RoutedEventArgs e)
        {
            _generalParams.FlipHorisontal = false;
            updateRotation();
        }

        private void checkBoxFlipVertical_Checked(object sender, RoutedEventArgs e)
        {
            _generalParams.FlipVertical = true;
            updateRotation();
        }

        private void checkBoxFlipVertical_Unchecked(object sender, RoutedEventArgs e)
        {
            _generalParams.FlipVertical = false;
            updateRotation();
        }

        private void updateRotation()
        {
            double sx = _generalParams.FlipHorisontal ? -1 : 1;
            double sy = _generalParams.FlipVertical ? -1 : 1;

            _mainImage = new TransformedBitmap();
            _mainImage.BeginInit();
            _mainImage.Source = _originalImage.Image;
            _mainImage.Transform = new ScaleTransform(sx, sy);
            _mainImage.EndInit();

            this.ImageLarge.Source = _mainImage;
        }

        private void radioButtonGTop_Checked(object sender, RoutedEventArgs e)
        {
            ActiveBeam.FA = 0;
        }

        private void radioButtonGRight_Checked(object sender, RoutedEventArgs e)
        {
            ActiveBeam.FA = 90;
        }

        private void radioButtonGBottom_Checked(object sender, RoutedEventArgs e)
        {
            ActiveBeam.FA = 180;
        }

        private void radioButtonGLeft_Checked(object sender, RoutedEventArgs e)
        {
            ActiveBeam.FA = 270;
        }

        async private void buttonAutoFuse_Click(object sender, RoutedEventArgs e)
        {
            Fusion fiter = new Fusion(_mainImage);
            BeamParams b = await fiter.DoFusion(ActiveBeam);
            ActiveBeam.Set(b);
            CursorSelector.SetCursorPosition(b.O);
        }

        private void buttonDetails_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BeamList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (beamItemsView == null) return;
            if (beamItemsView.View.CurrentItem == null) return;
            double fa = ActiveBeam.FA;
            if (fa == 0)
                radioButtonGTop.IsChecked = true;
            else if (fa == 90)
                radioButtonGRight.IsChecked = true;
            else if (fa == 180)
                radioButtonGBottom.IsChecked = true;
            else
                radioButtonGLeft.IsChecked = true;

            CursorSelector.SetCursorPosition(ActiveBeam.O);
        }

        private void buttonAddBeam_Click(object sender, RoutedEventArgs e)
        {
            BeamParams b = new BeamParams();
            ((App)Application.Current).BeamItems.Add(b);
            beamItemsView.View.MoveCurrentTo(b);
        }

        private void buttonDeleteBeam_Click(object sender, RoutedEventArgs e)
        {
            if (((App)Application.Current).BeamItems.Count > 1)
            {
                ((App)Application.Current).BeamItems.Remove(ActiveBeam);
                //refreshBeamGouping();
            }
        }

        private void buttonGroupBeams_Click(object sender, RoutedEventArgs e)
        {
            refreshBeamGouping();
        }

        private void refreshBeamGouping()
        {
            PropertyGroupDescription groupDescription = new PropertyGroupDescription();
            groupDescription.PropertyName = "Ta";
            groupDescription.Converter = new TaConverter();
            beamItemsView.GroupDescriptions.Clear();
            beamItemsView.GroupDescriptions.Add(groupDescription);
        }
        #endregion
    }
}
