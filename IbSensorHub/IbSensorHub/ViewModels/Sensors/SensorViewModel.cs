using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ReactiveUI;
using SkiaSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IbSensorHub.ViewModels.Sensors
{
    internal class SensorViewModel : ViewModelBase
    {
        private ObservableValue[] _values;
        /// <summary>
        /// Usage: <c>Values[0].Value</c>
        /// </summary>
        public ObservableValue[] Values
        {
            get => _values;
            private set => this.RaiseAndSetIfChanged(ref _values, value);
        }

        public SensorViewModel(int dimension)
        {
            _values = new ObservableValue[dimension];
            for (int i = 0; i < dimension; i++)
            {
                _values[i] = new ObservableValue();
            }
        }

        /// <param name="values"><c>values</c> can have more dimension than required.</param>
        public virtual void UpdateValues(IList<float> values)
        {
            // TODO: Time

            for (int i = 0; i < _values.Length; i++)
            {
                _values[i].Value = values[i];
            }
        }
    }

    internal class SensorViewModelWithSeries : SensorViewModel
    {
        private const int _historySamples = 50;

        private ObservableCollection<ObservableValue>[] _historyValues = new ObservableCollection<ObservableValue>[0];
        public ISeries[][] Series { get; set; } = new ISeries[0][];

        private static readonly SKColor[] _colors =
        {
            SKColors.Blue,
            SKColors.Green,
            SKColors.Red,
            SKColors.Yellow,
            SKColors.Purple,
            SKColors.Orange,
            SKColors.Brown,
            SKColors.Cyan,
            SKColors.Magenta,
            SKColors.Lime,
            SKColors.Maroon,
            SKColors.Navy,
            SKColors.Olive,
            SKColors.Pink,
            SKColors.Teal,
            SKColors.White,
            SKColors.Black,
        };

        public SensorViewModelWithSeries(int dimension)
            : base(dimension)
        {
            _historyValues = new ObservableCollection<ObservableValue>[dimension];
            Series = new ISeries[dimension][];
            for (int i = 0; i < dimension; i++)
            {
                _historyValues[i] = new ObservableCollection<ObservableValue>();
                Series[i] = new ISeries[]
                {
                    new LineSeries<ObservableValue>
                    {
                        Values = _historyValues[i],
                        Fill = null,
                        Stroke = new SolidColorPaint(_colors[i % _colors.Length]) { StrokeThickness = 1 },
                        GeometryStroke = new SolidColorPaint(_colors[i % _colors.Length]) { StrokeThickness = 2 },
                        GeometrySize = 2
                    }
                };
            }
        }

        public override void UpdateValues(IList<float> values)
        {
            base.UpdateValues(values);

            Dispatcher.UIThread.Post(() =>
            {
                for (int i = 0; i < Values.Length; i++)
                {
                    var history = _historyValues[i];
                    var value = values[i];
                    if (history.Count < _historySamples)
                    {
                        history.Add(new(value));
                    }
                    else
                    {
                        history.Move(0, history.Count - 1);
                        history[history.Count - 1] = new(value);
                    }
                }
            });
        }
    }
}
