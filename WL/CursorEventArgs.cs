// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
using System;
using System.Windows;

namespace WL
{
    /// <summary>
    /// Аргумент сообщения об изменении положения курсора.
    /// В отличие от стандартного аргумента содержит координату нового положения курсора.
    /// </summary>
    public class CursorEventArgs : EventArgs
    {
        public CursorEventArgs()
        {
            Position = new Point();
        }

        public CursorEventArgs(Point p)
        {
            Position = p;
        }

        /// <summary>
        /// Положение курсора
        /// </summary>
        public Point Position { get; set; }
    }
}
