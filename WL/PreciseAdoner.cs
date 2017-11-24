// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WL
{
    public class PreciseAdoner : Adorner
    {
        private MainWindow _window;
        private System.Windows.Shapes.Path _cursorband;
        private UIElement _adornedElement;
        private Point _selectPosition;
        public Point SelectPosition { get { return _selectPosition; } }
        public System.Windows.Shapes.Path Cursorband { get { return _cursorband; } }
        protected override int VisualChildrenCount { get { return 1; } }
        public MainWindow Window { set { _window = value; } }

        List<Point> _mlcShape = null;

        public BitmapSource Image
        {
            get
            {
                System.Windows.Controls.Image imageControl = _adornedElement as System.Windows.Controls.Image;
                return (BitmapSource)(imageControl.Source);
            }
        }

        public PreciseAdoner(UIElement adornedElement)
            : base(adornedElement)
        {
            _adornedElement = adornedElement;
            _selectPosition = new Point();
            _cursorband = new System.Windows.Shapes.Path();
            _cursorband.Data = null;
            _cursorband.StrokeThickness = 1;
            _cursorband.Stroke = Brushes.Blue;
            _cursorband.Opacity = 1.0;
            _cursorband.Visibility = Visibility.Hidden;
            AddVisualChild(_cursorband);
            MouseMove += new MouseEventHandler(DrawSelection);
            MouseUp += new MouseButtonEventHandler(EndSelection);

            _mlcShape = new List<Point>();
            _mlcShape.Add(new Point(-9.0, -3.0));
            _mlcShape.Add(new Point(-9.0, 3.0));
            _mlcShape.Add(new Point(-3.0, 3.0));
            _mlcShape.Add(new Point(-3.0, 9.0));
            _mlcShape.Add(new Point(3.0, 9.0));
            _mlcShape.Add(new Point(3.0, 3.0));
            _mlcShape.Add(new Point(9.0, 3.0));
            _mlcShape.Add(new Point(9.0, -3.0));
            _mlcShape.Add(new Point(3.0, -3.0));
            _mlcShape.Add(new Point(3.0, -9.0));
            _mlcShape.Add(new Point(-3.0, -9.0));
            _mlcShape.Add(new Point(-3.0, -3.0));

            updateCursor();
        }

        protected void updateCursor()
        {
            System.Windows.Controls.Image imageControl = _adornedElement as System.Windows.Controls.Image;
            _cursorband.Data = CreateCursorData(new Point(imageControl.ActualWidth / 2, imageControl.ActualHeight / 2));
            if (Visibility.Visible != _cursorband.Visibility)
                _cursorband.Visibility = Visibility.Visible;
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(_adornedElement);
            layer.InvalidateArrange();
        }

        protected override Size ArrangeOverride(Size size)
        {
            Size finalSize = base.ArrangeOverride(size);
            ((UIElement)GetVisualChild(0)).Arrange(new Rect(new Point(), finalSize));
            return finalSize;
        }

        public void StartSelection(Point p)
        {
            _selectPosition = p;
        }

        private void DrawSelection(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point p = e.GetPosition(_adornedElement);
                if (_selectPosition.X != 0 || _selectPosition.Y != 0)
                {
                    Point dxy = new Point(_selectPosition.X - p.X, _selectPosition.Y - p.Y);
                    OnCursorChanged(dxy);
                }
                _selectPosition = p;
            }
        }

        private void EndSelection(object sender, MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
            _selectPosition = new Point();
        }

        protected override Visual GetVisualChild(int index)
        {
            return _cursorband;
        }

        protected GeometryGroup CreateCursorData(Point p)
        {
            if (Image == null)
                return null;

            System.Windows.Controls.Image imageControl = _adornedElement as System.Windows.Controls.Image;
            if (imageControl.ActualWidth == 0)
                return null;

            //double zoom = imageControl.ActualWidth / Image.PixelWidth;
            //double filmScale = 1.07;
            //double f = zoom * filmScale * Image.DpiX / 25.4;

            BeamParams b = _window.ActiveBeam;
            if (b.PS == 0)
                b.PS = 0.92 * 25.4 / Image.DpiX;
            double f = imageControl.ActualWidth / (b.PS * Image.PixelWidth);

            GeometryGroup geometryGroup = new GeometryGroup();

            Point startMlc = _mlcShape[_mlcShape.Count - 1];
            foreach (Point pm in _mlcShape)
            {
                Point p1 = b.PointFromMlcToBeam(startMlc);
                Point p2 = b.PointFromMlcToBeam(pm);
                LineGeometry line = new LineGeometry(
                    new Point(p1.X * f + p.X, -p1.Y * f + p.Y),
                    new Point(p2.X * f + p.X, -p2.Y * f + p.Y));
                geometryGroup.Children.Add(line);
                startMlc = pm;
            }

            // Радиус тест объекта (диаметр 5 мм)
            double r = 2.5 * f;
            EllipseGeometry objectGeometry = new EllipseGeometry();
            objectGeometry.Center = p;
            objectGeometry.RadiusX = r;
            objectGeometry.RadiusY = r;
            geometryGroup.Children.Add(objectGeometry);

            // Радиус коллиматора
            double R = 6.25 * f;
            EllipseGeometry collimatorGeometry = new EllipseGeometry();
            Point cc = b.PointFromCCToBeam(new Point(0, 0));
            collimatorGeometry.Center = new Point(cc.X * f + p.X, -cc.Y * f + p.Y);
            collimatorGeometry.RadiusX = R;
            collimatorGeometry.RadiusY = R;
            geometryGroup.Children.Add(collimatorGeometry);

            return geometryGroup;
        }

        public void ChangeFrame(object sender, CursorEventArgs e)
        {
            CursorAdorner cusorAdoner = sender as CursorAdorner;
            BitmapSource img = cusorAdoner.Image;

            double R = img.DpiX * 0.4;

            Int32Rect rect = new Int32Rect();
            rect.X = (int)(e.Position.X - R);
            rect.Y = (int)(e.Position.Y - R);
            rect.Width = (int)(2 * R);
            rect.Height = (int)(2 * R);
            if (rect.X + rect.Width >= img.PixelWidth)
                rect.X -= (int)(rect.X + rect.Width - img.PixelWidth + 1);
            if (rect.Y + rect.Height >= img.PixelHeight)
                rect.Y -= (int)(rect.Y + rect.Height - img.PixelHeight + 1);
            if (rect.X < 0)
            {
                rect.Width += rect.X;
                if (rect.Width < 1) rect.Width = 1;
                rect.X = 0;
            }
            if (rect.Y < 0)
            {
                rect.Height += rect.Y;
                if (rect.Height < 1) rect.Height = 1;
                rect.Y = 0;
            }

            System.Windows.Controls.Image imageControl = _adornedElement as System.Windows.Controls.Image;
            imageControl.Source = new CroppedBitmap(img, rect);
            //if (_cursorband.Data == null)
            updateCursor();
        }

        /// <summary>
        /// Occurs when the user changed cursor position in this control window
        /// </summary>
        public event EventHandler<CursorEventArgs> CursorChanged;

        /// <summary>
        /// Raises the ValueChanged event.
        /// </summary>
        /// <param name="args">An EventArgs that contains the event data.</param>
        protected virtual void OnCursorChanged(Point dxy)
        {
            EventHandler<CursorEventArgs> handler = CursorChanged;
            if (handler != null)
            {
                // Генрируем точку в координатах Bitmap вместо окна элемента
                System.Windows.Controls.Image imageControl = _adornedElement as System.Windows.Controls.Image;
                BitmapSource img = (BitmapSource)(imageControl.Source);
                Point pbmp = new Point();
                pbmp.X = dxy.X * img.PixelWidth / imageControl.ActualWidth;
                pbmp.Y = dxy.Y * img.PixelHeight / imageControl.ActualHeight;
                handler(this, new CursorEventArgs(pbmp));
            }
        }
    }
}
