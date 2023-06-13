using Avalonia.Controls;
using IbSensorHub.ViewModels.Sensors;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.Widgets.Zoom;
using System.ComponentModel;

namespace IbSensorHub.Views.Sensors
{
    public partial class GeolocationView : UserControl
    {
        public GeolocationView()
        {
            InitializeComponent();

            MapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
            MapControl.Map.Widgets.Add(new ZoomInOutWidget { MarginX = 20, MarginY = 40 });
            MapControl.Map.Home = n => n.ZoomToLevel(15);

            DataContextChanged += (sender, args) =>
            {
                if (DataContext is GeolocationViewModel vm)
                {
                    PropertyChangedEventHandler handler = (o, eventArgs) =>
                    {
                        if (vm.Values[0].Value is not null && vm.Values[1].Value is not null)
                        {
                            var (x, y) = SphericalMercator.FromLonLat((double)vm.Values[1].Value, (double)vm.Values[0].Value);
                            MapControl.Map.Navigator.CenterOn(new Mapsui.MPoint(x, y));
                        }
                    };
                    vm.Values[0].PropertyChanged += handler;
                    vm.Values[1].PropertyChanged += handler;
                }
            };
        }
    }
}
