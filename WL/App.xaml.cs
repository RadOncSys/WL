// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
using System.Collections.ObjectModel;
using System.Windows;

namespace WL
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ObservableCollection<BeamParams> _beamItems = new ObservableCollection<BeamParams>();

        public App()
        {
            BeamItems.Add(new BeamParams());
        }

        public ObservableCollection<BeamParams> BeamItems
        {
            get { return this._beamItems; }
            set { this._beamItems = value; }
        }
    }
}
