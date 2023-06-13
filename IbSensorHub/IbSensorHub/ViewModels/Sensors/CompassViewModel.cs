using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System.Collections.Generic;

namespace IbSensorHub.ViewModels.Sensors
{
    internal class CompassViewModel : SensorViewModel
    {
        public IEnumerable<ISeries> Series { get; set; }

        public CompassViewModel()
            : base(1)
        {
            Series = new GaugeBuilder()
                .WithMaxColumnWidth(30)
                .AddValue(Values[0])
                .WithLabelFormatter((x) => $"{x.PrimaryValue:N4}")
                .BuildSeries();
        }
    }
}
