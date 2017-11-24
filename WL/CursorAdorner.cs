// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WL
{
    public class CursorAdorner : Adorner
    {
        private System.Windows.Shapes.Path _cursorband;
        private UIElement _adornedElement;
        private Point _selectPosition;
        public Point SelectPosition { get { return _selectPosition; } }
        public System.Windows.Shapes.Path Cursorband { get { return _cursorband; } }
        protected override int VisualChildrenCount { get { return 1; } }

        public BitmapSource Image
        {
            get
            {
                System.Windows.Controls.Image imageControl = _adornedElement as System.Windows.Controls.Image;
                return (BitmapSource)(imageControl.Source);
            }
        }

        public CursorAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            _adornedElement = adornedElement;
            _selectPosition = new Point();
            _cursorband = new System.Windows.Shapes.Path();
            _cursorband.Data = CreateCursorData(_selectPosition);
            _cursorband.StrokeThickness = 5;
            _cursorband.Stroke = Brushes.Red;
            _cursorband.Opacity = .6;
            _cursorband.Visibility = Visibility.Hidden;
            AddVisualChild(_cursorband);
            MouseMove += new MouseEventHandler(DrawSelection);
            MouseUp += new MouseButtonEventHandler(EndSelection);
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
            _cursorband.Data = CreateCursorData(_selectPosition);
            if (Visibility.Visible != _cursorband.Visibility)
                _cursorband.Visibility = Visibility.Visible;
        }

        private void DrawSelection(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _selectPosition = e.GetPosition(_adornedElement);
                _cursorband.Data = CreateCursorData(_selectPosition);
                AdornerLayer layer = AdornerLayer.GetAdornerLayer(_adornedElement);
                layer.InvalidateArrange();
                OnCursorChanged(_selectPosition);
            }
        }

        private void EndSelection(object sender, MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
        }

        protected override Visual GetVisualChild(int index)
        {
            return _cursorband;
        }

        protected GeometryGroup CreateCursorData(Point p)
        {
            double R = 25;
            double D = 15;

            // Create the ellipse geometry 
            EllipseGeometry ellipseGeometry = new EllipseGeometry();
            ellipseGeometry.Center = p;
            ellipseGeometry.RadiusX = R;
            ellipseGeometry.RadiusY = R;

            // L1
            LineGeometry L1 = new LineGeometry();
            L1.StartPoint = new Point(p.X - R - D, p.Y);
            L1.EndPoint = new Point(p.X - R + D, p.Y);

            // L2
            LineGeometry L2 = new LineGeometry();
            L2.StartPoint = new Point(p.X + R - D, p.Y);
            L2.EndPoint = new Point(p.X + R + D, p.Y);

            // L3
            LineGeometry L3 = new LineGeometry();
            L3.StartPoint = new Point(p.X, p.Y - R - D);
            L3.EndPoint = new Point(p.X, p.Y - R + D);

            // L4
            LineGeometry L4 = new LineGeometry();
            L4.StartPoint = new Point(p.X, p.Y + R - D);
            L4.EndPoint = new Point(p.X, p.Y + R + D);

            // Add all the geometries to a GeometryGroup.
            GeometryGroup geometryGroup = new GeometryGroup();
            geometryGroup.Children.Add(ellipseGeometry);
            geometryGroup.Children.Add(L1);
            geometryGroup.Children.Add(L2);
            geometryGroup.Children.Add(L3);
            geometryGroup.Children.Add(L4);

            return geometryGroup;
        }

        public void ChangeCursorPosition(object sender, CursorEventArgs e)
        {
            if (Image == null)
                return;

            System.Windows.Controls.Image imageControl = _adornedElement as System.Windows.Controls.Image;
            _selectPosition.X += e.Position.X * imageControl.ActualWidth / Image.PixelWidth;
            _selectPosition.Y += e.Position.Y * imageControl.ActualHeight / Image.PixelHeight;
            _cursorband.Data = CreateCursorData(_selectPosition);
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(_adornedElement);
            layer.InvalidateArrange();
            OnCursorChanged(_selectPosition);
        }

        /// <summary>
        /// Установка курсора в указанное положение.
        /// Предназначено для упраления курсором родительским объектом
        /// </summary>
        /// <param name="p">новое положение курсора в системе координат ImageControl</param>
        public void SetCursorPosition(Point p)
        {
            if (Image == null)
                return;

            System.Windows.Controls.Image imageControl = _adornedElement as System.Windows.Controls.Image;
            _selectPosition.X = p.X * imageControl.ActualWidth / Image.PixelWidth;
            _selectPosition.Y = p.Y * imageControl.ActualHeight / Image.PixelHeight;
            _cursorband.Data = CreateCursorData(_selectPosition);
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(_adornedElement);
            layer.InvalidateArrange();
            OnCursorChanged(_selectPosition);
        }

        /// <summary>
        /// Occurs when the user changed cursor position in this control window
        /// </summary>
        public event EventHandler<CursorEventArgs> CursorChanged;

        /// <summary>
        /// Raises the ValueChanged event.
        /// </summary>
        /// <param name="args">An EventArgs that contains the event data.</param>
        protected virtual void OnCursorChanged(Point p)
        {
            EventHandler<CursorEventArgs> handler = CursorChanged;
            if (handler != null)
            {
                // Генрируем точку в координатах Bitmap вместо окна элемента
                System.Windows.Controls.Image imageControl = _adornedElement as System.Windows.Controls.Image;
                BitmapSource img = (BitmapSource)(imageControl.Source);
                Point pbmp = new Point();
                pbmp.X = p.X * img.PixelWidth / imageControl.ActualWidth;
                pbmp.Y = p.Y * img.PixelHeight / imageControl.ActualHeight;

                handler(this, new CursorEventArgs(pbmp));
            }
        }
    }
}
